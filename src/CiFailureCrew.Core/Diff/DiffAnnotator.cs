using System.Text;
using System.Text.RegularExpressions;

namespace CiFailureCrew.Core.Diff;

/// <summary>
/// Prefixes every added and context line of a unified diff with its line number in the new version of
/// the file. Models miscount lines from hunk headers; with the numbers written down they cite them exactly.
/// </summary>
public static partial class DiffAnnotator
{
    private const int Width = 5;

    public static string WithLineNumbers(string diff)
    {
        var output = new StringBuilder();
        int newLine = 0, oldRemaining = 0, newRemaining = 0;

        foreach (var line in diff.ReplaceLineEndings("\n").Split('\n'))
        {
            if (line.StartsWith("diff --git ", StringComparison.Ordinal))
            {
                oldRemaining = newRemaining = 0;
            }

            if (oldRemaining > 0 || newRemaining > 0)
            {
                switch (line.FirstOrDefault())
                {
                    case '+':
                        output.Append(newLine++.ToString().PadLeft(Width)).Append(' ').AppendLine(line);
                        newRemaining--;
                        continue;
                    case '-':
                        output.Append(new string(' ', Width + 1)).AppendLine(line);
                        oldRemaining--;
                        continue;
                    case '\\':
                        output.Append(new string(' ', Width + 1)).AppendLine(line);
                        continue;
                    default:
                        output.Append(newLine++.ToString().PadLeft(Width)).Append(' ').AppendLine(line);
                        oldRemaining--;
                        newRemaining--;
                        continue;
                }
            }

            if (HunkHeader().Match(line) is { Success: true } hunk)
            {
                oldRemaining = hunk.Groups["oldCount"].Success ? int.Parse(hunk.Groups["oldCount"].Value) : 1;
                newLine = int.Parse(hunk.Groups["newStart"].Value);
                newRemaining = hunk.Groups["newCount"].Success ? int.Parse(hunk.Groups["newCount"].Value) : 1;
            }

            output.AppendLine(line);
        }

        return output.ToString().TrimEnd();
    }

    [GeneratedRegex(@"^@@ -\d+(?:,(?<oldCount>\d+))? \+(?<newStart>\d+)(?:,(?<newCount>\d+))? @@")]
    private static partial Regex HunkHeader();
}
