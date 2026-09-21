namespace AiSdlc;

// Which task a worktree's branch says it is about, as a proposal only. Mirrors harness
// taskFor/keyInBranch: the LAST digit run rides the leaf of the branch name; the lookup refuses.
public static class TaskResolution
{
    public static string? KeyInBranch(string branch) =>
        System.Text.RegularExpressions.Regex.Match(branch, @"(\d+)(?!.*\d)") is { Success: true } digits ? $"#{digits.Groups[1].Value}" : null;

    public static int? KeyNumber(string key) => key.Length > 1 && int.TryParse(key[1..], out var n) ? n : null;
}

/// The three answers a reader of a (possibly mirrored) body has: what it found, null for "there
/// is no such section", and unreadable for "the body we were handed is not the whole body".
public sealed class Read<T>
{
    public bool IsPresent { get; private init; }

    public bool IsUnreadable { get; private init; }

    public T? Value { get; private init; }

    public static Read<T> Of(T value) => new() { IsPresent = true, Value = value };

    public static Read<T> Absent { get; } = new();

    public static Read<T> Unreadable { get; } = new() { IsUnreadable = true };
}
