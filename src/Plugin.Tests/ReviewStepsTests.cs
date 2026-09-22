using AiSdlc;
using Shouldly;

namespace AiSdlcPlugin.Tests;

/// .harness/review.json reading: JSONC tolerance, bounding, absence-vs-unreadable, and the
/// read-only mark derivation. Absence assertions carry a positive anchor in the same fixture
/// set, so an absence that silently became "always absent" fails.
public class ReviewStepsTests
{
    private string Write(string content, string name = "review.json")
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
              "steps": [
                { "key": "proposal", "title": "Proposal", "asserts": "the declaration is worth reviewing", },
              ],
            }
            """);

        var read = ReviewSteps.Read(path);

        read.Absent.ShouldBeFalse();
        read.Unreadable.ShouldBeFalse();
        read.Problem.ShouldBeNull();
        read.Steps.Single().Key.ShouldBe("proposal");
        read.Steps.Single().Asserts.ShouldBe("the declaration is worth reviewing");
    }

    [Fact]
    public void Missing_file_is_absent_with_the_remedy_path()
    {
        var read = ReviewSteps.Read("/does/not/exist/review.json");

        read.Absent.ShouldBeTrue();
        read.Unreadable.ShouldBeFalse();
        read.Steps.ShouldBeEmpty();
        read.Problem.ShouldNotBeNull();
        read.Problem.ShouldContain("no review steps declared");
        read.Problem.ShouldContain("/does/not/exist/review.json");
    }

    [Fact]
    public void Broken_json_is_unreadable_not_lost()
    {
        var read = ReviewSteps.Read(Write("{ not json"));

        read.Unreadable.ShouldBeTrue();
        read.Absent.ShouldBeFalse();
        read.Steps.ShouldBeEmpty();
        read.Problem.ShouldNotBeNull();
        read.Problem.ShouldContain("could not be parsed");
    }

    [Fact]
    public void Oversized_file_is_refused_before_parsing()
    {
        var path = Write(new string(' ', 300 * 1024));

        var read = ReviewSteps.Read(path);

        read.Unreadable.ShouldBeTrue();
        read.Problem!.ShouldContain("over the");
    }

    [Fact]
    public void Blank_key_entries_are_refused_and_the_rest_parse()
    {
        var path = Write("""
            { "steps": [
              { "key": "", "title": "No key", "asserts": "" },
              { "key": "code", "title": "Code", "asserts": "the diff is read as a diff" },
            ] }
            """);

        var read = ReviewSteps.Read(path);

        read.Unreadable.ShouldBeFalse();
        read.Steps.Single().Key.ShouldBe("code");
    }

    [Fact]
    public void No_steps_array_is_a_problem_not_zero_steps()
    {
        var read = ReviewSteps.Read(Write("""{ }"""));

        read.Absent.ShouldBeFalse();
        read.Unreadable.ShouldBeFalse();
        read.Steps.ShouldBeEmpty();
        read.Problem.ShouldNotBeNull();
        read.Problem.ShouldContain("no \"steps\" array");
    }

    [Fact]
    public void Duplicate_keys_keep_the_first_declaration()
    {
        var path = Write("""
            { "steps": [
              { "key": "code", "title": "First", "asserts": "first declaration wins" },
              { "key": "Code", "title": "Second", "asserts": "" },
            ] }
            """);

        var read = ReviewSteps.Read(path);

        read.Steps.Single().Title.ShouldBe("First");
        read.Steps.Single().Asserts.ShouldBe("first declaration wins");
    }

    // --------------------------------------------------- marks, read from the provider

    [Fact]
    public void Label_present_for_a_declared_key_marks()
    {
        ReviewSteps.Marked("code", ["reviewed:code"]).ShouldBeTrue();
    }

    [Fact]
    public void Mark_imports_only_the_reviewed_prefix_and_casing()
    {
        // case-insensitive on the whole label, harness parity
        ReviewSteps.Marked("code", ["REVIEWED:Code"]).ShouldBeTrue();
        ReviewSteps.Marked("code", ["reviewed:prop"]).ShouldBeFalse();
        // labels without the prefix do not mark
        ReviewSteps.Marked("code", ["code", "review", "wip:code"]).ShouldBeFalse();
    }

    [Fact]
    public void Label_for_a_key_nobody_declared_is_just_a_label()
    {
        ReviewSteps.Marked("proposal", ["reviewed:unknown-step"]).ShouldBeFalse();
    }

    [Fact]
    public void Marks_not_asked_names_why_in_both_directions()
    {
        ReviewSteps.MarksNotAskedReason(null).ShouldBe("marks are not asked — the branch resolves no issue here");
        ReviewSteps.MarksNotAskedReason("bad credentials").ShouldContain("bad credentials");
    }

    // --------------------------------------------------- the finder both readers share

    [Fact]
    public void FindDeclared_walks_up_to_the_nearest_harness_dir()
    {
        var root = Path.Combine(Path.GetTempPath(), "ai-sdlc-tests", Guid.NewGuid().ToString("N"));
        var nested = Path.Combine(root, "a", "b");
        Directory.CreateDirectory(Path.Combine(root, ".harness"));
        var declared = Path.Combine(root, ".harness", "review.json");
        File.WriteAllText(declared, """{ "steps": [] }""");
        Directory.CreateDirectory(Path.Combine(nested, ".harness"));

        // the walk finds the nearest declared file, a worktree's own before the checkout's
        ReviewSteps.FindDeclared(Path.Combine(root, "a"), "review.json").ShouldBe(declared);
        // an empty .harness dir above does not shadow the file below it
        ReviewSteps.FindDeclared(nested, "review.json").ShouldBe(declared);
        // a chain with no declared file falls back to the start, so the remedy path is stable
        ReviewSteps.FindDeclared(Path.Combine(Path.GetTempPath(), "ai-sdlc-tests", Guid.NewGuid().ToString("N")), "review.json")
            .ShouldEndWith("/.harness/review.json");
    }
}
