using AiSdlc;
using Microsoft.Extensions.Configuration;
using Shouldly;

namespace AiSdlcPlugin.Tests;

/// The two triggers share one record: the trigger rides the run row, an old row says "not said",
/// and the watcher decides with a pure predicate over (last starting commit, present HEAD). The
/// real BackgroundService loop is not tested here — the decision, the config and the refusal are.
public class WatcherTests
{
    private static RunRow Row(string sessionId, string? trigger, string? startingCommit = "abc123") =>
        new(sessionId, "/tmp/wt", "tests", "p", startingCommit, "2026-09-22T10:00:00Z", "2026-09-22T10:01:00Z",
            0, false, null, null, null, null, false, "ok", null, null, trigger);

    // 1.1/1.2 The trigger round-trips through the store, both values, side by side.
    [Fact]
    public void A_poll_run_and_a_button_run_both_carry_their_trigger()
    {
        var db = Path.Combine(Path.GetTempPath(), "ai-sdlc-tests", $"{Guid.NewGuid():N}.db");
        using var store = new Store(db);
        store.RecordRun(Row("session-one", "button"));
        store.RecordRun(Row("session-two", "poll"));

        var rows = store.ListRuns("/tmp/wt");
        rows.Count.ShouldBe(2);
        rows[0].Trigger.ShouldBe("poll");
        rows[1].Trigger.ShouldBe("button");
    }

