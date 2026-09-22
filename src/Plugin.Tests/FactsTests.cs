using AiSdlc;
using Shouldly;

namespace AiSdlcPlugin.Tests;

/// Header facts over both check subjects. Absence assertions carry a positive anchor in the
/// same fixture set (subject B), so an absence that silently became "always absent" fails.
public class FactsTests
{
    private const string Head = "6d63601bb5c2f5a2a3a4f0f11a589d12984cf776";

    private static Facts.CheckOutcome Outcome(int? exit, int? since = 0) => new(exit, since);

    private static Facts.Input Input(
        int? ahead = null, int? behind = null, IReadOnlyList<NumstatRow>? diff = null,
        bool diffPending = false, IReadOnlyList<string>? conflicts = null, bool mergeUnknown = true,
        int uncommitted = 0, string? head = null, IReadOnlyList<string>? gates = null,
        IReadOnlyList<(string Name, Facts.CheckOutcome Outcome)>? outcomes = null, Read<Seam.Row[]>? seam = null,
        Facts.PullRequestAnswer? pr = null, string? issueKey = null, string? issueTitle = null) => new(
        ahead, behind, diff ?? [], diffPending, conflicts ?? [], mergeUnknown, uncommitted, head,
        gates ?? [], outcomes ?? [], seam ?? Read<Seam.Row[]>.Absent, pr ?? Subjects.NoPullRequest,
        PrPending: false, issueKey, issueTitle, null);

    private static IReadOnlyList<Facts.Fact> SubjectA(Facts.PullRequestAnswer? pr = null, Read<Seam.Row[]>? seam = null) =>
        Facts.Header(Input(pr: pr, seam: seam));

    private static IReadOnlyList<Facts.Fact> SubjectB()
    {
        var declared = Seam.ParseSeam(Subjects.EpicSeamBody).Value!;
        return Facts.Header(Input(
            ahead: 2, behind: 0, diff: Subjects.SubjectBDiff, mergeUnknown: false,
            uncommitted: 2, head: Head, gates: ["Build", "Tests"], outcomes: [("Build", Outcome(0)), ("Tests", Outcome(0))],
            seam: Read<Seam.Row[]>.Of([.. Seam.Compare(declared, Subjects.SubjectBDiff.Select(r => r.Path).ToList())]),
            pr: new Facts.PullRequestAnswer(false, null, null, 12, "Open", false, "the first subject's own PR"),
            issueKey: "#1", issueTitle: "observar un worktree"));
    }

    // --------------------------------------------------- the eight absences, named

    [Fact]
    public void SubjectA_names_every_absence_with_its_remedy()
    {
        var texts = SubjectA().Select(f => f.Text).ToList();

        texts.ShouldContain("no issue resolved — name the branch after it (change/123) or run a task here");
        texts.ShouldContain("no pull request");
        texts.ShouldContain("no trunk to compare against — fetch the default branch");
        texts.ShouldContain("merge not asked — no trunk to merge into");
        texts.ShouldContain("no checks declared — declare one in .harness/commands.json with \"gate\": true");
        texts.ShouldContain("seam not compared — no task or no declaration");
        texts.ShouldContain("tree clean");
    }

    [Fact]
    public void SubjectA_changed_is_a_count_not_a_silence()
    {
        SubjectA().Single(f => f.Key == "changed").Text.ShouldBe("0 files +0 -0");
    }

    [Fact]
    public void No_PR_is_not_unreachable()
    {
        // #235: "no pull request" and "could not ask" are different sentences.
        var none = SubjectA().Single(f => f.Key == "pr").Text;
        var unreachable = SubjectA(pr: Subjects.UnreachableNotSignedIn).Single(f => f.Key == "pr").Text;

        none.ShouldBe("no pull request");
        unreachable.ShouldBe("pull request could not be asked");
    }

    [Fact]
    public void Unreachable_PR_carries_the_remedy()
    {
        var fact = SubjectA(pr: Subjects.UnreachableNotSignedIn).Single(f => f.Key == "pr");

        fact.Title.ShouldNotBeNull();
        fact.Title.ShouldContain("gh auth login");
    }

    // --------------------------------------------------- subject B: the happy path is exercised

    [Fact]
    public void SubjectB_states_the_real_changes()
    {
        var facts = SubjectB().ToDictionary(f => f.Key);

        facts["issue"].Text.ShouldContain("#1");
        facts["issue"].Text.ShouldContain("observar un worktree");
        facts["pr"].Text.ShouldBe("pull request #12");
        facts["pr-state"].Text.ShouldBe("open");
        facts["changed"].Text.ShouldBe("4 files +254 -11");
        facts["base"].Text.ShouldBe("2 ahead, 0 behind");
        facts["merge"].Text.ShouldBe("no conflicts");
        facts["checks"].Text.ShouldBe($"2 checks passed on 6d63601");
        facts["tree"].Text.ShouldBe("2 uncommitted");
        facts["tree"].Tone.ShouldBe(Facts.Tone.Warn);
    }

