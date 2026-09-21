namespace AiSdlc;

/// One process runner for every read. Never raises: a git that cannot be asked is an
/// answer-shaped thing, because the whole header is built of readings that may be absent.
public sealed class Git
{
    private readonly Func<string, IReadOnlyList<string>, (int ExitCode, string StdOut, string StdErr)> run;

    public Git(Func<string, IReadOnlyList<string>, (int, string, string)>? runner = null)
    {
        this.run = runner ?? RunProcess;
    }

    public static (int ExitCode, string StdOut, string StdErr) RunProcess(string cwd, IReadOnlyList<string> args)
    {
        var info = new System.Diagnostics.ProcessStartInfo("git")
        {
            WorkingDirectory = cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        using var process = System.Diagnostics.Process.Start(info)!;
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit(15000);
        return (process.HasExited ? process.ExitCode : -1, stdout, stderr);
    }

    private string Output(string cwd, params string[] args)
    {
        var r = this.run(cwd, args);
        return r.ExitCode == 0 ? r.StdOut : "";
    }

    public bool IsRepository(string cwd) => this.run(cwd, ["rev-parse", "--git-dir"]).ExitCode == 0;

    public string? Branch(string cwd)
    {
        var name = this.Output(cwd, "rev-parse", "--abbrev-ref", "HEAD").Trim();
        return name.Length == 0 || name == "HEAD" ? null : name;
    }

    public string? Head(string cwd)
    {
        var head = this.Output(cwd, "rev-parse", "HEAD").Trim();
        return head.Length > 0 ? head : null;
    }

    public string? Trunk(string cwd)
    {
        // Conventional trunks tried in order; origin/main, origin/master, else null.
        var refs = this.Output(cwd, "for-each-ref", "--format=%(refname:short)", "refs/remotes/origin/main", "refs/remotes/origin/master").Trim();
        return refs.Length == 0 ? null : refs.Split('\n')[0].Trim();
    }

    public (int? Ahead, int? Behind) Position(string cwd, string? trunk)
    {
        if (trunk is null)
        {
            return (null, null);
        }

        var counts = this.run(cwd, ["rev-list", "--left-right", "--count", $"{trunk}...HEAD"]);
        if (counts.ExitCode != 0)
        {
            return (null, null);
        }

        var parts = counts.StdOut.Trim().Split('\t');
        return parts.Length == 2 && int.TryParse(parts[0], out var behind) && int.TryParse(parts[1], out var ahead)
            ? (ahead, behind)
            : (null, null);
    }

    public IReadOnlyList<NumstatRow> Numstat(string cwd, string basis)
    {
        var raw = this.Output(cwd, "diff", "--numstat", "-z", "-M", "-C", basis);
        var rows = new List<NumstatRow>();
        var fields = raw.Split('\0');
        for (var i = 0; i < fields.Length; i++)
        {
            var counts = fields[i].Split('\t');
            if (counts.Length < 3)
            {
                continue;
            }

            string name;
            string? renamedFrom = null;
            if (counts[2].Length == 0)
            {
                if (i + 2 >= fields.Length)
                {
                    break;
                }

                renamedFrom = fields[i + 1];
                name = fields[i + 2];
                i += 2;
            }
            else
            {
                name = counts[2];
            }

            if (name.Length == 0)
            {
                continue;
            }

            var binary = !int.TryParse(counts[0], out var added);
            _ = int.TryParse(counts[1], out var removed);
            rows.Add(new NumstatRow(name, added, removed, binary, renamedFrom));
        }

        return rows;
    }

    /// The merge base or, failing that, the trunk itself: what a diff of the change is taken against.
    public string? DiffBasis(string cwd, string? trunk)
    {
        if (trunk is null)
        {
            return null;
        }

        var basis = this.Output(cwd, "merge-base", trunk, "HEAD").Trim();
        return basis.Length > 0 ? basis : trunk;
    }

    public IReadOnlyList<string> Untracked(string cwd)
    {
        var raw = this.Output(cwd, "ls-files", "-z", "--others", "--exclude-standard");
        return raw.Split('\0').Where(e => e.Length > 0 && !e.EndsWith('/')).ToList();
    }

    public IReadOnlyList<string> Unmerged(string cwd)
    {
        // Unmerged index entries are what a merge in progress leaves; statuses DD AU UD UA DU AA UU.
        var raw = this.Output(cwd, "status", "--porcelain");
        return raw.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(l => l.Length > 2 && l[..2].Any(c => c is 'D' or 'A' or 'U') && l[..2].Any(c => c is 'U' or 'D'))
            .Select(l => l[3..].Trim('"'))
            .ToList();
    }

    public int UncommittedCount(string cwd)
    {
        var raw = this.Output(cwd, "status", "--porcelain");
        return raw.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
    }

    public string? RemoteUrl(string cwd)
    {
        var url = this.Output(cwd, "remote", "get-url", "origin").Trim();
        return url.Length > 0 ? url : null;
    }
}

public sealed record NumstatRow(string Path, int Added, int Removed, bool IsBinary, string? RenamedFrom);
