namespace AiSdlc;

public sealed class Git
{
    private readonly Func<string, IReadOnlyList<string>, (int ExitCode, string StdOut, string StdErr)> run;

    public Git(Func<string, IReadOnlyList<string>, (int, string, string)>? runner = null) => this.run = runner ?? RunProcess;

    public static (int, string, string) RunProcess(string cwd, IReadOnlyList<string> args)
    {
        var info = new System.Diagnostics.ProcessStartInfo("git") { WorkingDirectory = cwd, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        using var process = System.Diagnostics.Process.Start(info)!;
        var stdout = process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit(15000);
        return (process.HasExited ? process.ExitCode : -1, stdout, "");
    }

    private string Output(string cwd, params string[] args) => this.run(cwd, args) is { ExitCode: 0, StdOut: var o } ? o : "";

    public bool IsRepository(string cwd) => this.run(cwd, ["rev-parse", "--git-dir"]).ExitCode == 0;

    public string? Branch(string cwd) => Output(cwd, "rev-parse", "--abbrev-ref", "HEAD").Trim() is { Length: > 0 } name && name != "HEAD" ? name : null;

    public string? Head(string cwd) => Output(cwd, "rev-parse", "HEAD").Trim() is { Length: > 0 } head ? head : null;

    public string? Trunk(string cwd) => Output(cwd, "for-each-ref", "--format=%(refname:short)", "refs/remotes/origin/main", "refs/remotes/origin/master").Trim() is { Length: > 0 } refs ? refs.Split('\n')[0].Trim() : null;

    /// Three dots: the pair a person means by ahead and behind. Null is its own answer, not zero.
    public (int? Ahead, int? Behind) Position(string cwd, string? trunk)
    {
        var parts = trunk is null ? null : Output(cwd, "rev-list", "--left-right", "--count", $"{trunk}...HEAD").Trim().Split('\t');
        return parts is { Length: 2 } && int.TryParse(parts[0], out var behind) && int.TryParse(parts[1], out var ahead) ? (ahead, behind) : (null, null);
    }

    public IReadOnlyList<NumstatRow> Numstat(string cwd, string basis)
    {
        // -z rename shape is counts, old path, new path — the new name is the file; binary is a flag.
        var fields = Output(cwd, "diff", "--numstat", "-z", "-M", "-C", basis).Split('\0');
        var rows = new List<NumstatRow>();
        for (var i = 0; i < fields.Length; i++)
        {
            var counts = fields[i].Split('\t');
            var rename = counts.Length >= 3 && counts[2].Length == 0;
            if (counts.Length < 3 || (rename && i + 2 >= fields.Length))
            {
                continue;
            }

            var name = rename ? fields[i + 2] : counts[2];
            i += rename ? 2 : 0;
            if (name.Length == 0)
            {
                continue;
            }

            var binary = !int.TryParse(counts[0], out var added);
            _ = int.TryParse(counts[1], out var removed);
            rows.Add(new NumstatRow(name, added, removed, binary, null));
        }

        return rows;
    }

    public string? DiffBasis(string cwd, string? trunk) => Output(cwd, "merge-base", trunk ?? "HEAD", "HEAD").Trim() is { Length: > 0 } basis ? basis : trunk;

    /// The patch text of the change against a basis, porcelain-readable: quotepath off so paths
    /// stay literal, no color, three context lines. Untracked files ride along the way they do in
    /// numstat — git diff does not see a file nobody staged.
    public string PatchText(string cwd, string basis) =>
        Output(cwd, "-c", "core.quotepath=off", "diff", "--unified=3", "--no-color", basis);

    /// Files git has never been told about, as diff rows: without these a new-file change reads empty.
    public IReadOnlyList<string> Untracked(string cwd) =>
        Output(cwd, "ls-files", "-z", "--others", "--exclude-standard").Split('\0').Where(e => e.Length > 0 && !e.EndsWith('/')).ToList();

    /// Unmerged index entries (DD AU UD UA DU AA UU) — what a merge in progress leaves behind.
    public IReadOnlyList<string> Unmerged(string cwd) =>
        Output(cwd, "status", "--porcelain").Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(l => l.Length > 2 && l[..2].Contains('U')).Select(l => l[3..].Trim('"')).ToList();

    public int Uncommitted(string cwd) => Output(cwd, "status", "--porcelain").Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;

    public string? RemoteUrl(string cwd) => Output(cwd, "remote", "get-url", "origin").Trim() is { Length: > 0 } url ? url : null;
}

public sealed record NumstatRow(string Path, int Added, int Removed, bool IsBinary, string? RenamedFrom);