    [Fact]
    public void SubjectB_seam_names_the_outside_file()
    {
        var fact = SubjectB().Single(f => f.Key == "seam");

        // AGENTS.md is touched by subject B but not declared by the epic's seam: 1 outside.
        fact.Text.ShouldBe("1 outside the seam");
        fact.Title.ShouldBe("AGENTS.md");
        fact.Tone.ShouldBe(Facts.Tone.Warn);
    }

    [Fact]
    public void SubjectB_positive_anchor_the_declared_are_all_touched()
    {
        // The anchor: drop AGENTS.md from the diff and the seam reads all-inside — the same
        // fixture set proves the outside count was counting something real.
        var insideDiff = Subjects.SubjectBDiff.Where(r => r.Path != "AGENTS.md").ToList();
        var declared = Seam.ParseSeam(Subjects.EpicSeamBody).Value!;
        var facts = Facts.Header(Input(
            ahead: 2, behind: 0, diff: insideDiff, mergeUnknown: false,
            gates: ["Build"], outcomes: [("Build", Outcome(0))],
            seam: Read<Seam.Row[]>.Of([.. Seam.Compare(declared, insideDiff.Select(r => r.Path).ToList())]),
            pr: new Facts.PullRequestAnswer(false, null, null, 12, "Open", false, null), issueKey: "#1", issueTitle: "t"));

        facts.Single(f => f.Key == "seam").Text.ShouldBe("seam all inside");
    }

    // --------------------------------------------------- checks: the five readings

    [Fact]
    public void Checks_undeclared_vs_not_run()
    {
        var a = SubjectA().Single(f => f.Key == "checks").Text;
        var declared = Facts.Header(Input(gates: ["Build"])).Single(f => f.Key == "checks").Text;

        a.ShouldContain("no checks declared");
        declared.ShouldBe("checks not run here");
    }

    [Fact]
    public void Checks_failed_outranks_stale()
    {
        var fact = Facts.Header(Input(head: Head, gates: ["A", "B"], outcomes: [("A", Outcome(1)), ("B", Outcome(0))]))
            .Single(f => f.Key == "checks");

        fact.Text.ShouldBe("1 checks failing");
        fact.Tone.ShouldBe(Facts.Tone.Bad);
        fact.Title.ShouldBe("A");
    }

    [Fact]
    public void Checks_stale_when_moved()
    {
        Facts.Header(Input(head: Head, gates: ["A"], outcomes: [("A", Outcome(0, since: 2))]))
            .Single(f => f.Key == "checks").Text.ShouldBe("checks ran on an older commit");
    }

    [Fact]
    public void Checks_stale_when_no_longer_comparable()
    {
        Facts.Header(Input(head: Head, gates: ["A"], outcomes: [("A", Outcome(0, since: null))]))
            .Single(f => f.Key == "checks").Text.ShouldBe("checks ran on an older commit");
    }

    // --------------------------------------------------- seam over the header

    [Fact]
    public void Unreadable_seam_is_not_absent()
    {
        var cut = SubjectA(seam: Read<Seam.Row[]>.Unreadable).Single(f => f.Key == "seam").Text;
        var absent = SubjectA().Single(f => f.Key == "seam").Text;

        cut.ShouldBe("seam not compared — the issue body was cut");
        absent.ShouldBe("seam not compared — no task or no declaration");
    }

    [Fact]
    public void Draft_PR_is_its_own_state_fact()
    {
        var facts = Facts.Header(Input(pr: new Facts.PullRequestAnswer(false, null, null, 12, "Open", true, "draft PR")));

        facts.Single(f => f.Key == "pr").Text.ShouldBe("pull request #12");
        facts.Single(f => f.Key == "pr-state").Text.ShouldBe("draft");
    }

    [Fact]
    public void Conflicts_carry_the_paths()
    {
        var fact = Facts.Header(Input(conflicts: ["src/Plugin/Git.cs", "src/web/src/App.tsx"], mergeUnknown: false, ahead: 1, behind: 0, head: Head))
            .Single(f => f.Key == "merge");

        fact.Text.ShouldBe("2 conflicting");
        fact.Tone.ShouldBe(Facts.Tone.Bad);
        fact.Title.ShouldBe("src/Plugin/Git.cs, src/web/src/App.tsx");
    }

    // --------------------------------------------------- Header end-to-end over fakes

