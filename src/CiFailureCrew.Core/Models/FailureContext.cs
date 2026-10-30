namespace CiFailureCrew.Core.Models;

/// <summary>How the same job behaved in recent runs of the workflow on the default branch.</summary>
public sealed record JobHistory(int Runs, int Failures);

/// <param name="Log">The raw job log, as downloaded from GitHub.</param>
/// <param name="Url">Link to the job on GitHub.</param>
public sealed record FailedJob(string Name, string Log, string? Url = null, JobHistory? History = null);

/// <summary>Everything the crew knows about a failed workflow run.</summary>
/// <param name="Diff">Diff of the pull request that triggered the run, when there is one.</param>
public sealed record FailureContext(IReadOnlyList<FailedJob> Jobs, string? Diff = null, string? PullRequestTitle = null, string? Workflow = null);
