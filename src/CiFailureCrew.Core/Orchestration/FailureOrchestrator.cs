using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CiFailureCrew.Core.Agents;
using CiFailureCrew.Core.Configuration;
using CiFailureCrew.Core.Diff;
using CiFailureCrew.Core.LogAnalysis;
using CiFailureCrew.Core.Localization;
using CiFailureCrew.Core.Models;
using CiFailureCrew.Core.Rendering;
using CiFailureCrew.Core.Telemetry;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CiFailureCrew.Core.Orchestration;

/// <summary>
/// Explains a failed workflow run. Each failed job goes through a pipeline that only calls the agents
/// it needs: known failure rules first (no model), then triage, then either the flakiness check or the
/// investigation and fix. The synthesizer writes the headline over all jobs.
/// </summary>
/// <remarks>
/// Jobs are analyzed in parallel. Every agent call has a timeout and retries, and a failing agent
/// never takes the explanation down: the diagnosis keeps what the previous agents found.
/// </remarks>
public sealed class FailureOrchestrator
{
    public const string AgentsSource = "agents";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly CrewConfig _config;
    private readonly CrewLanguage _language;
    private readonly RetryPolicy _retry;
    private readonly bool _hasTools;
    private readonly AIAgent _triage;
    private readonly AIAgent _investigator;
    private readonly AIAgent _flakiness;
    private readonly AIAgent _fixer;
    private readonly AIAgent _synthesizer;

    /// <param name="tools">Tools the investigator and the fixer may call, such as the repository tools of an MCP server.</param>
    public FailureOrchestrator(
        IChatClient chatClient, CrewConfig? config = null, Func<int, TimeSpan>? retryBackoff = null, IReadOnlyList<AITool>? tools = null)
    {
        ArgumentNullException.ThrowIfNull(chatClient);

        _config = config ?? CrewConfig.Default;
        _language = _config.ResolveLanguage();
        _retry = new RetryPolicy(_config.MaxRetries, TimeSpan.FromSeconds(_config.AgentTimeoutSeconds), retryBackoff);
        _hasTools = tools is { Count: > 0 };
        _triage = CreateAgent(chatClient, Crew.Triage);
        _investigator = CreateAgent(chatClient, Crew.Investigator, tools);
        _flakiness = CreateAgent(chatClient, Crew.Flakiness);
        _fixer = CreateAgent(chatClient, Crew.Fixer, tools);
        _synthesizer = CreateAgent(chatClient, Crew.Synthesizer);
    }

    /// <summary>Agent name and job name.</summary>
    public event Action<string, string>? AgentStarted;

    /// <summary>Agent name, job name and the error message when the agent failed.</summary>
    public event Action<string, string, string?>? AgentCompleted;

    public event Action<string, string, int, Exception>? AgentRetrying;

    public async Task<CrewResult> ExplainAsync(FailureContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var analyzed = context.Jobs.Where(j => !_config.IsIgnored(j.Name)).Take(_config.MaxJobs).ToList();
        var skipped = context.Jobs.Except(analyzed).Select(j => j.Name).ToList();
        if (analyzed.Count == 0)
        {
            return new CrewResult(new FailureReport(_language.Strings.NoFailedJobs, "", [], skipped), UsageSummary.Empty, _language.Code);
        }

        var diff = context.Diff is { Length: > 0 } raw ? Truncate(SecretRedactor.Redact(raw), _config.MaxDiffChars) : null;
        var changedFiles = diff is null ? [] : DiffParser.Parse(diff).Select(f => f.Path).ToList();
        var usage = new UsageTracker(_config.Pricing);
        using var activity = CrewTelemetry.StartExplanation(analyzed.Count);

        var diagnoses = await Task.WhenAll(analyzed.Select(job => DiagnoseAsync(job, context, diff, changedFiles, usage, cancellationToken)));
        var headline = await HeadlineAsync(context, diagnoses, usage, cancellationToken);

        foreach (var diagnosis in diagnoses)
        {
            CrewTelemetry.Diagnoses.Add(1,
                new KeyValuePair<string, object?>("cfc.category", diagnosis.Category.ToString()),
                new KeyValuePair<string, object?>("cfc.source", diagnosis.IsKnownFailure ? "rule" : AgentsSource));
        }

        var summary = usage.ToSummary();
        activity?.SetTag("gen_ai.usage.input_tokens", summary.InputTokens);
        activity?.SetTag("gen_ai.usage.output_tokens", summary.OutputTokens);
        return new CrewResult(new FailureReport(headline.Title, headline.Summary, diagnoses, skipped), summary, _language.Code);
    }

