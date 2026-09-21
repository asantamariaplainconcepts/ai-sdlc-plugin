using AiSdlc;

namespace AiSdlcPlugin.Tests;

/// Fixtures for the two-subject check: a clean worktree with nothing resolved (subject A),
/// and a worktree with real changes on a branch whose digits resolve an issue (subject B).
/// The bodies are written the way this epic's own issues write their Seam sections.
public static class Subjects
{
    public const string EpicSeamBody = """
        ## Objetivo

        Probar la verificación fina.

        ## Seam

        - `src/Plugin/Git.cs` — porcelain reading
        - `src/web/src/App.tsx` — the screen
        - `Directory.Packages.props` — the pins

        ## Lo que no entra

        Nada más.
        """;

    public static Facts.PullRequestAnswer NoPullRequest => new(false, null, null, null, null, null, null);

    public static Facts.PullRequestAnswer UnreachableNotSignedIn => new(true, "NotSignedIn", "gh auth token returned nothing", null, null, null, null);

    public static IReadOnlyList<NumstatRow> SubjectBDiff =>
    [
        new NumstatRow("src/Plugin/Git.cs", 120, 4, false, null),
        new NumstatRow("src/web/src/App.tsx", 95, 6, false, null),
        new NumstatRow("Directory.Packages.props", 9, 1, false, null),
        new NumstatRow("AGENTS.md", 30, 0, false, null),
    ];
}
