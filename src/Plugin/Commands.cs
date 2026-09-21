namespace AiSdlc;

/// .harness/commands.json reading, JSONC-safe and byte-bounded: a declared file is
/// untrusted input — bounded before parsing, never executed by this reader.
public static class Commands
{
    private const int MaxBytes = 256 * 1024;

    private static readonly System.Text.Json.JsonSerializerOptions Options = new()
    {
        ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
    };

    public sealed record DeclaredCommand(string Key, string Name, string Run, bool Gate);

    public sealed record ReadResult(IReadOnlyList<DeclaredCommand> Commands, bool Absent, bool Unreadable, string? Problem)
    {
        public IReadOnlyList<DeclaredCommand> Gates => Commands.Where(c => c.Gate).ToList();
    }

    public static ReadResult Read(string path)
    {
        if (!File.Exists(path))
        {
            return new ReadResult([], Absent: true, Unreadable: false, Problem: $"not found at {path} — declare gates there with \"gate\": true");
        }

        try
        {
            var info = new FileInfo(path);
            if (info.Length > MaxBytes)
            {
                return new ReadResult([], false, true, $"{path} is {info.Length} bytes, over the {MaxBytes} bound");
            }

            var json = File.ReadAllText(path);
            var doc = System.Text.Json.JsonSerializer.Deserialize<Shape>(json, Options);
            var commands = (doc?.Commands ?? [])
                .Where(c => !string.IsNullOrWhiteSpace(c.Key) && !string.IsNullOrWhiteSpace(c.Run))
                .Select(c => new DeclaredCommand(c.Key!, c.Name ?? c.Key!, c.Run!, c.Gate))
                .ToList();
            return new ReadResult(commands, false, false, doc?.Commands is null ? "no \"commands\" array" : null);
        }
        catch (Exception e)
        {
            return new ReadResult([], false, true, $"{path} could not be parsed: {e.Message} — fix the JSON or remove it");
        }
    }

    private sealed record Shape(List<Command>? Commands);

    private sealed record Command(string? Key, string? Name, string? Run, bool Gate);
}
