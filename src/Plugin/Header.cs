namespace AiSdlc;

// Orchestrates the eight-fact reading for one worktree path. Pure git/declared-file inputs are
// read synchronously; the GitHub pair is read through the injected seam so tests run offline.
public sealed class Header
{
    private readonly Git git;
    private readonly GitHub github;
    private readonly Store? store;

    public Header(Git? git = null, GitHub? github = null, Store? store = null)
    {
        this.git = git ?? new Git();
        this.github = github ?? new GitHub();
        this.store = store;
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

        var commands = Commands.Read(ReviewSteps.FindDeclared(cwd, "commands.json"));
        var gates = commands.Gates.Select(g => g.Name).ToList();
        // POC-04 is the second writer: the latest recorded gate outcome per declared command,
        // read from the store — Since 0 when recorded against the present HEAD, null when HEAD
        // cannot bless the row. The checks fact keeps rendering POC-00's five readings.
        var head = this.git.Head(cwd);
        var recorded = this.store is null ? new Dictionary<string, GateRow>() : Gates.LatestPerGate(this.store.ListGateResults(cwd));
        var outcomes = commands.Gates
            .Select(g => recorded.TryGetValue(g.Key, out var row) ? (g.Name, new Facts.CheckOutcome(row.ExitCode, row.Commit == head ? 0 : null)) : ((string, Facts.CheckOutcome)?)null)
            .Where(o => o is not null)
            .Select(o => o!.Value)
            .ToList();

        var facts = Facts.Header(new Facts.Input(
            ahead, behind, changed, DiffPending: false, this.git.Unmerged(cwd), trunk is null,
            this.git.Uncommitted(cwd), head, gates, outcomes, seamRows,
            await this.github.PullRequestFor(branch, repo), PrPending: false, issueKey, issueTitle, issueState));

        return new HeaderReading(cwd, branch, null, facts);
    }
}

public sealed record HeaderReading(string Path, string? Branch, string? NotARepository, IReadOnlyList<Facts.Fact> Facts);
