using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CiFailureCrew.Core.Models;

namespace CiFailureCrew.Core.GitHub;

/// <param name="HeadOwner">Owner of the repository the run's commit came from (a fork, for pull requests from forks).</param>
/// <param name="PullRequests">Pull requests GitHub linked to the run; empty for pull requests from forks.</param>
public sealed record WorkflowRun(
    long Id, string Name, long WorkflowId, string HeadSha, string? HeadBranch, string? HeadOwner,
    string Event, string? Conclusion, string HtmlUrl, IReadOnlyList<int> PullRequests);

public sealed record WorkflowJob(long Id, string Name, string? Conclusion, string HtmlUrl)
{
    public bool Failed => Conclusion is "failure" or "timed_out";
}

public sealed record PullRequestInfo(int Number, string Title, string HeadSha);

public sealed record IssueComment(long Id, string Body);

public sealed record ReviewComment(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("line")] int Line,
    [property: JsonPropertyName("body")] string Body)
{
    [JsonPropertyName("side")]
    public string Side => "RIGHT";
}

public sealed class GitHubApiException(HttpStatusCode statusCode, string message) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}

/// <summary>Minimal client for the GitHub REST endpoints the crew uses.</summary>
public sealed class GitHubClient
{
    private const int MaxCommentPages = 5;

    private readonly HttpClient _http;

    /// <param name="http">An <see cref="HttpClient"/> whose base address is the GitHub API (e.g. https://api.github.com/).</param>
    /// <param name="token">Needs actions: read to read runs and logs, and pull-requests: write to comment.</param>
    public GitHubClient(HttpClient http, string? token)
    {
        _http = http;
        if (!string.IsNullOrEmpty(token))
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        _http.DefaultRequestHeaders.UserAgent.ParseAdd("ci-failure-crew");
        _http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    }

    public async Task<WorkflowRun> GetWorkflowRunAsync(string repository, long runId, CancellationToken cancellationToken = default)
    {
        using var json = await GetJsonAsync($"{Repo(repository)}/actions/runs/{runId}", cancellationToken);
        var root = json.RootElement;
        var headRepository = root.TryGetProperty("head_repository", out var head) && head.ValueKind == JsonValueKind.Object ? head : default;

        return new WorkflowRun(
            root.GetProperty("id").GetInt64(),
            String(root, "name") ?? "",
            root.GetProperty("workflow_id").GetInt64(),
            String(root, "head_sha") ?? "",
            String(root, "head_branch"),
            headRepository.ValueKind == JsonValueKind.Object && headRepository.TryGetProperty("owner", out var owner) ? String(owner, "login") : null,
            String(root, "event") ?? "",
            String(root, "conclusion"),
            String(root, "html_url") ?? "",
            root.TryGetProperty("pull_requests", out var pulls)
                ? pulls.EnumerateArray().Select(p => p.GetProperty("number").GetInt32()).ToList()
                : []);
    }

    /// <summary>Jobs of the latest attempt of the run.</summary>
    public async Task<IReadOnlyList<WorkflowJob>> GetJobsAsync(string repository, long runId, CancellationToken cancellationToken = default)
    {
        using var json = await GetJsonAsync($"{Repo(repository)}/actions/runs/{runId}/jobs?filter=latest&per_page=100", cancellationToken);
        return json.RootElement.GetProperty("jobs").EnumerateArray()
            .Select(j => new WorkflowJob(j.GetProperty("id").GetInt64(), String(j, "name") ?? "", String(j, "conclusion"), String(j, "html_url") ?? ""))
            .ToList();
    }

    /// <summary>The plain text log of a job. GitHub answers with a redirect to a signed URL, which is followed.</summary>
    public async Task<string> GetJobLogAsync(string repository, long jobId, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{Repo(repository)}/actions/jobs/{jobId}/logs");
        using var response = await SendAsync(request, cancellationToken);
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    public async Task<string> GetDefaultBranchAsync(string repository, CancellationToken cancellationToken = default)
    {
        using var json = await GetJsonAsync(Repo(repository), cancellationToken);
        return String(json.RootElement, "default_branch") ?? "main";
    }

    /// <summary>
    /// How each job behaved in the latest completed runs of the workflow on a branch, used to tell flaky
    /// jobs from real failures. Jobs that did not run in a run are not counted for it.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, JobHistory>> GetJobHistoryAsync(
        string repository, long workflowId, string branch, int runs, CancellationToken cancellationToken = default)
    {
        using var json = await GetJsonAsync(
            $"{Repo(repository)}/actions/workflows/{workflowId}/runs?branch={Uri.EscapeDataString(branch)}&status=completed&exclude_pull_requests=true&per_page={runs}",
            cancellationToken);

        var counts = new Dictionary<string, (int Runs, int Failures)>();
        foreach (var run in json.RootElement.GetProperty("workflow_runs").EnumerateArray().Take(runs))
        {
            foreach (var job in await GetJobsAsync(repository, run.GetProperty("id").GetInt64(), cancellationToken))
            {
                if (job.Conclusion is "success" or "failure" or "timed_out")
                {
                    var (total, failures) = counts.GetValueOrDefault(job.Name);
                    counts[job.Name] = (total + 1, failures + (job.Failed ? 1 : 0));
                }
            }
        }

        return counts.ToDictionary(c => c.Key, c => new JobHistory(c.Value.Runs, c.Value.Failures));
    }

