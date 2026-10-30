using System.Globalization;
using System.Text;

namespace CiFailureCrew.Evals;

public static class EvalReportRenderer
{
    public static string Render(EvalSummary summary, string model, string language)
    {
        var md = new StringBuilder();
        md.AppendLine("# CI Failure Crew evaluation").AppendLine();
        md.AppendLine($"Model: `{model}` · language: `{language}` · {summary.Jobs.Select(j => j.Case).Distinct().Count()} cases, {summary.Jobs.Count} jobs").AppendLine();

        md.AppendLine("| Metric | Result |");
        md.AppendLine("|---|---|");
        md.AppendLine($"| Category | {Metric(summary.Jobs.Select(j => (bool?)j.CategoryCorrect))} |");
        md.AppendLine($"| Root cause located | {Metric(summary.Jobs.Select(j => j.RootCauseFound))} |");
        md.AppendLine($"| Origin (caused by the PR or not) | {Metric(summary.Jobs.Select(j => j.OriginCorrect))} |");
        md.AppendLine($"| Explanation mentions the cause | {Metric(summary.Jobs.Select(j => j.ExplanationMentionsKeyword))} |");
        md.AppendLine($"| Known failures explained without AI | {Metric(summary.Jobs.Select(j => j.KnownRuleUsed))} |");
        md.AppendLine($"| **Fully correct jobs** | **{Metric(summary.Jobs.Select(j => (bool?)j.Correct))}** |");
        md.AppendLine();
        md.AppendLine($"Tokens: {summary.InputTokens.ToString("N0", CultureInfo.InvariantCulture)} input / {summary.OutputTokens.ToString("N0", CultureInfo.InvariantCulture)} output").AppendLine();

        md.AppendLine("| Case | Job | Expected | Actual | Location | Result |");
        md.AppendLine("|---|---|---|---|---|---|");
        foreach (var job in summary.Jobs)
        {
            var problems = new List<string>();
            if (!job.CategoryCorrect) problems.Add("category");
            if (job.RootCauseFound == false) problems.Add("root cause");
            if (job.OriginCorrect == false) problems.Add("origin");
            if (job.ExplanationMentionsKeyword == false) problems.Add("explanation");
            if (job.KnownRuleUsed == false) problems.Add("known rule");

            md.AppendLine($"| {job.Case} | {job.Job} | {job.Expected} | {job.Actual} | {(job.Location is null ? "" : $"`{job.Location}`")} | "
                + (problems.Count == 0 ? "✅" : $"❌ {string.Join(", ", problems)}") + " |");
        }

        return md.ToString();
    }

    public static string Percent(double value) => $"{(value * 100).ToString("0", CultureInfo.InvariantCulture)}%";

    private static string Metric(IEnumerable<bool?> checks)
    {
        var (hits, total) = EvalSummary.Count(checks);
        return total == 0 ? "n/a" : $"{Percent((double)hits / total)} ({hits} / {total})";
    }
}
