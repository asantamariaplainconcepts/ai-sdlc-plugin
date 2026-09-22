namespace AiSdlc;

public static class Facts
{
    public enum Tone { Plain, Warn, Bad }

    public sealed record Fact(string Key, string Text, Tone Tone, string? Title = null);

    public sealed record PullRequestAnswer(bool Unreachable, string? Reason, string? Detail, int? Number, string? State, bool? IsDraft, string? Title);

    public sealed record CheckOutcome(int? ExitCode, int? Since);

    public sealed record Input(
        int? Ahead, int? Behind, IReadOnlyList<NumstatRow> Changed, bool DiffPending,
        IReadOnlyList<string> Conflicts, bool MergeUnknown, int Uncommitted, string? Head,
        IReadOnlyList<string> Gates, IReadOnlyList<(string Name, CheckOutcome Outcome)> Outcomes,
        Read<Seam.Row[]> SeamRows, PullRequestAnswer Pr, bool PrPending,
        string? IssueKey, string? IssueTitle, string? IssueState);

    // One sentence per reason a pull request could not be asked about, each with its remedy.
    private static readonly (string Reason, string Sentence)[] PrUnreachable =
    [
        ("NotSignedIn", "gh is not signed in — run gh auth login"),
        ("CliMissing", "the gh CLI is not installed — install it"),
        ("NoGitHubRemote", "no GitHub remote — add one or set the issue elsewhere"),
        ("TimedOut", "asking GitHub timed out — try again"),
    ];

    /// The eight facts, in the order they are read. Every absence is its own sentence with its
    /// remedy: null is never zero, "could not ask" is never "there is none", no fact is a tick.
    public static IReadOnlyList<Fact> Header(Input i)
    {
        var facts = new List<Fact>
        {
            i.IssueKey is null
                ? new("issue", "no issue resolved — name the branch after it (change/123) or run a task here", Tone.Plain, i.IssueTitle)
                : new("issue", string.IsNullOrWhiteSpace(i.IssueTitle) ? $"issue {i.IssueKey}" : $"issue {i.IssueKey} {i.IssueTitle}", Tone.Plain, i.IssueState),
        };

        // PR: three answers drawn as three (#235); a draft is open and not asking, so two facts.
        if (i.PrPending)
        {
            facts.Add(new("pr", "pull request not read yet", Tone.Plain));
        }
        else if (i.Pr.Unreachable)
        {
            var sentence = PrUnreachable.FirstOrDefault(r => r.Reason == i.Pr.Reason).Sentence ?? "GitHub refused the question";
            facts.Add(new("pr", "pull request could not be asked", Tone.Plain, $"{sentence} {i.Pr.Detail}".Trim()));
        }
        else if (i.Pr.Number is null)
        {
            facts.Add(new("pr", "no pull request", Tone.Plain));
        }
        else
        {
            facts.Add(new("pr", $"pull request #{i.Pr.Number}", Tone.Plain, i.Pr.Title));
            facts.Add(new("pr-state", i.Pr.IsDraft == true ? "draft" : (i.Pr.State ?? "open").ToLowerInvariant(), Tone.Plain));
        }

        facts.Add(i.DiffPending
            ? new("changed", "diff not read yet", Tone.Plain)
            : new("changed", $"{i.Changed.Count} files +{i.Changed.Sum(d => d.Added)} -{i.Changed.Sum(d => d.Removed)}", Tone.Plain));

        facts.Add(i.Ahead is null || i.Behind is null
            ? new("base", "no trunk to compare against — fetch the default branch", Tone.Plain)
            : new("base", $"{i.Ahead} ahead, {i.Behind} behind", i.Behind == 0 ? Tone.Plain : Tone.Warn));

        facts.Add(i.MergeUnknown
            ? new("merge", "merge not asked — no trunk to merge into", Tone.Plain)
            : i.Conflicts.Count == 0
                ? new("merge", "no conflicts", Tone.Plain)
                : new("merge", $"{i.Conflicts.Count} conflicting", Tone.Bad, string.Join(", ", i.Conflicts)));

        facts.Add(Checks(i.Gates, i.Outcomes, i.Head));

        // Seam: three readings that stay three (#283) — a cut body is not an undeclared seam.
        if (i.SeamRows.IsUnreadable)
        {
            facts.Add(new("seam", "seam not compared — the issue body was cut", Tone.Plain));
        }
        else if (!i.SeamRows.IsPresent)
        {
            facts.Add(new("seam", "seam not compared — no task or no declaration", Tone.Plain));
        }
        else
        {
            var outside = i.SeamRows.Value!.Where(r => r.State == Seam.State.Undeclared).ToList();
            facts.Add(outside.Count == 0
                ? new("seam", "seam all inside", Tone.Plain, $"{i.SeamRows.Value!.Length} declared, all touched")
                : new("seam", $"{outside.Count} outside the seam", Tone.Warn, string.Join(", ", outside.Select(r => r.Path))));
        }

        facts.Add(new("tree", i.Uncommitted == 0 ? "tree clean" : $"{i.Uncommitted} uncommitted", i.Uncommitted == 0 ? Tone.Plain : Tone.Warn));
        return facts;
    }

    /// The five checks readings; the worst thing true of the set is the sentence (#147).
    private static Fact Checks(IReadOnlyList<string> gates, IReadOnlyList<(string Name, CheckOutcome Outcome)> outcomes, string? head)
    {
        if (gates.Count == 0)
        {
            return new("checks", "no checks declared — declare one in .harness/commands.json with \"gate\": true", Tone.Plain);
        }

        var ran = outcomes.Where(o => o.Outcome.ExitCode is not null).ToList();
        if (ran.Count == 0)
        {
            // POC-05: gates declared but never run is a failure to look, not silence — a run
            // arriving with empty gates (however triggered) reads as warn, never as quiet.
            return new("checks", "checks not run here", Tone.Warn);
        }

        var failed = ran.Where(o => o.Outcome.ExitCode != 0).ToList();
        if (failed.Count > 0)
        {
            return new("checks", $"{failed.Count} checks failing", Tone.Bad, string.Join(", ", failed.Select(o => o.Name)));
        }

        // Stale: an outcome whose commit the directory has moved past, or is no longer comparable.
        if (ran.Any(o => o.Outcome.Since is null || o.Outcome.Since > 0))
        {
            return new("checks", "checks ran on an older commit", Tone.Warn);
        }

        return new("checks", head is null ? $"{ran.Count} checks passed here" : $"{ran.Count} checks passed on {head[..Math.Min(7, head.Length)]}", Tone.Plain);
    }
}
