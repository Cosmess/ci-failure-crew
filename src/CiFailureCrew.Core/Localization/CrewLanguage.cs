using System.Globalization;
using CiFailureCrew.Core.Models;

namespace CiFailureCrew.Core.Localization;

/// <summary>
/// A language the explanation can be written in. The agents receive their instructions in English and are
/// asked to write in this language; the text the crew writes itself is translated in <see cref="ReportStrings"/>.
/// </summary>
public sealed record CrewLanguage(string Code, string EnglishName, CultureInfo Culture, ReportStrings Strings)
{
    public static readonly CrewLanguage English = new("en", "English", CultureInfo.GetCultureInfo("en-US"), ReportStrings.English);
    public static readonly CrewLanguage PortugueseBrazil = new("pt-BR", "Brazilian Portuguese", CultureInfo.GetCultureInfo("pt-BR"), ReportStrings.PortugueseBrazil);
    public static readonly CrewLanguage ChineseSimplified = new("zh-CN", "Simplified Chinese", CultureInfo.GetCultureInfo("zh-CN"), ReportStrings.ChineseSimplified);

    public static IReadOnlyList<CrewLanguage> All { get; } = [English, PortugueseBrazil, ChineseSimplified];

    /// <summary>Appended to every agent's instructions.</summary>
    public string Instruction =>
        $"\nWrite every explanation, summary and recommendation in {EnglishName}. "
        + "Keep code, identifiers, test names, file paths and error messages unchanged.";

    /// <summary>Accepts the exact codes and the short forms "pt" and "zh".</summary>
    public static CrewLanguage Parse(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return English;
        }

        var normalized = code.Trim().ToLowerInvariant();
        return All.FirstOrDefault(l => l.Code.Equals(normalized, StringComparison.OrdinalIgnoreCase) || l.Code[..2] == normalized)
            ?? throw new InvalidOperationException(
                $"Unsupported language '{code}'. Supported languages: {string.Join(", ", All.Select(l => l.Code))}.");
    }
}

/// <summary>Every piece of text the crew writes itself (the rest comes from the model).</summary>
public sealed record ReportStrings
{
    public required IReadOnlyDictionary<FailureCategory, string> Categories { get; init; }
    public required string Logs { get; init; }
    public required string Location { get; init; }
    public required string Failing { get; init; }
    public required string Origin { get; init; }
    public required string CausedByPullRequest { get; init; }
    public required string NotCausedByPullRequest { get; init; }
    public required string SuggestedFix { get; init; }
    public required string InlineLegend { get; init; }
    public required string LogExcerpt { get; init; }
    public required string Confidence { get; init; }
    public required string KnownPattern { get; init; }
    public required string AgentsFailed { get; init; }
    public required string CouldNotAnalyze { get; init; }
    public required string SkippedJobs { get; init; }
    public required string NoFailedJobs { get; init; }
    public required string Resolved { get; init; }
    public required string PreviousDiagnosis { get; init; }
    public required string TokenUsage { get; init; }
    public required string EstimatedCost { get; init; }
    public required string CommentTruncated { get; init; }

    public string Category(FailureCategory category) => Categories[category];

    public static readonly ReportStrings English = new()
    {
        Categories = new Dictionary<FailureCategory, string>
        {
            [FailureCategory.Compilation] = "🔨 Compilation error",
            [FailureCategory.TestFailure] = "🧪 Test failure",
            [FailureCategory.Flaky] = "🎲 Flaky test",
            [FailureCategory.Dependency] = "📦 Dependency problem",
            [FailureCategory.Infrastructure] = "🏗️ Infrastructure problem",
            [FailureCategory.Timeout] = "⏱️ Timeout",
            [FailureCategory.Configuration] = "⚙️ Configuration problem",
            [FailureCategory.Unknown] = "❓ Unknown cause"
        },
        Logs = "logs",
        Location = "Location",
        Failing = "Failing",
        Origin = "Origin",
        CausedByPullRequest = "probably caused by this pull request",
        NotCausedByPullRequest = "probably not caused by this pull request",
        SuggestedFix = "Suggested fix",
        InlineLegend = "💬 = a suggested change was posted on the line of code.",
        LogExcerpt = "Log excerpt",
        Confidence = "Confidence: {0}",
        KnownPattern = "Known failure pattern ({0}), explained without AI.",
        AgentsFailed = "Some agents failed and this diagnosis may be incomplete: {0}",
        CouldNotAnalyze = "The crew could not analyze this job. The log excerpt below shows where it failed.",
        SkippedJobs = "Not analyzed: {0}",
        NoFailedJobs = "No failed jobs were found in this run.",
        Resolved = "✅ The workflow passed on commit {0}. The previous diagnosis is kept below for reference.",
        PreviousDiagnosis = "Previous diagnosis",
        TokenUsage = "Tokens: {0} input / {1} output",
        EstimatedCost = "estimated cost: US$ {0}",
        CommentTruncated = "[truncated by GitHub's comment size limit]"
    };

