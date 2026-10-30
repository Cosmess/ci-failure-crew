using System.Text.RegularExpressions;
using CiFailureCrew.Core.Diff;
using CiFailureCrew.Core.LogAnalysis;
using CiFailureCrew.Core.Models;
using CiFailureCrew.Evals;

namespace CiFailureCrew.Tests;

public class EvalTests
{
    private static readonly string CasesDirectory = Path.Combine(AppContext.BaseDirectory, "evals", "cases");

    public static TheoryData<string> CaseNames() => [.. Directory.EnumerateDirectories(CasesDirectory).Select(Path.GetFileName).Order()!];

    [Fact]
    public void The_dataset_has_cases()
    {
        Assert.True(EvalCase.LoadAll(CasesDirectory).Count >= 10);
    }

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void Every_case_is_consistent(string name)
    {
        var evalCase = EvalCase.Load(Path.Combine(CasesDirectory, name));

        Assert.False(string.IsNullOrWhiteSpace(evalCase.Expected.Description));
        Assert.Equal(evalCase.Expected.Jobs.Keys.Order(), evalCase.Context.Jobs.Select(j => j.Name).Order());

        foreach (var job in evalCase.Context.Jobs)
        {
            var expected = evalCase.Expected.Jobs[job.Name];
            var failedStep = string.Join('\n', LogCleaner.FailedStep(LogCleaner.Clean(job.Log)));

            // Rule cases must be recognized without a model, and the other cases must reach the agents.
            Assert.Equal(expected.KnownRule, KnownFailures.Match(failedStep)?.Id);

            foreach (var location in expected.RootCause ?? [])
            {
                var inDiff = evalCase.Context.Diff is { } diff
                    && DiffParser.Parse(diff).Any(f => f.Path == location.File && f.CommentableLines.Contains(location.Line));
                var inLog = job.Log.Contains(Path.GetFileName(location.File));
                Assert.True(inDiff || inLog, $"{location.File}:{location.Line} is neither a line of the diff nor mentioned in the log.");
            }
        }
    }

    // Wrong counts in a hunk header shift every line number the agents are told, so the labels would be wrong too.
    [Fact]
    public void Hunk_headers_of_the_case_diffs_match_their_content()
    {
        foreach (var evalCase in EvalCase.LoadAll(CasesDirectory).Where(c => c.Context.Diff is not null))
        {
            var lines = evalCase.Context.Diff!.TrimEnd('\n').Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                if (Regex.Match(lines[i], @"^@@ -\d+(?:,(\d+))? \+\d+(?:,(\d+))? @@") is not { Success: true } header)
                {
                    continue;
                }

                int removed = 0, added = 0;
                for (var j = i + 1; j < lines.Length && !lines[j].StartsWith("@@", StringComparison.Ordinal) && !lines[j].StartsWith("diff --git", StringComparison.Ordinal); j++)
                {
                    removed += lines[j].StartsWith('+') ? 0 : 1;
                    added += lines[j].StartsWith('-') ? 0 : 1;
                }

                var expectedOld = header.Groups[1].Success ? int.Parse(header.Groups[1].Value) : 1;
                var expectedNew = header.Groups[2].Success ? int.Parse(header.Groups[2].Value) : 1;
                Assert.True((removed, added) == (expectedOld, expectedNew),
                    $"{evalCase.Name}: '{lines[i]}' has {removed} old and {added} new lines.");
            }
        }
    }

    private static readonly EvalCase Case = new("demo", new FailureContext([]), new ExpectedExplanation("demo", new()
    {
        ["test"] = new(FailureCategory.TestFailure, [FailureCategory.Configuration], true,
            [new("src/A.cs", 10, 2), new("src/B.cs", 5, 0)], ["discount", "desconto"], null),
        ["e2e"] = new(FailureCategory.Timeout, null, false, null, null, "job-timeout")
    }));

    private static JobDiagnosis Diagnosis(string job, FailureCategory category, string? file = null, int? line = null,
        bool? related = null, string explanation = "", string source = "agents") =>
        new(job, null, category, 0.9, explanation, [], file, line, related, null, null, source, "", []);

    [Fact]
    public void A_job_is_correct_when_every_labeled_check_passes()
    {
        var report = new FailureReport("", "", [
            Diagnosis("test", FailureCategory.Configuration, "src/A.cs", 12, true, "O desconto é aplicado duas vezes."),
            Diagnosis("e2e", FailureCategory.Timeout, related: false, source: "rule:job-timeout")
        ], []);

        var scores = EvalScorer.Score(Case, report);

        Assert.All(scores, s => Assert.True(s.Correct));
        Assert.Equal("src/A.cs:12", scores[0].Location);
        Assert.Null(scores[1].RootCauseFound);
    }

    [Fact]
    public void Each_wrong_check_is_reported()
    {
        var report = new FailureReport("", "", [
            Diagnosis("test", FailureCategory.Compilation, "src/A.cs", 13, false, "Something else broke."),
            Diagnosis("e2e", FailureCategory.Timeout, related: false)
        ], []);

        var scores = EvalScorer.Score(Case, report);
        var summary = new EvalSummary(scores, 0, 0);

        Assert.False(scores[0].CategoryCorrect);
        Assert.False(scores[0].RootCauseFound);
        Assert.False(scores[0].OriginCorrect);
        Assert.False(scores[0].ExplanationMentionsKeyword);
        Assert.False(scores[1].KnownRuleUsed);
        Assert.Equal(0.5, summary.CategoryAccuracy);
        Assert.Equal(0, summary.FullyCorrect);
        Assert.Contains("❌ category, root cause, origin, explanation", EvalReportRenderer.Render(summary, "openai/gpt-5-mini", "en"));
    }

    [Fact]
    public void A_job_missing_from_the_explanation_fails_every_check()
    {
        var score = Assert.Single(EvalScorer.Score(Case, new FailureReport("", "", [Diagnosis("test", FailureCategory.TestFailure)], [])), s => s.Job == "e2e");

        Assert.False(score.Correct);
        Assert.Equal(FailureCategory.Unknown, score.Actual);
    }

    [Fact]
    public void Keywords_ignore_case_and_accents()
    {
        Assert.True(EvalScorer.MentionsAny("O teste é INSTÁVEL", ["instavel"]));
        Assert.False(EvalScorer.MentionsAny("stable", ["instavel"]));
    }
}
