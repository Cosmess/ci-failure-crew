using System.Text;
using Microsoft.Extensions.FileSystemGlobbing;

namespace CiFailureCrew.McpServer;

/// <summary>
/// Read-only access to a repository, confined to its root directory. The callers are LLM agents
/// reading untrusted pull request content, so every path is treated as hostile: no escaping the root,
/// no symbolic links, no .git directory and no files that usually hold secrets.
/// </summary>
public sealed class RepositoryReader
{
    public const int MaxLinesPerRead = 400;
    public const int MaxSearchResults = 50;
    private const long MaxFileBytes = 1_000_000;
    private const int MaxConventionChars = 20_000;

    private static readonly string[] SkippedDirectories = [".git", "bin", "obj", "node_modules", ".vs", ".idea"];

    private static readonly Matcher SecretFiles = CreateSecretMatcher();

    private static readonly string[] ConventionFiles =
    [
        "AGENTS.md", "CLAUDE.md", "CONTRIBUTING.md", ".github/CONTRIBUTING.md", ".github/copilot-instructions.md",
        "STYLEGUIDE.md", "docs/CONTRIBUTING.md", ".editorconfig"
    ];

    public RepositoryReader(string root)
    {
        Root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        if (!Directory.Exists(Root))
        {
            throw new DirectoryNotFoundException($"Repository directory not found: {root}");
        }
    }

    public string Root { get; }

    public string ReadFile(string path, int startLine = 1, int? endLine = null)
    {
        var fullPath = Resolve(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"File not found: {path}");
        }

        var lines = ReadTextLines(fullPath, path);
        startLine = Math.Max(1, startLine);
        var last = Math.Min(lines.Length, endLine ?? lines.Length);
        last = Math.Min(last, startLine + MaxLinesPerRead - 1);

        var text = new StringBuilder();
        for (var i = startLine; i <= last; i++)
        {
            text.Append(i.ToString().PadLeft(5)).Append("| ").AppendLine(lines[i - 1]);
        }

        if (last < (endLine ?? lines.Length))
        {
            text.AppendLine($"[... more lines: use startLine={last + 1} to continue; the file has {lines.Length} lines ...]");
        }

        return text.Length > 0 ? text.ToString() : $"[{path} has no lines in the requested range; the file has {lines.Length} lines]";
    }

    public string ListDirectory(string path = ".")
    {
        var fullPath = Resolve(path);
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {path}");
        }

        var entries = new DirectoryInfo(fullPath).EnumerateFileSystemInfos()
            .Where(e => !IsHidden(Relative(e.FullName)))
            .OrderBy(e => e is FileInfo)
            .ThenBy(e => e.Name, StringComparer.Ordinal)
            .Take(200)
            .Select(e => e is DirectoryInfo ? e.Name + "/" : e.Name);

        return string.Join('\n', entries);
    }

    public string Search(string text, string? filePattern = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Provide the text to search for.");
        }

        var matcher = filePattern is { Length: > 0 } ? new Matcher(StringComparison.OrdinalIgnoreCase).AddInclude(filePattern) : null;
        var results = new List<string>();

        foreach (var file in EnumerateFiles(Root))
        {
            var relative = Relative(file);
            if (matcher is not null && !matcher.Match(relative).HasMatches)
            {
                continue;
            }

            string[] lines;
            try
            {
                lines = ReadTextLines(file, relative);
            }
            catch (InvalidOperationException)
            {
                continue; // binary or too large
            }

            for (var i = 0; i < lines.Length && results.Count < MaxSearchResults; i++)
            {
                if (lines[i].Contains(text, StringComparison.OrdinalIgnoreCase))
                {
                    var line = lines[i].Trim();
                    results.Add($"{relative}:{i + 1}: {(line.Length > 200 ? line[..200] + "..." : line)}");
                }
            }

            if (results.Count >= MaxSearchResults)
            {
                results.Add($"[... limit of {MaxSearchResults} results reached ...]");
                break;
            }
        }

        return results.Count > 0 ? string.Join('\n', results) : $"No results for \"{text}\".";
    }

    public string ReadConventions()
    {
        var text = new StringBuilder();
        foreach (var candidate in ConventionFiles)
        {
            var fullPath = Path.Combine(Root, candidate);
            if (!File.Exists(fullPath) || IsLinkOnPath(fullPath))
            {
                continue;
            }

            text.AppendLine($"===== {candidate} =====").AppendLine(File.ReadAllText(fullPath)).AppendLine();
            if (text.Length >= MaxConventionChars)
            {
                return text.ToString(0, MaxConventionChars) + "\n[... conventions truncated ...]";
            }
        }

        return text.Length > 0 ? text.ToString() : "No convention files found (AGENTS.md, CONTRIBUTING.md, .editorconfig...).";
    }

    private string Resolve(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path))
        {
            throw new UnauthorizedAccessException("Use a path relative to the repository root.");
        }

        var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(Root, path)));
        if (fullPath != Root && !fullPath.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException($"Access denied: {path} is outside the repository.");
        }

        if (fullPath != Root && IsHidden(Relative(fullPath)))
        {
            throw new UnauthorizedAccessException($"Access denied: {path} is a protected file.");
        }

        if (IsLinkOnPath(fullPath))
        {
            throw new UnauthorizedAccessException($"Access denied: {path} goes through a symbolic link.");
        }

        return fullPath;
    }

    private bool IsLinkOnPath(string fullPath)
    {
        for (var current = fullPath; current.Length > Root.Length; current = Path.GetDirectoryName(current)!)
        {
            FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            if (info.Exists && info.LinkTarget is not null)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsHidden(string relativePath)
    {
        var segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Contains(".git") || SecretFiles.Match(relativePath).HasMatches;
    }

    private IEnumerable<string> EnumerateFiles(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory).Order(StringComparer.Ordinal))
        {
            if (!IsHidden(Relative(file)) && new FileInfo(file).LinkTarget is null)
            {
                yield return file;
            }
        }

        foreach (var child in Directory.EnumerateDirectories(directory).Order(StringComparer.Ordinal))
        {
            var info = new DirectoryInfo(child);
            if (SkippedDirectories.Contains(info.Name) || info.LinkTarget is not null)
            {
                continue;
            }

            foreach (var file in EnumerateFiles(child))
            {
                yield return file;
            }
        }
    }

    private static string[] ReadTextLines(string fullPath, string displayPath)
    {
        if (new FileInfo(fullPath).Length > MaxFileBytes)
        {
            throw new InvalidOperationException($"{displayPath} is too large to read (1 MB limit).");
        }

        var content = File.ReadAllText(fullPath);
        if (content.Contains('\0'))
        {
            throw new InvalidOperationException($"{displayPath} is a binary file.");
        }

        return content.ReplaceLineEndings("\n").Split('\n');
    }

    private string Relative(string fullPath) => Path.GetRelativePath(Root, fullPath).Replace('\\', '/');

    private static Matcher CreateSecretMatcher()
    {
        var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        matcher.AddIncludePatterns([
            "**/.env", "**/.env.*", "**/*.pem", "**/*.key", "**/*.pfx", "**/*.p12", "**/*.jks", "**/*.keystore",
            "**/id_rsa*", "**/id_ed25519*", "**/secrets.json", "**/*.secrets.json", "**/.npmrc", "**/.pypirc",
            "**/.netrc", "**/credentials", "**/*.tfstate", "**/*.tfvars"
        ]);
        return matcher;
    }
}
