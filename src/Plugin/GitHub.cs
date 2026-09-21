namespace AiSdlc;

/// GitHub reads, Octokit with the token `gh auth token` returns. Read-only: PR for a branch,
/// issue by number. An absent token or a refused question is an answer with a remedy, never
/// an opaque failure (#235: gh unauthenticated is named, not folded into "no pull request").
public sealed class GitHub
{
    private readonly Func<string?> token;
    private readonly Func<string?, string?, Task<Facts.PullRequestAnswer>> pullRequest;
    private readonly Func<int, string, Task<IssueRead>> issue;

    public sealed record IssueRead(int Number, string State, string Title, string Body, bool Truncated);

    public GitHub(
        Func<string?>? tokenSource = null,
        Func<string?, string?, Task<Facts.PullRequestAnswer>>? pullRequestSource = null,
        Func<int, string, Task<IssueRead>>? issueSource = null)
    {
        this.token = tokenSource ?? GhToken;
        this.pullRequest = pullRequestSource ?? PrFor;
        this.issue = issueSource ?? IssueGet;
    }

    public string? CurrentToken() => this.token();

    /// The token from `gh auth token`, or null where gh refuses or is missing.
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

    /// The repository "owner/name" a git remote URL names, or null where it names none.
    public static string? RepoPath(string? remoteUrl)
    {
        if (remoteUrl is null)
        {
            return null;
        }

        var cleaned = remoteUrl.TrimEnd('/');
        var match = System.Text.RegularExpressions.Regex.Match(cleaned, "github\\.com[/:]([^/]+)/([^/]+?)(\\.git)?$");
        return match.Success ? $"{match.Groups[1].Value}/{match.Groups[2].Value}" : null;
    }

    public Task<Facts.PullRequestAnswer> PullRequestFor(string? branch, string? repo) =>
        branch is null || repo is null
            ? Task.FromResult(new Facts.PullRequestAnswer(false, "NotAskable", null, null, null, null, null))
            : this.pullRequest(branch, repo);

    public Task<IssueRead> Issue(int number, string repo) => this.issue(number, repo);

    private async Task<Facts.PullRequestAnswer> PrFor(string? branch, string? repo)
    {
        var token = this.token();
        if (token is null)
        {
            return new Facts.PullRequestAnswer(true, "NotSignedIn", "gh auth token returned nothing — run gh auth login", null, null, null, null);
        }

        try
        {
            var client = Client(token);
            var owner = repo!.Split('/')[0];
            var name = repo.Split('/')[1];
            var prs = await client.PullRequest.GetAllForRepository(owner, name, new Octokit.PullRequestRequest { State = Octokit.ItemStateFilter.All });
            var pr = prs.FirstOrDefault(p => p.Head?.Ref == branch);
            return pr is null
                ? new Facts.PullRequestAnswer(false, null, null, null, null, null, null)
                : new Facts.PullRequestAnswer(false, null, null, pr.Number, pr.State.ToString(), pr.Draft, pr.Title);
        }
        catch (Exception e)
        {
            return new Facts.PullRequestAnswer(true, "Failed", e.Message, null, null, null, null);
        }
    }

    private async Task<IssueRead> IssueGet(int number, string repo)
    {
        var token = this.token();
        if (token is null)
        {
            throw new InvalidOperationException("gh is not signed in — run gh auth login");
        }

        var client = Client(token);
        var found = await client.Issue.Get(repo.Split('/')[0], repo.Split('/')[1], number);
        // GitHub's REST body is whole; truncation is a mirror-only fact, carried for parity.
        return new IssueRead(found.Number, found.State.ToString(), found.Title, found.Body ?? "", false);
    }

    private static Octokit.GitHubClient Client(string token) =>
        new(new Octokit.ProductHeaderValue("ai-sdlc-poc")) { Credentials = new Octokit.Credentials(token) };
}
