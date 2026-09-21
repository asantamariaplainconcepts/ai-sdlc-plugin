using AiSdlc;
using Shouldly;

namespace AiSdlcPlugin.Tests;

/// Harness-parity tests for the seam reader: bullet-first-backtick, looksLikePath,
/// suffix/directory coverage, and the three readings of a body (#283 parity).
public class SeamTests
{
    private const string Declared = """
        ## Objetivo

        texto

        ## Seam

        - `shared/http/queries.ts` — why
        - `Modules/Workspace/Domain/CheckResult.cs` — a full path
        - `BuildingBlocks/World` — a namespace with a slash, not a file
        - `IWorkspaceProbe` — a type name in backticks, not a path
        - `src/generated/` — a directory

        ## El check

        nada
        """;

    [Fact]
    public void Bullet_first_backtick_only()
    {
        var read = Seam.ParseSeam(Declared);

        read.IsPresent.ShouldBeTrue();
        // The named files, and no second bare `queries.ts` from the prose lines.
        read.Value!.Select(e => e.Path).ShouldBe(
        [
            "shared/http/queries.ts",
            "Modules/Workspace/Domain/CheckResult.cs",
            "BuildingBlocks/World",
            "src/generated",
        ]);
    }

    [Fact]
    public void Directory_detection()
    {
        var read = Seam.ParseSeam(Declared);

        read.Value!.Single(e => e.Path == "src/generated").Directory.ShouldBeTrue();
        read.Value!.Single(e => e.Path == "BuildingBlocks/World").Directory.ShouldBeTrue();
        read.Value!.Single(e => e.Path == "shared/http/queries.ts").Directory.ShouldBeFalse();
    }

    [Fact]
    public void No_section_is_absent_not_empty()
    {
        var read = Seam.ParseSeam("## Objetivo\n\nnada aquí");

        read.IsPresent.ShouldBeFalse();
        read.IsUnreadable.ShouldBeFalse();
    }

    [Fact]
    public void Bold_section_title_counts()
    {
        var read = Seam.ParseSeam("**Seam:**\n- `a/b.ts` — x");

        read.IsPresent.ShouldBeTrue();
        read.Value!.Single().Path.ShouldBe("a/b.ts");
    }

    [Fact]
    public void Truncated_body_without_section_is_unreadable()
    {
        var read = Seam.ParseSeam("## Objetivo\n\nel cuerpo se corta aquí…", truncated: true);

        read.IsUnreadable.ShouldBeTrue();
    }

    [Fact]
    public void Truncated_body_with_section_ran_to_end_is_unreadable()
    {
        // The section exists but is the last thing in the cut text — same lie, smaller font.
        var read = Seam.ParseSeam("## Objetivo\n\n## Seam\n\n- `a/b.ts` — x", truncated: true);

        read.IsUnreadable.ShouldBeTrue();
    }

    [Fact]
    public void Truncated_body_with_closed_section_reads()
    {
        // A section that ended at a later title survived the cut whole.
        var read = Seam.ParseSeam("## SeAM\n\n- `a/b.ts` — x\n\n## El check\n\nnada", truncated: true);

        read.IsPresent.ShouldBeTrue();
        read.Value!.Single().Path.ShouldBe("a/b.ts");
    }

    [Fact]
    public void Compare_three_states()
    {
        var declared = new List<Seam.Entry>
        {
            new("shared/http/queries.ts", false),
            new("src/generated", true),
        };
        var touched = new List<string> { "src/frontend/shared/http/queries.ts", "other/file.ts" };

        var rows = Seam.Compare(declared, touched);

        // Suffix match: declared shorter than touched, boundary on '/'.
        rows.ShouldContain(r => r.Path == "shared/http/queries.ts" && r.State == Seam.State.DeclaredTouched);
        rows.ShouldContain(r => r.Path == "src/generated" && r.State == Seam.State.DeclaredUntouched);
        rows.ShouldContain(r => r.Path == "other/file.ts" && r.State == Seam.State.Undeclared);
    }

    [Fact]
    public void Directory_covers()
    {
        var declared = new List<Seam.Entry> { new("src/generated", true) };
        var rows = Seam.Compare(declared, ["src/generated/a.ts", "deep/nest/src/generated/b.ts", "src/generatedx/c.ts"]);

        // One row for the declared entry (covered twice over, still one row), one for the
        // touched path nothing declared — and none for the covered touched paths.
        rows.Count.ShouldBe(2);
        rows.Single(r => r.Path == "src/generated").State.ShouldBe(Seam.State.DeclaredTouched);
        rows.Single(r => r.Path == "src/generatedx/c.ts").State.ShouldBe(Seam.State.Undeclared);
    }
}
