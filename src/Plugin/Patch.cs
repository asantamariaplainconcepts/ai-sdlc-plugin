namespace AiSdlc;

// One change's diff as data: a pure parse of unified-diff text into files, hunks and clasped
// lines, plus the two-hundred-line cap the epic pins. No diff library — the patch text is the
// whole input and the parse never looks at the repository.
public static class Patch
{
    public const int CapLines = 200;

    public enum LineKind { Context, Added, Removed }

    public sealed record Line(LineKind Kind, int? OldLine, int? NewLine, string Text);

    public sealed record Hunk(string Header, IReadOnlyList<Line> Lines);

    public sealed record File(string Path, string? RenamedFrom, bool IsBinary, IReadOnlyList<Hunk> Hunks);

    public sealed record Read(IReadOnlyList<File> Files, int? CutAfter, int HiddenLines);

    public static Read Parse(string patchText)
    {
        var files = new List<File>();
        string? path = null;
        string? renamedFrom = null;
        string? oldSide = null;
        var isBinary = false;
        var hunks = new List<Hunk>();
        var lines = new List<Line>();
        string? header = null;
        int oldNo = 0, newNo = 0;
        var counted = 0;
        int? cutAfter = null;
        var hidden = 0;
        var cut = false;

        void FlushFile()
        {
            if (path is not null)
            {
                files.Add(new File(path, renamedFrom, isBinary, hunks));
            }

            (path, renamedFrom, oldSide, isBinary) = (null, null, null, false);
            hunks = [];
        }

        void FlushHunk()
        {
            if (header is not null)
            {
                hunks.Add(new Hunk(header, lines));
            }

            header = null;
            lines = [];
        }

        foreach (var raw in patchText.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.StartsWith("diff --git "))
            {
                FlushHunk();
                FlushFile();
                // The b/ side of the header names the file; rename detail may refine it below.
                var names = line["diff --git ".Length..].Split(' ');
                if (names.Length == 2)
                {
                    path = names[1].Trim('"');
                    if (path.StartsWith("b/"))
                    {
                        path = path[2..];
                    }
                }

                continue;
            }

            if (line.StartsWith("--- "))
            {
                // The old side names where the content came from; a rename is only when the
                // new side names a different file.
                var old = line[4..].Trim('"');
                oldSide = old.StartsWith("a/") ? old[2..] : old;
                continue;
            }

            if (line.StartsWith("+++ "))
            {
                var s = line[4..].Trim('"');
                if (s.StartsWith("b/"))
                {
                    s = s[2..];
                }

                FlushHunk();
                if (s != "/dev/null")
                {
                    path = s;
                    // A rename is old and new sides naming different files — a plain edit
                    // names itself twice, and /dev/null is the new-file origin, not a file.
                    renamedFrom = oldSide is not null && oldSide != s && oldSide != "/dev/null" ? oldSide : null;
                }

                continue;
            }

            if (line.StartsWith("Binary files ") || line.StartsWith("GIT binary patch"))
            {
                isBinary = true;
                continue;
            }

            if (line.StartsWith("@@ "))
            {
                if (cut)
                {
                    hidden++;
                    continue;
                }

                FlushHunk();
                header = line;
                (oldNo, newNo) = StartNumbers(line);
                continue;
            }

            if (header is null)
            {
                // A body line past the cut has no hunk to hold it (the flush emptied the state),
                // but it still counts toward what is hidden.
                if (cut && (line.StartsWith('+') || line.StartsWith('-') || line.StartsWith(' ')))
                {
                    hidden++;
                }

                continue;
            }

            if (line.StartsWith("\\"))
            {
                // "No newline at end of file" annotates the line before it; it is not a body line.
                continue;
            }

            var kind = line.StartsWith('+') ? LineKind.Added : line.StartsWith('-') ? LineKind.Removed : LineKind.Context;
            if (kind != LineKind.Context && line.Length == 1)
            {
                kind = LineKind.Context;
            }

            if (kind == LineKind.Context && !line.StartsWith(' ') && line.Length > 0 && !char.IsWhiteSpace(line[0]))
            {
                // Stray text after a hunk (unlikely in git output) is not a body line.
                continue;
            }

            if (cut)
            {
                hidden++;
                continue;
            }

            if (kind == LineKind.Context && line.Length == 0)
            {
                // An empty trailing line from split is not a body line; git prefixes blanks with a space.
                continue;
            }

            if (counted == CapLines && kind != LineKind.Context || counted >= CapLines)
            {
                cut = true;
                cutAfter = counted;
                hidden++;
                FlushHunk();
                FlushFile();
                continue;
            }

            lines.Add(new Line(kind,
                kind == LineKind.Added ? null : oldNo,
                kind == LineKind.Removed ? null : newNo,
                line.Length > 0 ? line[1..] : ""));
            if (kind == LineKind.Added)
            {
                newNo++;
            }
            else if (kind == LineKind.Removed)
            {
                oldNo++;
            }
            else
            {
                oldNo++;
                newNo++;
            }

            counted++;
        }

        FlushHunk();
        FlushFile();

        // Counted past the cap exactly at the end: the cut marker names what is hidden.
        return new Read(files, cutAfter, hidden);
    }

    /// The start numbers of a hunk header `@@ -a,b +c,d @@`.
    private static (int Old, int New) StartNumbers(string header)
    {
        var parts = header.Split(' ');
        return (Number(parts.FirstOrDefault(p => p.StartsWith("-"))), Number(parts.FirstOrDefault(p => p.StartsWith("+"))));
    }

    private static int Number(string? part) =>
        part is null ? 0 : int.TryParse(part.TrimStart('-', '+').Split(',')[0], out var n) ? n : 0;
}
