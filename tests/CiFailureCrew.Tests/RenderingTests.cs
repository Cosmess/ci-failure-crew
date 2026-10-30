using CiFailureCrew.Core.Configuration;
using CiFailureCrew.Core.Models;
using CiFailureCrew.Core.Orchestration;
using CiFailureCrew.Core.Rendering;

namespace CiFailureCrew.Tests;

public class RenderingTests
{
    private static JobDiagnosis Diagnosis(
        FailureCategory category = FailureCategory.TestFailure, string source = "agents", string? code = "        return total * 0.9m;") =>
        new("test (ubuntu-latest)", "https://github.com/acme/shop/actions/runs/1/job/2", category, 0.85,
            "The test fails because @hash now multiplies by 0.99.", ["Shop.Tests.DiscountTests.Gold"],
            "src/Shop.Core/DiscountCalculator.cs", 12, true, "Restore the multiplier.", code, source,
            "  Failed Shop.Tests.DiscountTests.Gold [12 ms]\nExpected: 90", []);

    private static CrewResult Result(string language = "en", params JobDiagnosis[] jobs) =>
        new(new FailureReport("The discount test failed because of the change in DiscountCalculator.cs.", "Fix the multiplier first.",
            jobs.Length > 0 ? jobs : [Diagnosis()], []), new UsageSummary(12_345, 678, 0.0042m), language);

    [Fact]
    public void Renders_the_headline_and_every_detail_of_a_job()
    {
        var md = MarkdownRenderer.Render(Result());

        Assert.StartsWith("## 🩺 CI Failure Crew", md);
        Assert.Contains("**The discount test failed because of the change in DiscountCalculator.cs.**", md);
        Assert.Contains("### 🧪 Test failure · `test (ubuntu-latest)` · [logs](https://github.com/acme/shop/actions/runs/1/job/2)", md);
        Assert.Contains("- **Location:** `src/Shop.Core/DiscountCalculator.cs:12`", md);
        Assert.Contains("- **Failing:** `Shop.Tests.DiscountTests.Gold`", md);
        Assert.Contains("- **Origin:** probably caused by this pull request", md);
        Assert.Contains("**Suggested fix:** Restore the multiplier.", md);
        Assert.Contains("return total * 0.9m;", md);
        Assert.Contains("<summary>Log excerpt</summary>", md);
        Assert.Contains("Confidence: 85%", md);
        Assert.Contains("Tokens: 12,345 input / 678 output · estimated cost: US$ 0.0042", md);
    }

    [Fact]
    public void Code_posted_as_a_suggestion_is_not_repeated_in_the_comment()
    {
        var job = Diagnosis();

        var md = MarkdownRenderer.Render(Result(jobs: job), new HashSet<JobDiagnosis> { job });

        Assert.Contains("`src/Shop.Core/DiscountCalculator.cs:12` 💬", md);
        Assert.DoesNotContain("return total * 0.9m;", md);
        Assert.Contains("💬 = a suggested change was posted on the line of code.", md);
    }

    [Fact]
    public void Model_text_cannot_mention_github_users()
    {
        var md = MarkdownRenderer.Render(Result());

        // Ordinal: a culture-aware comparison ignores the zero-width space.
        Assert.DoesNotContain("@hash", md, StringComparison.Ordinal);
        Assert.Contains("@\u200Bhash", md, StringComparison.Ordinal);
    }

    [Fact]
    public void Known_failures_say_they_were_explained_without_ai()
    {
        var md = MarkdownRenderer.Render(Result(jobs: Diagnosis(FailureCategory.Timeout, "rule:job-timeout", null)));

        Assert.Contains("### ⏱️ Timeout", md);
        Assert.Contains("Known failure pattern (job-timeout), explained without AI.", md);
        Assert.DoesNotContain("Confidence", md);
    }

    [Theory]
    [InlineData("pt-BR", "### 🧪 Teste falhou", "**Correção sugerida:**", "Confiança: 85")]
    [InlineData("zh-CN", "### 🧪 测试失败", "**建议的修复:**", "置信度：85")]
    public void Renders_in_the_language_of_the_result(string language, string heading, string fix, string confidence)
    {
        var md = MarkdownRenderer.Render(Result(language));

        Assert.Contains(heading, md);
        Assert.Contains(fix, md);
        Assert.Contains(confidence, md);
    }

    [Fact]
    public void Suggestion_comment_uses_a_github_suggestion_block()
    {
        var comment = MarkdownRenderer.RenderSuggestion(Diagnosis());

        Assert.Contains("```suggestion\n        return total * 0.9m;\n```", comment);
        Assert.Contains("Restore the multiplier.", comment);
    }

    [Fact]
    public void Fences_are_longer_than_the_backticks_inside_the_text()
    {
        Assert.Equal("````text\nuse ``` here\n````", MarkdownText.Fence("use ``` here", "text"));
        Assert.Equal("```\nplain\n```", MarkdownText.Fence("plain"));
    }

    [Theory]
    [InlineData("/home/runner/work/shop/shop/src/A.cs", "src/A.cs")]
    [InlineData(@"D:\a\shop\shop\src\A.cs", "src/A.cs")]
    [InlineData("./src/A.cs", "src/A.cs")]
    [InlineData("src/A.cs", "src/A.cs")]
    [InlineData(" ", null)]
    public void Runner_paths_become_repository_paths(string path, string? expected)
    {
        Assert.Equal(expected, RepositoryPath.Normalize(path));
    }

    [Fact]
    public void Configuration_is_read_from_yaml_and_validated()
    {
        var config = CrewConfig.Parse("""
            language: pt
            maxJobs: 5
            ignoreJobs: ["deploy*", "Lint"]
            historyRuns: 0
            pricing:
              inputPerMillion: 0.25
              outputPerMillion: 2.00
            """);

        Assert.Equal("pt-BR", config.ResolveLanguage().Code);
        Assert.Equal(5, config.MaxJobs);
        Assert.True(config.IsIgnored("deploy-production"));
        Assert.True(config.IsIgnored("lint"));
        Assert.False(config.IsIgnored("build"));
        Assert.Equal(2.00m, config.Pricing!.OutputPerMillion);
        Assert.Throws<InvalidOperationException>(() => CrewConfig.Parse("maxJobs: 0"));
        Assert.Throws<InvalidOperationException>(() => CrewConfig.Parse("language: fr"));
        Assert.Equal(3, CrewConfig.Parse("").MaxJobs);
    }

    [Fact]
    public void The_example_configuration_is_valid_and_matches_the_defaults()
    {
        var example = CrewConfig.Load(Path.Combine(AppContext.BaseDirectory, ".ci-failure-crew.example.yml"));
        var defaults = CrewConfig.Default;

        Assert.Equal(defaults.Language, example.Language);
        Assert.Equal(defaults.MaxJobs, example.MaxJobs);
        Assert.Equal(defaults.HistoryRuns, example.HistoryRuns);
        Assert.Equal(defaults.MaxLogChars, example.MaxLogChars);
        Assert.Equal(defaults.MaxDiffChars, example.MaxDiffChars);
        Assert.Equal(defaults.AgentTimeoutSeconds, example.AgentTimeoutSeconds);
        Assert.Equal(defaults.MaxRetries, example.MaxRetries);
    }
}
