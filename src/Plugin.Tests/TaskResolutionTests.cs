using AiSdlc;
using Shouldly;

namespace AiSdlcPlugin.Tests;

/// Branch-proposed task keys: the LAST digit run, proposal-only semantics (harness taskFor).
public class TaskResolutionTests
{
    [Theory]
    [InlineData("change/275", "#275")]
    [InlineData("poc/poc-00-header", "#00")]
    [InlineData("feature/2026/issue-275", "#275")]
    [InlineData("release/24", "#24")]
    public void Last_digit_run_proposes(string branch, string expected)
    {
        TaskResolution.KeyInBranch(branch).ShouldBe(expected);
    }

    [Theory]
    [InlineData("main")]
    [InlineData("master")]
    [InlineData("fix/narrow-layout")]
    [InlineData("poc/epic-one")]
    public void No_digits_propose_nothing(string branch)
    {
        TaskResolution.KeyInBranch(branch).ShouldBeNull();
    }

    [Fact]
    public void Key_number_parses()
    {
        TaskResolution.KeyNumber("#275").ShouldBe(275);
        TaskResolution.KeyNumber("#").ShouldBeNull();
        TaskResolution.KeyNumber("no_hash").ShouldBeNull();
    }
}
