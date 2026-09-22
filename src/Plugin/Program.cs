using AiSdlc;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// .harness/data is gitignored runtime state, found from the repo root whatever cwd the host starts in.
var store = new Store(Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "..", ".harness", "data", "poc.db")));
var header = new Header();
var git = new Git();
var github = new GitHub();

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
    });
});

// What the branch declares as its change: the live openspec/changes directories of the worktree,
// listed; one artifact's text per read, bounded. Absence names the path where a declaration
// would be written — a branch with no change is an ordinary branch, said out loud.
app.MapGet("/api/proposal", (string? path) =>
{
    var cwd = Path.GetFullPath(path ?? app.Environment.ContentRootPath);
    if (!git.IsRepository(cwd))
    {
        return Results.Ok(new { path = cwd, problem = "not a git repository — point at a directory git describes", root = (string?)null,
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
    if (!git.IsRepository(cwd))
    {
        return Results.Ok(new { path = cwd, problem = "not a git repository — point at a directory git describes", basis = (string?)null,
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

static int CountLines(string body) => body.Length == 0 ? 0 : body.Split('\n').Length - (body.EndsWith('\n') ? 1 : 0);

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
