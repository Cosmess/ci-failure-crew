using System.Text.RegularExpressions;
using CiFailureCrew.Core.Localization;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CiFailureCrew.Core.Configuration;

/// <summary>Per-repository settings, usually loaded from a <c>.ci-failure-crew.yml</c> file.</summary>
public sealed class CrewConfig
{
    public const string DefaultFileName = ".ci-failure-crew.yml";

    /// <summary>Language of the explanation: en (default), pt-BR or zh-CN.</summary>
    public string Language { get; set; } = CrewLanguage.English.Code;

    /// <summary>Maximum number of failed jobs analyzed per run; the others are only listed.</summary>
    public int MaxJobs { get; set; } = 3;

    /// <summary>Maximum size, in characters, of the log excerpt sent to the agents for each job.</summary>
    public int MaxLogChars { get; set; } = 20_000;

    /// <summary>Maximum size, in characters, of the pull request diff sent to the agents.</summary>
    public int MaxDiffChars { get; set; } = 40_000;

    /// <summary>Job names that are never analyzed. <c>*</c> matches any text, e.g. "deploy*".</summary>
    public List<string> IgnoreJobs { get; set; } = [];

    /// <summary>Recent runs on the default branch checked to tell flaky jobs from real failures. 0 disables it.</summary>
    public int HistoryRuns { get; set; } = 10;

    public int AgentTimeoutSeconds { get; set; } = 180;

    public int MaxRetries { get; set; } = 2;

    /// <summary>Optional model prices, used to estimate the cost of an explanation.</summary>
    public PricingConfig? Pricing { get; set; }

    public static CrewConfig Default => new();

    public static CrewConfig Parse(string yaml)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var config = deserializer.Deserialize<CrewConfig?>(yaml) ?? new CrewConfig();
        config.Validate();
        return config;
    }

    public static CrewConfig Load(string path) => Parse(File.ReadAllText(path));

    public CrewLanguage ResolveLanguage() => CrewLanguage.Parse(Language);

    public bool IsIgnored(string jobName) =>
        IgnoreJobs.Any(pattern => Regex.IsMatch(jobName, "^" + Regex.Escape(pattern).Replace(@"\*", ".*") + "$", RegexOptions.IgnoreCase));

    private void Validate()
    {
        if (MaxJobs < 1)
        {
            throw new InvalidOperationException("maxJobs must be at least 1.");
        }

        if (MaxLogChars < 1000 || MaxDiffChars < 1000)
        {
            throw new InvalidOperationException("maxLogChars and maxDiffChars must be at least 1000.");
        }

        if (HistoryRuns < 0 || AgentTimeoutSeconds <= 0 || MaxRetries < 0)
        {
            throw new InvalidOperationException("historyRuns and maxRetries cannot be negative, and agentTimeoutSeconds must be positive.");
        }

        _ = ResolveLanguage();
    }
}

/// <summary>Model prices in US dollars per one million tokens.</summary>
public sealed class PricingConfig
{
    public decimal InputPerMillion { get; set; }

    public decimal OutputPerMillion { get; set; }
}
