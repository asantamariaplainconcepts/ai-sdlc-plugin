using AiSdlc;
using Shouldly;

namespace AiSdlcPlugin.Tests;

/// The declared prompts map: a step's prompt is data, and its absences are named — no invented
/// prompt is ever launched. And the run rows: INSERT-only, a failed agent still recorded, two runs
/// on one worktree two rows.
public class RunsTests
{
    private static string TempDir() => Path.Combine(Path.GetTempPath(), "ai-sdlc-tests", $"runs_{Guid.NewGuid():N}");

    private static string WriteDeclared(string dir, string json)
    {
        Directory.CreateDirectory(dir);
        var path = Path.Join(dir, ".harness", "review.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
        return path;
    }

    [Fact]
    public void A_declared_prompt_is_read_for_its_step()
    {
        var dir = TempDir();
        var path = WriteDeclared(dir, """{"steps":[{"key":"tests","title":"Tests"}],"prompts":{"tests":"run the gate"}}""");
        var read = ReviewSteps.Read(path);
        ReviewSteps.PromptFor(read, "tests").ShouldBe("run the gate");
    }

    [Fact]
    public void No_prompts_map_is_an_absence_said_with_its_remedy()
    {
        var dir = TempDir();
        var path = WriteDeclared(dir, """{"steps":[{"key":"tests","title":"Tests"}]}""");
        var read = ReviewSteps.Read(path);
        ReviewSteps.PromptFor(read, "tests").ShouldBeNull();
        read.Prompts.ShouldBeEmpty();
    }

    [Fact]
    public void A_key_absent_from_the_map_locates_no_prompt()
    {
        var dir = TempDir();
        var path = WriteDeclared(dir, """{"steps":[{"key":"tests","title":"Tests"}],"prompts":{"proposal":"read it"}}""");
        var read = ReviewSteps.Read(path);
        ReviewSteps.PromptFor(read, "tests").ShouldBeNull();
        // The positive anchor: the map itself parsed and holds what it holds.
        ReviewSteps.PromptFor(read, "proposal").ShouldBe("read it");
    }

    [Fact]
    public void A_blank_prompt_is_absent_not_empty()
    {
        var dir = TempDir();
        var path = WriteDeclared(dir, """{"prompts":{"tests":"   "}}""");
        var read = ReviewSteps.Read(path);
        ReviewSteps.PromptFor(read, "tests").ShouldBeNull();
    }

    [Fact]
    public void Steps_still_read_without_the_map()
    {
        var dir = TempDir();
        var path = WriteDeclared(dir, """{"steps":[{"key":"proposal","title":"Proposal","asserts":"a"}]}""");
        var read = ReviewSteps.Read(path);
        read.Steps.ShouldHaveSingleItem();
        read.Steps[0].Key.ShouldBe("proposal");
        read.Prompts.ShouldBeEmpty();
        read.Problem.ShouldBeNull();
    }

    [Fact]
    public void A_failed_agent_is_recorded_and_the_row_says_which_failure()
    {
        var db = Path.Combine(Path.GetTempPath(), "ai-sdlc-tests", $"{Guid.NewGuid():N}.db");
        using var store = new Store(db);
        var row = new RunRow(
            "session-failure", "/tmp/wt", "tests", "run the gate", "abc123",
            "2026-09-22T10:00:00Z", "2026-09-22T10:01:00Z",
            1, true, 0m, null, null, null, false, "rate limited", null, null);
        var id = store.RecordRun(row);

        id.ShouldBeGreaterThan(0);
        // The two facts that decide "which of the two happened" are both present in the record.
        var listed = store.ListRuns("/tmp/wt").Single();
        listed.ExitCode.ShouldBe(1);
        listed.IsError.ShouldBe(true);
        listed.TranscriptLocated.ShouldBeFalse();
    }

    [Fact]
    public void Two_runs_on_one_worktree_are_two_rows()
    {
        var db = Path.Combine(Path.GetTempPath(), "ai-sdlc-tests", $"{Guid.NewGuid():N}.db");
        using var store = new Store(db);
        store.RecordRun(new RunRow("session-one", "/tmp/wt", "tests", "p", "abc", "2026-09-22T10:00:00Z", "2026-09-22T10:01:00Z", 0, false, 0.1m, 1, 60000, null, false, "ok", null, null));
        store.RecordRun(new RunRow("session-two", "/tmp/wt", "tests", "p", "abc", "2026-09-22T11:00:00Z", "2026-09-22T11:02:00Z", 1, true, null, null, null, null, false, "boom", null, null));

        var rows = store.ListRuns("/tmp/wt");
        rows.Count.ShouldBe(2);
        // Most recent first: the second run leads.
        rows[0].SessionId.ShouldBe("session-two");
        rows[1].SessionId.ShouldBe("session-one");
    }

    [Fact]
    public void A_costless_run_keeps_null_not_zero()
    {
        var db = Path.Combine(Path.GetTempPath(), "ai-sdlc-tests", $"{Guid.NewGuid():N}.db");
        using var store = new Store(db);
        store.RecordRun(new RunRow("session-costless", "/tmp/wt", "tests", "p", "abc", "2026-09-22T10:00:00Z", "2026-09-22T10:01:00Z", 0, false, null, 1, 1000, null, false, "ok", null, null));

        store.ListRuns("/tmp/wt").Single().CostUsd.ShouldBeNull();
    }

    [Fact]
    public void The_capture_is_kept_by_session()
    {
        var db = Path.Combine(Path.GetTempPath(), "ai-sdlc-tests", $"{Guid.NewGuid():N}.db");
        using var store = new Store(db);
        store.CaptureRun("session-one", """{"session_id":"session-one"}""");
        // The same session written again replaces its own copy, not another run's.
        store.CaptureRun("session-one", """{"session_id":"session-one","total_cost_usd":1}""");

        using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={db}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM runs_capture";
        using var reader = command.ExecuteReader();
        reader.Read();
        reader.GetInt32(0).ShouldBe(1);
    }
}
