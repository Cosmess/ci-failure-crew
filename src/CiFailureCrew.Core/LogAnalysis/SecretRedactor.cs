using System.Text.RegularExpressions;

namespace CiFailureCrew.Core.LogAnalysis;

/// <summary>
/// Hides secrets that GitHub did not mask (it only masks the values of registered secrets) before a
/// log reaches the model or a comment: tokens, keys, private keys and passwords in connection strings.
/// </summary>
public static partial class SecretRedactor
{
    public const string Mask = "[REDACTED]";

    public static string Redact(string text)
    {
        text = PrivateKey().Replace(text, Mask);
        text = KnownToken().Replace(text, Mask);
        text = BearerToken().Replace(text, m => m.Groups["scheme"].Value + Mask);
        return Assignment().Replace(text, m => m.Groups["name"].Value + m.Groups["separator"].Value + Mask);
    }

    [GeneratedRegex(@"-----BEGIN [A-Z ]*PRIVATE KEY-----[\s\S]*?-----END [A-Z ]*PRIVATE KEY-----")]
    private static partial Regex PrivateKey();

    // GitHub, OpenAI/Anthropic, AWS, Slack, Google API keys, npm tokens and JSON web tokens.
    [GeneratedRegex(
        @"\b(gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{30,}|sk-[A-Za-z0-9_-]{20,}|AKIA[0-9A-Z]{16}|"
        + @"xox[abprs]-[A-Za-z0-9-]{10,}|AIza[0-9A-Za-z_-]{35}|npm_[A-Za-z0-9]{36}|eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,})")]
    private static partial Regex KnownToken();

    [GeneratedRegex(@"(?<scheme>\b(Bearer|Basic|token)\s+)[A-Za-z0-9._~+/-]{16,}=*", RegexOptions.IgnoreCase)]
    private static partial Regex BearerToken();

    // password=..., Password: ..., API_KEY=..., client_secret="..." (connection strings, env dumps, CLI flags).
    [GeneratedRegex(
        @"(?<name>\b[\w.-]*(password|passwd|pwd|secret|api[_-]?key|access[_-]?key|token)[\w.-]*)(?<separator>\s*[=:]\s*[""']?)(?!\[REDACTED\])[^\s;""',]{4,}",
        RegexOptions.IgnoreCase)]
    private static partial Regex Assignment();
}
