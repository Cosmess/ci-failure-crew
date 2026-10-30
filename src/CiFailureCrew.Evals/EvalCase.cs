using System.Text.Json;
using CiFailureCrew.Core.Models;

namespace CiFailureCrew.Evals;

/// <summary>
/// A labeled failed run: one <c>&lt;job&gt;.log</c> per failed job, the pull request diff (<c>diff.patch</c>, optional),
/// the job history on the default branch (<c>history.json</c>, optional) and what a good explanation says (<c>expected.json</c>).
/// </summary>
public sealed record EvalCase(string Name, FailureContext Context, ExpectedExplanation Expected)
{
    public const string DiffFileName = "diff.patch";
    public const string HistoryFileName = "history.json";
    public const string ExpectedFileName = "expected.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<EvalCase> LoadAll(string directory) =>
        Directory.EnumerateDirectories(directory)
            .Order(StringComparer.Ordinal)
            .Select(Load)
            .ToList();

    public static EvalCase Load(string caseDirectory)
    {
        var expected = Read<ExpectedExplanation>(Path.Combine(caseDirectory, ExpectedFileName))
            ?? throw new InvalidOperationException($"Empty {ExpectedFileName} in {caseDirectory}");
        var historyPath = Path.Combine(caseDirectory, HistoryFileName);
        var history = File.Exists(historyPath) ? Read<Dictionary<string, JobHistory>>(historyPath) ?? [] : [];
        var diffPath = Path.Combine(caseDirectory, DiffFileName);

        var jobs = Directory.EnumerateFiles(caseDirectory, "*.log")
            .Order(StringComparer.Ordinal)
            .Select(log =>
            {
                var name = Path.GetFileNameWithoutExtension(log);
                return new FailedJob(name, File.ReadAllText(log), History: history.GetValueOrDefault(name));
            })
            .ToList();

        var context = new FailureContext(jobs, File.Exists(diffPath) ? File.ReadAllText(diffPath) : null, Workflow: "CI");
        return new EvalCase(Path.GetFileName(caseDirectory), context, expected);
    }

    private static T? Read<T>(string path) => JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions);
}

public sealed record ExpectedExplanation(string Description, Dictionary<string, ExpectedJob> Jobs);

/// <param name="AcceptCategories">Other categories that are also right for this job.</param>
/// <param name="RootCause">Places a correct root cause may point to; any of them counts.</param>
/// <param name="Keywords">The explanation must mention at least one (accents and case are ignored).</param>
/// <param name="KnownRule">Id of the known failure rule that must explain the job without a model.</param>
public sealed record ExpectedJob(
    FailureCategory Category,
    List<FailureCategory>? AcceptCategories,
    bool? RelatedToPullRequest,
    List<ExpectedLocation>? RootCause,
    List<string>? Keywords,
    string? KnownRule);

public sealed record ExpectedLocation(string File, int Line, int Tolerance);
