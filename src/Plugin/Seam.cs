namespace AiSdlc;

// Port of harness seam.ts at 837c7ca: bullet-first-backtick, looksLikePath, suffix/directory
// coverage, and the three readings of a possibly-mirrored body (present / absent / unreadable).
public static class Seam
{
    public const string Unreadable = "unreadable";

    public sealed record Entry(string Path, bool Directory);

    public enum State { DeclaredTouched, DeclaredUntouched, Undeclared }

    public sealed record Row(string Path, State State);

    private static readonly string[] Titles = ["^#{1,6}\\s*(.+?)\\s*$", "^\\*\\*(.+?)\\*\\*:?\\s*$"];

    private static string? SectionTitle(string line)
    {
        foreach (var pattern in Titles)
        {
            if (System.Text.RegularExpressions.Regex.Match(line, pattern) is { Success: true } match)
            {
                return match.Groups[1].Value;
            }
        }

        return null;
    }

    private static (string[] Lines, bool RanToTheEnd)? FindSection(string[] lines, Func<string, bool> matches)
    {
        var start = -1;
        for (var i = 0; i < lines.Length && start == -1; i++)
        {
            if (SectionTitle(lines[i]) is { } title && matches(title))
            {
                start = i + 1;
            }
        }

        if (start == -1)
        {
            return null;
        }

        // The section runs to the first later title — or to the end, which over a truncated body
        // is the front of a section, not a section.
        var end = lines.Length;
        for (var i = start; i < lines.Length; i++)
        {
            if (SectionTitle(lines[i]) is not null)
            {
                end = i;
                break;
            }
        }

        return (lines[start..end], end == lines.Length);
    }

    /// The one place the third state is decided: not-in-text + truncated, or ran-to-end + truncated.
    private static Read<string[]> ReadSection(string[] lines, bool truncated, Func<string, bool> matches)
    {
        var section = FindSection(lines, matches);
        if (section is null)
        {
            return truncated ? Read<string[]>.Unreadable : Read<string[]>.Absent;
        }

        return truncated && section.Value.RanToTheEnd ? Read<string[]>.Unreadable : Read<string[]>.Of(section.Value.Lines);
    }

    private static readonly System.Text.RegularExpressions.Regex BulletPath = new("^\\s*[-*]\\s+`([^`]+)`");

    // A backtick span with no '/' and no recognizable extension is a type name, not a path.
    private static bool LooksLikePath(string raw) =>
        !raw.Any(char.IsWhiteSpace) && (raw.Contains('/') || System.Text.RegularExpressions.Regex.IsMatch(raw, "\\.[A-Za-z0-9]{1,5}$"));

    public static Read<List<Entry>> ParseSeam(string body, bool truncated = false)
    {
        var section = ReadSection(body.Split(["\r\n", "\n"], StringSplitOptions.None), truncated, t => t.Contains("seam", StringComparison.OrdinalIgnoreCase));
        if (section.IsUnreadable)
        {
            return Read<List<Entry>>.Unreadable;
        }

        if (!section.IsPresent)
        {
            return Read<List<Entry>>.Absent;
        }

        var declared = new Dictionary<string, bool>();
        foreach (var line in section.Value!)
        {
            if (BulletPath.Match(line) is not { Success: true } match || !LooksLikePath(match.Groups[1].Value.Trim()))
            {
                continue;
            }

            var raw = match.Groups[1].Value.Trim();
            var path = raw.TrimEnd('/');
            declared[path] = raw.EndsWith('/') || !System.Text.RegularExpressions.Regex.IsMatch(path.Split('/').Last(), "\\.[A-Za-z0-9]{1,5}$");
        }

        return Read<List<Entry>>.Of(declared.Select(kv => new Entry(kv.Key, kv.Value)).ToList());
    }

    // Suffix, not equality: the same seam is written at different roots across issues.
    private static bool Covers(Entry entry, string touchedPath) => !entry.Directory
        ? touchedPath == entry.Path || touchedPath.EndsWith($"/{entry.Path}", StringComparison.Ordinal)
        : touchedPath == entry.Path || touchedPath.StartsWith($"{entry.Path}/", StringComparison.Ordinal) || touchedPath.Contains($"/{entry.Path}/", StringComparison.Ordinal);

    public static List<Row> Compare(List<Entry> declared, IReadOnlyList<string> touchedPaths)
    {
        var rows = declared
            .Select(e => new Row(e.Path, touchedPaths.Any(p => Covers(e, p)) ? State.DeclaredTouched : State.DeclaredUntouched))
            .ToList();
        rows.AddRange(touchedPaths.Where(p => !declared.Any(e => Covers(e, p))).Select(p => new Row(p, State.Undeclared)));
        return rows;
    }
}
