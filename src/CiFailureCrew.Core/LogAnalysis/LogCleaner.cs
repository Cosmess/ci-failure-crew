using System.Text.RegularExpressions;

namespace CiFailureCrew.Core.LogAnalysis;

/// <summary>Turns a raw GitHub Actions job log into plain text lines.</summary>
public static partial class LogCleaner
{
    /// <summary>Removes the timestamp GitHub adds to every line, ANSI color codes and the byte order mark.</summary>
    public static IReadOnlyList<string> Clean(string rawLog)
    {
        var lines = rawLog.TrimStart('﻿').ReplaceLineEndings("\n").Split('\n');
        return lines
            .Select(line => Ansi().Replace(Timestamp().Replace(line.TrimStart('﻿'), ""), ""))
            .ToList();
    }

    /// <summary>
    /// The lines of the step that failed: from the last <c>##[group]Run</c> header before the first
    /// <c>##[error]</c> line to the end of that step. Returns every line when there is no error marker.
    /// </summary>
    public static IReadOnlyList<string> FailedStep(IReadOnlyList<string> lines)
    {
        var error = -1;
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].StartsWith("##[error]", StringComparison.Ordinal))
            {
                error = i;
                break;
            }
        }

        if (error < 0)
        {
            return lines;
        }

        var start = 0;
        for (var i = error; i >= 0; i--)
        {
            if (IsStepHeader(lines[i]))
            {
                start = i;
                break;
            }
        }

        var end = lines.Count;
        for (var i = error + 1; i < lines.Count; i++)
        {
            if (IsStepHeader(lines[i]) || lines[i].StartsWith("Post job cleanup", StringComparison.Ordinal))
            {
                end = i;
                break;
            }
        }

        return lines.Skip(start).Take(end - start).ToList();
    }

    private static bool IsStepHeader(string line) => line.StartsWith("##[group]Run ", StringComparison.Ordinal);

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?Z ")]
    private static partial Regex Timestamp();

    [GeneratedRegex(@"\x1B\[[0-9;]*[A-Za-z]")]
    private static partial Regex Ansi();
}
