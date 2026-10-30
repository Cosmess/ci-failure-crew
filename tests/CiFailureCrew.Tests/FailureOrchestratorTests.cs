using CiFailureCrew.Core.Configuration;
using CiFailureCrew.Core.Models;
using CiFailureCrew.Core.Orchestration;

namespace CiFailureCrew.Tests;

public class FailureOrchestratorTests
{
    private const string Diff = """
        diff --git a/src/Shop.Core/DiscountCalculator.cs b/src/Shop.Core/DiscountCalculator.cs
        --- a/src/Shop.Core/DiscountCalculator.cs
        +++ b/src/Shop.Core/DiscountCalculator.cs
        @@ -10,3 +10,3 @@ public class DiscountCalculator
             public decimal Apply(decimal total, Customer customer) =>
        -        customer.IsGold ? total * 0.9m : total;
        +        customer.IsGold ? total * 0.99m : total;
         }
        """;

    private const string CompilationTriage = """{"category":"Compilation","confidence":0.9,"summary":"The build failed with CS1061.","failingItems":["CS1061"],"nextStep":"Fix the call."}""";
    private const string TestTriage = """{"category":"TestFailure","confidence":0.8,"summary":"The discount test failed.","failingItems":["DiscountTests.Applies_ten_percent_for_gold_customers"],"nextStep":"Check the calculation."}""";
    private const string Investigation = """{"rootCause":"The test fails because line 12 now multiplies by 0.99 instead of 0.9.","file":"/home/runner/work/shop/shop/src/Shop.Core/DiscountCalculator.cs","line":12,"relatedToPullRequest":true}""";
    private const string Fix = """{"description":"Restore the 10% discount.","file":"src/Shop.Core/DiscountCalculator.cs","line":12,"suggestedCode":"        customer.IsGold ? total * 0.9m : total;"}""";
    private const string Headline = """{"title":"The discount test failed because the change in DiscountCalculator.cs applies 1% instead of 10%.","summary":"Fix the multiplier."}""";

    private static Func<string, string> Answers(
        string triage = TestTriage, string investigation = Investigation, string fix = Fix, string headline = Headline, string? flakiness = null) =>
        prompt => Agent(prompt) switch
        {
            "triage" => triage,
            "investigator" => investigation,
            "fixer" => fix,
            "flakiness" => flakiness ?? throw new InvalidOperationException("The flakiness agent should not be called."),
            "synthesizer" => headline,
            _ => throw new InvalidOperationException("Unknown agent.")
        };

    private static string Agent(string prompt) =>
        prompt.Contains("triaging a failed CI job") ? "triage"
        : prompt.Contains("finding the root cause") ? "investigator"
        : prompt.Contains("proposing the smallest fix") ? "fixer"
        : prompt.Contains("deciding whether a CI failure is flaky") ? "flakiness"
        : prompt.Contains("writing the top of a comment") ? "synthesizer"
        : "unknown";

    private static FailureContext Context(string? log = null, string? diff = Diff, JobHistory? history = null) =>
        new([new FailedJob("test", log ?? SampleLogs.DotnetTestFailure(), "https://github.com/acme/shop/actions/runs/1/job/2", history)], diff, "Adjust discounts", "CI");

    private static FailureOrchestrator Orchestrator(FakeChatClient client, CrewConfig? config = null) =>
        new(client, config, retryBackoff: _ => TimeSpan.Zero);

    private static IEnumerable<string> Calls(FakeChatClient client) => client.Prompts.Select(Agent);

    [Fact]
    public async Task A_code_failure_goes_through_triage_investigation_fix_and_headline()
    {
        var client = new FakeChatClient(Answers());

        var result = await Orchestrator(client).ExplainAsync(Context());

        var job = Assert.Single(result.Report.Jobs);
        Assert.Equal(FailureCategory.TestFailure, job.Category);
        Assert.Equal("src/Shop.Core/DiscountCalculator.cs", job.File);
        Assert.Equal(12, job.Line);
        Assert.True(job.RelatedToPullRequest);
        Assert.Equal("Restore the 10% discount.", job.Fix);
        Assert.Contains("0.9m", job.SuggestedCode);
        Assert.Equal(["DiscountTests.Applies_ten_percent_for_gold_customers"], job.FailingItems);
        Assert.Equal("agents", job.Source);
        Assert.StartsWith("The discount test failed because", result.Report.Title);
        Assert.Equal(["triage", "investigator", "fixer", "synthesizer"], Calls(client));
        Assert.Equal(4000, result.Usage.InputTokens);
    }

