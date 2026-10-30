using CiFailureCrew.Core.Configuration;
using CiFailureCrew.Core.GitHub;
using CiFailureCrew.Core.Localization;
using CiFailureCrew.Core.Models;
using CiFailureCrew.Core.Orchestration;
using CiFailureCrew.Core.Rendering;
using CiFailureCrew.Providers;

namespace CiFailureCrew.Cli;

internal sealed record ExplainCommandOptions(
    long? RunId,
    string? Repository,
    bool Post,
    IReadOnlyList<FileInfo> Logs,
    FileInfo? DiffFile,
    FileInfo? ConfigFile,
    string? Language,
    DirectoryInfo? RepositoryPath,
    FileInfo? OutputFile);

internal sealed class ExplainCommand(ExplainCommandOptions options)
{
    public const int Success = 0;
    public const int Error = 1;

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await ExecuteAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or GitHubApiException or IOException or HttpRequestException)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return Error;
        }
    }

    private async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        if (options.RunId is null && options.Logs.Count == 0)
        {
            throw new ArgumentException("Provide --run with a workflow run id, or --log with job log files. Use --help to see the options.");
        }

        if (options.RunId is not null && options.Logs.Count > 0)
        {
            throw new ArgumentException("Use either --run or --log, not both.");
        }

        if (options.Post && options.RunId is null)
        {
            throw new ArgumentException("--post requires --run.");
        }

        if (options.RepositoryPath is { Exists: false } missing)
        {
            throw new ArgumentException($"Repository directory not found: {missing.FullName}");
        }

        var config = LoadConfig();
        if (options.Language is { Length: > 0 } language)
        {
            config.Language = CrewLanguage.Parse(language).Code;
        }

        return options.RunId is { } runId
            ? await ExplainRunAsync(runId, config, cancellationToken)
            : await ExplainLogsAsync(config, cancellationToken);
    }

    private async Task<int> ExplainLogsAsync(CrewConfig config, CancellationToken cancellationToken)
    {
        var jobs = new List<FailedJob>();
        foreach (var log in options.Logs)
        {
            jobs.Add(new FailedJob(Path.GetFileNameWithoutExtension(log.Name), await File.ReadAllTextAsync(log.FullName, cancellationToken)));
        }

        var diff = options.DiffFile is null ? null : await File.ReadAllTextAsync(options.DiffFile.FullName, cancellationToken);
        var result = await ExplainAsync(new FailureContext(jobs, diff), config, cancellationToken);
        await WriteOutputsAsync(result, cancellationToken);
        return Success;
    }

    private async Task<int> ExplainRunAsync(long runId, CrewConfig config, CancellationToken cancellationToken)
    {
        var repository = options.Repository
            ?? throw new ArgumentException("Provide the repository with --repo owner/name or the GITHUB_REPOSITORY variable.");
        var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN") is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException("The GITHUB_TOKEN variable is required to read the run and its logs.");

        using var http = new HttpClient { BaseAddress = GitHubApiUrl() };
        var gitHub = new GitHubClient(http, token);
        var run = await gitHub.GetWorkflowRunAsync(repository, runId, cancellationToken);
        var pullRequest = await gitHub.FindPullRequestAsync(repository, run, cancellationToken);
        Console.Error.WriteLine($"Run {run.Id} of '{run.Name}' ({run.Event}, {run.Conclusion ?? "in progress"}){(pullRequest is { } n ? $", pull request #{n}" : "")}");

        if (run.Conclusion == "success")
        {
            if (options.Post && pullRequest is { } number
                && await new FailurePublisher(gitHub).ResolveAsync(repository, number, run.WorkflowId, run.HeadSha, config.ResolveLanguage().Code, cancellationToken) is { } url)
            {
                Console.Error.WriteLine($"The workflow passed: comment marked as resolved: {url}");
            }
            else
            {
                Console.Error.WriteLine("The workflow passed: nothing to explain.");
            }

            return Success;
        }

        if (run.Conclusion is not ("failure" or "timed_out"))
        {
            Console.Error.WriteLine($"The run concluded as '{run.Conclusion ?? "in progress"}': nothing to explain.");
            return Success;
        }

        var failed = (await gitHub.GetJobsAsync(repository, runId, cancellationToken)).Where(j => j.Failed).ToList();
        var analyzed = failed.Where(j => !config.IsIgnored(j.Name)).Take(config.MaxJobs).ToList();
        var history = await HistoryAsync(gitHub, repository, run, config, cancellationToken);

        var jobs = new List<FailedJob>();
        foreach (var job in analyzed)
        {
            Console.Error.WriteLine($"Downloading the log of '{job.Name}'");
            jobs.Add(new FailedJob(job.Name, await gitHub.GetJobLogAsync(repository, job.Id, cancellationToken), job.HtmlUrl, history.GetValueOrDefault(job.Name)));
        }

        // Jobs over the limit or ignored are listed in the explanation without downloading their logs.
        jobs.AddRange(failed.Except(analyzed).Select(j => new FailedJob(j.Name, "", j.HtmlUrl)));

        string? diff = null;
        string? title = null;
        if (pullRequest is { } pr)
        {
            title = (await gitHub.GetPullRequestAsync(repository, pr, cancellationToken)).Title;
            diff = await gitHub.GetPullRequestDiffAsync(repository, pr, cancellationToken);
        }

        var result = await ExplainAsync(new FailureContext(jobs, diff, title, run.Name), config, cancellationToken);
        await WriteOutputsAsync(result, cancellationToken);

        if (options.Post)
        {
            if (pullRequest is { } number)
            {
                var url = await new FailurePublisher(gitHub).PublishAsync(repository, number, run.WorkflowId, run.HeadSha, diff, result, cancellationToken);
                Console.Error.WriteLine($"Explanation posted: {url}");
            }
            else
            {
                Console.Error.WriteLine("The run has no open pull request: the explanation is only in the job summary.");
            }
        }

        return Success;
    }

    private async Task<CrewResult> ExplainAsync(FailureContext context, CrewConfig config, CancellationToken cancellationToken)
    {
        await using var mcp = await McpToolHost.StartAsync(options.RepositoryPath?.FullName, cancellationToken);
        var orchestrator = new FailureOrchestrator(ChatClientFactory.Create(), config, tools: mcp.Tools);
        orchestrator.AgentStarted += (agent, job) => Console.Error.WriteLine($"▶ {agent} started ({job})");
        orchestrator.AgentRetrying += (agent, job, attempt, ex) => Console.Error.WriteLine($"↻ {agent} ({job}): attempt {attempt} failed ({ex.Message}), retrying");
        orchestrator.AgentCompleted += (agent, job, error) => Console.Error.WriteLine(error is null ? $"✔ {agent} finished ({job})" : $"⚠ {agent} failed ({job}): {error}");

        var result = await orchestrator.ExplainAsync(context, cancellationToken);
        foreach (var job in result.Report.Jobs.Where(j => j.IsKnownFailure))
        {
            Console.Error.WriteLine($"✔ {job.Job}: known failure ({job.Source}), explained without a model");
        }

        Console.Error.WriteLine(MarkdownRenderer.UsageLine(result.Usage, result.Language));
        return result;
    }

    private static async Task<IReadOnlyDictionary<string, JobHistory>> HistoryAsync(
        GitHubClient gitHub, string repository, WorkflowRun run, CrewConfig config, CancellationToken cancellationToken)
    {
        if (config.HistoryRuns == 0)
        {
            return new Dictionary<string, JobHistory>();
        }

        try
        {
            var branch = await gitHub.GetDefaultBranchAsync(repository, cancellationToken);
            return await gitHub.GetJobHistoryAsync(repository, run.WorkflowId, branch, config.HistoryRuns, cancellationToken);
        }
        catch (Exception ex) when (ex is GitHubApiException or HttpRequestException)
        {
            // Without history the flakiness check has less evidence, but the explanation still works.
            Console.Error.WriteLine($"⚠ Could not read the job history: {ex.Message}");
            return new Dictionary<string, JobHistory>();
        }
    }

    private async Task WriteOutputsAsync(CrewResult result, CancellationToken cancellationToken)
    {
        var markdown = MarkdownRenderer.Render(result);
        Console.WriteLine(markdown);

        if (options.OutputFile is not null)
        {
            await File.WriteAllTextAsync(options.OutputFile.FullName, markdown, cancellationToken);
        }

        // Shows the explanation on the workflow run page when running inside GitHub Actions.
        if (Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY") is { Length: > 0 } summaryPath)
        {
            await File.AppendAllTextAsync(summaryPath, markdown, cancellationToken);
        }
    }

    private CrewConfig LoadConfig()
    {
        var path = options.ConfigFile?.FullName
            ?? (File.Exists(CrewConfig.DefaultFileName) ? CrewConfig.DefaultFileName : null);

        if (path is null)
        {
            return CrewConfig.Default;
        }

        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Configuration file not found: {path}");
        }

        Console.Error.WriteLine($"Using the configuration in {path}");
        return CrewConfig.Load(path);
    }

    private static Uri GitHubApiUrl()
    {
        var url = Environment.GetEnvironmentVariable("GITHUB_API_URL") ?? "https://api.github.com";
        return new Uri(url.EndsWith('/') ? url : url + "/");
    }
}
