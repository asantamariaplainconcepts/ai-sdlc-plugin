namespace AiSdlc;

// Orchestrates the eight-fact reading for one worktree path. Pure git/declared-file inputs are
// read synchronously; the GitHub pair is read through the injected seam so tests run offline.
public sealed class Header
{
    private readonly Git git;
    private readonly GitHub github;

    public Header(Git? git = null, GitHub? github = null)
    {
        this.git = git ?? new Git();
        this.github = github ?? new GitHub();
    }

    public async Task<HeaderReading> Read(string path)
    {
        var cwd = Path.GetFullPath(path);
        if (!this.git.IsRepository(cwd))
        {
            return new HeaderReading(cwd, null, "not a git repository — point at a directory git describes", []);
        }

        var branch = this.git.Branch(cwd);
        var trunk = this.git.Trunk(cwd);
        var (ahead, behind) = this.git.Position(cwd, trunk);

        // From the merge base, not HEAD: an agent that commits still shows its work. Untracked
        // files ride along — a new file is invisible to git diff until somebody stages it.
        IReadOnlyList<NumstatRow> changed = [];
        if (trunk is not null && this.git.DiffBasis(cwd, trunk) is { } basis)
        {
            changed = [.. this.git.Numstat(cwd, basis), .. this.git.Untracked(cwd).Select(u => new NumstatRow(u, 0, 0, false, null))];
        }

        // Branch proposes, GitHub confirms; a failing lookup refuses the proposal.
        string? issueKey = null, issueTitle = null, issueState = null;
        var seamRows = Read<Seam.Row[]>.Absent;
        var repo = GitHub.RepoPath(this.git.RemoteUrl(cwd));
        if (branch is not null && TaskResolution.KeyInBranch(branch) is { } key && repo is not null && TaskResolution.KeyNumber(key) is { } number)
        {
            try
            {
                var found = await this.github.Issue(number, repo);
                (issueKey, issueTitle, issueState) = (key, found.Title, found.State.ToLowerInvariant());
                var declared = Seam.ParseSeam(found.Body, found.Truncated);
                seamRows = declared.IsUnreadable ? Read<Seam.Row[]>.Unreadable
                    : declared.IsPresent ? Read<Seam.Row[]>.Of([.. Seam.Compare(declared.Value!, changed.Select(r => r.Path).ToList())])
                    : Read<Seam.Row[]>.Absent;
            }
            catch (Exception e)
            {
                issueTitle = e.Message;
            }
        }

        var commands = Commands.Read(FindCommandsJson(cwd));
        var gates = commands.Gates.Select(g => g.Name).ToList();
        // POC-00 records no outcomes; the checks fact reads over the declared set alone.
        var outcomes = new List<(string Name, Facts.CheckOutcome Outcome)>();

        var facts = Facts.Header(new Facts.Input(
            ahead, behind, changed, DiffPending: false, this.git.Unmerged(cwd), trunk is null,
            this.git.Uncommitted(cwd), this.git.Head(cwd), gates, outcomes, seamRows,
            await this.github.PullRequestFor(branch, repo), PrPending: false, issueKey, issueTitle, issueState));

        return new HeaderReading(cwd, branch, null, facts);
    }

    // The nearest .harness/commands.json from the worktree up: a worktree offers its own branch's list.
    private static string FindCommandsJson(string cwd)
    {
        for (var dir = (DirectoryInfo?)new DirectoryInfo(cwd); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Join(dir.FullName, ".harness", "commands.json")))
            {
                return Path.Join(dir.FullName, ".harness", "commands.json");
            }
        }

        return Path.Join(cwd, ".harness", "commands.json");
    }
}

public sealed record HeaderReading(string Path, string? Branch, string? NotARepository, IReadOnlyList<Facts.Fact> Facts);
