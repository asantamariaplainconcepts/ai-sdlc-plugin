using AiSdlc;
using Shouldly;

namespace AiSdlcPlugin.Tests;

/// Live change discovery: present, several, archive skipped, and the absence that names where
/// a declaration would be written. Absence assertions carry a positive anchor from the same
/// fixture family.
public class ChangeTests
{
    private string Root()
    {
        var root = Path.Combine(Path.GetTempPath(), "ai-sdlc-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static string Write(string dir, string name, string content)
    {
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static string ChangeDir(string root, string name) => Path.Combine(root, "openspec", "changes", name);

    [Fact]
    public void One_live_change_lists_its_markdown_artifacts()
    {
        var root = Root();
        Write(ChangeDir(root, "poc-99-one"), "proposal.md", "# why\n\nthe change\n");
        Write(ChangeDir(root, "poc-99-one"), "design.md", "# design\n");
        // Not markdown, not listed: a change is its Markdown declaration.
        Write(ChangeDir(root, "poc-99-one"), "notes.txt", "not an artifact\n");

        var discovery = Change.Discover(root);

        discovery.Changes.Single().Name.ShouldBe("poc-99-one");
        discovery.Changes.Single().Artifacts.Select(a => a.Name).ShouldBe(["design.md", "proposal.md"]);
    }

    [Fact]
    public void Several_live_changes_are_drawn_without_deciding()
    {
        var root = Root();
        Write(ChangeDir(root, "aaa-first"), "proposal.md", "one\n");
        Write(ChangeDir(root, "zzz-second"), "proposal.md", "two\n");

        var discovery = Change.Discover(root);

        discovery.Changes.Count.ShouldBe(2);
        discovery.Changes.Select(c => c.Name).ShouldBe(["aaa-first", "zzz-second"], ignoreOrder: true);
    }

    [Fact]
    public void Archive_is_skipped()
    {
        var root = Root();
        Write(ChangeDir(root, "archive"), "2026-01-01-old", "old\n");

        var discovery = Change.Discover(root);

        discovery.Changes.ShouldBeEmpty();
    }

    [Fact]
    public void No_changes_dir_names_where_it_would_be_written()
    {
        var root = Root();

        var discovery = Change.Discover(root);

        // Positive anchor: the root travels and the absence sentence names it.
        discovery.Root.ShouldBe(root);
        discovery.Changes.ShouldBeEmpty();
        discovery.Absence.ShouldContain("no change declared");
        discovery.Absence.ShouldContain(Path.Join(root, "openspec", "changes", "<name>", "proposal.md"));
    }

    [Fact]
    public void Empty_changes_dir_is_the_same_absence()
    {
        var root = Root();
        Directory.CreateDirectory(Path.Combine(root, "openspec", "changes"));

        var discovery = Change.Discover(root);

        discovery.Changes.ShouldBeEmpty();
        discovery.Absence.ShouldContain("no change declared");
    }

    [Fact]
    public void A_change_with_only_non_markdown_files_is_not_declared()
    {
        var root = Root();
        Write(ChangeDir(root, "only-txt"), "notes.txt", "not markdown\n");

        var discovery = Change.Discover(root);

        discovery.Changes.ShouldBeEmpty();
    }

    // ------------------------------------------------ artifact reads, confined
    [Fact]
    public void An_artifact_reads_its_text()
    {
        var root = Root();
        var path = Write(ChangeDir(root, "poc-99"), "proposal.md", "# the proposal\n\nbody\n");

        var read = Change.ReadArtifact(path);

        read.Text.ShouldNotBeNull();
        read.Text.ShouldContain("# the proposal");
        read.Problem.ShouldBeNull();
    }

    [Fact]
    public void A_vanished_artifact_is_named_with_its_path()
    {
        var read = Change.ReadArtifact(Path.Combine(Path.GetTempPath(), "ai-sdlc-tests", Guid.NewGuid().ToString("N"), "gone.md"));

        read.Text.ShouldBeNull();
        read.Problem.ShouldNotBeNull();
        read.Problem.ShouldContain("not there to read anymore");
        read.Problem.ShouldContain("gone.md");
    }

    [Fact]
    public void An_oversize_artifact_is_refused_before_reading()
    {
        var root = Root();
        var path = Write(ChangeDir(root, "poc-99"), "big.md", new string('x', 600 * 1024));

        var read = Change.ReadArtifact(path);

        read.Text.ShouldBeNull();
        read.Problem.ShouldNotBeNull();
        read.Problem.ShouldContain("over the");
    }

    // ------------------------------------------ artifact reads, confined (F1)

    [Fact]
    public void A_listed_artifact_reads_through_the_confined_read()
    {
        var root = Root();
        Write(ChangeDir(root, "poc-99"), "proposal.md", "# the proposal\n");

        var read = Change.ReadListedArtifact(root, Path.Join("openspec", "changes", "poc-99", "proposal.md"));

        // The positive anchor: confinement refuses nothing it used to serve.
        read.Path.ShouldNotBeNull();
        read.Text.ShouldNotBeNull();
        read.Text!.ShouldContain("# the proposal");
        read.Problem.ShouldBeNull();
    }

    [Theory]
    [InlineData("../../../../../etc/passwd")]
    [InlineData("../../../../etc/passwd")]
    [InlineData("../../..")]
    public void A_climbing_path_is_refused(string file)
    {
        var root = Root();
        // The escape target exists, outside the worktree: the point is it is never read.
        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(root, "..", "outside.md"))!);
        File.WriteAllText(Path.Combine(root, "..", "outside.md"), "secret\n");

        var read = Change.ReadListedArtifact(root, file);

        read.Path.ShouldBeNull();
        read.Text.ShouldBeNull();
        read.Problem.ShouldNotBeNull();
        read.Problem.ShouldNotContain("secret");
    }

    [Theory]
    [InlineData("/etc/passwd")]
    public void An_absolute_path_is_refused(string file)
    {
        var root = Root();

        var read = Change.ReadListedArtifact(root, file);

        read.Path.ShouldBeNull();
        read.Text.ShouldBeNull();
        read.Problem.ShouldNotBeNull();
    }

    [Fact]
    public void A_relative_path_outside_the_listed_change_roots_is_refused()
    {
        var root = Root();
        Write(ChangeDir(root, "poc-99"), "proposal.md", "the change\n");

        // Valid markdown, but under no change root the discovery listed: refused by name —
        // "not what the listing returned" is a different answer from "there is no change".
        Write(root, "loose.md", "loose\n");

        var read = Change.ReadListedArtifact(root, "loose.md");

        read.Path.ShouldBeNull();
        read.Text.ShouldBeNull();
        read.Problem.ShouldNotBeNull();
        read.Problem!.ShouldContain("listing returned");
    }

    [Fact]
    public void A_worktree_with_no_changes_serves_its_absence_through_the_confined_read()
    {
        var root = Root();

        var read = Change.ReadListedArtifact(root, Path.Join("openspec", "changes", "any", "proposal.md"));

        read.Path.ShouldBeNull();
        read.Problem.ShouldNotBeNull();
        read.Problem!.ShouldContain("no change declared");
    }

    [Fact]
    public void An_escaped_path_over_a_real_change_still_refuses()
    {
        // Mutation-proof companion: the climb is refused even when a real change exists and the
        // resolved escape lands inside the worktree's parent — not vacuous, not readable.
        var root = Root();
        Write(ChangeDir(root, "poc-99"), "proposal.md", "the change\n");
        var outside = Path.Combine(root, "..", "escaped.md");
        File.WriteAllText(outside, "secret\n");

        var read = Change.ReadListedArtifact(root, "../escaped.md");

        read.Path.ShouldBeNull();
        read.Problem.ShouldNotBeNull();
        read.Problem.ShouldNotContain("secret");
    }

    // ------------------------------------- mutation proof: the absence is real

    [Fact]
    public void Mutation_proof_declared_change_removes_the_absence()
    {
        // If Discover could never find a change, the absence test above would pass vacuously.
        // The same walk over a root WITH a declared change must return it — proof the walk reads
        // the disk rather than always answering empty.
        var root = Root();
        Write(ChangeDir(root, "poc-99-proof"), "proposal.md", "the change\n");

        var declared = Change.Discover(root);
        declared.Changes.Select(c => c.Name).ShouldBe(["poc-99-proof"]);

        var empty = Change.Discover(Root());
        empty.Changes.ShouldBeEmpty();
        empty.Absence.ShouldContain("no change declared");
    }
}
