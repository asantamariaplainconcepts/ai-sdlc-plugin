namespace AiSdlc;

// The closed trigger vocabulary — the only difference between the two ways a run starts. The
// button and the poller share one launch contract; the value stored here is the divergence,
// and it lives in the record, never in the view.
public static class Triggers
{
    public const string Button = "button";
    public const string Poll = "poll";

    public static bool Known(string? trigger) => trigger is Button or Poll;

    public static string Refusal(string? trigger) => $"unknown trigger \"{trigger}\" — say {Button} or {Poll}";
}

// The watcher's shape, read from configuration the way the harness reads its mode keys: absent
// means off, not guessed. Off by default, five minutes between ticks, no paths watched — the
// rate-limit reasoning a GitHub-bound successor inherits already decided.
public sealed record WatcherConfig(bool Enabled, int IntervalSeconds, IReadOnlyList<string> Paths)
{
    public const int DefaultIntervalSeconds = 300;

    public static WatcherConfig Read(Microsoft.Extensions.Configuration.IConfiguration configuration) => new(
        bool.TryParse(configuration["Watcher:Enabled"], out var enabled) && enabled,
        int.TryParse(configuration["Watcher:IntervalSeconds"], out var seconds) && seconds > 0 ? seconds : DefaultIntervalSeconds,
        configuration.GetSection("Watcher:Paths").Get<string[]>() is { Length: > 0 } paths
            ? [.. paths.Where(p => !string.IsNullOrWhiteSpace(p))]
            : []);
}

// The second trigger: an in-process poller that starts runs by the SAME contract the panel's
// button uses — Runs.LaunchAndRecord with trigger "poll" is the only thing it can do to a
// worktree. The predicate is the recorded, reversible default a human must ratify: new work
// means HEAD moved since the last recorded run's starting commit, nothing about content.
public sealed class Watcher(Runs runs, Git git, Store store, Microsoft.Extensions.Configuration.IConfiguration configuration) : Microsoft.Extensions.Hosting.BackgroundService
{
    /// The launch decision, pure: differ ⇒ launch; the last run's commit still current ⇒ not.
    /// No run recorded at all ⇒ the first run is new work; a HEAD that cannot be read is
    /// compared against nothing and launches nothing — silence, not a guess.
    public static bool ShouldLaunch(string? lastStartingCommit, string? head) =>
        (lastStartingCommit, head) switch
        {
            (null, null) => false,
            (null, { }) => true,
            (_, null) => false,
            var (last, now) => last != now,
        };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var shape = WatcherConfig.Read(configuration);
        if (!shape.Enabled || shape.IntervalSeconds <= 0)
        {
            // Off by default: no key, no loop — indistinguishable from no watcher at all.
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(shape.IntervalSeconds));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                Tick();
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    /// One tick, serialized: a launch holds the tick to completion (the contract runs to
    /// completion), and one path that cannot be read never stops the others.
    private void Tick()
    {
        foreach (var path in WatcherConfig.Read(configuration).Paths)
        {
            try
            {
                var worktree = Path.GetFullPath(path);
                if (!Directory.Exists(worktree) || !git.IsRepository(worktree))
                {
                    continue;
                }

                var last = store.ListRuns(worktree).FirstOrDefault()?.StartingCommit;
                if (ShouldLaunch(last, git.Head(worktree)))
                {
                    runs.LaunchAndRecord(worktree, "tests", Triggers.Poll);
                }
            }
            catch
            {
                // A path that fails is a path skipped this tick — the watcher outlives it.
            }
        }
    }
}
