namespace CiFailureCrew.Core.Models;

/// <summary>The crew's conclusion about one failed job.</summary>
/// <param name="Source">"agents", or "rule:&lt;id&gt;" when a known failure pattern explained it without a model.</param>
/// <param name="Excerpt">The redacted log lines the diagnosis was based on.</param>
/// <param name="Errors">Agents that failed while analyzing this job.</param>
public sealed record JobDiagnosis(
    string Job,
    string? Url,
    FailureCategory Category,
    double Confidence,
    string Explanation,
    IReadOnlyList<string> FailingItems,
    string? File,
    int? Line,
    bool? RelatedToPullRequest,
    string? Fix,
    string? SuggestedCode,
    string Source,
    string Excerpt,
    IReadOnlyList<string> Errors)
{
    public bool IsKnownFailure => Source.StartsWith("rule:", StringComparison.Ordinal);
}

/// <param name="Title">One sentence: what failed and why.</param>
/// <param name="Skipped">Failed jobs not analyzed because of the maxJobs limit or ignoreJobs.</param>
public sealed record FailureReport(string Title, string Summary, IReadOnlyList<JobDiagnosis> Jobs, IReadOnlyList<string> Skipped);

public sealed record UsageSummary(long InputTokens, long OutputTokens, decimal? EstimatedCostUsd)
{
    public static UsageSummary Empty { get; } = new(0, 0, null);
}

public sealed record CrewResult(FailureReport Report, UsageSummary Usage, string Language = "en");
