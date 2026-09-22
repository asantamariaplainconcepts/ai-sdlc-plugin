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
