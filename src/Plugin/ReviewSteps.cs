namespace AiSdlc;

// .harness/review.json reading, JSONC-safe and byte-bounded: the steps of a review are declared
// data, changing the file changes the wizard, and the reader executes nothing it reads. Absent
// and unreadable differ, each with its remedy path — the same discipline commands.json follows.
public static class ReviewSteps
{
    private const int MaxBytes = 256 * 1024;

    public const string MarkPrefix = "reviewed:";

    // Which step keys have panels in this build (POC-02 lands the first two). The declared file is
    // the source of what draws; this set is the source of what opens. A declared key outside
    // it draws disabled, named.
    public static readonly IReadOnlySet<string> KnownImplemented = new HashSet<string>(["proposal", "code"], StringComparer.OrdinalIgnoreCase);

    private static readonly System.Text.Json.JsonSerializerOptions Options = new()
    {
        ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
    };

    public sealed record DeclaredStep(string Key, string Title, string Asserts);

    public sealed record ReadResult(IReadOnlyList<DeclaredStep> Steps, bool Absent, bool Unreadable, string? Problem);

    public static ReadResult Read(string path)
    {
        if (!File.Exists(path))
        {
            return new([], true, false, $"no review steps declared at {path} — declare them there in a \"steps\" array");
        }

        try
        {
            var info = new FileInfo(path);
            if (info.Length > MaxBytes)
            {
                return new([], false, true, $"{path} is {info.Length} bytes, over the {MaxBytes} bound");
            }

            var shape = System.Text.Json.JsonSerializer.Deserialize<Shape>(File.ReadAllText(path), Options);
            var steps = (shape?.Steps ?? [])
                .Where(s => !string.IsNullOrWhiteSpace(s?.Key) && !string.IsNullOrWhiteSpace(s?.Title))
                .GroupBy(s => s!.Key!.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .Select(s => new DeclaredStep(s!.Key!.Trim(), s.Title!.Trim(), (s.Asserts ?? "").Trim()))
                .ToList();
            return new(steps, false, false, shape?.Steps is null ? $"no \"steps\" array in {path}" : null);
        }
        catch (Exception e)
        {
            return new([], false, true, $"{path} could not be parsed: {e.Message}");
        }
    }

    /// A step is marked where the resolved issue carries its `reviewed:<key>` label — read-only.
    public static bool Marked(string key, IReadOnlyList<string> issueLabels) =>
        issueLabels.Any(l => l.Equals(MarkPrefix + key, StringComparison.OrdinalIgnoreCase));

    /// The sentence the rail draws when marks cannot be read: the branch resolves no issue here
    /// (or the lookup refused, message included) — the same words the header's issue fact uses.
    public static string MarksNotAskedReason(string? issueLookupFailure) =>
        issueLookupFailure is null
            ? "marks are not asked — the branch resolves no issue here"
            : $"marks are not asked — the issue could not be read ({issueLookupFailure})";

    /// The nearest .harness/<name> from the worktree up: a worktree offers its own branch's files.
    public static string FindDeclared(string cwd, string fileName)
    {
        for (var dir = (DirectoryInfo?)new DirectoryInfo(cwd); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Join(dir.FullName, ".harness", fileName)))
            {
                return Path.Join(dir.FullName, ".harness", fileName);
            }
        }

        return Path.Join(cwd, ".harness", fileName);
    }

    private sealed record Shape(List<Step>? Steps);

    private sealed record Step(string? Key, string? Title, string? Asserts);
}
