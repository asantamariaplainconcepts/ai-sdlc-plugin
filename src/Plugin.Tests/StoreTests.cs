using AiSdlc;
using Shouldly;

namespace AiSdlcPlugin.Tests;

/// The store opens, creates its tables idempotently, and records worktree reads.
public class StoreTests
{
    [Fact]
    public void Opens_creates_and_records()
    {
        var path = Path.Combine(Path.GetTempPath(), "ai-sdlc-tests", $"{Guid.NewGuid():N}.db");

        using (var store = new Store(path))
        {
            store.RecordWorktree("/tmp/wt", "poc/poc-00-header", "abc", "2026-09-21T00:00:00Z");
        }

        using (var again = new Store(path))
        {
            // CREATE TABLE IF NOT EXISTS over an existing file: the second open must not throw.
            again.RecordWorktree("/tmp/wt", "poc/poc-00-header", "def", "2026-09-21T01:00:00Z");
        }

        using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={path}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT branch, head FROM worktrees WHERE path = '/tmp/wt'";
        using var reader = command.ExecuteReader();
        reader.Read().ShouldBeTrue();
        reader.GetString(0).ShouldBe("poc/poc-00-header");
        reader.GetString(1).ShouldBe("def");
    }
}
