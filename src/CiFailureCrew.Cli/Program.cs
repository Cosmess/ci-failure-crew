using System.CommandLine;
using CiFailureCrew.Cli;

var runOption = new Option<long?>("--run") { Description = "Id of the GitHub Actions workflow run to explain. Requires GITHUB_TOKEN." };
var repoOption = new Option<string?>("--repo")
{
    Description = "Repository in owner/name format (default: the GITHUB_REPOSITORY variable).",
    DefaultValueFactory = _ => Environment.GetEnvironmentVariable("GITHUB_REPOSITORY")
};
var postOption = new Option<bool>("--post")
{
    Description = "Posts the explanation on the pull request of the run (or marks it resolved when the run passed). Requires --run."
};
var logOption = new Option<FileInfo[]>("--log")
{
    Description = "Job log file to explain instead of a run. Repeat it for several jobs; the file name is the job name.",
    AllowMultipleArgumentsPerToken = true
};
var diffOption = new Option<FileInfo?>("--diff") { Description = "Diff of the change that made the jobs fail, used with --log." };
var configOption = new Option<FileInfo?>("--config") { Description = "Configuration file (default: .ci-failure-crew.yml in the current directory, if it exists)." };
var languageOption = new Option<string?>("--language") { Description = "Language of the explanation: en, pt-BR or zh-CN (default: the language in the configuration, or en)." };
var repoPathOption = new Option<DirectoryInfo?>("--repo-path") { Description = "Repository directory: gives the agents MCP tools to read files and search the code." };
var outputOption = new Option<FileInfo?>("--output") { Description = "Also saves the explanation as Markdown to this file." };

var root = new RootCommand("CI Failure Crew: explains why a CI workflow failed, with a crew of AI agents.")
{
    runOption, repoOption, postOption, logOption, diffOption, configOption, languageOption, repoPathOption, outputOption
};

root.SetAction((parseResult, cancellationToken) => new ExplainCommand(new ExplainCommandOptions(
    parseResult.GetValue(runOption),
    parseResult.GetValue(repoOption),
    parseResult.GetValue(postOption),
    parseResult.GetValue(logOption) ?? [],
    parseResult.GetValue(diffOption),
    parseResult.GetValue(configOption),
    parseResult.GetValue(languageOption),
    parseResult.GetValue(repoPathOption),
    parseResult.GetValue(outputOption))).RunAsync(cancellationToken));

using var telemetry = TelemetrySetup.StartIfConfigured();
return await root.Parse(args).InvokeAsync();
