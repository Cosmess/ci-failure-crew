using System.CommandLine;
using CiFailureCrew.Core.Configuration;
using CiFailureCrew.Core.Orchestration;
using CiFailureCrew.Evals;
using CiFailureCrew.Providers;

var casesOption = new Option<DirectoryInfo>("--cases")
{
    Description = "Directory of the labeled cases.",
    DefaultValueFactory = _ => new DirectoryInfo(Path.Combine("evals", "cases"))
};
var filterOption = new Option<string?>("--filter") { Description = "Only runs the cases whose name contains this text." };
var outputOption = new Option<FileInfo?>("--output") { Description = "Also saves the report as Markdown to this file." };
var languageOption = new Option<string>("--language") { Description = "Language of the explanations: en, pt-BR or zh-CN.", DefaultValueFactory = _ => "en" };
var minimumOption = new Option<double?>("--min-correct") { Description = "Exits with code 2 if the rate of fully correct jobs is below this value (0 to 1)." };

var root = new RootCommand("Runs the crew over labeled failed runs and measures how often it is right.")
{
    casesOption, filterOption, outputOption, languageOption, minimumOption
};

root.SetAction(async (parseResult, cancellationToken) =>
{
    var directory = parseResult.GetValue(casesOption)!;
    if (!directory.Exists)
    {
        Console.Error.WriteLine($"error: cases directory not found: {directory.FullName}");
        return 1;
    }

    var filter = parseResult.GetValue(filterOption);
    var cases = EvalCase.LoadAll(directory.FullName).Where(c => filter is null || c.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
    var language = parseResult.GetValue(languageOption)!;
    var orchestrator = new FailureOrchestrator(ChatClientFactory.Create(), new CrewConfig { Language = language });

    var scores = new List<JobScore>();
    long input = 0, output = 0;
    foreach (var evalCase in cases)
    {
        Console.Error.WriteLine($"▶ {evalCase.Name}");
        var result = await orchestrator.ExplainAsync(evalCase.Context, cancellationToken);
        input += result.Usage.InputTokens;
        output += result.Usage.OutputTokens;

        foreach (var score in EvalScorer.Score(evalCase, result.Report))
        {
            scores.Add(score);
            Console.Error.WriteLine($"  {(score.Correct ? "✔" : "✘")} {score.Job}: {score.Actual}{(score.Location is null ? "" : $" at {score.Location}")}");
        }
    }

    var summary = new EvalSummary(scores, input, output);
    var (provider, model) = ChatClientFactory.Selection;
    var report = EvalReportRenderer.Render(summary, $"{provider}/{model}", language);
    Console.WriteLine(report);

    if (parseResult.GetValue(outputOption) is { } file)
    {
        await File.WriteAllTextAsync(file.FullName, report, cancellationToken);
    }

    if (Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY") is { Length: > 0 } summaryPath)
    {
        await File.AppendAllTextAsync(summaryPath, report, cancellationToken);
    }

    return parseResult.GetValue(minimumOption) is { } minimum && summary.FullyCorrect < minimum ? 2 : 0;
});

return await root.Parse(args).InvokeAsync();
