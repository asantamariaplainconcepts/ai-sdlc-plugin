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

    /// INSERT only: a second run on the same worktree is a second row, whatever happened. Cost
    /// absent stays NULL — null is not the same as free.
    public long RecordRun(RunRow row)
    {
        using var command = this.connection.CreateCommand();
        command.CommandText = """
            INSERT INTO runs (session_id, worktree_path, step, prompt, starting_commit, started_at, finished_at,
                exit_code, is_error, cost_usd, num_turns, duration_ms, transcript_path, transcript_located,
                result_summary, stderr_tail, problem)
            VALUES ($session, $worktree, $step, $prompt, $commit, $started, $finished,
                $exit, $isError, $cost, $turns, $duration, $transcript, $transcriptLocated,
                $summary, $stderr, $problem);
            SELECT last_insert_rowid();
            """;
        foreach (var (name, value) in RunParameters(row))
        {
            command.Parameters.AddWithValue(name, value);
        }

        return (long)command.ExecuteScalar()!;
    }

    private static IEnumerable<(string Name, object Value)> RunParameters(RunRow row)
    {
        yield return ("$session", row.SessionId);
        yield return ("$worktree", row.WorktreePath);
        yield return ("$step", (object?)row.Step ?? DBNull.Value);
        yield return ("$prompt", (object?)row.Prompt ?? DBNull.Value);
        yield return ("$commit", (object?)row.StartingCommit ?? DBNull.Value);
        yield return ("$started", row.StartedAtIso);
        yield return ("$finished", (object?)row.FinishedAtIso ?? DBNull.Value);
        yield return ("$exit", (object?)row.ExitCode ?? DBNull.Value);
        yield return ("$isError", row.IsError is { } isError ? (object)(isError ? 1 : 0) : DBNull.Value);
        yield return ("$cost", (object?)row.CostUsd ?? DBNull.Value);
        yield return ("$turns", (object?)row.NumTurns ?? DBNull.Value);
        yield return ("$duration", (object?)row.DurationMs ?? DBNull.Value);
        yield return ("$transcript", (object?)row.TranscriptPath ?? DBNull.Value);
        yield return ("$transcriptLocated", row.TranscriptLocated ? 1 : 0);
        yield return ("$summary", (object?)row.ResultSummary ?? DBNull.Value);
        yield return ("$stderr", (object?)row.StderrTail ?? DBNull.Value);
        yield return ("$problem", (object?)row.Problem ?? DBNull.Value);
    }

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
                reader.GetString(0), worktreePath,
                reader.IsDBNull(1) ? null : reader.GetString(1),
                null,
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetInt32(5),
                reader.IsDBNull(6) ? null : reader.GetInt32(6) == 1,
                reader.IsDBNull(7) ? null : reader.GetDecimal(7),
                reader.IsDBNull(8) ? null : reader.GetInt32(8),
                reader.IsDBNull(9) ? null : reader.GetDouble(9),
                reader.IsDBNull(10) ? null : reader.GetString(10),
                reader.GetInt32(11) == 1,
                reader.IsDBNull(12) ? null : reader.GetString(12),
                null,
                null));
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

/// A run's record: what was launched, where it started from, how it ended. Nulls are absences,
/// each named by whoever draws the row — cost null is "not given", exit null is "never ran".
public sealed record RunRow(
    string SessionId, string WorktreePath, string? Step, string? Prompt, string? StartingCommit,
    string StartedAtIso, string? FinishedAtIso, int? ExitCode, bool? IsError, decimal? CostUsd,
    int? NumTurns, double? DurationMs, string? TranscriptPath, bool TranscriptLocated,
    string? ResultSummary, string? StderrTail, string? Problem);
