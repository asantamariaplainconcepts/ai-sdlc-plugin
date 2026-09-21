namespace AiSdlc;

/// The eight header facts, as plain data derived from plain data. Port of harness facts.ts
/// (837c7ca): tone carries no success color; every absence is its own sentence; null is never zero.
public static class Facts
{
    public enum Tone
    {
        Plain,
        Warn,
        Bad,
    }

    public sealed record Fact(string Key, string Text, Tone Tone, string? Title = null);

    public sealed record PullRequestAnswer(bool Unreachable, string? Reason, string? Detail, int? Number, string? State, bool? IsDraft, string? Title);

    public sealed record CheckOutcome(int? ExitCode, string? Commit, int? Since);

    /// The five checks readings, worst-true-thing-first: undeclared / none-ran / failed / stale / passed (naming the commit).
    public sealed record ChecksFact(string Key, string Text, Tone Tone, string? Title = null);

    public static IReadOnlyList<Fact> Header(
        string? branch,
        int? ahead,
        int? behind,
        IReadOnlyList<NumstatRow> diff,
        bool diffPending,
        IReadOnlyList<string> conflicts,
        bool mergeUnknown,
        int uncommittedCount,
        string? head,
        IReadOnlyList<string> gateCommandNames,
        IReadOnlyList<(string Name, CheckOutcome Outcome)> outcomes,
        Read<Seam.Row[]> seamRows,
        PullRequestAnswer pr,
        bool prPending,
        string? issueKey,
        string? issueTitle,
        string? issueState)
    {
        var facts = new List<Fact>();

        // Issue context: said, not implied — the header names which issue this is, or that none is resolved.
        facts.Add(issueKey is null
            ? new Fact("issue", "no issue resolved — declare one by naming the branch after it (change/123) or running a task here", Tone.Plain, Title: issueTitle)
            : new Fact("issue", string.IsNullOrWhiteSpace(issueTitle) ? $"issue {issueKey}" : $"issue {issueKey} {issueTitle}", Tone.Plain, Title: issueState));

        // PR: three answers drawn as three (#235).
        if (prPending)
        {
            facts.Add(new Fact("pr", "pull request not read yet", Tone.Plain));
        }
        else if (pr.Unreachable)
        {
            facts.Add(new Fact("pr", "pull request could not be asked", Tone.Plain,
                Title: $"{PullRequestUnreachable(pr.Reason)} {pr.Detail}".Trim()));
        }
        else if (pr.Number is null)
        {
            facts.Add(new Fact("pr", "no pull request", Tone.Plain));
        }
        else
        {
            facts.Add(new Fact("pr", $"pull request #{pr.Number}", Tone.Plain, Title: pr.Title));
            facts.Add(new Fact("pr-state", pr.IsDraft == true ? "draft" : (pr.State ?? "open").ToLowerInvariant(), Tone.Plain));
        }

        // Changed: null is "diff still being read", never zero.
        if (diffPending)
        {
            facts.Add(new Fact("changed", "diff not read yet", Tone.Plain));
        }
        else
        {
            var files = diff.Count;
            var added = diff.Sum(d => d.Added);
            var removed = diff.Sum(d => d.Removed);
            facts.Add(new Fact("changed", $"{files} files +{added} -{removed}", Tone.Plain));
        }

        // Base: null is its own answer (#146).
        facts.Add(ahead is null || behind is null
            ? new Fact("base", "no trunk to compare against", Tone.Plain)
            : new Fact("base", $"{ahead} ahead, {behind} behind", behind == 0 ? Tone.Plain : Tone.Warn));

        // Merge.
        if (mergeUnknown)
        {
            facts.Add(new Fact("merge", "merge not asked", Tone.Plain));
        }
        else if (conflicts.Count == 0)
        {
            facts.Add(new Fact("merge", "no conflicts", Tone.Plain));
        }
        else
        {
            facts.Add(new Fact("merge", $"{conflicts.Count} conflicting", Tone.Bad, Title: string.Join(", ", conflicts)));
        }

        // Checks: success avoided; the worst thing true of the set is the sentence.
        var checks = Checks(gateCommandNames, outcomes, head);
        facts.Add(new Fact(checks.Key, checks.Text, checks.Tone, checks.Title));

        // Seam: three readings that stay three (#283).
        if (seamRows.IsUnreadable)
        {
            facts.Add(new Fact("seam", "seam not compared — the issue body was cut", Tone.Plain));
        }
        else if (!seamRows.IsPresent)
        {
            facts.Add(new Fact("seam", "seam not compared — no task or no declaration", Tone.Plain));
        }
        else
        {
            var rows = seamRows.Value!;
            var outside = rows.Where(r => r.State == Seam.State.Undeclared).ToList();
            facts.Add(outside.Count == 0
                ? new Fact("seam", $"seam all inside — {rows.Length} declared, all touched", Tone.Plain)
                : new Fact("seam", $"{outside.Count} outside the seam", Tone.Warn, Title: string.Join(", ", outside.Select(r => r.Path))));
        }

        // Tree: the probe's own sentence ("2 uncommitted"), amber not red.
        facts.Add(new Fact("tree", uncommittedCount == 0 ? "tree clean" : $"{uncommittedCount} uncommitted", uncommittedCount == 0 ? Tone.Plain : Tone.Warn));

        return facts;
    }

    /// One sentence per reason a pull request could not be asked about, with its remedy.
    private static string PullRequestUnreachable(string? reason) => reason switch
    {
        "NotSignedIn" => "gh is not signed in — run gh auth login",
        "CliMissing" => "the gh CLI is not installed — install it",
        "NoGitHubRemote" => "no GitHub remote — add one or set the issue elsewhere",
        "NotAskable" => "no GitHub remote — add one or set the issue elsewhere",
        "TimedOut" => "asking GitHub timed out — try again",
        _ => "GitHub refused the question",
    };

    private static ChecksFact Checks(IReadOnlyList<string> declared, IReadOnlyList<(string Name, CheckOutcome Outcome)> outcomes, string? head)
    {
        if (declared.Count == 0)
        {
            return new("checks", "no checks declared — declare one in .harness/commands.json with \"gate\": true", Tone.Plain);
        }

        var ran = outcomes.Where(o => o.Outcome.ExitCode is not null).ToList();
        if (ran.Count == 0)
        {
            return new("checks", "checks not run here", Tone.Plain);
        }

        var failed = ran.Where(o => o.Outcome.ExitCode != 0).ToList();
        if (failed.Count > 0)
        {
            return new("checks", $"{failed.Count} checks failing", Tone.Bad, Title: string.Join(", ", failed.Select(o => o.Name)));
        }

        var stale = ran.Where(o => o.Outcome.Since is null || o.Outcome.Since > 0).ToList();
        if (stale.Count > 0)
        {
            return new("checks", "checks ran on an older commit", Tone.Warn);
        }

        return head is null
            ? new("checks", $"{ran.Count} checks passed here", Tone.Plain)
            : new("checks", $"{ran.Count} checks passed on {head[..Math.Min(7, head.Length)]}", Tone.Plain);
    }
}
