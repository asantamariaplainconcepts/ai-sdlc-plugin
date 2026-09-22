namespace AiSdlc;

// .harness/review.json reading, JSONC-safe and byte-bounded: the steps of a review are declared
// data, changing the file changes the wizard, and the reader executes nothing it reads. Absent
// and unreadable differ, each with its remedy path — the same discipline commands.json follows.
public static class ReviewSteps
{
    private const int MaxBytes = 256 * 1024;

    public const string MarkPrefix = "reviewed:";

    // Which step keys have panels in this build (POC-02 lands the first two, POC-03 the tests
    // panel). The declared file is the source of what draws; this set is the source of what opens.
    // A declared key outside it draws disabled, named.
    public static readonly IReadOnlySet<string> KnownImplemented = new HashSet<string>(["proposal", "code", "tests", "evidence"], StringComparer.OrdinalIgnoreCase);

    private static readonly System.Text.Json.JsonSerializerOptions Options = new()
    {
        ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
    };

    public sealed record DeclaredStep(string Key, string Title, string Asserts);

    /// The review-steps reading plus its prompts map, read in one bounded JSONC pass.
    public sealed record ReadResult(IReadOnlyList<DeclaredStep> Steps, IReadOnlyDictionary<string, string> Prompts, bool Absent, bool Unreadable, string? Problem);

    public static ReadResult Read(string path)
    {
        if (!File.Exists(path))
        {
            return new([], EmptyPrompts, true, false, $"no review steps declared at {path} — declare them there in a \"steps\" array");
        }

        try
        {
            var info = new FileInfo(path);
            if (info.Length > MaxBytes)
            {
                return new([], EmptyPrompts, false, true, $"{path} is {info.Length} bytes, over the {MaxBytes} bound");
            }

            var shape = System.Text.Json.JsonSerializer.Deserialize<Shape>(File.ReadAllText(path), Options);
            var steps = (shape?.Steps ?? [])
                .Where(s => !string.IsNullOrWhiteSpace(s?.Key) && !string.IsNullOrWhiteSpace(s?.Title))
                .GroupBy(s => s!.Key!.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .Select(s => new DeclaredStep(s!.Key!.Trim(), s.Title!.Trim(), (s.Asserts ?? "").Trim()))
                .ToList();
            var prompts = shape?.Prompts is { } map
                ? new Dictionary<string, string>(map, StringComparer.OrdinalIgnoreCase) as IReadOnlyDictionary<string, string>
                : EmptyPrompts;
            return new(steps, prompts, false, false, shape?.Steps is null ? $"no \"steps\" array in {path}" : null);
        }
        catch (Exception e)
        {
            return new([], EmptyPrompts, false, true, $"{path} could not be parsed: {e.Message}");
        }
    }

    private static readonly IReadOnlyDictionary<string, string> EmptyPrompts = new Dictionary<string, string>();

    /// The prompt a step's run is launched with, from the declared prompts map. An absent map or
    /// key is its own absence — null, never an invented prompt.
    public static string? PromptFor(ReadResult declared, string stepKey) =>
        declared.Prompts.TryGetValue(stepKey, out var prompt) && !string.IsNullOrWhiteSpace(prompt) ? prompt : null;

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

    private sealed record Shape(List<Step>? Steps, Dictionary<string, string>? Prompts);

    private sealed record Step(string? Key, string? Title, string? Asserts);
}
