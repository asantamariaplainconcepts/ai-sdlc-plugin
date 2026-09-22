using AiSdlc;
using Shouldly;

namespace AiSdlcPlugin.Tests;

/// .harness/commands.json reading: JSONC tolerance, bounding, absence-vs-unreadable.
public class CommandsTests
{
    private string Write(string content, string name = "commands.json")
    {
        var dir = Path.Combine(Path.GetTempPath(), "ai-sdlc-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, name);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Comments_and_trailing_commas_parse()
    {
        var path = Write("""
            {
              // one comment
              "commands": [
                { "key": "build", "name": "Build", "run": "dotnet build", "gate": true, },
              ],
            }
            """);

        var read = Commands.Read(path);

        read.Absent.ShouldBeFalse();
        read.Unreadable.ShouldBeFalse();
        read.Gates.Single().Key.ShouldBe("build");
    }

    [Fact]
    public void Missing_file_is_absent_with_the_remedy_path()
    {
        var read = Commands.Read("/does/not/exist/commands.json");

        read.Absent.ShouldBeTrue();
        read.Problem.ShouldNotBeNull();
        read.Problem.ShouldContain("declare gates there");
    }

    [Fact]
    public void Broken_json_is_unreadable_not_lost()
    {
        var read = Commands.Read(Write("{ not json"));

        read.Unreadable.ShouldBeTrue();
        read.Problem.ShouldNotBeNull();
        read.Problem.ShouldContain("could not be parsed");
    }

    [Fact]
    public void Gate_false_by_default()
    {
        var path = Write("""{ "commands": [ { "key": "start", "run": "npm start" } ] }""");

        var read = Commands.Read(path);

        read.Gates.ShouldBeEmpty();
        read.Commands.Single().Key.ShouldBe("start");
    }

    [Fact]
    public void Oversized_file_is_refused_before_parsing()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ai-sdlc-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "commands.json");
        File.WriteAllText(path, " ".Repeat(300 * 1024));

        var read = Commands.Read(path);

        read.Unreadable.ShouldBeTrue();
        read.Problem!.ShouldContain("over the");
    }
}

file static class StringExt
{
    public static string Repeat(this string value, int count) => new(char.TryParse(value, out var c) ? c : ' ', count);
}
