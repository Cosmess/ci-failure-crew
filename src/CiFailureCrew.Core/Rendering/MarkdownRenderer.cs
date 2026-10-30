using System.Globalization;
using System.Text;
using CiFailureCrew.Core.Localization;
using CiFailureCrew.Core.Models;
using static CiFailureCrew.Core.Rendering.MarkdownText;

namespace CiFailureCrew.Core.Rendering;

/// <summary>Renders the explanation as GitHub-flavored Markdown, in the language of the result.</summary>
public static class MarkdownRenderer
{
    public const string Heading = "## 🩺 CI Failure Crew";

    private const int MaxExcerptLines = 60;

    /// <param name="postedInline">Diagnoses whose suggested code was posted as a comment on the line.</param>
    public static string Render(CrewResult result, IReadOnlySet<JobDiagnosis>? postedInline = null)
    {
        var language = CrewLanguage.Parse(result.Language);
        var s = language.Strings;
        var report = result.Report;
        var md = new StringBuilder();

        md.AppendLine(Heading).AppendLine();
        md.AppendLine($"**{Safe(report.Title)}**").AppendLine();
        if (report.Summary.Length > 0)
        {
            md.AppendLine(Safe(report.Summary)).AppendLine();
        }

        foreach (var job in report.Jobs)
        {
            AppendJob(md, job, postedInline?.Contains(job) == true, language);
        }

        if (report.Skipped.Count > 0)
        {
            md.AppendLine($"_{string.Format(s.SkippedJobs, string.Join(", ", report.Skipped.Select(j => $"`{j}`")))}_").AppendLine();
        }

        if (postedInline is { Count: > 0 })
        {
            md.AppendLine(s.InlineLegend).AppendLine();
        }

        if (result.Usage.InputTokens > 0)
        {
            md.AppendLine($"<sub>{UsageLine(result.Usage, language.Code)}</sub>");
        }

        return md.ToString().TrimEnd() + "\n";
    }

    /// <summary>A review comment with a GitHub suggestion that applies the fix to the line.</summary>
    public static string RenderSuggestion(JobDiagnosis job, string language = "en")
    {
        var s = CrewLanguage.Parse(language).Strings;
        var md = new StringBuilder();
        md.AppendLine($"**{s.Category(job.Category)}** · `{job.Job}`").AppendLine();
        md.AppendLine(Safe(job.Fix ?? job.Explanation)).AppendLine();
        md.AppendLine(Fence(job.SuggestedCode ?? "", "suggestion")).AppendLine();
        md.Append("<sub>CI Failure Crew</sub>");
        return md.ToString();
    }

    public static string UsageLine(UsageSummary usage, string language = "en")
    {
        var lang = CrewLanguage.Parse(language);
        var line = string.Format(lang.Culture, lang.Strings.TokenUsage, usage.InputTokens.ToString("N0", lang.Culture), usage.OutputTokens.ToString("N0", lang.Culture));
        return usage.EstimatedCostUsd is { } cost
            ? $"{line} · {string.Format(lang.Strings.EstimatedCost, cost.ToString("0.0000", CultureInfo.InvariantCulture))}"
            : line;
    }

    private static void AppendJob(StringBuilder md, JobDiagnosis job, bool postedInline, CrewLanguage language)
    {
        var s = language.Strings;
        var logs = job.Url is { Length: > 0 } url ? $" · [{s.Logs}]({url})" : "";
        md.AppendLine($"### {s.Category(job.Category)} · `{job.Job}`{logs}").AppendLine();
        md.AppendLine(Safe(job.Explanation)).AppendLine();

        var details = new List<string>();
        if (job.File is { Length: > 0 } file)
        {
            var location = job.Line is { } line ? $"{file}:{line}" : file;
            details.Add($"**{s.Location}:** `{location}`{(postedInline ? " 💬" : "")}");
        }

        if (job.FailingItems.Count > 0)
        {
            details.Add($"**{s.Failing}:** {string.Join(", ", job.FailingItems.Take(10).Select(i => $"`{i.Replace("`", "'")}`"))}");
        }

        if (job.RelatedToPullRequest is { } related)
        {
            details.Add($"**{s.Origin}:** {(related ? s.CausedByPullRequest : s.NotCausedByPullRequest)}");
        }

        foreach (var detail in details)
        {
            md.AppendLine($"- {detail}");
        }

        if (details.Count > 0)
        {
            md.AppendLine();
        }

        if (job.Fix is { Length: > 0 } fix)
        {
            md.AppendLine($"**{s.SuggestedFix}:** {Safe(fix)}").AppendLine();
            if (job.SuggestedCode is { Length: > 0 } code && !postedInline)
            {
                md.AppendLine(Fence(code)).AppendLine();
            }
        }

        if (job.Errors.Count > 0)
        {
            md.AppendLine($"> ⚠️ {Safe(string.Format(s.AgentsFailed, string.Join("; ", job.Errors)))}").AppendLine();
        }

        if (job.Excerpt.Length > 0)
        {
            var lines = job.Excerpt.Split('\n');
            var excerpt = string.Join('\n', lines.Length > MaxExcerptLines ? lines[^MaxExcerptLines..] : lines);
            md.AppendLine($"<details><summary>{s.LogExcerpt}</summary>").AppendLine();
            md.AppendLine(Fence(excerpt, "text")).AppendLine();
            md.AppendLine("</details>").AppendLine();
        }

        md.AppendLine(job.IsKnownFailure
            ? $"<sub>{string.Format(s.KnownPattern, job.Source["rule:".Length..])}</sub>"
            : $"<sub>{string.Format(s.Confidence, job.Confidence.ToString("P0", language.Culture))}</sub>");
        md.AppendLine();
    }
}
