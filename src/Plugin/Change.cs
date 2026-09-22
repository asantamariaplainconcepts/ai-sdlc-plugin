namespace AiSdlc;

// What a branch declares as its change: the live openspec/changes directories of the worktree's
// working tree, not git show — the branch's own claim, found where it would be written. Absence
// carries the path a declaration would occupy, and several changes are drawn without deciding.
public static class Change
{
    private const int MaxBytes = 512 * 1024;

    public sealed record Artifact(string Name, string Path);

    public sealed record Declared(string Name, string Path, IReadOnlyList<Artifact> Artifacts);

    public sealed record Discovery(string Root, IReadOnlyList<Declared> Changes)
    {
        /// The sentence the panel draws when the branch declares no change: where one would be written.
        public string Absence => $"no change declared here — it would be written at {Path.Join(Root, "openspec", "changes", "<name>", "proposal.md")}";
    }

    public sealed record ArtifactRead(string Path, string? Text, string? Problem);

    public sealed record ConfinedRead(string? Path, string? Text, string? Problem)
    {
        /// The refusal marker: a confined read that names no path read nothing and refuses.
        public bool Refused => this.Path is null;
    }

    /// The artifact read the endpoint serves, confined: the worktree names where to look, the
    /// path must land inside a change root that worktree's own discovery listed. Refusal is
    /// two-layered — lexical first (absolute paths and `..` climbs refused before anything is
    /// asked of the disk), then a component-wise walk that follows symlinks (a link planted in
    /// a change dir cannot ground outside the root it sits in). A landing outside the listed
    /// roots is refused with a named sentence — Path null is the marker the endpoint turns
    /// into a 400 — except a worktree with no changes, whose answer is the absence itself.
    public static ConfinedRead ReadListedArtifact(string worktree, string file)
    {
        var root = new DirectoryInfo(worktree).FullName;
        var candidate = Path.GetFullPath(Path.Join(root, file));
        var relative = Path.GetRelativePath(root, candidate);
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}") || relative.StartsWith($"..{Path.AltDirectorySeparatorChar}"))
        {
            return new(null, null, RefusedSentence(file));
        }

        var discovery = Discover(root);
        var resolved = Walk(candidate) ?? candidate;
        var inside = discovery.Changes.Any(c => resolved.StartsWith(c.Path + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || candidate.StartsWith(c.Path + Path.DirectorySeparatorChar, StringComparison.Ordinal));
        return inside && ReadArtifact(resolved) is { } read
            ? new(read.Path, read.Text, read.Problem)
            : discovery.Changes.Count == 0
                ? new(null, null, discovery.Absence)
                : new(null, null, RefusedSentence(file));
    }

    /// Follow symlinks one component at a time from the root down: each existing link in the
    /// chain is resolved to its final target and accumulated, so a mid-path link that escapes
    /// the change roots is caught in the landed path even when the leaf itself is innocent.
    /// Null is "no component resolved" — the lexical path is what it is.
    private static string? Walk(string candidate)
    {
        string? resolved = null;
        for (var i = 0; i < 8 && candidate.Length > 0; i++)
        {
            var parent = Path.GetDirectoryName(candidate);
            FileSystemInfo? entry = Directory.Exists(candidate) ? new DirectoryInfo(candidate) : File.Exists(candidate) ? new FileInfo(candidate) : null;
            var target = entry?.ResolveLinkTarget(returnFinalTarget: true)?.FullName;
            if (target is null)
            {
                return resolved;
            }

            resolved = target;
            candidate = parent ?? "";
        }

        return resolved;
    }

    private static string RefusedSentence(string file) =>
        $"\"{file}\" is not an artifact the proposal listing returned — name one under this worktree's openspec/changes";

    public static Discovery Discover(string root)
    {
        var changes = new List<Declared>();
        var dir = new DirectoryInfo(Path.Join(root, "openspec", "changes"));
        if (dir.Exists)
        {
            foreach (var change in dir.EnumerateDirectories().OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (change.Name.Equals("archive", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var artifacts = change.EnumerateFiles("*.md")
                    .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(f => new Artifact(f.Name, f.FullName))
                    .ToList();
                if (artifacts.Count > 0)
                {
                    changes.Add(new Declared(change.Name, change.FullName, artifacts));
                }
            }
        }

        return new Discovery(root, changes);
    }

    public static ArtifactRead ReadArtifact(string path)
    {
        if (!File.Exists(path))
        {
            return new ArtifactRead(path, null, $"{path} is not there to read anymore — reread the declaration");
        }

        try
        {
            var info = new FileInfo(path);
            if (info.Length > MaxBytes)
            {
                return new ArtifactRead(path, null, $"{path} is {info.Length} bytes, over the {MaxBytes} bound");
            }

            return new ArtifactRead(path, File.ReadAllText(path), null);
        }
        catch (Exception e)
        {
            return new ArtifactRead(path, null, $"{path} could not be read: {e.Message}");
        }
    }
}