    /// <summary>
    /// The open pull request of the run. Runs of pull requests from forks have no linked pull request,
    /// so they are found by the fork owner and branch.
    /// </summary>
    public async Task<int?> FindPullRequestAsync(string repository, WorkflowRun run, CancellationToken cancellationToken = default)
    {
        if (run.PullRequests.Count > 0)
        {
            return run.PullRequests[0];
        }

        if (run.HeadOwner is null || run.HeadBranch is null)
        {
            return null;
        }

        var head = Uri.EscapeDataString($"{run.HeadOwner}:{run.HeadBranch}");
        using var json = await GetJsonAsync($"{Repo(repository)}/pulls?state=open&head={head}&per_page=10", cancellationToken);
        var pulls = json.RootElement.EnumerateArray().ToList();
        var match = pulls.FirstOrDefault(p => p.GetProperty("head").GetProperty("sha").GetString() == run.HeadSha);
        var pull = match.ValueKind == JsonValueKind.Object ? match : pulls.FirstOrDefault();
        return pull.ValueKind == JsonValueKind.Object ? pull.GetProperty("number").GetInt32() : null;
    }

    public async Task<PullRequestInfo> GetPullRequestAsync(string repository, int number, CancellationToken cancellationToken = default)
    {
        using var json = await GetJsonAsync($"{Repo(repository)}/pulls/{number}", cancellationToken);
        var root = json.RootElement;
        return new PullRequestInfo(number, String(root, "title") ?? "", root.GetProperty("head").GetProperty("sha").GetString()!);
    }

    public async Task<string> GetPullRequestDiffAsync(string repository, int number, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{Repo(repository)}/pulls/{number}");
        request.Headers.Accept.ParseAdd("application/vnd.github.diff");

        using var response = await SendAsync(request, cancellationToken);
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    /// <summary>The first comment of the pull request whose body starts with the prefix.</summary>
    public async Task<IssueComment?> FindCommentAsync(string repository, int number, string prefix, CancellationToken cancellationToken = default)
    {
        for (var page = 1; page <= MaxCommentPages; page++)
        {
            using var json = await GetJsonAsync($"{Repo(repository)}/issues/{number}/comments?per_page=100&page={page}", cancellationToken);
            var comments = json.RootElement.EnumerateArray().ToList();
            foreach (var comment in comments)
            {
                if (String(comment, "body") is { } body && body.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return new IssueComment(comment.GetProperty("id").GetInt64(), body);
                }
            }

            if (comments.Count < 100)
            {
                break;
            }
        }

        return null;
    }

    /// <returns>The URL of the comment.</returns>
    public Task<string> CreateCommentAsync(string repository, int number, string body, CancellationToken cancellationToken = default) =>
        SendJsonAsync(HttpMethod.Post, $"{Repo(repository)}/issues/{number}/comments", new { body }, cancellationToken);

    /// <returns>The URL of the comment.</returns>
    public Task<string> UpdateCommentAsync(string repository, long commentId, string body, CancellationToken cancellationToken = default) =>
        SendJsonAsync(HttpMethod.Patch, $"{Repo(repository)}/issues/comments/{commentId}", new { body }, cancellationToken);

    /// <summary>Posts a COMMENT review: it never blocks the merge and works with the default GITHUB_TOKEN.</summary>
    public Task<string> CreateReviewAsync(
        string repository, int number, string commitId, string body, IReadOnlyList<ReviewComment> comments, CancellationToken cancellationToken = default) =>
        SendJsonAsync(HttpMethod.Post, $"{Repo(repository)}/pulls/{number}/reviews",
            new { commit_id = commitId, body, @event = "COMMENT", comments }, cancellationToken);

    private async Task<string> SendJsonAsync(HttpMethod method, string url, object content, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(content) };
        request.Headers.Accept.ParseAdd("application/vnd.github+json");

        using var response = await SendAsync(request, cancellationToken);
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        return String(json.RootElement, "html_url") ?? "";
    }

    private async Task<JsonDocument> GetJsonAsync(string url, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");

        using var response = await SendAsync(request, cancellationToken);
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            response.Dispose();
            throw new GitHubApiException(response.StatusCode, $"GitHub returned {(int)response.StatusCode} for {request.Method} {request.RequestUri}: {error}");
        }

        return response;
    }

    private static string? String(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string Repo(string repository)
    {
        if (repository.Split('/') is not [{ Length: > 0 }, { Length: > 0 }])
        {
            throw new ArgumentException($"Invalid repository '{repository}'. Use the owner/name format.", nameof(repository));
        }

        return $"repos/{repository}";
    }
}
