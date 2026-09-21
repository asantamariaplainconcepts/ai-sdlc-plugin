namespace AiSdlc;

/// SQLite, raw Microsoft.Data.Sqlite: opened at startup, schema created with
/// CREATE TABLE IF NOT EXISTS. One store file under .harness/data (gitignored).
/// POC-00 owns worktrees + fact_cache; POC-03 adds runs, POC-04 adds gate_results —
/// later tables are additive, never a migration of these.
public sealed class Store : IDisposable
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection connection;

    public Store(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        this.connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={path}");
        this.connection.Open();
        using var create = this.connection.CreateCommand();
        create.CommandText = """
            CREATE TABLE IF NOT EXISTS worktrees (
                path TEXT PRIMARY KEY,
                branch TEXT,
                head TEXT,
                read_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS fact_cache (
                worktree TEXT NOT NULL,
                fact_key TEXT NOT NULL,
                value TEXT NOT NULL,
                PRIMARY KEY (worktree, fact_key)
            );
            """;
        create.ExecuteNonQuery();
    }

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

    public void Dispose() => this.connection.Dispose();
}
