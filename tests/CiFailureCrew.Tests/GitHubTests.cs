using System.Net;
using System.Text.Json;
using CiFailureCrew.Core.GitHub;
using CiFailureCrew.Core.Models;

namespace CiFailureCrew.Tests;

public class GitHubTests
{
    private const string Repository = "acme/shop";

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

    private static (GitHubClient Client, FakeHttpHandler Handler) Client(Func<RecordedRequest, HttpResponseMessage> respond)
    {
        var handler = new FakeHttpHandler(respond);
        return (new GitHubClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") }, "token"), handler);
    }

    private static CrewResult Result(string? code = "        customer.IsGold ? total * 0.9m : total;", int line = 11) =>
        new(new FailureReport("The discount test failed.", "", [
            new JobDiagnosis("test", null, FailureCategory.TestFailure, 0.9, "Line 11 multiplies by 0.99.", [],
                "src/Shop.Core/DiscountCalculator.cs", line, true, "Restore 0.9.", code, "agents", "Expected: 90", [])
        ], []), UsageSummary.Empty);

    [Fact]
    public async Task Reads_a_run_with_its_linked_pull_request()
    {
        var (client, handler) = Client(_ => FakeHttpHandler.Json("""
            {"id":7,"name":"CI","workflow_id":99,"head_sha":"abc123","head_branch":"feature","event":"pull_request",
             "conclusion":"failure","html_url":"https://github.com/acme/shop/actions/runs/7",
             "head_repository":{"owner":{"login":"acme"}},"pull_requests":[{"number":42}]}
            """));

        var run = await client.GetWorkflowRunAsync(Repository, 7);

        Assert.Equal(new WorkflowRun(7, "CI", 99, "abc123", "feature", "acme", "pull_request", "failure", "https://github.com/acme/shop/actions/runs/7", run.PullRequests), run);
        Assert.Equal([42], run.PullRequests);
        Assert.Equal(42, await client.FindPullRequestAsync(Repository, run));
        Assert.Equal("https://api.github.com/repos/acme/shop/actions/runs/7", handler.Requests[0].Url);
    }

    [Fact]
    public async Task Finds_the_pull_request_of_a_fork_by_owner_and_branch()
    {
        var (client, handler) = Client(_ => FakeHttpHandler.Json("""
            [{"number":5,"head":{"sha":"old"}},{"number":8,"head":{"sha":"abc123"}}]
            """));
        var run = new WorkflowRun(7, "CI", 99, "abc123", "fix-typo", "contributor", "pull_request", "failure", "", []);

        var number = await client.FindPullRequestAsync(Repository, run);

        Assert.Equal(8, number);
        Assert.Equal("https://api.github.com/repos/acme/shop/pulls?state=open&head=contributor%3Afix-typo&per_page=10", handler.Requests[0].Url);
    }

    [Fact]
    public async Task Lists_the_jobs_of_the_latest_attempt_and_downloads_a_log()
    {
        var (client, handler) = Client(r => r.Url.EndsWith("/logs")
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("2026-10-30T12:00:00.0000000Z log line") }
            : FakeHttpHandler.Json("""
                {"jobs":[{"id":1,"name":"build","conclusion":"success","html_url":"u1"},
                         {"id":2,"name":"test","conclusion":"failure","html_url":"u2"},
                         {"id":3,"name":"e2e","conclusion":"timed_out","html_url":"u3"}]}
                """));

        var jobs = await client.GetJobsAsync(Repository, 7);
        var log = await client.GetJobLogAsync(Repository, 2);

        Assert.Equal(["test", "e2e"], jobs.Where(j => j.Failed).Select(j => j.Name));
        Assert.EndsWith("/actions/runs/7/jobs?filter=latest&per_page=100", handler.Requests[0].Url);
        Assert.Contains("log line", log);
    }

