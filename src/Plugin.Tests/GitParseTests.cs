using AiSdlc;
using Shouldly;

namespace AiSdlcPlugin.Tests;

/// numstat -z parsing (rename pairs, binary dashes) and the repo-path regex.
public class GitParseTests
{
    private static Git Git(string output) => new((cwd, args) => args[0] switch
    {
        "diff" => (0, output, ""),
        _ => (0, "", ""),
    });

    [Fact]
    public void Numstat_plain_and_binary()
    {
        var git = Git("12\t3\tsrc/a.ts\0");

        var rows = git.Numstat("/r", "basis");

        rows.Single().ShouldBe(new NumstatRow("src/a.ts", 12, 3, false, null));
    }

    [Fact]
    public void Numstat_binary_dash_is_a_flag()
    {
        var git = Git("-\t-\tlogo.png\0");

        var rows = git.Numstat("/r", "basis");

        rows.Single().IsBinary.ShouldBeTrue();
        rows.Single().Added.ShouldBe(0);
    }

    [Fact]
    public void Numstat_rename_pair()
    {
        var git = Git("5\t0\t\0old-name.ts\0new-name.ts\0");

        var rows = git.Numstat("/r", "basis");

        rows.Single().ShouldBe(new NumstatRow("new-name.ts", 5, 0, false, "old-name.ts"));
    }

    [Theory]
    [InlineData("git@github.com:asantamariaplainconcepts/ai-sdlc-plugin.git", "asantamariaplainconcepts/ai-sdlc-plugin")]
    [InlineData("https://github.com/asantamariaplainconcepts/ai-sdlc-plugin", "asantamariaplainconcepts/ai-sdlc-plugin")]
    [InlineData("https://gitlab.com/a/b", null)]
    [InlineData(null, null)]
    public void Repo_path_from_remote(string? remote, string? expected)
    {
        GitHub.RepoPath(remote).ShouldBe(expected);
    }
}
