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
