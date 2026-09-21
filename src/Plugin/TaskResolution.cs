namespace AiSdlc;

/// Which task a worktree's branch says it is about, as a proposal only.
/// Mirrors harness task.ts keyInBranch: the LAST digit run, because the ticket number
/// rides the leaf of a branch name; a year or team number sits in front of it.
public static class TaskResolution
{
    public static string? KeyInBranch(string branch)
    {
        var digits = System.Text.RegularExpressions.Regex.Match(branch, @"(\d+)(?!.*\d)");
        return digits.Success ? $"#{digits.Groups[1].Value}" : null;
    }

    /// The key trimmed of its '#' for the numeric lookup.
    public static int? KeyNumber(string key) =>
        key.Length > 1 && int.TryParse(key[1..], out var number) ? number : null;
}
