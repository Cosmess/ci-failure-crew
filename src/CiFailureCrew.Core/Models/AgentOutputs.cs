namespace CiFailureCrew.Core.Models;

// Structured outputs requested from each agent.

public sealed record Triage(FailureCategory Category, double Confidence, string Summary, List<string>? FailingItems, string? NextStep);

public sealed record Investigation(string RootCause, string? File, int? Line, bool RelatedToPullRequest);

public sealed record FlakinessAssessment(bool IsFlaky, string Reason, string? Recommendation);

/// <param name="SuggestedCode">Code that replaces exactly the line <paramref name="Line"/> of <paramref name="File"/>.</param>
public sealed record FixProposal(string Description, string? File, int? Line, string? SuggestedCode);

public sealed record Headline(string Title, string Summary);
