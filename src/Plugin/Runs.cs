namespace AiSdlc;

// One launch contract: resolve the step's declared prompt, mint the session id, pin the commit,
// run claude to completion, and write the row — whatever happened. POC-05's two triggers call
// this same function; a run is recorded even when the agent fails, and the row says which of
// process failure and provider error it was.
public sealed class Runs(Git git, Store store)
{
    public sealed record LaunchOutcome(RunRow Row, long Id, string? Problem);

    public LaunchOutcome LaunchAndRecord(string cwd, string step)
    {
        var declared = ReviewSteps.Read(ReviewSteps.FindDeclared(cwd, "review.json"));
        var prompt = ReviewSteps.PromptFor(declared, step);
        var sessionId = Guid.NewGuid().ToString();
        var startedAt = DateTimeOffset.UtcNow;

        // No prompt is a nameable absence, not an invented one — the launch is refused with its
        // remedy, and no row pretends a run happened.
        if (prompt is null)
        {
            var why = declared.Problem ?? "no prompt is declared for that step";
            return new(new RunRow(sessionId, Path.GetFullPath(cwd), step, null, null, startedAt.ToString("o"), null, null, null, null, null, null, null, false, null, null, why), 0, why);
        }

        var startingCommit = git.Head(Path.GetFullPath(cwd));
        var command = ResolveClaude();
        var launch = command is { } exe
            ? Agent.Launch(exe, Agent.ClaudeArguments(sessionId, prompt), Path.GetFullPath(cwd))
            : new Agent.LaunchResult(-1, "", "the claude CLI could not be found — install it or point PATH at it", 0, null);

        var facts = Agent.ParseResult(launch.StdOut);
        var transcript = Agent.Transcript(cwd, sessionId);
        var finishedAt = DateTimeOffset.UtcNow;
        var row = new RunRow(
            sessionId, Path.GetFullPath(cwd), step, prompt, startingCommit, startedAt.ToString("o"), finishedAt.ToString("o"),
            launch.ExitCode, facts.IsError, facts.CostUsd, facts.NumTurns,
            facts.DurationMs ?? (launch.DurationMs > 0 ? launch.DurationMs : null),
            transcript.Path, transcript.Located,
            facts.ResultSummary is { Length: > 512 } summary ? summary[..512] : facts.ResultSummary,
            launch.StdErr is { Length: > 0 } err ? (err.Length > 2048 ? err[^2048..] : err) : null,
            launch.Problem ?? facts.Problem);
        var id = store.RecordRun(row);
        if (launch.StdOut.Length > 0)
        {
            store.CaptureRun(sessionId, launch.StdOut);
        }

        return new(row, id, null);
    }

    public IReadOnlyList<RunRow> List(string cwd) => store.ListRuns(Path.GetFullPath(cwd));

    /// PATH first, the homebrew seat second — a named absence when neither is there.
    private static string? ResolveClaude() =>
        (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(System.IO.Path.PathSeparator).Where(p => p.Length > 0)
            .Select(p => System.IO.Path.Combine(p, "claude"))
            .Concat(["/opt/homebrew/bin/claude"])
            .FirstOrDefault(File.Exists);
}
