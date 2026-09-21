namespace AiSdlc;

/// Reading the Seam section of an issue body and comparing it against touched paths.
/// Port of harness seam.ts at 837c7ca — same three readings, same parsing rules.
public static class Seam
{
    public const string Unreadable = "unreadable";

    public sealed record Entry(string Path, bool Directory);

    public enum State
    {
        DeclaredTouched,
        DeclaredUntouched,
        Undeclared,
    }

    public sealed record Row(string Path, State State);

    /// "The section is not in the text we got, or it ran to the end of a truncated body."
    private static readonly string[] sectionTitlePatterns = ["^#{1,6}\\s*(.+?)\\s*$", "^\\*\\*(.+?)\\*\\*:?\\s*$"];

    public static string? SectionTitle(string line)
    {
        foreach (var pattern in sectionTitlePatterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(line, pattern);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }

        return null;
    }

    /// The lines under the first section whose title matches; null when there is no such section.
    /// Also answers whether the section ran to the end of the text (the truncated-body tell).
    private static (string[] Lines, bool RanToTheEnd)? FindSection(string[] lines, Func<string, bool> matches)
    {
        var start = -1;
        for (var i = 0; i < lines.Length; i++)
        {
            var title = SectionTitle(lines[i]);
            if (title is not null && matches(title))
            {
                start = i + 1;
                break;
            }
        }

        if (start == -1)
        {
            return null;
        }

        var end = lines.Length;
        var ranToTheEnd = true;
        for (var i = start; i < lines.Length; i++)
        {
            if (SectionTitle(lines[i]) is not null)
            {
                end = i;
                ranToTheEnd = false;
                break;
            }
        }

        return (lines[start..end], ranToTheEnd);
    }

    /// The one place the third state is decided: not-in-text + truncated, or ran-to-end + truncated.
    public static Read<string[]> ReadSection(string[] lines, bool truncated, Func<string, bool> matches)
    {
        var section = FindSection(lines, matches);
        if (section is null)
        {
            return truncated ? Read<string[]>.Unreadable : Read<string[]>.Absent;
        }

        if (truncated && section.Value.RanToTheEnd)
        {
            return Read<string[]>.Unreadable;
        }

        return Read<string[]>.Of(section.Value.Lines);
    }

    private static readonly System.Text.RegularExpressions.Regex BulletPath = new("^\\s*[-*]\\s+`([^`]+)`");

    private static bool LooksLikePath(string raw) =>
        !raw.Any(char.IsWhiteSpace)
        && (raw.Contains('/') || System.Text.RegularExpressions.Regex.IsMatch(raw, "\\.[A-Za-z0-9]{1,5}$"));

    /// The declared seam, absent when the body has no Seam section, unreadable when a truncated
    /// body cannot promise the section ever arrived whole.
    public static Read<List<Entry>> ParseSeam(string body, bool truncated = false)
    {
        var lines = body.Split(["\r\n", "\n"], StringSplitOptions.None);
        var section = ReadSection(lines, truncated, t => t.Contains("seam", StringComparison.OrdinalIgnoreCase));
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
            var match = BulletPath.Match(line);
            if (!match.Success)
            {
                continue;
            }

            var raw = match.Groups[1].Value.Trim();
            if (!LooksLikePath(raw))
            {
                continue;
            }

            var path = raw.TrimEnd('/');
            var last = path.Split('/').Last();
            declared[path] = raw.EndsWith('/') || !System.Text.RegularExpressions.Regex.IsMatch(last, "\\.[A-Za-z0-9]{1,5}$");
        }

        return Read<List<Entry>>.Of(declared.Select(kv => new Entry(kv.Key, kv.Value)).ToList());
    }

    private static bool SuffixMatch(string touchedPath, string declaredPath) =>
        touchedPath == declaredPath || touchedPath.EndsWith($"/{declaredPath}", StringComparison.Ordinal);

    private static bool Covers(Entry entry, string touchedPath)
    {
        if (!entry.Directory)
        {
            return SuffixMatch(touchedPath, entry.Path);
        }

        return touchedPath == entry.Path
            || touchedPath.StartsWith($"{entry.Path}/", StringComparison.Ordinal)
            || touchedPath.Contains($"/{entry.Path}/", StringComparison.Ordinal);
    }

    public static List<Row> Compare(List<Entry> declared, IReadOnlyList<string> touchedPaths)
    {
        var rows = new List<Row>();
        foreach (var entry in declared)
        {
            var matched = touchedPaths.Any(p => Covers(entry, p));
            rows.Add(new Row(entry.Path, matched ? State.DeclaredTouched : State.DeclaredUntouched));
        }

        foreach (var path in touchedPaths)
        {
            if (!declared.Any(e => Covers(e, path)))
            {
                rows.Add(new Row(path, State.Undeclared));
            }
        }

        return rows;
    }
}

/// The three answers a reader of a (possibly mirrored) body has.
public sealed class Read<T>
{
    public bool IsPresent { get; private init; }

    public bool IsUnreadable { get; private init; }

    public T? Value { get; private init; }

    public static Read<T> Of(T value) => new() { IsPresent = true, Value = value };

    public static Read<T> Absent { get; } = new();

    public static Read<T> Unreadable { get; } = new() { IsUnreadable = true };
}
