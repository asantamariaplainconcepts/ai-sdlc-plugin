using System.Text;
using AiSdlc;
using Shouldly;

namespace AiSdlcPlugin.Tests;

/// Tests for the gate readings: outcome (pass / fail / no concluyente), staleness against the
/// current HEAD, the bounded tail, the run-line split, executable resolution and the latest-row
/// per gate. All pure — the only process tests are the cheap seam ones further down.
public class GatesTests
{
    // 1.1 Outcome — exit 0 with output is a pass.
    [Fact]
    public void Exit_zero_with_output_passes()
    {
        Gates.Outcome(0, "gate passed", null).ShouldBe(Gates.Reading.Pass);
    }

    // 1.1 A non-zero exit fails whatever the output says.
    [Fact]
    public void Nonzero_exit_fails()
    {
        Gates.Outcome(1, "gate failed", null).ShouldBe(Gates.Reading.Fail);
        Gates.Outcome(2, "", null).ShouldBe(Gates.Reading.Fail);
    }

    // 1.1 The epic's own sentence: a run with zero output is never a pass.
    [Fact]
    public void Exit_zero_without_output_is_inconclusive()
    {
        Gates.Outcome(0, "", null).ShouldBe(Gates.Reading.Inconclusive);
    }

    // 1.1 A problem (could not start, timeout, killed) is inconclusive — the wall clock is not
    // a judge and neither is a start failure.
    [Fact]
    public void A_problem_is_inconclusive()
    {
        Gates.Outcome(-1, "", "./gate.sh could not start: no such file").ShouldBe(Gates.Reading.Inconclusive);
        Gates.Outcome(null, "", "timed out").ShouldBe(Gates.Reading.Inconclusive);
    }

    // 1.2 Staleness — a recorded commit equal to HEAD is current.
    [Fact]
    public void Recorded_commit_matching_head_is_current()
    {
        Gates.Staleness("abc123", "abc123").ShouldBe(Gates.Freshness.Current);
    }

    // 1.2 A different commit is stale — the reading must say so rather than show it as valid.
    [Fact]
    public void A_moved_head_makes_the_recording_stale()
    {
        Gates.Staleness("abc123", "def456").ShouldBe(Gates.Freshness.Stale);
    }

    // 1.2 A HEAD that cannot be read is its own answer, not a default.
    [Fact]
    public void An_unreadable_head_is_its_own_answer()
    {
        Gates.Staleness("abc123", null).ShouldBe(Gates.Freshness.Unknown);
        // Recorded-commit absent is also unknown — comparing nothing is not current.
        Gates.Staleness(null, "abc123").ShouldBe(Gates.Freshness.Unknown);
    }

    // 1.3 Short output is kept whole.
    [Fact]
    public void Short_output_is_kept_whole()
    {
        Gates.Tail("one\ntwo", 64, 8192).ShouldBe("one\ntwo");
    }

    // 1.3 Long output is tailed to the configured lines, earlier lines named as cut.
    [Fact]
    public void Long_output_keeps_the_last_lines_and_marks_the_cut()
    {
        var lines = Enumerable.Range(1, 100).Select(i => $"line {i}").ToList();
        var tail = Gates.Tail(string.Join('\n', lines), 64, 8192);
        var tailed = tail.Split('\n').ToList();
        tailed.Count.ShouldBe(65); // the marker plus 64 lines
        tailed[0].ShouldContain("cut");
        tailed[^1].ShouldBe("line 100");
        tailed[1].ShouldBe("line 37"); // 100 - 64 + 1
    }

    // 1.3 Output longer than the byte bound is capped.
    [Fact]
    public void Output_over_the_byte_bound_is_capped()
    {
        var text = string.Join('\n', Enumerable.Range(1, 10).Select(i => new string('x', 2000)));
        var tail = Gates.Tail(text, 64, 8192);
        Encoding.UTF8.GetByteCount(tail).ShouldBeLessThanOrEqualTo(8192);
    }

    // 1.4 The run line splits into executable and args; the executable is resolvable later.
    [Fact]
    public void A_run_line_splits_into_executable_and_args()
    {
        var line = Gates.ParseRunLine("./gate.sh");
        line.Executable.ShouldBe("./gate.sh");
        line.Args.ShouldBeEmpty();
    }

    [Fact]
    public void A_run_line_with_arguments_carries_them()
    {
        var line = Gates.ParseRunLine("dotnet test --no-build");
        line.Executable.ShouldBe("dotnet");
        line.Args.ShouldBe(["test", "--no-build"]);
    }

