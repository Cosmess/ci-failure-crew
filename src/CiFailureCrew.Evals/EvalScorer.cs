using System.Globalization;
using System.Text;
using CiFailureCrew.Core.Models;

namespace CiFailureCrew.Evals;

/// <summary>Score of one job. A check that does not apply to the job (nothing labeled) is null.</summary>
public sealed record JobScore(
    string Case,
    string Job,
    FailureCategory Expected,
    FailureCategory Actual,
    bool CategoryCorrect,
    bool? RootCauseFound,
    bool? OriginCorrect,
    bool? ExplanationMentionsKeyword,
    bool? KnownRuleUsed,
    string? Location)
{
    public bool Correct => CategoryCorrect && RootCauseFound != false && OriginCorrect != false
        && ExplanationMentionsKeyword != false && KnownRuleUsed != false;
}

public sealed record EvalSummary(IReadOnlyList<JobScore> Jobs, long InputTokens, long OutputTokens)
{
    public double CategoryAccuracy => Rate(Jobs.Select(j => (bool?)j.CategoryCorrect));
    public double RootCauseAccuracy => Rate(Jobs.Select(j => j.RootCauseFound));
    public double OriginAccuracy => Rate(Jobs.Select(j => j.OriginCorrect));
    public double KeywordRate => Rate(Jobs.Select(j => j.ExplanationMentionsKeyword));
    public double FullyCorrect => Rate(Jobs.Select(j => (bool?)j.Correct));

    public static (int Hits, int Total) Count(IEnumerable<bool?> checks)
    {
        var applicable = checks.OfType<bool>().ToList();
        return (applicable.Count(c => c), applicable.Count);
    }

    private static double Rate(IEnumerable<bool?> checks)
    {
        var (hits, total) = Count(checks);
        return total == 0 ? 1 : (double)hits / total;
    }
}

/// <summary>Compares an explanation with the labels of a case.</summary>
public static class EvalScorer
{
    public static IReadOnlyList<JobScore> Score(EvalCase evalCase, FailureReport report) =>
        evalCase.Expected.Jobs.Select(pair =>
        {
            var (name, expected) = pair;
            var actual = report.Jobs.FirstOrDefault(j => j.Job == name);
            if (actual is null)
            {
                return new JobScore(evalCase.Name, name, expected.Category, FailureCategory.Unknown, false, false, false, false, false, null);
            }

            var categories = new[] { expected.Category }.Concat(expected.AcceptCategories ?? []);
            return new JobScore(
                evalCase.Name,
                name,
                expected.Category,
                actual.Category,
                categories.Contains(actual.Category),
                expected.RootCause is { Count: > 0 } locations ? locations.Any(l => Matches(l, actual)) : null,
                expected.RelatedToPullRequest is { } related ? actual.RelatedToPullRequest == related : null,
                expected.Keywords is { Count: > 0 } keywords ? MentionsAny($"{actual.Explanation} {actual.Fix}", keywords) : null,
                expected.KnownRule is { } rule ? actual.Source == $"rule:{rule}" : null,
                actual.File is null ? null : actual.Line is { } line ? $"{actual.File}:{line}" : actual.File);
        }).ToList();

    public static bool Matches(ExpectedLocation expected, JobDiagnosis actual) =>
        actual.File == expected.File && actual.Line is { } line && Math.Abs(line - expected.Line) <= expected.Tolerance;

    public static bool MentionsAny(string text, IEnumerable<string> keywords)
    {
        var normalized = Normalize(text);
        return keywords.Any(k => normalized.Contains(Normalize(k), StringComparison.Ordinal));
    }

    /// <summary>Lower case without accents, so "instável" matches the keyword "instavel".</summary>
    public static string Normalize(string text)
    {
        var decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                result.Append(c);
            }
        }

        return result.ToString();
    }
}
