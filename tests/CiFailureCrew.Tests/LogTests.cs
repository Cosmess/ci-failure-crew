using CiFailureCrew.Core.LogAnalysis;
using CiFailureCrew.Core.Models;

namespace CiFailureCrew.Tests;

public class LogTests
{
    [Fact]
    public void Clean_removes_timestamps_and_ansi_codes()
    {
        var lines = LogCleaner.Clean("﻿" + SampleLogs.CompilationError());

        Assert.Equal("##[group]Run dotnet build -c Release", lines[0]);
        Assert.StartsWith("/home/runner/work/shop/shop/src/Shop.Api/OrdersController.cs(27,30): error CS1061", lines[2]);
        Assert.DoesNotContain(lines, l => l.Contains('\u001b') || l.StartsWith("2026-", StringComparison.Ordinal));
    }

    [Fact]
    public void Failed_step_starts_at_the_header_of_the_step_with_the_error()
    {
        var step = LogCleaner.FailedStep(LogCleaner.Clean(SampleLogs.DotnetTestFailure()));

        Assert.Equal("##[group]Run dotnet test -c Release --no-build", step[0]);
        Assert.DoesNotContain(step, l => l.Contains("Build succeeded"));
        Assert.DoesNotContain(step, l => l.Contains("Post job cleanup"));
    }

    [Fact]
    public void Failed_step_is_the_whole_log_without_an_error_marker()
    {
        var lines = LogCleaner.Clean(SampleLogs.WithTimestamps("one", "two"));

        Assert.Equal(lines, LogCleaner.FailedStep(lines));
    }

    [Fact]
    public void Excerpt_keeps_the_failing_test_and_its_assertion()
    {
        var excerpt = LogExcerpt.Extract(SampleLogs.DotnetTestFailure());

        Assert.Contains("Failed Shop.Tests.DiscountTests.Applies_ten_percent_for_gold_customers", excerpt);
        Assert.Contains("Expected: 90", excerpt);
        Assert.Contains("DiscountTests.cs:line 14", excerpt);
        Assert.Contains("Process completed with exit code 1", excerpt);
    }

    [Fact]
    public void Excerpt_drops_the_noise_of_a_long_log()
    {
        var noise = Enumerable.Range(0, 5000).Select(i => $"  Restored package Package{i}");
        var log = SampleLogs.WithTimestamps(
            ["##[group]Run dotnet build", .. noise, "src/A.cs(3,1): error CS0246: The type or namespace name 'Foo' could not be found", .. noise, "##[error]Process completed with exit code 1."]);

        var excerpt = LogExcerpt.Extract(log);

        Assert.Contains("error CS0246", excerpt);
        Assert.Contains("[...]", excerpt);
        Assert.True(excerpt.Length < 5000, $"The excerpt has {excerpt.Length} characters.");
    }

    [Fact]
    public void Excerpt_respects_the_size_limit_and_keeps_the_first_error_and_the_end()
    {
        var errors = Enumerable.Range(0, 400).SelectMany(i => new[] { $"src/F{i}.cs(1,1): error CS0103: The name 'x{i}' does not exist", "filler", "filler" });
        var log = SampleLogs.WithTimestamps(["##[group]Run dotnet build", .. errors, "Build FAILED.", "##[error]Process completed with exit code 1."]);

        var excerpt = LogExcerpt.Extract(log, maxChars: 3000);

        Assert.True(excerpt.Length <= 3000, $"The excerpt has {excerpt.Length} characters.");
        Assert.Contains("'x0'", excerpt);
        Assert.Contains("Process completed with exit code 1", excerpt);
    }

    [Theory]
    [InlineData("password=hunter2-prod", "password=[REDACTED]")]
    [InlineData("Server=db;User Id=app;Password=S3cr3t!;", "Server=db;User Id=app;Password=[REDACTED];")]
    [InlineData("curl -H \"Authorization: Bearer abcdefghijklmnopqrstuvwxyz123456\"", "curl -H \"Authorization: Bearer [REDACTED]\"")]
    [InlineData("export API_KEY='abcd1234efgh'", "export API_KEY='[REDACTED]'")]
    public void Redacts_secrets_in_assignments_and_headers(string input, string expected)
    {
        Assert.Equal(expected, SecretRedactor.Redact(input));
    }

    [Fact]
    public void Redacts_known_token_formats_and_private_keys()
    {
        // Built at runtime so no string in the repository looks like a real credential.
        var gitHubToken = "gh" + "p_" + new string('a', 36);
        var openAiKey = "sk-" + "proj-" + new string('b', 40);
        var privateKey = "-----BEGIN RSA PRIVATE" + " KEY-----\nMIIEow\n-----END RSA PRIVATE" + " KEY-----";

        var redacted = SecretRedactor.Redact($"token {gitHubToken} key {openAiKey}\n{privateKey}");

        Assert.DoesNotContain(gitHubToken, redacted);
        Assert.DoesNotContain(openAiKey, redacted);
        Assert.DoesNotContain("MIIEow", redacted);
    }

    [Theory]
    [InlineData("Failed!  - Failed:     1, Passed:    41")]
    [InlineData("error CS1061: 'OrderService' does not contain a definition for 'GetTotal'")]
    [InlineData("The token was not found in the response")]
    public void Leaves_ordinary_log_lines_alone(string line)
    {
        Assert.Equal(line, SecretRedactor.Redact(line));
    }

    [Theory]
    [InlineData("##[error]The job running on runner GitHub Actions 3 has exceeded the maximum execution time of 360 minutes.", "job-timeout", FailureCategory.Timeout)]
    [InlineData("##[error]The runner has received a shutdown signal.", "runner-lost", FailureCategory.Infrastructure)]
    [InlineData("System.IO.IOException: No space left on device", "disk-full", FailureCategory.Infrastructure)]
    [InlineData("##[error]Process completed with exit code 137.", "out-of-memory", FailureCategory.Infrastructure)]
    [InlineData("/src/App.csproj : error NU1301: Unable to load the service index for source https://api.nuget.org/v3/index.json.", "package-feed-unreachable", FailureCategory.Dependency)]
    [InlineData("toomanyrequests: You have reached your pull rate limit.", "rate-limited", FailureCategory.Infrastructure)]
    public void Recognizes_known_failures(string line, string id, FailureCategory category)
    {
        var known = KnownFailures.Match(line);

        Assert.NotNull(known);
        Assert.Equal(id, known.Id);
        Assert.Equal(category, known.Category);
    }

    [Fact]
    public void Code_failures_are_not_known_failures()
    {
        Assert.Null(KnownFailures.Match(SampleLogs.DotnetTestFailure()));
        Assert.Null(KnownFailures.Match(SampleLogs.CompilationError()));
    }

    [Fact]
    public void Every_known_failure_is_written_in_every_language()
    {
        Assert.All(KnownFailures.All, rule =>
        {
            Assert.Equal(["en", "pt-BR", "zh-CN"], rule.Texts.Keys.Order());
            Assert.All(rule.Texts.Values, text => Assert.False(string.IsNullOrWhiteSpace(text.Explanation + text.Fix)));
        });
        Assert.Equal(KnownFailures.All.Count, KnownFailures.All.Select(r => r.Id).Distinct().Count());
        Assert.Same(KnownFailures.All[0].Texts["en"], KnownFailures.All[0].Text("fr"));
    }
}
