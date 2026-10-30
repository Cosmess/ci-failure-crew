namespace CiFailureCrew.Core.Diff;

/// <summary>One file of a unified diff.</summary>
/// <param name="Path">Path of the file in the new version (or the old one, when deleted).</param>
/// <param name="Text">Raw diff text of this file, including headers.</param>
/// <param name="CommentableLines">New-file line numbers that appear in the diff (added or context lines).</param>
public sealed record DiffFile(string Path, string Text, IReadOnlySet<int> CommentableLines);
