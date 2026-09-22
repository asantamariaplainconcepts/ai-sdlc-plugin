using AiSdlc;
using Shouldly;

namespace AiSdlcPlugin.Tests;

/// The agent's readable facts: blob parsing against the shape claude 2.1.234 prints, and the
/// transcript rule measured in temp dirs. Fixtures only — no test launches the real CLI.
public class AgentParseTests
{
    // The blob a healthy run prints (shape verified against the CLI; fields trimmed to what is read).
    private const string Full =
        """
        {"is_error":false,"num_turns":1,"session_id":"11111111-2222-3333-4444-555555555555","total_cost_usd":0.084,"usage":{"input_tokens":2},"result":"done","duration_ms":1603,"type":"result"}
        """;

    [Fact]
    public void A_full_blob_reads_every_fact()
    {
        var facts = Agent.ParseResult(Full);
        facts.SessionId.ShouldBe("11111111-2222-3333-4444-555555555555");
        facts.CostUsd.ShouldBe(0.084m);
        facts.IsError.ShouldBe(false);
        facts.NumTurns.ShouldBe(1);
        facts.DurationMs.ShouldBe(1603);
        facts.ResultSummary.ShouldBe("done");
        facts.Problem.ShouldBeNull();
    }

    [Fact]
    public void An_erroring_blob_is_error()
    {
        var facts = Agent.ParseResult("""{"is_error":true,"subtype":"success","api_error_status":429,"session_id":"0a0a","total_cost_usd":0,"result":"rate limited"}""");
        facts.IsError.ShouldBe(true);
        facts.CostUsd.ShouldBe(0m);
        facts.ResultSummary.ShouldBe("rate limited");
    }

    [Fact]
    public void A_costless_blob_keeps_the_absence()
    {
        var facts = Agent.ParseResult("""{"is_error":false,"session_id":"abc","num_turns":2}""");
        // Null is not zero: the provider said nothing about cost, and the row must say so.
        facts.CostUsd.ShouldBeNull();
        facts.NumTurns.ShouldBe(2);
        facts.DurationMs.ShouldBeNull();
    }

    [Fact]
    public void An_unparseable_stdout_is_its_own_sentence()
    {
        var facts = Agent.ParseResult("the agent raved in prose");
        facts.SessionId.ShouldBeNull();
        facts.Problem.ShouldNotBeNull();
        facts.Problem!.ShouldContain("did not parse");
    }

    [Fact]
    public void Silence_is_named_not_zeroed()
    {
        var facts = Agent.ParseResult("");
        facts.SessionId.ShouldBeNull();
        facts.CostUsd.ShouldBeNull();
        facts.Problem!.ShouldContain("printed nothing");
    }

    [Fact]
    public void The_launch_line_pins_the_session_and_the_permission_posture()
    {
        var args = Agent.ClaudeArguments("guid-1", "run the gate");
        args.ShouldContain("--session-id");
        args.ShouldContain("guid-1");
        args.ShouldContain("--permission-mode");
        args.ShouldContain("acceptEdits");
        args.ShouldContain("--print");
        args.ShouldContain("--output-format");
        args.ShouldContain("run the gate");
    }

    [Fact]
    public void The_transcript_rule_munges_the_real_path()
    {
        // A cwd existing on disk, so the located side is exercised on a real file: the temp dir.
        var cwd = Path.Combine(Path.GetTempPath(), "ai-sdlc-tests", $"munge_{Guid.NewGuid():N}");
        Directory.CreateDirectory(cwd);
        try
        {
            var read = Agent.Transcript(cwd, "session-abcd");
            read.Path.ShouldNotBeNull();
            // The derived directory is the munged real path: separators, dots, underscores and
            // spaces are dashes, under .claude/projects of the user's home.
            read.Path.ShouldEndWith("session-abcd.jsonl");
            var munged = Path.GetFileName(Path.GetDirectoryName(read.Path)!);
            munged.ShouldNotContain("_");
            munged.ShouldNotContain(".");
            // The temp path's own separators are gone.
            munged.ShouldStartWith("-");
            read.Located.ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(cwd, true);
        }
    }

    [Fact]
    public void A_session_id_that_is_not_a_file_name_locates_nothing()
    {
        var read = Agent.Transcript(Path.GetTempPath(), "not/allowed");
        read.Path.ShouldBeNull();
        read.Located.ShouldBeFalse();
    }

    [Fact]
    public void A_process_that_cannot_start_says_so()
    {
        var launch = Agent.Launch("definitely-not-a-command-xyz", ["--version"], Path.GetTempPath());
        launch.ExitCode.ShouldBe(-1);
        launch.Problem.ShouldNotBeNull();
        launch.Problem.ShouldContain("could not start");
    }

    [Fact]
    public void A_real_command_reports_its_exit_code_and_output()
    {
        // A command that exists everywhere this suite runs: the exit code and captured stdout
        // are the two facts the run row records.
        var launch = Agent.Launch("/bin/echo", ["agent-seam-check"], Path.GetTempPath());
        launch.ExitCode.ShouldBe(0);
        launch.Problem.ShouldBeNull();
        launch.StdOut.ShouldContain("agent-seam-check");
        launch.DurationMs.ShouldBeGreaterThanOrEqualTo(0);
    }
}