    // 1.5 Resolution: relative to the declaration's repo root first.
    [Fact]
    public void An_executable_relative_to_the_root_is_found()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ai-sdlc-tests", $"gates_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Join(dir, "gate.sh"), "#!/bin/bash\n");
        var resolved = Gates.ResolveExecutable("./gate.sh", dir, []);
        resolved.ShouldNotBeNull();
        resolved!.EndsWith("gate.sh").ShouldBeTrue();
    }

    // 1.5 Resolution: PATH is the fallback.
    [Fact]
    public void An_executable_on_path_is_found()
    {
        var resolved = Gates.ResolveExecutable("sh", "/tmp", ["/bin", "/usr/bin"]);
        resolved.ShouldNotBeNull();
    }

    // 1.5 An executable that is nowhere is not resolvable — the caller refutes at declaration
    // time, before any run.
    [Fact]
    public void A_missing_executable_is_not_resolvable()
    {
        var resolved = Gates.ResolveExecutable("./not-there.sh", "/tmp", []);
        resolved.ShouldBeNull();
    }

    // 1.6 The latest recorded outcome per command key.
    [Fact]
    public void The_latest_row_per_gate_wins()
    {
        var rows = new List<GateRow>
        {
            new(1, "/wt", "abc", "gate", 0, "ok", "2026-09-22T10:00:00Z", null),
            new(2, "/wt", "abc", "gate", 1, "later failure", "2026-09-22T11:00:00Z", null),
            new(3, "/wt", "def", "other", 0, "other gate", "2026-09-22T09:00:00Z", null),
        };
        var latest = Gates.LatestPerGate(rows);
        latest["gate"].ExitCode.ShouldBe(1);
        latest["gate"].FinishedAtIso.ShouldBe("2026-09-22T11:00:00Z");
        latest["other"].ExitCode.ShouldBe(0);
    }

    // 2.2/2.3 Store rows: INSERT-only — a second run adds rows, earlier rows stay untouched.
    [Fact]
    public void Gate_results_insert_only()
    {
        var db = Path.Combine(Path.GetTempPath(), "ai-sdlc-tests", $"{Guid.NewGuid():N}.db");
        using (var store = new Store(db))
        {
            store.RecordGateResult(new GateRow(0, "/wt", "abc", "gate", 0, "first", "2026-09-22T10:00:00Z", null));
            store.RecordGateResult(new GateRow(0, "/wt", "abc", "gate", 1, "second", "2026-09-22T11:00:00Z", null));
        }

        using (var store = new Store(db))
        {
            var rows = store.ListGateResults("/wt");
            rows.Count.ShouldBe(2);
            rows[0].Tail.ShouldBe("second");
            rows[1].Tail.ShouldBe("first");
        }
    }

    // 3.3 Cheap seam tests: echo passes, a missing command names its problem, a timeout kills.
    [Fact]
    public void The_seam_runs_echo_to_completion()
    {
        var launch = Agent.Launch("/bin/echo", ["hello"], Path.GetTempPath());
        launch.ExitCode.ShouldBe(0);
        launch.StdOut.Trim().ShouldBe("hello");
        launch.Problem.ShouldBeNull();
    }

    [Fact]
    public void A_command_that_cannot_start_says_so()
    {
        var launch = Agent.Launch("/no/such/binary", [], Path.GetTempPath());
        launch.ExitCode.ShouldBe(-1);
        launch.Problem.ShouldNotBeNull();
        launch.Problem!.ShouldContain("could not start");
    }

    [Fact]
    public void A_timeout_kills_and_names_itself()
    {
        var launch = Agent.Launch("/bin/sleep", ["30"], Path.GetTempPath(), timeoutMs: 1000);
        launch.ExitCode.ShouldBe(-1);
        launch.Problem.ShouldNotBeNull();
        launch.Problem!.ShouldContain("timed out");
    }

    // 3.4 Orchestration survivors (F4): the refute-at-declaration wiring through the real
    // RunDeclaredGates/ReadDeclaredGates — the pure helpers above are covered, but the wiring
    // a refactor could silently break (missing ⇒ no launch, no row) is its own fact.
    [Fact]
    public void A_declared_missing_binary_is_refuted_through_the_run_orchestration()
    {
        var repo = Path.Combine(Path.GetTempPath(), "ai-sdlc-tests", $"gates_{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Join(repo, ".harness"));
        File.WriteAllText(Path.Join(repo, ".harness", "commands.json"),
            """{"commands":[{"key":"ghost","name":"Ghost","run":"definitely-not-a-real-binary","gate":true}]}""");
        var db = Path.Combine(Path.GetTempPath(), "ai-sdlc-tests", $"{Guid.NewGuid():N}.db");
        using var store = new Store(db);
        var git = new Git();

        var outcome = Gates.RunDeclaredGates(repo, git, store);

        outcome.Gates.ShouldHaveSingleItem();
        var gate = outcome.Gates[0];
        gate.Missing.ShouldBeTrue();
        gate.Reading.ShouldBeNull();
        gate.MissingSentence.ShouldNotBeNull();
        gate.MissingSentence!.ShouldContain("definitely-not-a-real-binary");
        // The contract of the refusal: no process started, no row recorded — the store is empty.
        store.ListGateResults(repo).ShouldBeEmpty();
    }

    [Fact]
    public void A_declared_missing_binary_is_refuted_through_the_read_orchestration()
    {
        var repo = Path.Combine(Path.GetTempPath(), "ai-sdlc-tests", $"gates_{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Join(repo, ".harness"));
        File.WriteAllText(Path.Join(repo, ".harness", "commands.json"),
            """{"commands":[{"key":"ghost","name":"Ghost","run":"definitely-not-a-real-binary","gate":true}]}""");
        var db = Path.Combine(Path.GetTempPath(), "ai-sdlc-tests", $"{Guid.NewGuid():N}.db");
        using var store = new Store(db);
        var git = new Git();

        var outcome = Gates.ReadDeclaredGates(repo, git, store);

        outcome.Gates.ShouldHaveSingleItem();
        outcome.Gates[0].Missing.ShouldBeTrue();
        outcome.Gates[0].MissingSentence.ShouldNotBeNull();
        outcome.Gates[0].MissingSentence!.ShouldContain("definitely-not-a-real-binary");
        store.ListGateResults(repo).ShouldBeEmpty();
    }
}

