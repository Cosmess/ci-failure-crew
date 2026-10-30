using System.Text;
using System.Text.RegularExpressions;

namespace CiFailureCrew.Core.LogAnalysis;

/// <summary>
/// Reduces a job log to the lines that explain the failure, so the agents read a few hundred
/// lines instead of tens of thousands: windows around error patterns plus the end of the step.
/// </summary>
public static partial class LogExcerpt
{
    private const int LinesBefore = 3;
    private const int LinesAfter = 15;
    private const int TailLines = 25;
    private const string Gap = "[...]";

    /// <param name="rawLog">The job log as downloaded from GitHub.</param>
    /// <param name="maxChars">Upper bound of the excerpt size; the first errors and the end of the step are kept.</param>
    public static string Extract(string rawLog, int maxChars = 20_000)
    {
        var step = LogCleaner.FailedStep(LogCleaner.Clean(rawLog));
        var ranges = new List<(int Start, int End)>();

        for (var i = 0; i < step.Count; i++)
        {
            if (IsInteresting(step[i]))
            {
                ranges.Add((Math.Max(0, i - LinesBefore), Math.Min(step.Count - 1, i + LinesAfter)));
            }
        }

        // The end of the step usually has the test summary and the exit code.
        ranges.Add((Math.Max(0, step.Count - TailLines), step.Count - 1));

        var merged = Merge(ranges);
        var text = Render(step, merged);
        if (text.Length <= maxChars)
        {
            return text;
        }

        // Too long: keep the first errors (usually the root cause) and the end of the step, drop the middle.
        var tail = string.Join("\n", step.Skip(Math.Max(0, step.Count - TailLines)));
        if (tail.Length > maxChars / 2)
        {
            tail = tail[^(maxChars / 2)..];
        }

        var budget = Math.Max(0, maxChars - tail.Length - Gap.Length - 2);
        var cut = text.LastIndexOf('\n', Math.Min(budget, text.Length - 1));
        var head = cut > 0 ? text[..cut] : text[..budget];
        return $"{head}\n{Gap}\n{tail}";
    }

    public static bool IsInteresting(string line) => ErrorPattern().IsMatch(line);

    private static List<(int Start, int End)> Merge(List<(int Start, int End)> ranges)
    {
        var merged = new List<(int Start, int End)>();
        foreach (var range in ranges.OrderBy(r => r.Start))
        {
            if (merged.Count > 0 && range.Start <= merged[^1].End + 1)
            {
                merged[^1] = (merged[^1].Start, Math.Max(merged[^1].End, range.End));
            }
            else
            {
                merged.Add(range);
            }
        }

        return merged;
    }

    private static string Render(IReadOnlyList<string> lines, IReadOnlyList<(int Start, int End)> ranges)
    {
        var text = new StringBuilder();
        foreach (var (start, end) in ranges)
        {
            if (text.Length > 0 || start > 0)
            {
                text.AppendLine(Gap);
            }

            for (var i = start; i <= end; i++)
            {
                text.AppendLine(lines[i]);
            }
        }

        return text.ToString().TrimEnd();
    }

    // MSBuild/NuGet/C# diagnostics, dotnet test, xUnit/NUnit/MSTest, exceptions, Node, Python, Go, GitHub annotations.
    [GeneratedRegex(
        @"##\[error\]|\berror [A-Z]+\d+\s*:|^\s*Failed [\w.`<>,\[\]]+ \[|Error Message:|Assert\.\w+\(\) Failure|Expected:|Exception:|"
        + @"^\s*at .+ in .+:line \d+|npm ERR!|^\s*FAIL\s|Traceback \(most recent call last\)|^E\s{3}|^panic:|^fatal:|"
        + @"\b(timed out|time limit|exceeded the maximum execution time)\b|No space left on device|Unhandled exception",
        RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex ErrorPattern();
}
