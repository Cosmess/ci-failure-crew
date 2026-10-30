using System.Net;
using System.Text.RegularExpressions;
using CiFailureCrew.Core.Diff;
using CiFailureCrew.Core.Localization;
using CiFailureCrew.Core.Models;
using CiFailureCrew.Core.Rendering;

namespace CiFailureCrew.Core.GitHub;

/// <summary>
/// Keeps one comment per workflow on the pull request, updated on every run instead of piling up new
/// comments. A hidden marker in the first line identifies the workflow, the commit and what was posted.
/// Fixes that fit on a line of the diff are also posted as review suggestions, once per commit.
/// </summary>
public sealed partial class FailurePublisher(GitHubClient gitHub)
{
    // GitHub rejects comments longer than 65536 characters.
    private const int MaxBodyLength = 65_000;

    public sealed record Marker(long WorkflowId, string Sha, bool Suggestions, bool Resolved)
    {
        public static string Prefix(long workflowId) => $"<!-- ci-failure-crew:workflow={workflowId} ";

        public override string ToString() =>
            $"{Prefix(WorkflowId)}sha={Sha}{(Suggestions ? " suggestions=1" : "")}{(Resolved ? " resolved=1" : "")} -->";

        public static Marker? Parse(string body) =>
            MarkerPattern().Match(body) is { Success: true } m
                ? new Marker(long.Parse(m.Groups["workflow"].Value), m.Groups["sha"].Value, m.Groups["suggestions"].Success, m.Groups["resolved"].Success)
                : null;
    }

    /// <returns>The URL of the comment.</returns>
    public async Task<string> PublishAsync(
        string repository, int pullRequest, long workflowId, string headSha, string? diff, CrewResult result, CancellationToken cancellationToken = default)
    {
        var language = CrewLanguage.Parse(result.Language);
        var existing = await gitHub.FindCommentAsync(repository, pullRequest, Marker.Prefix(workflowId), cancellationToken);
        var previous = existing is null ? null : Marker.Parse(existing.Body);
        var suggestions = SelectSuggestions(diff, result.Report.Jobs);

        // Suggestions are posted once per commit: a re-run of the same commit only updates the comment.
        var alreadyPosted = previous is { Suggestions: true } && previous.Sha == headSha;
        var posted = alreadyPosted;
        if (suggestions.Count > 0 && !alreadyPosted)
        {
            posted = await TryPostSuggestionsAsync(repository, pullRequest, headSha, suggestions, language, cancellationToken);
        }

        var marker = new Marker(workflowId, headSha, posted && suggestions.Count > 0, Resolved: false);
        var body = Body(marker, MarkdownRenderer.Render(result, posted ? suggestions : null), language);

        return existing is null
            ? await gitHub.CreateCommentAsync(repository, pullRequest, body, cancellationToken)
            : await gitHub.UpdateCommentAsync(repository, existing.Id, body, cancellationToken);
    }

    /// <summary>When the workflow passes again, marks the comment as resolved and folds the old diagnosis.</summary>
    /// <returns>The URL of the comment, or null when there was nothing to resolve.</returns>
    public async Task<string?> ResolveAsync(
        string repository, int pullRequest, long workflowId, string headSha, string language, CancellationToken cancellationToken = default)
    {
        var existing = await gitHub.FindCommentAsync(repository, pullRequest, Marker.Prefix(workflowId), cancellationToken);
        if (existing is null || Marker.Parse(existing.Body) is not { } previous || previous.Resolved)
        {
            return null;
        }

        var strings = CrewLanguage.Parse(language).Strings;
        var diagnosis = existing.Body[(existing.Body.IndexOf('\n') + 1)..].Replace(MarkdownRenderer.Heading, "").Trim();
        var content = $"{MarkdownRenderer.Heading}\n\n{string.Format(strings.Resolved, $"`{headSha[..Math.Min(7, headSha.Length)]}`")}\n\n"
            + $"<details><summary>{strings.PreviousDiagnosis}</summary>\n\n{diagnosis}\n\n</details>\n";

        var body = Body(previous with { Sha = headSha, Resolved = true }, content, CrewLanguage.Parse(language));
        return await gitHub.UpdateCommentAsync(repository, existing.Id, body, cancellationToken);
    }

    /// <summary>Diagnoses with suggested code on a line GitHub accepts review comments on (a line of the diff).</summary>
    public static HashSet<JobDiagnosis> SelectSuggestions(string? diff, IEnumerable<JobDiagnosis> jobs)
    {
        if (string.IsNullOrEmpty(diff))
        {
            return [];
        }

        var commentable = DiffParser.Parse(diff).ToDictionary(f => f.Path, f => f.CommentableLines);
        return jobs
            .Where(j => j.SuggestedCode is { Length: > 0 } && j.File is { } file && j.Line is { } line
                && commentable.TryGetValue(file, out var lines) && lines.Contains(line))
            .DistinctBy(j => (j.File, j.Line))
            .ToHashSet();
    }

    private async Task<bool> TryPostSuggestionsAsync(
        string repository, int pullRequest, string headSha, IReadOnlySet<JobDiagnosis> suggestions, CrewLanguage language, CancellationToken cancellationToken)
    {
        var comments = suggestions
            .Select(j => new ReviewComment(j.File!, j.Line!.Value, MarkdownRenderer.RenderSuggestion(j, language.Code)))
            .ToList();

        try
        {
            await gitHub.CreateReviewAsync(repository, pullRequest, headSha, language.Strings.SuggestionsReview, comments, cancellationToken);
            return true;
        }
        catch (GitHubApiException ex) when (ex.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            // GitHub refuses a line it does not consider part of the diff: the fix stays in the comment instead.
            return false;
        }
    }

    private static string Body(Marker marker, string content, CrewLanguage language)
    {
        var body = $"{marker}\n{content}";
        return body.Length <= MaxBodyLength ? body : body[..MaxBodyLength] + $"\n\n_{language.Strings.CommentTruncated}_";
    }

    [GeneratedRegex(@"^<!-- ci-failure-crew:workflow=(?<workflow>\d+) sha=(?<sha>[0-9a-fA-F]+)(?<suggestions> suggestions=1)?(?<resolved> resolved=1)? -->")]
    private static partial Regex MarkerPattern();
}
