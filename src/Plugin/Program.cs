using AiSdlc;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// .harness/data is gitignored runtime state, found from the repo root whatever cwd the host starts in.
var store = new Store(Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "..", ".harness", "data", "poc.db")));
var header = new Header();
var git = new Git();

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
