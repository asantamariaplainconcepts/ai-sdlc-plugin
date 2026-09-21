namespace AiSdlc;

// GitHub reads: Octokit with the token `gh auth token` returns, read-only (PR for a branch,
// issue by number). An absent token or refused question is an answer with its remedy, never an
// opaque failure — gh unauthenticated is named, not folded into "no pull request" (#235).
public sealed class GitHub
{
    public sealed record IssueRead(int Number, string State, string Title, string Body, bool Truncated);

    private readonly Func<string?> token;
    private readonly Func<string?, string?, Task<Facts.PullRequestAnswer>> pullRequest;
    private readonly Func<int, string, Task<IssueRead>> issue;

    public GitHub(
        Func<string?>? tokenSource = null,
        Func<string?, string?, Task<Facts.PullRequestAnswer>>? pullRequestSource = null,
        Func<int, string, Task<IssueRead>>? issueSource = null)
    {
        this.token = tokenSource ?? GhToken;
        this.pullRequest = pullRequestSource ?? PrFor;
        this.issue = issueSource ?? IssueGet;
    }

    private static string? GhToken()
    {
        try
        {
            var info = new System.Diagnostics.ProcessStartInfo("gh") { Arguments = "auth token", RedirectStandardOutput = true, RedirectStandardError = true };
            using var process = System.Diagnostics.Process.Start(info)!;
            var value = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(10000);
            return process.ExitCode == 0 && value.Length > 0 ? value : null;
        }
        catch
        {
            return null;
        }
    }

    /// The "owner/name" a git remote URL names, or null where it names none.
    public static string? RepoPath(string? remoteUrl) => remoteUrl is null ? null
        : System.Text.RegularExpressions.Regex.Match(remoteUrl.TrimEnd('/'), "github\\.com[/:]([^/]+)/([^/]+?)(\\.git)?$") is { Success: true } m ? $"{m.Groups[1].Value}/{m.Groups[2].Value}" : null;

    public Task<Facts.PullRequestAnswer> PullRequestFor(string? branch, string? repo) =>
        branch is null || repo is null
            ? Task.FromResult(new Facts.PullRequestAnswer(false, "NotAskable", null, null, null, null, null))
            : this.pullRequest(branch, repo);

    public Task<IssueRead> Issue(int number, string repo) => this.issue(number, repo);

    private async Task<Facts.PullRequestAnswer> PrFor(string? branch, string? repo)
    {
        if (this.token() is not { } token)
        {
            return new(true, "NotSignedIn", "gh auth token returned nothing — run gh auth login", null, null, null, null);
        }

        try
        {
            var parts = repo!.Split('/');
            var prs = await Client(token).PullRequest.GetAllForRepository(parts[0], parts[1], new Octokit.PullRequestRequest { State = Octokit.ItemStateFilter.All });
            var pr = prs.FirstOrDefault(p => p.Head?.Ref == branch);
            return pr is null ? new Facts.PullRequestAnswer(false, null, null, null, null, null, null)
                : new Facts.PullRequestAnswer(false, null, null, pr.Number, pr.State.StringValue, pr.Draft, pr.Title);
        }
        catch (Exception e)
        {
            return new(true, "Failed", e.Message, null, null, null, null);
        }
    }

    private async Task<IssueRead> IssueGet(int number, string repo)
    {
        var token = this.token() ?? throw new InvalidOperationException("gh is not signed in — run gh auth login");
        var parts = repo.Split('/');
        var found = await Client(token).Issue.Get(parts[0], parts[1], number);
        // GitHub's REST body is whole; truncation is a mirror-only fact, carried for parity.
        return new(found.Number, found.State.StringValue, found.Title, found.Body ?? "", false);
    }

    private static Octokit.GitHubClient Client(string token) =>
        new(new Octokit.ProductHeaderValue("ai-sdlc-poc")) { Credentials = new Octokit.Credentials(token) };
}
