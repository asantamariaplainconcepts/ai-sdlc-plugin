namespace AiSdlc;

// SQLite, raw: opened at startup, schema created with CREATE TABLE IF NOT EXISTS. POC-00 owns
// worktrees + fact_cache; POC-03 adds runs, POC-04 adds gate_results — additive, never migrated.
public sealed class Store : IDisposable
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection connection;

    public Store(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        this.connection = new($"Data Source={path}");
        this.connection.Open();
        using var create = this.connection.CreateCommand();
        // Two tables now; the header needs none of them yet (facts are read live). Runs (POC-03)
        // and gate results (POC-04) join here additively.
        create.CommandText = """
            CREATE TABLE IF NOT EXISTS worktrees (
                path TEXT PRIMARY KEY, branch TEXT, head TEXT, read_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS fact_cache (
                worktree TEXT NOT NULL, fact_key TEXT NOT NULL, value TEXT NOT NULL,
                PRIMARY KEY (worktree, fact_key)
            );
            CREATE TABLE IF NOT EXISTS runs (
                id INTEGER PRIMARY KEY AUTOINCREMENT, session_id TEXT NOT NULL, worktree_path TEXT NOT NULL,
                step TEXT, prompt TEXT, starting_commit TEXT, started_at TEXT NOT NULL, finished_at TEXT,
                exit_code INTEGER, is_error INTEGER, cost_usd REAL, num_turns INTEGER, duration_ms REAL,
                transcript_path TEXT, transcript_located INTEGER NOT NULL DEFAULT 0,
                result_summary TEXT, stderr_tail TEXT, problem TEXT
            );
            CREATE TABLE IF NOT EXISTS runs_capture (
                session_id TEXT PRIMARY KEY, stdout TEXT
            );
            CREATE TABLE IF NOT EXISTS gate_results (
                id INTEGER PRIMARY KEY AUTOINCREMENT, worktree_path TEXT NOT NULL, head_commit TEXT NOT NULL,
                command_key TEXT NOT NULL, exit_code INTEGER, output_tail TEXT NOT NULL,
                finished_at TEXT NOT NULL, problem TEXT
            );
            """;
        create.ExecuteNonQuery();
    }

    /// The full output blob, kept by session id: one session is one run, so overwriting the same
    /// key is replacing the run's own copy with itself. Bounded before it is written.
    public void CaptureRun(string sessionId, string stdout)
    {
        using var command = this.connection.CreateCommand();
        command.CommandText = "INSERT INTO runs_capture (session_id, stdout) VALUES ($session, $stdout) ON CONFLICT(session_id) DO UPDATE SET stdout = $stdout";
        command.Parameters.AddWithValue("$session", sessionId);
        command.Parameters.AddWithValue("$stdout", stdout);
        command.ExecuteNonQuery();
    }

    /// Paired with the RunRow order they fill; nulls kept as absences.
    private static readonly string[] RunColumns = ["session_id", "worktree_path", "step", "prompt", "starting_commit", "started_at", "finished_at", "exit_code", "is_error", "cost_usd", "num_turns", "duration_ms", "transcript_path", "transcript_located", "result_summary", "stderr_tail", "problem"];

    /// INSERT only: a second run on the same worktree is a second row, whatever happened. Cost
    /// absent stays NULL — null is not the same as free.
    public long RecordRun(RunRow row)
    {
        var values = new object[]
        {
            row.SessionId, row.WorktreePath, row.Step ?? (object)DBNull.Value, row.Prompt ?? (object)DBNull.Value,
            row.StartingCommit ?? (object)DBNull.Value, row.StartedAtIso, row.FinishedAtIso ?? (object)DBNull.Value,
            row.ExitCode ?? (object)DBNull.Value, row.IsError is { } e ? (e ? 1 : 0) : DBNull.Value, row.CostUsd ?? (object)DBNull.Value,
            row.NumTurns ?? (object)DBNull.Value, row.DurationMs ?? (object)DBNull.Value, row.TranscriptPath ?? (object)DBNull.Value,
            row.TranscriptLocated ? 1 : 0, row.ResultSummary ?? (object)DBNull.Value, row.StderrTail ?? (object)DBNull.Value, row.Problem ?? (object)DBNull.Value,
        };
        using var command = this.connection.CreateCommand();
        var columns = string.Join(", ", RunColumns);
        var parameters = string.Join(", ", RunColumns.Select(c => $"${c}"));
        command.CommandText = $"INSERT INTO runs ({columns}) VALUES ({parameters}); SELECT last_insert_rowid();";
        for (var i = 0; i < values.Length; i++)
        {
            command.Parameters.AddWithValue($"${RunColumns[i]}", values[i]);
        }

        return (long)command.ExecuteScalar()!;
    }

    // Listers for each column kind, paired with the SELECT's order; each null is an absence kept.
    private static string? Text(Microsoft.Data.Sqlite.SqliteDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);
    private static int? Int(Microsoft.Data.Sqlite.SqliteDataReader r, int i) => r.IsDBNull(i) ? null : r.GetInt32(i);

    /// The worktree's runs, most recent first — the listing the run panel draws.
    public IReadOnlyList<RunRow> ListRuns(string worktreePath)
    {
        using var command = this.connection.CreateCommand();
        command.CommandText = "SELECT session_id, step, starting_commit, started_at, finished_at, exit_code, is_error, cost_usd, num_turns, duration_ms, transcript_path, transcript_located, result_summary, problem FROM runs WHERE worktree_path = $path ORDER BY id DESC";
        command.Parameters.AddWithValue("$path", worktreePath);
        using var reader = command.ExecuteReader();
        var rows = new List<RunRow>();
        while (reader.Read())
        {
            rows.Add(new RunRow(
                reader.GetString(0), worktreePath, Text(reader, 1), null, Text(reader, 2), reader.GetString(3),
                Text(reader, 4), Int(reader, 5), Int(reader, 6) == 1 ? true : Int(reader, 6) is null ? null : false,
                reader.IsDBNull(7) ? null : reader.GetDecimal(7), Int(reader, 8),
                reader.IsDBNull(9) ? null : reader.GetDouble(9), Text(reader, 10), reader.GetInt32(11) == 1,
                Text(reader, 12), null, null));
        }

        return rows;
    }

    /// Gate outcomes, INSERT-only (POC-04): a re-run adds rows, a moved HEAD makes old rows
    /// stale by comparison — never an update. Most recent first for the listing.
    public long RecordGateResult(GateRow row)
    {
        using var command = this.connection.CreateCommand();
        command.CommandText = """
            INSERT INTO gate_results (worktree_path, head_commit, command_key, exit_code, output_tail, finished_at, problem)
            VALUES ($wt, $commit, $key, $exit, $tail, $finished, $problem); SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$wt", row.WorktreePath);
        command.Parameters.AddWithValue("$commit", row.Commit);
        command.Parameters.AddWithValue("$key", row.CommandKey);
        command.Parameters.AddWithValue("$exit", (object?)row.ExitCode ?? DBNull.Value);
        command.Parameters.AddWithValue("$tail", row.Tail);
        command.Parameters.AddWithValue("$finished", row.FinishedAtIso);
        command.Parameters.AddWithValue("$problem", (object?)row.Problem ?? DBNull.Value);
        return (long)command.ExecuteScalar()!;
    }

    public IReadOnlyList<GateRow> ListGateResults(string worktreePath)
    {
        using var command = this.connection.CreateCommand();
        command.CommandText = "SELECT id, worktree_path, head_commit, command_key, exit_code, output_tail, finished_at, problem FROM gate_results WHERE worktree_path = $path ORDER BY id DESC";
        command.Parameters.AddWithValue("$path", worktreePath);
        using var reader = command.ExecuteReader();
        var rows = new List<GateRow>();
        while (reader.Read())
        {
            rows.Add(new(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), Int(reader, 4), reader.GetString(5), reader.GetString(6), Text(reader, 7)));
        }

        return rows;
    }

    public void Dispose() => this.connection.Dispose();

    /// Re-reading a worktree is still a point-in-time snapshot: UPSERT the observation, never
    /// accumulate it — a worktree is one path, however many readings.
    public void RecordWorktree(string path, string? branch, string? head, string readAtIso)
    {
        using var command = this.connection.CreateCommand();
        command.CommandText = """
            INSERT INTO worktrees (path, branch, head, read_at) VALUES ($path, $branch, $head, $readAt)
            ON CONFLICT(path) DO UPDATE SET branch = $branch, head = $head, read_at = $readAt;
            """;
        command.Parameters.AddWithValue("$path", path);
        command.Parameters.AddWithValue("$branch", (object?)branch ?? DBNull.Value);
        command.Parameters.AddWithValue("$head", (object?)head ?? DBNull.Value);
        command.Parameters.AddWithValue("$readAt", readAtIso);
        command.ExecuteNonQuery();
    }
}

/// A run's record; every null is an absence — cost null is "not given", exit null is "never ran".
public sealed record RunRow(
    string SessionId, string WorktreePath, string? Step, string? Prompt, string? StartingCommit,
    string StartedAtIso, string? FinishedAtIso, int? ExitCode, bool? IsError, decimal? CostUsd,
    int? NumTurns, double? DurationMs, string? TranscriptPath, bool TranscriptLocated,
    string? ResultSummary, string? StderrTail, string? Problem);
