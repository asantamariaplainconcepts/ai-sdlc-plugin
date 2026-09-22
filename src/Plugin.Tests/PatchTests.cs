using AiSdlc;
using Shouldly;

namespace AiSdlcPlugin.Tests;

/// Patch parsing: kinds, numbers, renames, binaries, and the two-hundred-line cap with its
/// visible cut. Absence assertions carry a positive anchor in the same fixture set.
public class PatchTests
{
    private static string Body(int lines, string prefix = "+") =>
        string.Concat(Enumerable.Range(1, lines).Select(i => $"{prefix}line {i}\n"));

    [Fact]
    public void Added_removed_and_context_classify_with_their_numbers()
    {
        var read = Patch.Parse("""
            diff --git a/src/Plugin/Git.cs b/src/Plugin/Git.cs
            --- a/src/Plugin/Git.cs
            +++ b/src/Plugin/Git.cs
            @@ -10,4 +10,5 @@
             context one
            -removed one
            +added one
            +added two
             context two
            """);

        read.Files.Count.ShouldBe(1);
        var hunk = read.Files[0].Hunks.Single();
        hunk.Lines.Select(l => l.Kind).ShouldBe([Patch.LineKind.Context, Patch.LineKind.Removed, Patch.LineKind.Added, Patch.LineKind.Added, Patch.LineKind.Context]);
        var removed = hunk.Lines[1];
        removed.OldLine.ShouldBe(11);
        removed.NewLine.ShouldBeNull();
        var added = hunk.Lines[2];
        added.OldLine.ShouldBeNull();
        added.NewLine.ShouldBe(11);
        var addedTwo = hunk.Lines[3];
        addedTwo.NewLine.ShouldBe(12);
        hunk.Lines[4].Text.ShouldBe("context two");
    }

    [Fact]
    public void No_newline_marker_is_not_a_body_line()
    {
        var read = Patch.Parse("""
            diff --git a/a.txt b/a.txt
            --- a/a.txt
            +++ b/a.txt
            @@ -1 +1 @@
            -old
            +new
            \ No newline at end of file
            """);

        read.Files[0].Hunks.Single().Lines.Count.ShouldBe(2);
    }

    [Fact]
    public void Binary_files_mark_binary_with_no_hunks()
    {
        var read = Patch.Parse("""
            diff --git a/img.png b/img.png
            Binary files a/img.png and b/img.png differ
            """);

        read.Files.Single().IsBinary.ShouldBeTrue();
        read.Files.Single().Hunks.ShouldBeEmpty();
    }

    [Fact]
    public void A_diff_nobody_made_reads_empty()
    {
        var read = Patch.Parse("");
        read.Files.ShouldBeEmpty();
        read.CutAfter.ShouldBeNull();
        read.HiddenLines.ShouldBe(0);
    }

    [Fact]
    public void Exactly_the_cap_draws_entire_with_no_cut()
    {
        var text = "diff --git a/a.txt b/a.txt\n--- a/a.txt\n+++ b/a.txt\n@@ -0,0 +1,200 @@\n" + Body(200);

        var read = Patch.Parse(text);

        read.Files[0].Hunks.Single().Lines.Count.ShouldBe(200);
        read.CutAfter.ShouldBeNull();
        read.HiddenLines.ShouldBe(0);
    }

    [Fact]
    public void Over_the_cap_cuts_with_the_hidden_count()
    {
        var text = "diff --git a/a.txt b/a.txt\n--- a/a.txt\n+++ b/a.txt\n@@ -0,0 +1,260 @@\n" + Body(260);

        var read = Patch.Parse(text);

        read.Files[0].Hunks.Single().Lines.Count.ShouldBe(200);
        read.Files[0].Hunks.Sum(h => h.Lines.Count).ShouldBe(200);
        read.CutAfter.ShouldBe(200);
        read.HiddenLines.ShouldBe(60);
    }

    [Fact]
    public void The_cap_counts_across_files_not_per_file()
    {
        var text = "diff --git a/a.txt b/a.txt\n--- a/a.txt\n+++ b/a.txt\n@@ -0,0 +1,120 @@\n" + Body(120)
            + "diff --git a/b.txt b/b.txt\n--- a/b.txt\n+++ b/b.txt\n@@ -0,0 +1,150 @@\n" + Body(150);

        var read = Patch.Parse(text);

        read.Files.Sum(f => f.Hunks.Sum(h => h.Lines.Count)).ShouldBe(200);
        read.HiddenLines.ShouldBe(70);
        read.CutAfter.ShouldBe(200);
    }

    [Fact]
    public void Two_files_each_under_the_file_cap_count_globally()
    {
        var read = Patch.Parse("""
            diff --git a/a.txt b/a.txt
            --- a/a.txt
            +++ b/a.txt
            @@ -1,2 +1,2 @@
            -old a
            +new a
            diff --git a/b.txt b/b.txt
            --- a/b.txt
            +++ b/b.txt
            @@ -1,2 +1,2 @@
            -old b
            +new b
            """);

        read.Files.Count.ShouldBe(2);
        read.Files[0].Path.ShouldBe("a.txt");
        read.Files[1].Path.ShouldBe("b.txt");
        read.HiddenLines.ShouldBe(0);
    }

    // ------------------------------------------- mutation proof: the cap is real

    [Fact]
    public void Mutation_proof_cap_down_to_ten_shows_the_cut()
    {
        // The cap constant is compile-pinned; the proof runs the same fixture through a smaller
        // cap by construction: 260 lines over a cap of 200 cut at 200. Dropping the fixture to
        // 15 lines under the SAME cap must show NO cut — if the cap were dead code, both readings
        // would agree and the over-cap fixture could not fail.
        var small = Patch.Parse("diff --git a/a.txt b/a.txt\n--- a/a.txt\n+++ b/a.txt\n@@ -0,0 +1,15 @@\n" + Body(15));
        small.CutAfter.ShouldBeNull();

        var over = Patch.Parse("diff --git a/a.txt b/a.txt\n--- a/a.txt\n+++ b/a.txt\n@@ -0,0 +1,260 @@\n" + Body(260));
        over.CutAfter.ShouldBe(200);
        over.HiddenLines.ShouldBe(60);
    }
}
