namespace AiSdlc;

// The gates cut POC-04 owns: running the declared gate commands over the candidate the agent
// left, capturing exit code and a bounded tail, and recording both against the resulting commit
// so a moved HEAD is staleness, not a silent pass. The outcome rule is written down — exit 0
// with output = pass, non-zero = fail, could-not-start/timeout/silence = no concluyente — and
// a gate that cannot run is refuted when its declaration is read, never at run time.
public static class Gates
{
    public enum Reading { Pass, Fail, Inconclusive }

    public enum Freshness { Current, Stale, Unknown }

    /// The recorded outcome of one gate run: (exit, combined output, problem) → one of three
    /// readings. Null is not zero — a missing exit (killed, never ran) is inconclusive, and a
    /// silent exit-0 is the epic's "un comando arrancado y cero tests no es un pass".
    public static Reading Outcome(int? exitCode, string output, string? problem) =>
        problem is not null || exitCode is null
            ? Reading.Inconclusive
            : exitCode != 0 ? Reading.Fail : output.Trim().Length == 0 ? Reading.Inconclusive : Reading.Pass;

    /// Whether a recorded row is still about the directory: the commit it was recorded against
    /// versus the present HEAD. Unknown is its own answer — a HEAD that cannot be read cannot
    /// bless a row.
    public static Freshness Staleness(string? recordedCommit, string? head) =>
        recordedCommit is null || head is null ? Freshness.Unknown : recordedCommit == head ? Freshness.Current : Freshness.Stale;

    /// The stored tail of a gate's output: the last lines, byte-capped, with the cut named
    /// rather than silent. Empty string in, empty string out.
    public static string Tail(string output, int maxLines, int maxBytes)
    {
        if (output.Length == 0)
        {
            return "";
        }

        var lines = output.Split('\n').ToList();
        var tailed = lines.Count > maxLines ? [$".. {lines.Count - maxLines} earlier lines cut ..", .. lines[^maxLines..]] : lines;
        var text = string.Join('\n', tailed).TrimEnd();
        if (System.Text.Encoding.UTF8.GetByteCount(text) <= maxBytes)
        {
            return text;
        }

        var bounded = text[^Math.Min(text.Length, maxBytes)..];
        var cut = bounded.IndexOf('\n');
        return $".. cut to the last bytes ..{Environment.NewLine}{(cut >= 0 && cut + 1 < bounded.Length ? bounded[(cut + 1)..] : bounded)}";
    }

    public sealed record RunLine(string Executable, IReadOnlyList<string> Args);

