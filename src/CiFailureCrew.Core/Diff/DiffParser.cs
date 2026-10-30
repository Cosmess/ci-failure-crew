using System.Text;
using System.Text.RegularExpressions;

namespace CiFailureCrew.Core.Diff;

/// <summary>Parses a unified diff (as produced by <c>git diff</c> or the GitHub API) into files.</summary>
public static partial class DiffParser
{
    public static IReadOnlyList<DiffFile> Parse(string diff)
    {
        var files = new List<DiffFile>();
        var lines = diff.ReplaceLineEndings("\n").Split('\n');

        var text = new StringBuilder();
        string? oldPath = null, newPath = null;
        var commentable = new HashSet<int>();
        int oldRemaining = 0, newRemaining = 0, newLine = 0;

        void Flush()
        {
            var path = newPath is null or "/dev/null" ? oldPath : newPath;
            if (path is not null && path != "/dev/null")
            {
                files.Add(new DiffFile(path, text.ToString(), commentable));
            }

            text.Clear();
            oldPath = newPath = null;
            commentable = [];
        }

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            // A new file header always ends the current hunk, even if its line counts were wrong.
            if (line.StartsWith("diff --git ", StringComparison.Ordinal))
            {
                oldRemaining = newRemaining = 0;
            }

            var inHunk = oldRemaining > 0 || newRemaining > 0;

            if (inHunk)
            {
                switch (line.FirstOrDefault())
                {
                    case '+':
                        commentable.Add(newLine++);
                        newRemaining--;
                        break;
                    case '-':
                        oldRemaining--;
                        break;
                    case '\\':
                        break;
                    default:
                        commentable.Add(newLine++);
                        oldRemaining--;
                        newRemaining--;
                        break;
                }

                text.AppendLine(line);
                continue;
            }

            var startsFile = line.StartsWith("diff --git ", StringComparison.Ordinal)
                || (line.StartsWith("--- ", StringComparison.Ordinal)
                    && i + 1 < lines.Length
                    && lines[i + 1].StartsWith("+++ ", StringComparison.Ordinal)
                    && oldPath is not null);

            if (startsFile && text.Length > 0)
            {
                Flush();
            }

            if (line.StartsWith("--- ", StringComparison.Ordinal))
            {
                oldPath = StripPrefix(line[4..]);
            }
            else if (line.StartsWith("+++ ", StringComparison.Ordinal))
            {
                newPath = StripPrefix(line[4..]);
            }
            else if (HunkHeader().Match(line) is { Success: true } hunk)
            {
                oldRemaining = hunk.Groups["oldCount"].Success ? int.Parse(hunk.Groups["oldCount"].Value) : 1;
                newLine = int.Parse(hunk.Groups["newStart"].Value);
                newRemaining = hunk.Groups["newCount"].Success ? int.Parse(hunk.Groups["newCount"].Value) : 1;
            }

            if (line.Length > 0 || text.Length > 0)
            {
                text.AppendLine(line);
            }
        }

        Flush();
        return files;
    }

    private static string StripPrefix(string path)
    {
        path = path.Split('\t')[0].Trim();
        return path.StartsWith("a/", StringComparison.Ordinal) || path.StartsWith("b/", StringComparison.Ordinal) ? path[2..] : path;
    }

    [GeneratedRegex(@"^@@ -\d+(?:,(?<oldCount>\d+))? \+(?<newStart>\d+)(?:,(?<newCount>\d+))? @@")]
    private static partial Regex HunkHeader();
}
