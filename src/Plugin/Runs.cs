namespace AiSdlc;

// One launch contract: resolve the step's declared prompt, mint the session id, pin the commit,
// run claude to completion, and write the row — whatever happened. A run is recorded even when
// the agent fails, and the row says which of process failure and provider error it was.
public sealed class Runs(Git git, Store store)
{
    public sealed record LaunchOutcome(RunRow Row, long Id, string? Problem);

    public LaunchOutcome LaunchAndRecord(string cwd, string step, string trigger = "button")
    {
        // The closed vocabulary is a declaration checked, not guessed — an unknown trigger is
        // refused before anything is minted, the same discipline a declared file gets. The
        // refused row carries no trigger: the column stays button/poll/not-said, and the
        // problem sentence is where the refused value is named.
        if (!Triggers.Known(trigger))
        {
            return this.Refused(cwd, step, Guid.NewGuid().ToString(), DateTimeOffset.UtcNow.ToString("o"), Triggers.Refusal(trigger), null);
        }

        var declared = ReviewSteps.Read(ReviewSteps.FindDeclared(cwd, "review.json"));
        var prompt = ReviewSteps.PromptFor(declared, step);
        var sessionId = Guid.NewGuid().ToString();
        var startedAt = DateTimeOffset.UtcNow.ToString("o");
        if (prompt is null)
        {
            return this.Refused(cwd, step, sessionId, startedAt, declared.Problem ?? "no prompt is declared for that step", trigger);
        }

        var worktree = Path.GetFullPath(cwd);
        var exe = ResolveClaude();
        var launch = exe is { }
            ? Agent.Launch(exe, Agent.ClaudeArguments(sessionId, prompt), worktree)
            : new Agent.LaunchResult(-1, "", "the claude CLI could not be found — install it or point PATH at it", 0, null);
        var facts = Agent.ParseResult(launch.StdOut);
        var transcript = Agent.Transcript(worktree, sessionId);
        var row = new RunRow(
            sessionId, worktree, step, prompt, git.Head(worktree), startedAt, DateTimeOffset.UtcNow.ToString("o"),
            launch.ExitCode, facts.IsError, facts.CostUsd, facts.NumTurns,
            facts.DurationMs ?? (launch.DurationMs > 0 ? launch.DurationMs : null),
            transcript.Path, transcript.Located,
            facts.ResultSummary is { Length: > 512 } summary ? summary[..512] : facts.ResultSummary,
            launch.StdErr is { Length: > 0 } err ? err[^Math.Min(err.Length, 2048)..] : null,
            launch.Problem ?? facts.Problem, trigger);
        var id = store.RecordRun(row);
        if (launch.StdOut.Length > 0)
        {
            store.CaptureRun(sessionId, launch.StdOut);
        }

        return new(row, id, null);
    }

    // Refused: the launch never happened, and the row says why rather than pretending a run did.
    // The trigger rides even a refused row — a refused poller run and a refused button run are
    // still rows of the same two contracts.
    private LaunchOutcome Refused(string cwd, string step, string sessionId, string startedAt, string why, string? trigger) =>
        new(new RunRow(sessionId, Path.GetFullPath(cwd), step, null, null, startedAt, null, null, null, null, null, null, null, false, null, null, why, trigger), 0, why);

    public IReadOnlyList<RunRow> List(string cwd) => store.ListRuns(Path.GetFullPath(cwd));

    /// PATH first, the homebrew seat second — a named absence when neither is there.
    private static string? ResolveClaude() =>
        (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(System.IO.Path.PathSeparator).Where(p => p.Length > 0)
            .Select(p => System.IO.Path.Combine(p, "claude"))
            .Concat(["/opt/homebrew/bin/claude"])
            .FirstOrDefault(File.Exists);
}
