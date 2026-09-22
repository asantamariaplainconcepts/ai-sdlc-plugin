namespace AiSdlc;

// The agent seam: one shared process helper plus the readable facts of a claude run. The output
// shape is the one `claude --print --output-format json` prints (verified against CLI 2.1.234 —
// one JSON blob, exit 0 even when the agent's own task failed, is_error for provider trouble).
// The transcript rule is measured, not guessed: real path with /, ., _ and space munged to '-'
// under ~/.claude/projects, the session id as filename.
public static class Agent
{
    /// The launch line, a pure function of (session, prompt). The permission posture lives here
    /// and in the change's Why — acceptEdits, the disposable-worktree defense, strictly narrower
    /// than bypassPermissions because a prompt in --print is a denial.
    public static IReadOnlyList<string> ClaudeArguments(string sessionId, string prompt) =>
        ["--print", "--output-format", "json", "--session-id", sessionId, "--permission-mode", "acceptEdits", prompt];

    private const int MaxStdOutBytes = 1024 * 1024;
    private const int MaxStdErrBytes = 2 * 1024;

    public sealed record LaunchResult(int ExitCode, string StdOut, string StdErr, double DurationMs, string? Problem);

    /// Start a command in a directory and wait for it to end. ExitCode -1 with a Problem is the
    /// "could not start" answer — its own sentence, not a crash and not exit 0. A timeoutMs kills
    /// the process tree and reports the timeout as a Problem: a wall clock is not a judge.
    public static LaunchResult Launch(string command, IReadOnlyList<string> args, string cwd, int? timeoutMs = null)
    {
        try
        {
            var info = new System.Diagnostics.ProcessStartInfo(command) { WorkingDirectory = cwd, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in args)
            {
                info.ArgumentList.Add(arg);
            }

            var started = System.Diagnostics.Stopwatch.StartNew();
            using var process = System.Diagnostics.Process.Start(info)!;
            string stdout, stderr;
            if (timeoutMs is { } limit)
            {
                // Drain through tasks so a stuck child cannot wedge the read before the kill.
                var outTask = Task.Run(() => ReadBounded(process.StandardOutput, MaxStdOutBytes));
                var errTask = Task.Run(() => ReadBounded(process.StandardError, MaxStdErrBytes));
                if (!process.WaitForExit(limit))
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                    return new(-1, "", "", started.Elapsed.TotalMilliseconds, $"{command} timed out after {limit}ms — killed, not judged");
                }

                stdout = outTask.Result;
                stderr = errTask.Result;
            }
            else
            {
                stdout = ReadBounded(process.StandardOutput, MaxStdOutBytes);
                stderr = ReadBounded(process.StandardError, MaxStdErrBytes);
                process.WaitForExit();
            }

            started.Stop();
            return new(process.ExitCode, stdout, stderr, started.Elapsed.TotalMilliseconds, null);
        }
        catch (Exception e)
        {
            return new(-1, "", e.Message, 0, $"{command} could not start: {e.Message}");
        }
    }

    private static string ReadBounded(StreamReader reader, int max)
    {
        var buffer = new char[max / 2];
        var read = reader.Read(buffer, 0, buffer.Length);
        return read <= 0 ? "" : new string(buffer, 0, read);
    }

    public sealed record RunFacts(
        string? SessionId, decimal? CostUsd, bool? IsError, int? NumTurns, double? DurationMs, string? ResultSummary, string? Problem);

    /// The blob the CLI prints, read tolerantly: every fact it may lack it reports as absent,
    /// never zero — cost absent is null, an unparseable blob is its own problem sentence.
    public static RunFacts ParseResult(string stdout) =>
        string.IsNullOrWhiteSpace(stdout)
            ? new(null, null, null, null, null, null, "the agent printed nothing — no JSON blob to read")
            : ParseJson(stdout);

    private static RunFacts ParseJson(string stdout)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(stdout);
            var root = doc.RootElement;
            return new(
                String(root, "session_id"), Number(root, "total_cost_usd")?.GetDecimal(), Bool(root, "is_error"),
                Number(root, "num_turns")?.GetInt32(), Number(root, "duration_ms")?.GetDouble(), String(root, "result"),
                root.ValueKind == System.Text.Json.JsonValueKind.Object ? null : "the agent's output is not a JSON object");
        }
        catch (Exception e)
        {
            return new(null, null, null, null, null, null, $"the agent's output did not parse: {e.Message}");
        }
    }

    private static System.Text.Json.JsonElement? Property(System.Text.Json.JsonElement root, string name) =>
        root.ValueKind == System.Text.Json.JsonValueKind.Object && root.TryGetProperty(name, out var value) ? value : null;

    private static string? String(System.Text.Json.JsonElement root, string name) =>
        Property(root, name) is { ValueKind: System.Text.Json.JsonValueKind.String } value ? value.GetString() : null;

    private static System.Text.Json.JsonElement? Number(System.Text.Json.JsonElement root, string name) =>
        Property(root, name) is { ValueKind: System.Text.Json.JsonValueKind.Number } value ? value : null;

    private static bool? Bool(System.Text.Json.JsonElement root, string name) =>
        Property(root, name) is { ValueKind: System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False } value ? value.GetBoolean() : null;

    public sealed record TranscriptReading(string? Path, bool Located);

    /// Where this session's transcript has to be, by the measured rule: the real path munged
    /// (/ . _ and space to '-'), the session id as filename. The path is carried when the file is
    /// there; absent is absent — said, never guessed elsewhere.
    public static TranscriptReading Transcript(string cwd, string sessionId)
    {
        if (sessionId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return new(null, false);
        }

        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "projects");
        var munged = new string([.. Path.GetFullPath(cwd).Select(c => c is '/' or '.' or '_' or ' ' ? '-' : c)]);
        var path = Path.Join(root, munged, $"{sessionId}.jsonl");
        return new(path, File.Exists(path));
    }
}
