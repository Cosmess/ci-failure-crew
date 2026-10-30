using System.Text.RegularExpressions;

namespace CiFailureCrew.Core.Orchestration;

/// <summary>Turns the absolute paths runners print into paths relative to the repository root.</summary>
public static partial class RepositoryPath
{
    public static string? Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var relative = RunnerWorkspace().Replace(path.Trim(), "").Replace('\\', '/');
        return relative.StartsWith("./", StringComparison.Ordinal) ? relative[2..] : relative;
    }

    // /home/runner/work/<repo>/<repo>/, /Users/runner/work/<repo>/<repo>/ and D:\a\<repo>\<repo>\
    [GeneratedRegex(@"^(/home/runner/work|/Users/runner/work|/__w)/[^/]+/[^/]+/|^[A-Za-z]:\\a\\[^\\]+\\[^\\]+\\")]
    private static partial Regex RunnerWorkspace();
}
