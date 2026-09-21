using AiSdlc;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var storePath = Path.Combine(app.Environment.ContentRootPath, "..", "..", ".harness", "data", "poc.db");
var store = new Store(storePath);
var header = new Header();
var git = new Git();

app.MapGet("/api/header", async (string? path) =>
{
    var target = path ?? app.Environment.ContentRootPath;
    var reading = await header.Read(Path.GetFullPath(target));
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
    app.UseDefaultFiles();
    app.UseStaticFiles(new StaticFileOptions { FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(webRoot) });
    app.MapFallbackToFile("index.html", new StaticFileOptions { FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(webRoot) });
}

app.Run();

public partial class Program;