    private async Task<JobDiagnosis> DiagnoseAsync(
        FailedJob job, FailureContext context, string? diff, IReadOnlyList<string> changedFiles, UsageTracker usage, CancellationToken cancellationToken)
    {
        var failedStep = string.Join('\n', LogCleaner.FailedStep(LogCleaner.Clean(job.Log)));
        var excerpt = SecretRedactor.Redact(LogExcerpt.Extract(job.Log, _config.MaxLogChars));

        if (KnownFailures.Match(failedStep) is { } known)
        {
            var text = known.Text(_language.Code);
            return new JobDiagnosis(job.Name, job.Url, known.Category, 1, text.Explanation, [], null, null, false, text.Fix, null,
                $"rule:{known.Id}", excerpt, []);
        }

        var errors = new List<string>();
        var jobContext = JobContext(job, context, changedFiles, excerpt);

        var triage = await RunAsync<Triage>(_triage, Crew.Triage, job.Name, jobContext, usage, errors, cancellationToken);
        if (triage is null)
        {
            return new JobDiagnosis(job.Name, job.Url, FailureCategory.Unknown, 0, _language.Strings.CouldNotAnalyze, [], null, null, null, null, null,
                AgentsSource, excerpt, errors);
        }

        var diagnosis = new JobDiagnosis(job.Name, job.Url, triage.Category, Math.Clamp(triage.Confidence, 0, 1), triage.Summary,
            triage.FailingItems ?? [], null, null, null, triage.NextStep, null, AgentsSource, excerpt, errors);
        var triageJson = JsonSerializer.Serialize(triage, JsonOptions);

        if (MayBeFlaky(triage.Category, job.History))
        {
            var flakiness = await RunAsync<FlakinessAssessment>(
                _flakiness, Crew.Flakiness, job.Name, $"Triage:\n{triageJson}\n\n{jobContext}", usage, errors, cancellationToken);

            if (flakiness?.IsFlaky == true)
            {
                return diagnosis with
                {
                    Category = FailureCategory.Flaky,
                    Explanation = flakiness.Reason,
                    RelatedToPullRequest = false,
                    Fix = flakiness.Recommendation ?? diagnosis.Fix
                };
            }

            if (flakiness is not null && triage.Category == FailureCategory.Flaky)
            {
                diagnosis = diagnosis with { Category = FailureCategory.TestFailure };
            }
        }

        if (!NeedsInvestigation(diagnosis.Category) || (diff is null && !_hasTools))
        {
            return diagnosis;
        }

        var codeContext = $"Triage:\n{triageJson}\n\n{jobContext}\n\n{DiffSection(diff)}";
        var investigation = await RunAsync<Investigation>(_investigator, Crew.Investigator, job.Name, codeContext, usage, errors, cancellationToken);
        if (investigation is null)
        {
            return diagnosis;
        }

        diagnosis = diagnosis with
        {
            Explanation = investigation.RootCause,
            File = RepositoryPath.Normalize(investigation.File),
            Line = investigation.Line is > 0 ? investigation.Line : null,
            RelatedToPullRequest = investigation.RelatedToPullRequest
        };

        if (diagnosis.File is null)
        {
            return diagnosis;
        }

        var fix = await RunAsync<FixProposal>(
            _fixer, Crew.Fixer, job.Name, $"Root cause:\n{JsonSerializer.Serialize(investigation, JsonOptions)}\n\n{codeContext}", usage, errors, cancellationToken);
        if (fix is null)
        {
            return diagnosis;
        }

        // The suggested code replaces one line, so it is only kept when it targets the line of the root cause.
        var sameLocation = (fix.File is null || RepositoryPath.Normalize(fix.File) == diagnosis.File)
            && (fix.Line is null || fix.Line == diagnosis.Line);
        return diagnosis with
        {
            Fix = fix.Description,
            SuggestedCode = sameLocation && diagnosis.Line is not null && !string.IsNullOrWhiteSpace(fix.SuggestedCode) ? fix.SuggestedCode : null
        };
    }

    private async Task<Headline> HeadlineAsync(
        FailureContext context, IReadOnlyList<JobDiagnosis> diagnoses, UsageTracker usage, CancellationToken cancellationToken)
    {
        var fallback = new Headline(FirstSentence(diagnoses.First(d => d.Explanation.Length > 0).Explanation), "");

        // Known failures are explained without a model: keep it that way for the headline.
        if (diagnoses.All(d => d.IsKnownFailure))
        {
            return fallback;
        }

        var prompt = new StringBuilder();
        if (context.PullRequestTitle is { Length: > 0 } title)
        {
            prompt.AppendLine($"Pull request: {title}");
        }

        prompt.AppendLine("Diagnosis of each failed job:");
        prompt.AppendLine(JsonSerializer.Serialize(diagnoses.Select(d => d with { Excerpt = "" }), JsonOptions));

        var headline = await RunAsync<Headline>(_synthesizer, Crew.Synthesizer, "all jobs", prompt.ToString(), usage, [], cancellationToken);
        return headline is { Title.Length: > 0 } ? headline : fallback;
    }

