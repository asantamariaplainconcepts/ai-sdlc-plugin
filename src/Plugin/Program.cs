using AiSdlc;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// .harness/data is gitignored runtime state, found from the repo root whatever cwd the host starts in.
var store = new Store(Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "..", ".harness", "data", "poc.db")));
var header = new Header(git: null, github: null, store: store);
var git = new Git();
var github = new GitHub();
var runs = new Runs(git, store);

app.MapGet("/api/header", async (string? path) =>
{
    var reading = await header.Read(Path.GetFullPath(path ?? app.Environment.ContentRootPath));
    store.RecordWorktree(reading.Path, reading.Branch, git.Head(reading.Path), DateTimeOffset.UtcNow.ToString("o"));
    return Results.Ok(new
    {
        path = reading.Path,
        branch = reading.Branch,
        notARepository = reading.NotARepository,
        facts = reading.Facts.Select(f => new { key = f.Key, text = f.Text, tone = f.Tone.ToString().ToLowerInvariant(), title = f.Title }),
    });
});

// The steps of a review, declared in .harness/review.json and marked against the resolved
// issue's reviewed:* labels — read from the provider, never written. Absent and unreadable
// differ, each with its remedy; a declared step with no panel draws disabled saying so.
app.MapGet("/api/steps", async (string? path) =>
{
    var cwd = Path.GetFullPath(path ?? app.Environment.ContentRootPath);
    if (!git.IsRepository(cwd))
    {
        return Results.Ok(new { path = cwd, steps = Array.Empty<object>(), problem = (string?)null,
            marksNotAsked = "not a git repository — point at a directory git describes", issueKey = (string?)null, issueTitle = (string?)null });
    }

    var read = ReviewSteps.Read(ReviewSteps.FindDeclared(cwd, "review.json"));
    var branch = git.Branch(cwd);
    var repo = GitHub.RepoPath(git.RemoteUrl(cwd));
    // The prompt keys ride along so a panel can say "no prompt is declared" before it is asked.
    var promptKeys = read.Prompts.Keys.ToList();

    string? issueKey = null, issueTitle = null, marksNotAsked = null;
    List<string> labels = [];
    // Branch proposes, GitHub confirms — the same resolution the header's issue fact performs.
    if (branch is null || TaskResolution.KeyInBranch(branch) is not { } key || repo is null || TaskResolution.KeyNumber(key) is not { } number)
    {
        marksNotAsked = ReviewSteps.MarksNotAskedReason(null);
    }
    else
    {
        try
        {
            var found = await github.Issue(number, repo);
            (issueKey, issueTitle, labels) = (key, found.Title, [.. found.Labels]);
        }
        catch (Exception e)
        {
            marksNotAsked = ReviewSteps.MarksNotAskedReason(e.Message);
        }
    }

    return Results.Ok(new
    {
        path = cwd,
        steps = read.Steps.Select(s => new { key = s.Key, title = s.Title, asserts = s.Asserts,
            implemented = ReviewSteps.KnownImplemented.Contains(s.Key), marked = issueKey is not null && ReviewSteps.Marked(s.Key, labels) }),
        problem = read.Problem,
        marksNotAsked,
        issueKey,
        issueTitle,
        prompts = promptKeys,
    });
});

// What the branch declares as its change: the live openspec/changes directories of the worktree,
// listed; one artifact's text per read, bounded. Absence names the path where a declaration
// would be written — a branch with no change is an ordinary branch, said out loud.
app.MapGet("/api/proposal", (string? path) =>
{
    var cwd = Path.GetFullPath(path ?? app.Environment.ContentRootPath);
    var problem = NotGit(cwd, git);
    if (problem is not null)
    {
        return Results.Ok(new { path = cwd, problem, root = (string?)null,
            changes = Array.Empty<object>() });
    }

    var discovery = Change.Discover(cwd);
    return Results.Ok(new
    {
        path = cwd,
        problem = (string?)null,
        root = discovery.Root,
        changes = discovery.Changes.Select(c => new { name = c.Name, path = c.Path,
            artifacts = c.Artifacts.Select(a => new { name = a.Name, path = a.Path }) }),
    });
});

app.MapGet("/api/artifact", (string? path, string? file) =>
{
    if (string.IsNullOrWhiteSpace(file))
    {
        return Results.BadRequest(new { problem = "name the artifact file to read (?file=)" });
    }

    var read = Change.ReadArtifact(Path.GetFullPath(file));
    return Results.Ok(new { path = read.Path, text = read.Text, problem = read.Problem });
});