    [Fact]
    public async Task A_known_failure_is_explained_without_calling_any_model()
    {
        var client = new FakeChatClient(_ => throw new InvalidOperationException("No model call expected."));
        var log = SampleLogs.WithTimestamps(
            "##[group]Run dotnet test",
            "Starting test execution, please wait...",
            "##[error]The job running on runner GitHub Actions 3 has exceeded the maximum execution time of 360 minutes.");

        var result = await Orchestrator(client, new CrewConfig { Language = "pt-BR" }).ExplainAsync(Context(log));

        var job = Assert.Single(result.Report.Jobs);
        Assert.Equal(FailureCategory.Timeout, job.Category);
        Assert.Equal("rule:job-timeout", job.Source);
        Assert.False(job.RelatedToPullRequest);
        Assert.StartsWith("O job foi cancelado", job.Explanation);
        Assert.StartsWith("O job foi cancelado", result.Report.Title);
        Assert.Empty(client.Prompts);
        Assert.Equal(0, result.Usage.InputTokens);
    }

    [Fact]
    public async Task A_failure_that_also_happens_on_the_default_branch_is_checked_for_flakiness()
    {
        var client = new FakeChatClient(Answers(flakiness: """{"isFlaky":true,"reason":"The same test failed 3 times on main without changes.","recommendation":"Re-run and quarantine the test."}"""));

        var result = await Orchestrator(client).ExplainAsync(Context(history: new JobHistory(10, 3)));

        var job = Assert.Single(result.Report.Jobs);
        Assert.Equal(FailureCategory.Flaky, job.Category);
        Assert.False(job.RelatedToPullRequest);
        Assert.Equal("Re-run and quarantine the test.", job.Fix);
        Assert.Equal(["triage", "flakiness", "synthesizer"], Calls(client));
    }

    [Fact]
    public async Task A_flaky_triage_rejected_by_the_flakiness_check_is_investigated_as_a_test_failure()
    {
        var triage = TestTriage.Replace("\"TestFailure\"", "\"Flaky\"");
        var client = new FakeChatClient(Answers(triage: triage, flakiness: """{"isFlaky":false,"reason":"The failing code was changed by the pull request."}"""));

        var result = await Orchestrator(client).ExplainAsync(Context());

        var job = Assert.Single(result.Report.Jobs);
        Assert.Equal(FailureCategory.TestFailure, job.Category);
        Assert.Equal("src/Shop.Core/DiscountCalculator.cs", job.File);
        Assert.Equal(["triage", "flakiness", "investigator", "fixer", "synthesizer"], Calls(client));
    }

    [Fact]
    public async Task Without_a_diff_or_tools_there_is_nothing_to_investigate()
    {
        var client = new FakeChatClient(Answers(triage: CompilationTriage));

        var result = await Orchestrator(client).ExplainAsync(Context(SampleLogs.CompilationError(), diff: null));

        var job = Assert.Single(result.Report.Jobs);
        Assert.Equal(FailureCategory.Compilation, job.Category);
        Assert.Equal("Fix the call.", job.Fix);
        Assert.Null(job.File);
        Assert.Equal(["triage", "synthesizer"], Calls(client));
    }

    [Fact]
    public async Task An_infrastructure_failure_is_not_investigated_in_the_code()
    {
        var client = new FakeChatClient(Answers(triage: """{"category":"Infrastructure","confidence":0.7,"summary":"The Docker registry was unreachable.","nextStep":"Re-run the job."}"""));

        var result = await Orchestrator(client).ExplainAsync(Context());

        Assert.Equal(FailureCategory.Infrastructure, Assert.Single(result.Report.Jobs).Category);
        Assert.Equal(["triage", "synthesizer"], Calls(client));
    }