    [Fact]
    public async Task Counts_how_often_each_job_failed_on_the_default_branch()
    {
        var (client, handler) = Client(r =>
            r.Url.Contains("/workflows/99/runs") ? FakeHttpHandler.Json("""{"workflow_runs":[{"id":1},{"id":2},{"id":3}]}""")
            : r.Url.Contains("/runs/1/") ? FakeHttpHandler.Json("""{"jobs":[{"id":11,"name":"test","conclusion":"failure"},{"id":12,"name":"build","conclusion":"success"}]}""")
            : r.Url.Contains("/runs/2/") ? FakeHttpHandler.Json("""{"jobs":[{"id":21,"name":"test","conclusion":"success"},{"id":22,"name":"build","conclusion":"success"}]}""")
            : FakeHttpHandler.Json("""{"jobs":[{"id":31,"name":"test","conclusion":"failure"},{"id":32,"name":"build","conclusion":"skipped"}]}"""));

        var history = await client.GetJobHistoryAsync(Repository, 99, "main", 3);

        Assert.Equal(new JobHistory(3, 2), history["test"]);
        Assert.Equal(new JobHistory(2, 0), history["build"]);
        Assert.Contains("branch=main&status=completed&exclude_pull_requests=true&per_page=3", handler.Requests[0].Url);
    }

    [Fact]
    public async Task Creates_the_comment_and_posts_the_suggestion_on_the_line()
    {
        var (client, handler) = Client(r => r.Method == HttpMethod.Get
            ? FakeHttpHandler.Json("""[{"id":1,"body":"Nice work!"}]""")
            : FakeHttpHandler.Json("""{"html_url":"https://github.com/acme/shop/pull/42#issuecomment-9"}"""));

        var url = await new FailurePublisher(client).PublishAsync(Repository, 42, 99, "abc123", Diff, Result());

        Assert.Equal("https://github.com/acme/shop/pull/42#issuecomment-9", url);
        var review = handler.Requests.Single(r => r.Url.EndsWith("/pulls/42/reviews"));
        using var reviewJson = JsonDocument.Parse(review.Body!);
        var comment = reviewJson.RootElement.GetProperty("comments")[0];
        Assert.Equal("src/Shop.Core/DiscountCalculator.cs", comment.GetProperty("path").GetString());
        Assert.Equal(11, comment.GetProperty("line").GetInt32());
        Assert.Contains("```suggestion", comment.GetProperty("body").GetString());
        Assert.Equal("COMMENT", reviewJson.RootElement.GetProperty("event").GetString());

        var created = handler.Requests.Single(r => r.Method == HttpMethod.Post && r.Url.EndsWith("/issues/42/comments"));
        var body = JsonDocument.Parse(created.Body!).RootElement.GetProperty("body").GetString()!;
        Assert.StartsWith("<!-- ci-failure-crew:workflow=99 sha=abc123 suggestions=1 -->\n## 🩺 CI Failure Crew", body);
        Assert.Contains("`src/Shop.Core/DiscountCalculator.cs:11` 💬", body);
    }

    [Fact]
    public async Task A_rerun_of_the_same_commit_updates_the_comment_without_repeating_suggestions()
    {
        var (client, handler) = Client(r => r.Method == HttpMethod.Get
            ? FakeHttpHandler.Json("""[{"id":5,"body":"<!-- ci-failure-crew:workflow=99 sha=abc123 suggestions=1 -->\nold"}]""")
            : FakeHttpHandler.Json("""{"html_url":"u"}"""));

        await new FailurePublisher(client).PublishAsync(Repository, 42, 99, "abc123", Diff, Result());

        Assert.DoesNotContain(handler.Requests, r => r.Url.EndsWith("/reviews"));
        var update = Assert.Single(handler.Requests, r => r.Method == HttpMethod.Patch);
        Assert.EndsWith("/issues/comments/5", update.Url);
        Assert.Contains("suggestions=1", update.Body);
    }