    [Fact]
    public async Task Header_over_a_rejected_proposal_refuses_the_issue()
    {
        // A branch proposing a key the repository does not hold: the lookup refuses, and the
        // issue fact says "no issue resolved", not issue 404.
        var git = new Git((cwd, args) => args[0] switch
        {
            "rev-parse" when args[1] == "--abbrev-ref" => (0, "release/24\n", ""),
            "rev-parse" => (0, $"{Head}\n", ""),
            "for-each-ref" => (0, "origin/main\n", ""),
            "rev-list" => (0, "1\t2\n", ""),
            "merge-base" => (0, "abc\n", ""),
            "diff" => (0, "1\t2\tsrc/a.ts\0", ""),
            "ls-files" => (0, "", ""),
            "status" => (0, "?? src/a.ts\n", ""),
            "remote" => (0, "git@github.com:asantamariaplainconcepts/ai-sdlc-plugin.git\n", ""),
            _ => (0, "", ""),
        });
        var github = new GitHub(
            tokenSource: () => "token",
            pullRequestSource: (b, r) => Task.FromResult(Subjects.NoPullRequest),
            issueSource: (n, r) => throw new InvalidOperationException("issue 24 not found"));

        var reading = await new Header(git, github).Read("/tmp/repo");

        reading.Facts.Single(f => f.Key == "issue").Text.ShouldContain("no issue resolved");
        reading.Facts.Single(f => f.Key == "issue").Title!.ShouldContain("issue 24 not found");
    }

    [Fact]
    public async Task Header_over_a_resolving_branch_compares_the_seam()
    {
        var git = new Git((cwd, args) => args[0] switch
        {
            "rev-parse" when args[1] == "--abbrev-ref" => (0, "change/1\n", ""),
            "rev-parse" => (0, $"{Head}\n", ""),
            "for-each-ref" => (0, "origin/main\n", ""),
            "rev-list" => (0, "0\t1\n", ""),
            "merge-base" => (0, "basis\n", ""),
            "diff" => (0, "120\t4\tsrc/Plugin/Git.cs\0", ""),
            "ls-files" => (0, "AGENTS.md\0src/web/src/App.tsx\0", ""),
            "status" => (0, "", ""),
            "remote" => (0, "git@github.com:asantamariaplainconcepts/ai-sdlc-plugin.git\n", ""),
            _ => (0, "", ""),
        });
        var github = new GitHub(
            tokenSource: () => "token",
            pullRequestSource: (b, r) => Task.FromResult(Subjects.NoPullRequest),
            issueSource: (n, r) => Task.FromResult(new GitHub.IssueRead(1, "Open", "the epic", Subjects.EpicSeamBody, false, [])));

        var reading = await new Header(git, github).Read("/tmp/repo");
        var facts = reading.Facts.ToDictionary(f => f.Key);

        facts["issue"].Text.ShouldContain("issue #1");
        facts["seam"].Text.ShouldBe("1 outside the seam");
        facts["seam"].Title.ShouldBe("AGENTS.md");
    }

    [Fact]
    public async Task Header_seam_all_inside_when_no_undeclared_touch()
    {
        // The positive anchor of the above: with only declared paths touched, the seam reads
        // all-inside (harness sentence + count title), proving the outside count was counting.
        var git = new Git((cwd, args) => args[0] switch
        {
            "rev-parse" when args[1] == "--abbrev-ref" => (0, "change/1\n", ""),
            "rev-parse" => (0, $"{Head}\n", ""),
            "for-each-ref" => (0, "origin/main\n", ""),
            "rev-list" => (0, "0\t1\n", ""),
            "merge-base" => (0, "basis\n", ""),
            "diff" => (0, "120\t4\tsrc/Plugin/Git.cs\0", ""),
            "ls-files" => (0, "src/web/src/App.tsx\0", ""),
            "status" => (0, "", ""),
            "remote" => (0, "git@github.com:asantamariaplainconcepts/ai-sdlc-plugin.git\n", ""),
            _ => (0, "", ""),
        });
        var github = new GitHub(
            tokenSource: () => "token",
            pullRequestSource: (b, r) => Task.FromResult(Subjects.NoPullRequest),
            issueSource: (n, r) => Task.FromResult(new GitHub.IssueRead(1, "Open", "the epic", Subjects.EpicSeamBody, false, [])));

        var reading = await new Header(git, github).Read("/tmp/repo");
        var fact = reading.Facts.Single(f => f.Key == "seam");

        fact.Text.ShouldBe("seam all inside");
        fact.Title.ShouldBe("3 declared, all touched");
    }

    [Fact]
    public async Task Header_over_not_a_repository_says_so()
    {
        var git = new Git((cwd, args) => args[0] == "rev-parse" ? (128, "", "not a git repository") : (0, "", ""));

        var reading = await new Header(git, new GitHub(() => "t", (b, r) => Task.FromResult(Subjects.NoPullRequest), (n, r) => throw new InvalidOperationException())).Read("/tmp/plain");

        reading.NotARepository.ShouldNotBeNull();
        reading.Facts.ShouldBeEmpty();
    }
}
