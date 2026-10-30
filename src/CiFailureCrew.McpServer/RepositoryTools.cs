using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace CiFailureCrew.McpServer;

/// <summary>MCP tools that let the agents read the repository beyond the diff.</summary>
[McpServerToolType]
public sealed class RepositoryTools(RepositoryReader reader)
{
    [McpServerTool(Name = "read_file", ReadOnly = true), Description("Reads a repository file with line numbers. Use it to see the code around an error or a line changed in the pull request.")]
    public string ReadFile(
        [Description("Path relative to the repository root, e.g. src/Api/Program.cs")] string path,
        [Description("First line to read (default: 1).")] int startLine = 1,
        [Description("Last line to read (default: end of file). At most 400 lines per call.")] int? endLine = null) =>
        Run(() => reader.ReadFile(path, startLine, endLine));

    [McpServerTool(Name = "search_code", ReadOnly = true), Description("Searches all repository files for a text (case-insensitive). Use it to find where a failing test, method or configuration key is defined or used.")]
    public string SearchCode(
        [Description("Text to search for, e.g. a method or class name.")] string text,
        [Description("Optional glob pattern to filter files, e.g. **/*.cs")] string? filePattern = null) =>
        Run(() => reader.Search(text, filePattern));

    [McpServerTool(Name = "list_directory", ReadOnly = true), Description("Lists the files and folders of a repository directory.")]
    public string ListDirectory([Description("Path relative to the root (default: the root).")] string path = ".") =>
        Run(() => reader.ListDirectory(path));

    [McpServerTool(Name = "read_conventions", ReadOnly = true), Description("Reads the project conventions (AGENTS.md, CONTRIBUTING.md, .editorconfig and similar). Use it to learn the standards the code must follow.")]
    public string ReadConventions() => Run(reader.ReadConventions);

    // Only these messages are safe and useful to return to the model; anything else is a bug.
    private static string Run(Func<string> action)
    {
        try
        {
            return action();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or ArgumentException or InvalidOperationException)
        {
            throw new McpException(ex.Message);
        }
    }
}