    [Fact]
    public async Task Each_workflow_keeps_its_own_comment()
    {
        var (client, handler) = Client(r => r.Method == HttpMethod.Get
            ? FakeHttpHandler.Json("""[{"id":5,"body":"<!-- ci-failure-crew:workflow=12 sha=abc123 -->\nother workflow"}]""")
            : FakeHttpHandler.Json("""{"html_url":"u"}"""));

        await new FailurePublisher(client).PublishAsync(Repository, 42, 99, "abc123", null, Result());

        Assert.DoesNotContain(handler.Requests, r => r.Method == HttpMethod.Patch);
        Assert.Single(handler.Requests, r => r.Method == HttpMethod.Post && r.Url.EndsWith("/issues/42/comments"));
    }

    [Fact]
    public async Task A_suggestion_github_rejects_stays_in_the_comment()
    {
        var (client, handler) = Client(r =>
            r.Method == HttpMethod.Get ? FakeHttpHandler.Json("[]")
            : r.Url.EndsWith("/reviews") ? FakeHttpHandler.Json("""{"message":"line must be part of the diff"}""", HttpStatusCode.UnprocessableEntity)
            : FakeHttpHandler.Json("""{"html_url":"u"}"""));

        await new FailurePublisher(client).PublishAsync(Repository, 42, 99, "abc123", Diff, Result());

        var body = handler.Requests.Single(r => r.Url.EndsWith("/issues/42/comments") && r.Method == HttpMethod.Post).Body!;
        Assert.DoesNotContain("suggestions=1", body);
        Assert.Contains("0.9m", body);
    }

    [Theory]
    [InlineData(null, 11)]
    [InlineData("        return 1;", 30)]
    public void Only_suggestions_on_lines_of_the_diff_are_selected(string? code, int line)
    {
        Assert.Empty(FailurePublisher.SelectSuggestions(Diff, Result(code, line).Report.Jobs));
        Assert.Single(FailurePublisher.SelectSuggestions(Diff, Result().Report.Jobs));
        Assert.Empty(FailurePublisher.SelectSuggestions(null, Result().Report.Jobs));
    }

    [Fact]
    public async Task Resolving_folds_the_previous_diagnosis_once()
    {
        const string previous = "<!-- ci-failure-crew:workflow=99 sha=abc123 -->\n## 🩺 CI Failure Crew\n\n**The discount test failed.**";
        var (client, handler) = Client(r => r.Method == HttpMethod.Get
            ? FakeHttpHandler.Json(JsonSerializer.Serialize(new[] { new { id = 5, body = previous } }))
            : FakeHttpHandler.Json("""{"html_url":"u"}"""));

        var url = await new FailurePublisher(client).ResolveAsync(Repository, 42, 99, "def4567890", "pt-BR");

        Assert.Equal("u", url);
        var body = JsonDocument.Parse(handler.Requests.Single(r => r.Method == HttpMethod.Patch).Body!).RootElement.GetProperty("body").GetString()!;
        Assert.StartsWith("<!-- ci-failure-crew:workflow=99 sha=def4567890 resolved=1 -->", body);
        Assert.Contains("✅ O workflow passou no commit `def4567`", body);
        Assert.Contains("<details><summary>Diagnóstico anterior</summary>", body);
        Assert.Contains("**The discount test failed.**", body);
        Assert.Equal(1, body.Split(MarkdownHeading).Length - 1);

        var resolved = FailurePublisher.Marker.Parse(body)!;
        Assert.True(resolved.Resolved);
    }

    private const string MarkdownHeading = "## 🩺 CI Failure Crew";

    [Fact]
    public async Task Nothing_is_resolved_without_a_previous_comment()
    {
        var (client, handler) = Client(_ => FakeHttpHandler.Json("[]"));

        Assert.Null(await new FailurePublisher(client).ResolveAsync(Repository, 42, 99, "abc", "en"));
        Assert.DoesNotContain(handler.Requests, r => r.Method != HttpMethod.Get);
    }
}