    [Fact]
    public async Task A_failing_triage_still_produces_a_diagnosis_with_the_log_excerpt()
    {
        var client = new FakeChatClient(prompt => Agent(prompt) == "triage" ? throw new HttpRequestException("model unavailable") : Headline);

        var result = await Orchestrator(client, new CrewConfig { MaxRetries = 1 }).ExplainAsync(Context());

        var job = Assert.Single(result.Report.Jobs);
        Assert.Equal(FailureCategory.Unknown, job.Category);
        Assert.Contains("could not analyze", job.Explanation);
        Assert.Contains("TriageAgent: model unavailable", Assert.Single(job.Errors));
        Assert.Contains("Expected: 90", job.Excerpt);
        Assert.Equal(2, Calls(client).Count(c => c == "triage"));
    }

    [Fact]
    public async Task A_failing_investigator_keeps_the_triage_diagnosis()
    {
        var client = new FakeChatClient(prompt => Agent(prompt) == "investigator" ? throw new TimeoutException("slow") : Answers()(prompt));

        var result = await Orchestrator(client, new CrewConfig { MaxRetries = 0 }).ExplainAsync(Context());

        var job = Assert.Single(result.Report.Jobs);
        Assert.Equal("The discount test failed.", job.Explanation);
        Assert.Equal("Check the calculation.", job.Fix);
        Assert.Null(job.File);
        Assert.Single(job.Errors);
        Assert.DoesNotContain("fixer", Calls(client));
    }

    [Fact]
    public async Task The_headline_falls_back_to_the_first_diagnosis_when_the_synthesizer_fails()
    {
        var client = new FakeChatClient(prompt => Agent(prompt) == "synthesizer" ? throw new HttpRequestException("down") : Answers()(prompt));

        var result = await Orchestrator(client, new CrewConfig { MaxRetries = 0 }).ExplainAsync(Context());

        Assert.Equal("The test fails because line 12 now multiplies by 0.99 instead of 0.9.", result.Report.Title);
    }

    [Fact]
    public async Task Suggested_code_for_another_line_than_the_root_cause_is_dropped()
    {
        var client = new FakeChatClient(Answers(fix: Fix.Replace("\"line\":12", "\"line\":40")));

        var result = await Orchestrator(client).ExplainAsync(Context());

        var job = Assert.Single(result.Report.Jobs);
        Assert.Equal("Restore the 10% discount.", job.Fix);
        Assert.Null(job.SuggestedCode);
    }

    [Fact]
    public async Task Ignored_jobs_and_jobs_over_the_limit_are_listed_but_not_analyzed()
    {
        var client = new FakeChatClient(Answers());
        var log = SampleLogs.DotnetTestFailure();
        var context = new FailureContext([new("deploy-staging", log), new("test-1", log), new("test-2", log), new("test-3", log)], Diff);

        var result = await Orchestrator(client, new CrewConfig { MaxJobs = 2, IgnoreJobs = ["deploy*"] }).ExplainAsync(context);

        Assert.Equal(["test-1", "test-2"], result.Report.Jobs.Select(j => j.Job));
        Assert.Equal(["deploy-staging", "test-3"], result.Report.Skipped);
    }

    [Fact]
    public async Task Agents_are_asked_to_write_in_the_configured_language_and_never_see_secrets()
    {
        var client = new FakeChatClient(Answers());
        var log = SampleLogs.WithTimestamps(
            "##[group]Run dotnet test",
            "Connecting with Server=db;User Id=app;Password=hunter2-prod;",
            "  Failed Shop.Tests.DbTests.Connects [3 ms]",
            "  Error Message:",
            "##[error]Process completed with exit code 1.");

        await Orchestrator(client, new CrewConfig { Language = "zh-CN" }).ExplainAsync(Context(log, Diff + "\n+var apiKey = \"api_key=supersecretvalue\";"));

        Assert.All(client.Prompts, p => Assert.Contains("Simplified Chinese", p));
        Assert.All(client.Prompts, p => Assert.DoesNotContain("hunter2-prod", p));
        Assert.All(client.Prompts, p => Assert.DoesNotContain("supersecretvalue", p));
    }

    [Fact]
    public async Task A_run_without_failed_jobs_needs_no_agents()
    {
        var client = new FakeChatClient(Answers());

        var result = await Orchestrator(client).ExplainAsync(new FailureContext([]));

        Assert.Empty(result.Report.Jobs);
        Assert.Equal("No failed jobs were found in this run.", result.Report.Title);
        Assert.Empty(client.Prompts);
    }
}