// The change as a diff: parsed to files, hunks and clasped lines, capped at two hundred presented
// lines with the hidden count named. Not asked — no trunk, not a repository — is a sentence,
// never an empty diff.
app.MapGet("/api/code", (string? path) =>
{
    var cwd = Path.GetFullPath(path ?? app.Environment.ContentRootPath);
    var problem = NotGit(cwd, git);
    if (problem is not null)
    {
        return Results.Ok(new { path = cwd, problem, basis = (string?)null,
            cutAfter = (int?)null, hiddenLinesCount = 0, files = Array.Empty<object>() });
    }

    var trunk = git.Trunk(cwd);
    if (trunk is null || git.DiffBasis(cwd, trunk) is not { } basis)
    {
        return Results.Ok(new { path = cwd, problem = "the diff is not asked — no trunk to compare against, fetch the default branch", basis = (string?)null,
            cutAfter = (int?)null, hiddenLinesCount = 0, files = Array.Empty<object>() });
    }

    var text = git.PatchText(cwd, basis);
    // Files git has never been told about are all-added rows, the numstat rule: they are part of
    // the change the branch declares even though no diff engine has seen them.
    var untracked = git.Untracked(cwd);
    foreach (var name in untracked)
    {
        var body = File.Exists(Path.Join(cwd, name)) ? File.ReadAllText(Path.Join(cwd, name)) : "";
        text += $"\ndiff --git a/{name} b/{name}\n--- /dev/null\n+++ b/{name}\n@@ -0,0 +1,{CountLines(body)} @@\n{string.Concat(body.Split('\n').Select(l => $"+{l}\n"))}";
    }

    var read = Patch.Parse(text);
    return Results.Ok(new
    {
        path = cwd,
        problem = (string?)null,
        basis,
        cutAfter = read.CutAfter,
        hiddenLinesCount = read.HiddenLines,
        files = read.Files.Select(f => new
        {
            path = f.Path, renamedFrom = f.RenamedFrom, isBinary = f.IsBinary,
            hunks = f.Hunks.Select(h => new { header = h.Header,
                lines = h.Lines.Select(l => new { kind = l.Kind.ToString().ToLowerInvariant(), oldLine = l.OldLine, newLine = l.NewLine, text = l.Text }) }),
        }),
    });
});

// A worktree's runs: POST launches claude for a declared step's prompt and holds to completion
// (no streaming — the epic's "no es"), recording the run against its starting commit whatever
// happened; GET lists, most recent first.
app.MapPost("/api/runs", (string? path, string? step) =>
{
    var cwd = Path.GetFullPath(path ?? app.Environment.ContentRootPath);
    var problem = NotGit(cwd, git) ?? (string.IsNullOrWhiteSpace(step) ? "name the step to run (?step=)" : null);
    if (problem is not null)
    {
        return Results.Ok(new { path = cwd, problem, run = (object?)null });
    }

    var outcome = runs.LaunchAndRecord(cwd, step!);
    return Results.Ok(new { path = cwd, problem = outcome.Problem, run = RunView(outcome.Row) });
});

app.MapGet("/api/runs", (string? path) =>{
    var cwd = Path.GetFullPath(path ?? app.Environment.ContentRootPath);
    var problem = NotGit(cwd, git);
    return problem is not null
        ? Results.Ok(new { path = cwd, problem, runs = Array.Empty<object>() })
        : Results.Ok(new { path = cwd, problem = (string?)null, runs = runs.List(cwd).Select(RunView) });
});

// Null is not zero: an absent cost is a sentence about the provider, not a free run.
static object RunView(RunRow row) => new
{
    sessionId = row.SessionId, step = row.Step, prompt = row.Prompt, startingCommit = row.StartingCommit,
    startedAt = row.StartedAtIso, finishedAt = row.FinishedAtIso, exitCode = row.ExitCode, isError = row.IsError,
    costUsd = row.CostUsd, numTurns = row.NumTurns, durationMs = row.DurationMs,
    transcriptPath = row.TranscriptPath, transcriptLocated = row.TranscriptLocated,
    resultSummary = row.ResultSummary, problem = row.Problem,
};

static int CountLines(string body) => body.Length == 0 ? 0 : body.Split('\n').Length - (body.EndsWith('\n') ? 1 : 0);

// The declared gates over the candidate: POST runs them (held to completion — the same contract
// as the runs POST) and records exit + bounded tail against the resulting commit; GET reads the
// recorded outcomes without running anything, with staleness against the present HEAD.
app.MapPost("/api/gates", (string? path) =>
{
    var cwd = Path.GetFullPath(path ?? app.Environment.ContentRootPath);
    var problem = NotGit(cwd, git);
    if (problem is not null)
    {
        return Results.Ok(new { path = cwd, problem, gates = Array.Empty<object>() });
    }

    var outcome = Gates.RunDeclaredGates(cwd, git, store);
    return Results.Ok(new { path = cwd, problem = outcome.Problem, gates = outcome.Gates.Select(GateView) });
});

app.MapGet("/api/gates", (string? path) =>
{
    var cwd = Path.GetFullPath(path ?? app.Environment.ContentRootPath);
    var problem = NotGit(cwd, git);
    if (problem is not null)
    {
        return Results.Ok(new { path = cwd, problem, gates = Array.Empty<object>() });
    }

    var outcome = Gates.ReadDeclaredGates(cwd, git, store);
    return Results.Ok(new { path = cwd, problem = outcome.Problem, gates = outcome.Gates.Select(GateView) });
});

// Null is not zero: a missing exit is "never judged", a missing gate is refuted with its remedy.
static object GateView(Gates.GateView g) => new
{
    key = g.Key,
    name = g.Name,
    missing = g.Missing,
    missingSentence = g.MissingSentence,
    reading = g.Reading is { } reading ? reading.ToString().ToLowerInvariant() : null,
    exitCode = g.ExitCode,
    commit = g.Commit,
    stale = g.Freshness == Gates.Freshness.Stale,
    freshness = g.Freshness.ToString().ToLowerInvariant(),
    finishedAt = g.FinishedAtIso,
    tail = g.Tail,
    problem = g.Problem,
};

static string? NotGit(string cwd, Git git) => !Directory.Exists(cwd)
    ? $"{cwd} is not there — point at a directory that exists"
    : git.IsRepository(cwd) ? null : "not a git repository — point at a directory git describes";

// One host, no CORS, no base URL: the built frontend is served same-origin from src/web/dist.

var webRoot = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "web", "dist"));
if (Directory.Exists(webRoot))
{
    var files = new StaticFileOptions { FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(webRoot) };
    app.UseDefaultFiles();
    app.UseStaticFiles(files);
    app.MapFallbackToFile("index.html", files);
}

app.Run();

public partial class Program;
