using System.Text.RegularExpressions;

namespace CiFailureCrew.Core.Rendering;

/// <summary>Helpers to put untrusted text (model output, logs) into GitHub Markdown safely.</summary>
public static partial class MarkdownText
{
    /// <summary>Breaks @mentions with a zero-width space so the comment never notifies GitHub users or teams.</summary>
    public static string Safe(string text) => Mention().Replace(text, "@\u200B$1");

    /// <summary>A fenced code block whose fence is longer than any backtick run inside the text.</summary>
    public static string Fence(string text, string language = "")
    {
        var longest = Backticks().Matches(text).Select(m => m.Length).DefaultIfEmpty(0).Max();
        var fence = new string('`', Math.Max(3, longest + 1));
        return $"{fence}{language}\n{text.TrimEnd()}\n{fence}";
    }

    [GeneratedRegex(@"(?<![\w`])@([A-Za-z0-9][A-Za-z0-9-]*)")]
    private static partial Regex Mention();

    [GeneratedRegex("`+")]
    private static partial Regex Backticks();
}