    /// A declared run line is a line a person types, not argv: split on whitespace, first token
    /// the executable, the rest arguments. No quoting, no pipes — the simplification this PoC
    /// records rather than grows.
    public static RunLine ParseRunLine(string run) =>
        run.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) is { Length: > 0 } parts
            ? new(parts[0], parts[1..])
            : new("", []);

    /// Where a declared executable has to be: relative to the repo root that declares it, then
    /// PATH. Null is "nowhere" — the caller refutes before running, at declaration-read time.
    public static string? ResolveExecutable(string executable, string repoRoot, IReadOnlyList<string> pathEntries)
    {
        if (executable.Length == 0)
        {
            return null;
        }

        if (executable.Contains('/') || executable.Contains('\\'))
        {
            var joined = Path.Join(repoRoot, executable);
            return File.Exists(joined) ? joined : null;
        }

        var name = OperatingSystem.IsWindows() && !executable.EndsWith(".exe") ? $"{executable}.exe" : executable;
        return pathEntries.Select(p => Path.Join(p, name)).Concat([Path.Join(repoRoot, name)]).FirstOrDefault(File.Exists);
    }

    /// The newest recorded outcome per command key — a worktree's gates presentation, one row
    /// per gate rather than a ledger.
    public static IReadOnlyDictionary<string, GateRow> LatestPerGate(IReadOnlyList<GateRow> rows) =>
        rows.GroupBy(r => r.CommandKey).ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.Id).First());

    public static readonly TimeSpan Timeout = TimeSpan.FromMinutes(10);

    public sealed record GateView(
        string Key, string Name, bool Missing, string? MissingSentence, Reading? Reading,
        int? ExitCode, string? Commit, string? Head, Freshness Freshness, string? FinishedAtIso, string? Tail, string? Problem);

    public sealed record RunOutcome(IReadOnlyList<GateView> Gates, string? Problem);

    /// Run the declared gates over the candidate the agent left: refute unrunnable gates at
    /// declaration time (no row, they never ran), launch the rest through the shared seam with
    /// the timeout, read the commit AFTER the runs (the resulting commit), and record exit +
    /// bounded tail against it. The declaration is untrusted input — read bounded, executed
    /// only as the person's explicit act of asking for gates to run.
    public static RunOutcome RunDeclaredGates(string cwd, Git git, Store store)
    {
        var declared = Commands.Read(ReviewSteps.FindDeclared(cwd, "commands.json"));
        if (declared.Absent || declared.Unreadable)
        {
            return new([], declared.Problem);
        }

        var repoRoot = Path.GetDirectoryName(Path.GetDirectoryName(ReviewSteps.FindDeclared(cwd, "commands.json")))!;
        var pathEntries = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(System.IO.Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var views = new List<GateView>();
        var dirty = git.Uncommitted(cwd) > 0;
        foreach (var gate in declared.Gates)
        {
            var line = ParseRunLine(gate.Run);
            var exe = ResolveExecutable(line.Executable, repoRoot, pathEntries);
            if (exe is null)
            {
                // Refuted at declaration time, never at run time: no process, no row, a sentence.
                views.Add(new(gate.Key, gate.Name, true, $"{line.Executable} is not findable — not a path from {repoRoot} and not on PATH", null, null, null, null, Freshness.Unknown, null, null, null));
                continue;
            }

            var launch = Agent.Launch(exe, line.Args, cwd, (int)Timeout.TotalMilliseconds);
            views.Add(View(gate.Key, gate.Name, launch.ExitCode, launch.StdOut + launch.StdErr, launch.Problem));
        }

        // The resulting commit, read after the gates ran; a dirty tree is said, not refused.
        var head = git.Head(cwd);
        var finishedAt = DateTimeOffset.UtcNow.ToString("o");
        foreach (var view in views.Where(v => !v.Missing).ToList())
        {
            var tail = view.Tail ?? "";
            store.RecordGateResult(new GateRow(0, cwd, head ?? "", view.Key, view.ExitCode, tail, finishedAt, view.Problem));
        }

        return new(views.Select(v => v with { Commit = head, Head = head, Freshness = Freshness.Current }).ToList(), dirty ? "the tree is dirty — the gates ran against uncommitted work; the recording is keyed to the observed HEAD" : null);
    }

    /// The reading surface: each declared gate with its latest outcome, its reading, the commit
    /// it was recorded against and whether HEAD has moved since — never re-run here.
    public static RunOutcome ReadDeclaredGates(string cwd, Git git, Store store)
    {
        var declared = Commands.Read(ReviewSteps.FindDeclared(cwd, "commands.json"));
        if (declared.Absent || declared.Unreadable)
        {
            return new([], declared.Problem);
        }

        var repoRoot = Path.GetDirectoryName(Path.GetDirectoryName(ReviewSteps.FindDeclared(cwd, "commands.json")))!;
        var pathEntries = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(System.IO.Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var head = git.Head(cwd);
        var latest = LatestPerGate(store.ListGateResults(cwd));
        var views = new List<GateView>();
        foreach (var gate in declared.Gates)
        {
            var line = ParseRunLine(gate.Run);
            var exe = ResolveExecutable(line.Executable, repoRoot, pathEntries);
            if (exe is null)
            {
                views.Add(new(gate.Key, gate.Name, true, $"{line.Executable} is not findable — not a path from {repoRoot} and not on PATH", null, null, null, head, Freshness.Unknown, null, null, null));
                continue;
            }

            if (!latest.TryGetValue(gate.Key, out var row))
            {
                views.Add(new(gate.Key, gate.Name, false, null, null, null, null, head, Freshness.Unknown, null, null, null));
                continue;
            }

            views.Add(new(gate.Key, gate.Name, false, null, Outcome(row.ExitCode, row.Tail, row.Problem), row.ExitCode, row.Commit, head, Staleness(row.Commit, head), row.FinishedAtIso, row.Tail, row.Problem));
        }

        return new(views, null);
    }

    private static GateView View(string key, string name, int? exitCode, string output, string? problem)
    {
        var tail = Tail(output, 64, 8192);
        return new(key, name, false, null, Outcome(exitCode, output, problem), exitCode, null, null, Freshness.Current, null, tail, problem);
    }
}

/// One recorded gate outcome: exit, tail, and the commit it was recorded against. INSERT-only —
/// a moved HEAD makes rows stale by comparison, never by update.
public sealed record GateRow(long Id, string WorktreePath, string Commit, string CommandKey, int? ExitCode, string Tail, string FinishedAtIso, string? Problem);
