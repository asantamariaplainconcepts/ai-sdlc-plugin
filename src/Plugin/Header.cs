namespace AiSdlc;

/// Orchestrates the eight-fact reading for one worktree path. Pure git/declared-file inputs
/// are read synchronously; the GitHub pair (PR for branch, issue) is read through the
/// injected GitHub seam so tests run without a network.
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
        var head = this.git.Head(cwd);
        var trunk = this.git.Trunk(cwd);
        var (ahead, behind) = this.git.Position(cwd, trunk);

        // Changed = tracked diff from the merge base + untracked files (an agent's new file is
        // untracked until staged; without these the count is wrong in the most common case).
        IReadOnlyList<NumstatRow> changed = [];
        if (trunk is not null && this.git.DiffBasis(cwd, trunk) is { } basis)
        {
            var rows = this.git.Numstat(cwd, basis);
            changed = [.. rows, .. this.git.Untracked(cwd).Select(u => new NumstatRow(u, 0, 0, false, null))];
        }

        // Issue resolution: branch proposes, GitHub confirms (harness taskFor parity).
        string? issueKey = null;
        string? issueTitle = null;
        string? issueState = null;
        var seamRows = Read<Seam.Row[]>.Absent;
        var remote = this.git.RemoteUrl(cwd);
        var repo = GitHub.RepoPath(remote);
        var proposed = branch is null ? null : TaskResolution.KeyInBranch(branch);
        if (proposed is { } key && repo is not null && TaskResolution.KeyNumber(key) is { } number)
        {
            try
            {
                var found = await this.github.Issue(number, repo);
                issueKey = key;
                issueTitle = found.Title;
                issueState = found.State.ToLowerInvariant();
                var declared = Seam.ParseSeam(found.Body, found.Truncated);
                if (declared.IsUnreadable)
                {
                    seamRows = Read<Seam.Row[]>.Unreadable;
                }
                else if (declared.IsPresent)
                {
                    var touched = changed.Select(r => r.Path).ToList();
                    seamRows = Read<Seam.Row[]>.Of([.. Seam.Compare(declared.Value!, touched)]);
                }
            }
            catch (Exception e)
            {
                // The proposal is refused — named, with the vendor's own words as the title.
                issueKey = null;
                issueTitle = e.Message;
            }
        }

        // PR: three answers. No branch or no GitHub remote answers NotAskable rather than claiming "none".
        var pr = await this.github.PullRequestFor(branch, repo);

        var commands = Commands.Read(FindCommandsJson(cwd));
        var gates = commands.Gates.Select(g => g.Name).ToList();
        // POC-00 records no outcomes; the checks fact therefore reads over the declared set.
        var outcomes = new List<(string Name, Facts.CheckOutcome Outcome)>();

        var facts = Facts.Header(
            branch,
            ahead,
            behind,
            changed,
            trunk is not null && changed.Count == 0 && this.git.DiffBasis(cwd, trunk) is null,
            this.git.Unmerged(cwd),
            trunk is null,
            this.git.UncommittedCount(cwd),
            head,
            gates,
            outcomes,
            seamRows,
            pr,
            prPending: false,
            issueKey,
            issueTitle,
            issueState);

        return new HeaderReading(cwd, branch, null, facts);
    }

    /// The nearest .harness/commands.json from the worktree up: the file is declared in the
    /// repository being read, so a worktree offers its own branch's list.
    private static string FindCommandsJson(string cwd)
    {
        var dir = new DirectoryInfo(cwd);
        while (dir is not null)
        {
            var candidate = Path.Join(dir.FullName, ".harness", "commands.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return Path.Join(cwd, ".harness", "commands.json");
    }
}

public sealed record HeaderReading(string Path, string? Branch, string? NotARepository, IReadOnlyList<Facts.Fact> Facts);