    private async Task<T?> RunAsync<T>(
        AIAgent agent, AgentDefinition definition, string jobName, string prompt, UsageTracker usage, List<string> errors, CancellationToken cancellationToken)
        where T : class
    {
        AgentStarted?.Invoke(definition.Name, jobName);
        using var activity = CrewTelemetry.StartAgent(definition.Name, jobName);
        var stopwatch = Stopwatch.StartNew();
        var agentTag = new KeyValuePair<string, object?>("gen_ai.agent.name", definition.Name);

        try
        {
            var response = await _retry.ExecuteAsync(
                token => agent.RunAsync<T>(prompt, serializerOptions: JsonOptions, cancellationToken: token),
                (attempt, ex) =>
                {
                    activity?.AddEvent(new ActivityEvent("retry", tags: new ActivityTagsCollection
                    {
                        ["cfc.attempt"] = attempt,
                        ["exception.message"] = ex.Message
                    }));
                    AgentRetrying?.Invoke(definition.Name, jobName, attempt, ex);
                },
                cancellationToken);

            usage.Add(response.Usage, definition.Name);
            AgentCompleted?.Invoke(definition.Name, jobName, null);
            return response.Result;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            CrewTelemetry.RecordError(activity, ex);
            CrewTelemetry.AgentFailures.Add(1, agentTag);
            errors.Add($"{definition.Name}: {ex.Message}");
            AgentCompleted?.Invoke(definition.Name, jobName, ex.Message);
            return null;
        }
        finally
        {
            CrewTelemetry.AgentDuration.Record(stopwatch.Elapsed.TotalSeconds, agentTag);
        }
    }

    private static bool MayBeFlaky(FailureCategory category, JobHistory? history) =>
        category == FailureCategory.Flaky
        || (history is { Failures: > 0 } && category is FailureCategory.TestFailure or FailureCategory.Timeout
            or FailureCategory.Infrastructure or FailureCategory.Unknown);

    private static bool NeedsInvestigation(FailureCategory category) =>
        category is FailureCategory.Compilation or FailureCategory.TestFailure or FailureCategory.Dependency
            or FailureCategory.Configuration or FailureCategory.Unknown;

    private static string JobContext(FailedJob job, FailureContext context, IReadOnlyList<string> changedFiles, string excerpt)
    {
        var text = new StringBuilder();
        text.AppendLine($"Job: {job.Name}");
        if (context.Workflow is { Length: > 0 } workflow)
        {
            text.AppendLine($"Workflow: {workflow}");
        }

        if (context.PullRequestTitle is { Length: > 0 } title)
        {
            text.AppendLine($"Pull request: {title}");
        }

        text.AppendLine(changedFiles.Count > 0
            ? $"Files changed by the pull request:\n{string.Join('\n', changedFiles.Select(f => $"- {f}"))}"
            : "No pull request diff is available.");
        text.AppendLine(job.History is { Runs: > 0 } history
            ? $"History of this job on the default branch: failed in {history.Failures} of the last {history.Runs} runs."
            : "No history of this job is available.");
        text.AppendLine().AppendLine("Log excerpt:").AppendLine(MarkdownText.Fence(excerpt, "text"));
        return text.ToString();
    }

    private static string DiffSection(string? diff) =>
        diff is null
            ? "No pull request diff is available."
            : $"Pull request diff. Each added or unchanged line starts with its line number in the new version of the file; cite those numbers:\n{MarkdownText.Fence(DiffAnnotator.WithLineNumbers(diff), "diff")}";

    private static string Truncate(string text, int maxChars) =>
        text.Length <= maxChars ? text : text[..maxChars] + "\n[diff truncated: it is larger than maxDiffChars]";

    // A sentence ends at a period followed by a space or the end ("0.99" is not an end), or at a Chinese full stop.
    private static string FirstSentence(string text)
    {
        var match = Regex.Match(text, @"[.!?](\s|$)|。");
        return match.Success ? text[..(match.Index + 1)] : text;
    }

    private AIAgent CreateAgent(IChatClient chatClient, AgentDefinition definition, IReadOnlyList<AITool>? tools = null)
    {
        var instructions = definition.Instructions + _language.Instruction;
        return tools is { Count: > 0 }
            ? new ChatClientAgent(chatClient, instructions + Crew.ToolsInstructions, definition.Name, definition.Description, [.. tools])
            : new ChatClientAgent(chatClient, instructions: instructions, name: definition.Name, description: definition.Description);
    }
}