    public static readonly ReportStrings PortugueseBrazil = new()
    {
        Categories = new Dictionary<FailureCategory, string>
        {
            [FailureCategory.Compilation] = "🔨 Erro de compilação",
            [FailureCategory.TestFailure] = "🧪 Teste falhou",
            [FailureCategory.Flaky] = "🎲 Teste instável",
            [FailureCategory.Dependency] = "📦 Problema de dependência",
            [FailureCategory.Infrastructure] = "🏗️ Problema de infraestrutura",
            [FailureCategory.Timeout] = "⏱️ Tempo esgotado",
            [FailureCategory.Configuration] = "⚙️ Problema de configuração",
            [FailureCategory.Unknown] = "❓ Causa desconhecida"
        },
        Logs = "logs",
        Location = "Local",
        Failing = "Falhando",
        Origin = "Origem",
        CausedByPullRequest = "provavelmente causado por este pull request",
        NotCausedByPullRequest = "provavelmente não causado por este pull request",
        SuggestedFix = "Correção sugerida",
        InlineLegend = "💬 = uma alteração sugerida foi publicada na linha do código.",
        LogExcerpt = "Trecho do log",
        Confidence = "Confiança: {0}",
        KnownPattern = "Padrão de falha conhecido ({0}), explicado sem IA.",
        AgentsFailed = "Alguns agentes falharam e este diagnóstico pode estar incompleto: {0}",
        CouldNotAnalyze = "A equipe não conseguiu analisar este job. O trecho do log abaixo mostra onde ele falhou.",
        SkippedJobs = "Não analisados: {0}",
        NoFailedJobs = "Nenhum job com falha foi encontrado nesta execução.",
        Resolved = "✅ O workflow passou no commit {0}. O diagnóstico anterior continua abaixo para referência.",
        PreviousDiagnosis = "Diagnóstico anterior",
        TokenUsage = "Tokens: {0} de entrada / {1} de saída",
        EstimatedCost = "custo estimado: US$ {0}",
        CommentTruncated = "[truncado pelo limite de tamanho de comentários do GitHub]"
    };

    public static readonly ReportStrings ChineseSimplified = new()
    {
        Categories = new Dictionary<FailureCategory, string>
        {
            [FailureCategory.Compilation] = "🔨 编译错误",
            [FailureCategory.TestFailure] = "🧪 测试失败",
            [FailureCategory.Flaky] = "🎲 不稳定的测试",
            [FailureCategory.Dependency] = "📦 依赖问题",
            [FailureCategory.Infrastructure] = "🏗️ 基础设施问题",
            [FailureCategory.Timeout] = "⏱️ 超时",
            [FailureCategory.Configuration] = "⚙️ 配置问题",
            [FailureCategory.Unknown] = "❓ 原因未知"
        },
        Logs = "日志",
        Location = "位置",
        Failing = "失败项",
        Origin = "来源",
        CausedByPullRequest = "很可能由此 Pull Request 引起",
        NotCausedByPullRequest = "很可能与此 Pull Request 无关",
        SuggestedFix = "建议的修复",
        InlineLegend = "💬 = 已在对应代码行发布修改建议。",
        LogExcerpt = "日志摘录",
        Confidence = "置信度：{0}",
        KnownPattern = "已知的失败模式（{0}），未使用 AI 进行解释。",
        AgentsFailed = "部分智能体执行失败，此诊断可能不完整：{0}",
        CouldNotAnalyze = "未能分析此任务。下方的日志摘录显示了失败的位置。",
        SkippedJobs = "未分析：{0}",
        NoFailedJobs = "此次运行中没有找到失败的任务。",
        Resolved = "✅ 工作流已在提交 {0} 上通过。之前的诊断保留在下方供参考。",
        PreviousDiagnosis = "之前的诊断",
        TokenUsage = "Token：输入 {0} / 输出 {1}",
        EstimatedCost = "预估费用：US$ {0}",
        CommentTruncated = "[内容因 GitHub 评论长度限制被截断]"
    };
}