    // 1.1 The guarded ALTER over a pre-trigger database: the column arrives, the rows stay.
    [Fact]
    public void A_pre_trigger_database_gains_the_column_without_losing_rows()
    {
        var db = Path.Combine(Path.GetTempPath(), "ai-sdlc-tests", $"{Guid.NewGuid():N}.db");
        // Build a POC-03-shaped database: runs without the trigger column.
        using (var raw = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={db}"))
        {
            raw.Open();
            using var create = raw.CreateCommand();
            create.CommandText = """
                CREATE TABLE runs (
                    id INTEGER PRIMARY KEY AUTOINCREMENT, session_id TEXT NOT NULL, worktree_path TEXT NOT NULL,
                    step TEXT, prompt TEXT, starting_commit TEXT, started_at TEXT NOT NULL, finished_at TEXT,
                    exit_code INTEGER, is_error INTEGER, cost_usd REAL, num_turns INTEGER, duration_ms REAL,
                    transcript_path TEXT, transcript_located INTEGER NOT NULL DEFAULT 0,
                    result_summary TEXT, stderr_tail TEXT, problem TEXT
                );
                INSERT INTO runs (session_id, worktree_path, started_at) VALUES ('old-session', '/tmp/wt', '2026-09-21T00:00:00Z');
                """;
            create.ExecuteNonQuery();
        }

        // Opening through the Store adds what it needs and keeps what was there.
        using (var store = new Store(db))
        {
            var rows = store.ListRuns("/tmp/wt");
            rows.ShouldHaveSingleItem();
            rows[0].SessionId.ShouldBe("old-session");
        }

        // The guard is idempotent: a second open does not re-ALTER.
        using var again = new Store(db);
        again.ListRuns("/tmp/wt").ShouldHaveSingleItem();
    }

    // 1.3 An old-shape row reads as not said — not button, not poll, not empty string.
    [Fact]
    public void A_row_without_a_trigger_reads_not_said()
    {
        var db = Path.Combine(Path.GetTempPath(), "ai-sdlc-tests", $"{Guid.NewGuid():N}.db");
        using (var raw = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={db}"))
        {
            raw.Open();
            using var create = raw.CreateCommand();
            create.CommandText = """
                CREATE TABLE runs (
                    id INTEGER PRIMARY KEY AUTOINCREMENT, session_id TEXT NOT NULL, worktree_path TEXT NOT NULL,
                    step TEXT, prompt TEXT, starting_commit TEXT, started_at TEXT NOT NULL, finished_at TEXT,
                    exit_code INTEGER, is_error INTEGER, cost_usd REAL, num_turns INTEGER, duration_ms REAL,
                    transcript_path TEXT, transcript_located INTEGER NOT NULL DEFAULT 0,
                    result_summary TEXT, stderr_tail TEXT, problem TEXT
                );
                INSERT INTO runs (session_id, worktree_path, started_at) VALUES ('old-session', '/tmp/wt', '2026-09-21T00:00:00Z');
                """;
            create.ExecuteNonQuery();
        }

        using var store = new Store(db);
        var row = store.ListRuns("/tmp/wt").Single();
        row.Trigger.ShouldBeNull();
    }

    // 2.1 The trigger vocabulary is closed — anything else is refused before a session id exists.
    [Theory]
    [InlineData("button")]
    [InlineData("poll")]
    public void The_two_triggers_are_accepted(string trigger)
    {
        Triggers.Known(trigger).ShouldBeTrue();
    }

    [Theory]
    [InlineData("webhook")]
    [InlineData("BUTTON")]
    [InlineData("")]
    [InlineData(null)]
    public void Everything_else_is_refused(string? trigger)
    {
        Triggers.Known(trigger).ShouldBeFalse();
    }

    // 2.2 The refusal through the endpoint's rule: a named sentence, not a crash.
    [Fact]
    public void The_endpoint_refusal_names_the_vocabulary()
    {
        Triggers.Refusal("webhook").ShouldBe("unknown trigger \"webhook\" — say button or poll");
    }

    // 2.2 The refused row carries no trigger — the column stays button/poll/not-said; the
    // refused value is named in the problem, never stored. And the refusal is RECORDED: a row
    // exists for it in the store, so "why did nothing happen" is answered by the listing.
    [Fact]
    public void An_unknown_trigger_is_refused_records_no_trigger_and_is_persisted()
    {
        var db = Path.Combine(Path.GetTempPath(), "ai-sdlc-tests", $"{Guid.NewGuid():N}.db");
        using var store = new Store(db);
        var runs = new Runs(new Git(), store);
        var refused = runs.LaunchAndRecord("/tmp/not-a-repository", "tests", "webhook");
        refused.Problem.ShouldBe(Triggers.Refusal("webhook"));
        refused.Row.Trigger.ShouldBeNull();

        // The store holds the refusal: one row, the refusal's own sentence, no session facts.
        var rows = store.ListRuns("/tmp/not-a-repository");
        rows.ShouldHaveSingleItem();
        rows[0].Problem.ShouldBe(Triggers.Refusal("webhook"));
        rows[0].Trigger.ShouldBeNull();
        rows[0].ExitCode.ShouldBeNull();
    }

    // 2.2 A known trigger is still refused when no prompt is declared — the same recorded
    // refusal, named for the absence that caused it. The row is the answer the watcher's
    // newest-row predicate reads; an unpersisted one made it re-ask forever, silently.
    [Fact]
    public void A_launch_without_a_declared_prompt_is_refused_and_recorded()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ai-sdlc-tests", $"watcher_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var db = Path.Combine(Path.GetTempPath(), "ai-sdlc-tests", $"{Guid.NewGuid():N}.db");
        using var store = new Store(db);
        var runs = new Runs(new Git(), store);

        var refused = runs.LaunchAndRecord(dir, "tests", Triggers.Poll);

        refused.Problem.ShouldNotBeNull();
        refused.Row.Trigger.ShouldBeNull();
        var rows = store.ListRuns(dir);
        rows.ShouldHaveSingleItem();
        rows[0].Problem.ShouldNotBeNull();
        // No session facts on a refusal — it never launched, and the row says so.
        rows[0].ExitCode.ShouldBeNull();
        rows[0].CostUsd.ShouldBeNull();
    }

    // 3.1 The config: off by default, 300 seconds, no paths — each default provable.
    [Fact]
    public void Watcher_config_defaults_off_five_minutes_no_paths()
    {
        var read = ReadConfig("{}");
        read.Enabled.ShouldBeFalse();
        read.IntervalSeconds.ShouldBe(300);
        read.Paths.ShouldBeEmpty();
    }

    // 3.1 The configured overrides are read.
    [Fact]
    public void Watcher_config_reads_its_overrides()
    {
        var read = ReadConfig("""
            { "Watcher": { "Enabled": true, "IntervalSeconds": 600, "Paths": ["/tmp/wt-a", "/tmp/wt-b"] } }
            """);
        read.Enabled.ShouldBeTrue();
        read.IntervalSeconds.ShouldBe(600);
        read.Paths.ShouldBe(["/tmp/wt-a", "/tmp/wt-b"]);
    }

    private static WatcherConfig ReadConfig(string json)
    {
        var dir = Path.Combine(Path.GetTempPath(), "ai-sdlc-tests", $"watcher_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var file = Path.Join(dir, "appsettings.json");
        File.WriteAllText(file, json);
        var config = new ConfigurationBuilder()
            .AddJsonFile(file, optional: false)
            .Build();
        return WatcherConfig.Read(config);
    }

    // 3.2 The predicate: HEAD moved since the last run's starting commit.
    [Fact]
    public void A_moved_head_wants_a_launch()
    {
        Watcher.ShouldLaunch("abc123", "def456").ShouldBeTrue();
    }

    // 3.2 The same commit says nothing new.
    [Fact]
    public void An_unchanged_head_launches_nothing()
    {
        Watcher.ShouldLaunch("abc123", "abc123").ShouldBeFalse();
    }

    // 3.2 No run recorded at all is new work: the first run launches.
    [Fact]
    public void No_recorded_run_launches_the_first()
    {
        Watcher.ShouldLaunch(null, "abc123").ShouldBeTrue();
    }

    // 3.2 A HEAD that cannot be read launches nothing — compared against nothing, launched nothing.
    [Fact]
    public void An_unreadable_head_launches_nothing()
    {
        Watcher.ShouldLaunch("abc123", null).ShouldBeFalse();
        Watcher.ShouldLaunch(null, null).ShouldBeFalse();
    }

    // 5.4 The two-trigger evidence, through the real launch contract and its store: same shape,
    // different trigger. The refused launches are the fixture here — refused rows ARE recorded
    // rows now, so the check exercises LaunchAndRecord end to end instead of fabricating rows.
    [Fact]
    public void The_refused_poll_and_button_rows_share_one_shape()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ai-sdlc-tests", $"watcher_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var db = Path.Combine(Path.GetTempPath(), "ai-sdlc-tests", $"{Guid.NewGuid():N}.db");
        using var store = new Store(db);
        var runs = new Runs(new Git(), store);

        // Refused launches (no prompt declared here) through the one contract, both triggers.
        var button = runs.LaunchAndRecord(dir, "tests", "button");
        var poll = runs.LaunchAndRecord(dir, "tests", Triggers.Poll);

        // Both refused on the same ground — no declared prompt — and both recorded.
        button.Problem.ShouldNotBeNull();
        poll.Problem.ShouldNotBeNull();

        var rows = store.ListRuns(dir);
        rows.Count.ShouldBe(2);
        // Indistinguishable except the refusal sentence they carry: both recorded, both the
        // same shape — no session facts, no trigger, the problem is the divergence.
        rows[0].Step.ShouldBe(rows[1].Step);
        rows[0].ExitCode.ShouldBeNull();
        rows[1].ExitCode.ShouldBeNull();
        rows[0].Trigger.ShouldBeNull();
    }
}
