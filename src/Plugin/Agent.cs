namespace AiSdlc;

// The agent seam: one shared process helper plus the readable facts of a claude run. The output
// shape is the one `claude --print --output-format json` prints (verified against CLI 2.1.234 —
// one JSON blob, exit 0 even when the agent's own task failed, is_error for provider trouble).
// The transcript rule is measured, not guessed: real path with /, ., _ and space munged to '-'
// under ~/.claude/projects, the session id as filename.
public static class Agent
{
    /// The claude launch line as a pure function: what any run is started with. The permission
    /// posture lives here and in the change's Why — acceptEdits, the disposable-worktree defense,
    /// strictly narrower than bypassPermissions because a prompt in --print is a denial.
    public static IReadOnlyList<string> ClaudeArguments(string sessionId, string prompt) =>
        ["--print", "--output-format", "json", "--session-id", sessionId, "--permission-mode", "acceptEdits", prompt];

    // Two captures, both bounded: the blob is small but a chatty failure is not, and the row keeps
    // summaries, not streams.
    private const int MaxStdOutBytes = 1024 * 1024;
    private const int MaxStdErrBytes = 2 * 1024;

    public sealed record LaunchResult(int ExitCode, string StdOut, string StdErr, double DurationMs, string? Problem);

    /// Start a command in a directory and wait for it to end. ExitCode -1 with a Problem is the
    /// "could not start" answer — its own sentence, not a crash and not exit 0.
    public static LaunchResult Launch(string command, IReadOnlyList<string> args, string cwd)
    {
        try
        {
            var info = new System.Diagnostics.ProcessStartInfo(command)
            {
                WorkingDirectory = Directory.Exists(cwd) ? cwd : Path.GetDirectoryName(cwd)!,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (var arg in args)
            {
                info.ArgumentList.Add(arg);
            }

            var started = System.Diagnostics.Stopwatch.StartNew();
            using var process = System.Diagnostics.Process.Start(info)!;
            var stdout = ReadBounded(process.StandardOutput, MaxStdOutBytes);
            var stderr = ReadBounded(process.StandardError, MaxStdErrBytes);
            process.WaitForExit();
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
        string? SessionId, decimal? CostUsd, bool? IsError, int? NumTurns, double? DurationMs,
        string? ResultSummary, string? Problem);

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
                root.TryGetProperty("session_id", out var session) && session.ValueKind == System.Text.Json.JsonValueKind.String ? session.GetString() : null,
                root.TryGetProperty("total_cost_usd", out var cost) && cost.ValueKind == System.Text.Json.JsonValueKind.Number ? cost.GetDecimal() : null,
                root.TryGetProperty("is_error", out var error) && error.ValueKind is System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False ? error.GetBoolean() : null,
                root.TryGetProperty("num_turns", out var turns) && turns.ValueKind == System.Text.Json.JsonValueKind.Number ? turns.GetInt32() : null,
                root.TryGetProperty("duration_ms", out var duration) && duration.ValueKind == System.Text.Json.JsonValueKind.Number ? duration.GetDouble() : null,
                root.TryGetProperty("result", out var result) && result.ValueKind == System.Text.Json.JsonValueKind.String ? result.GetString() : null,
                root.ValueKind == System.Text.Json.JsonValueKind.Object ? null : "the agent's output is not a JSON object");
        }
        catch (Exception e)
        {
            return new(null, null, null, null, null, null, $"the agent's output did not parse: {e.Message}");
        }
    }

    public sealed record TranscriptReading(string? Path, bool Located);

    /// Where this session's transcript has to be, by the measured rule. The path is carried when
    /// the file is there; absent is absent — said, never guessed elsewhere.
    public static TranscriptReading Transcript(string cwd, string sessionId)
    {
        if (sessionId.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
        {
            return new(null, false);
        }

        var real = RealPath(cwd);
        var munged = new string([.. real.Select(c => c is '/' or '.' or '_' or ' ' ? '-' : c)]);
        var path = System.IO.Path.Join(
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "projects"),
            munged,
            $"{sessionId}.jsonl");
        return new(path, File.Exists(path));
    }

    /// The OS's own answer to "where is this really" — /var is /private/var on macOS, and the
    /// munged directory name is made from the real path, not the alias a person typed.
    private static string RealPath(string path)
    {
        try
        {
            return System.IO.Path.GetFullPath(path);
        }
        catch (Exception)
        {
            return path;
        }
    }
}
