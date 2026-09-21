namespace AiSdlc;

// .harness/commands.json reading, JSONC-safe and byte-bounded: a declared file is untrusted
// input — bounded before parsing, never executed by its reader. Absent and unreadable differ.
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
            return new([], true, false, $"not found at {path} — declare gates there with \"gate\": true");
        }

        try
        {
            var info = new FileInfo(path);
            if (info.Length > MaxBytes)
            {
                return new([], false, true, $"{path} is {info.Length} bytes, over the {MaxBytes} bound");
            }

            var shape = System.Text.Json.JsonSerializer.Deserialize<Shape>(File.ReadAllText(path), Options);
            var commands = (shape?.Commands ?? [])
                .Where(c => !string.IsNullOrWhiteSpace(c?.Key) && !string.IsNullOrWhiteSpace(c?.Run))
                .Select(c => new DeclaredCommand(c!.Key!, c.Name ?? c.Key!, c.Run!, c.Gate)).ToList();
            return new(commands, false, false, shape?.Commands is null ? $"no \"commands\" array in {path}" : null);
        }
        catch (Exception e)
        {
            return new([], false, true, $"{path} could not be parsed: {e.Message}");
        }
    }

    private sealed record Shape(List<Command>? Commands);

    private sealed record Command(string? Key, string? Name, string? Run, bool Gate);
}
