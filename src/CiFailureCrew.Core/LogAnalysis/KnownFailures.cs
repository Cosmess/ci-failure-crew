using System.Text.RegularExpressions;
using CiFailureCrew.Core.Models;

namespace CiFailureCrew.Core.LogAnalysis;

/// <summary>Text of a known failure in one language.</summary>
public sealed record KnownFailureText(string Explanation, string Fix);

/// <summary>
/// A failure recognized by a fixed pattern, explained without calling a model:
/// it is cheaper, faster and these cases are never caused by the pull request.
/// </summary>
public sealed record KnownFailure(string Id, FailureCategory Category, Regex Pattern, IReadOnlyDictionary<string, KnownFailureText> Texts)
{
    /// <summary>The text in the requested language, falling back to English.</summary>
    public KnownFailureText Text(string language) => Texts.TryGetValue(language, out var text) ? text : Texts["en"];
}

public static class KnownFailures
{
    public static IReadOnlyList<KnownFailure> All { get; } =
    [
        new("job-timeout", FailureCategory.Timeout,
            new Regex(@"has exceeded the maximum execution time of \d+ minutes", RegexOptions.IgnoreCase),
            Texts(
                ("The job was cancelled because it exceeded its maximum execution time (timeout-minutes).",
                 "Look for a step that hangs (a test waiting forever, an interactive prompt) or raise timeout-minutes if the job is legitimately slow."),
                ("O job foi cancelado porque passou do tempo máximo de execução (timeout-minutes).",
                 "Procure um passo travado (um teste esperando para sempre, um prompt interativo) ou aumente o timeout-minutes se o job for lento de verdade."),
                ("该任务超过了最长执行时间（timeout-minutes），已被取消。",
                 "检查是否有卡住的步骤（例如无限等待的测试或交互式提示），如果任务确实耗时较长，请增大 timeout-minutes。"))),

        new("runner-lost", FailureCategory.Infrastructure,
            new Regex(@"The runner has received a shutdown signal|lost communication with the server|The hosted runner encountered an error", RegexOptions.IgnoreCase),
            Texts(
                ("The runner stopped or lost connection to GitHub while the job was running. This is an infrastructure problem, not the code.",
                 "Re-run the failed jobs. If it keeps happening, check whether the job uses too much memory or CPU for the runner."),
                ("O runner parou ou perdeu a conexão com o GitHub durante o job. É um problema de infraestrutura, não do código.",
                 "Rode de novo os jobs que falharam. Se continuar acontecendo, veja se o job usa memória ou CPU demais para o runner."),
                ("任务运行期间，运行器停止或与 GitHub 断开了连接。这是基础设施问题，与代码无关。",
                 "重新运行失败的任务。如果问题持续出现，请检查任务是否占用了超出运行器能力的内存或 CPU。"))),

        new("disk-full", FailureCategory.Infrastructure,
            new Regex(@"No space left on device|System\.IO\.IOException: There is not enough space on the disk", RegexOptions.IgnoreCase),
            Texts(
                ("The runner ran out of disk space.",
                 "Clean up large build outputs or Docker images in the job, or use a runner with more disk."),
                ("O runner ficou sem espaço em disco.",
                 "Limpe saídas grandes de build ou imagens Docker no job, ou use um runner com mais disco."),
                ("运行器的磁盘空间已用完。",
                 "在任务中清理较大的构建产物或 Docker 镜像，或改用磁盘更大的运行器。"))),

        new("out-of-memory", FailureCategory.Infrastructure,
            new Regex(@"exit code 137\b|Killed\s+\S*dotnet|JavaScript heap out of memory", RegexOptions.IgnoreCase),
            Texts(
                ("The process was killed for using too much memory (exit code 137 or heap out of memory).",
                 "Reduce the memory use of the build or tests (fewer parallel workers, smaller test data) or use a larger runner."),
                ("O processo foi encerrado por usar memória demais (código de saída 137 ou falta de heap).",
                 "Reduza o uso de memória do build ou dos testes (menos workers em paralelo, dados de teste menores) ou use um runner maior."),
                ("进程因占用内存过多而被终止（退出码 137 或堆内存不足）。",
                 "降低构建或测试的内存占用（减少并行数、缩小测试数据），或改用更大的运行器。"))),

        new("package-feed-unreachable", FailureCategory.Dependency,
            new Regex(@"error NU1301|Unable to load the service index for source|npm ERR! code (ETIMEDOUT|ECONNRESET|EAI_AGAIN)", RegexOptions.IgnoreCase),
            Texts(
                ("The package feed could not be reached while restoring dependencies. This is usually a temporary network or registry outage.",
                 "Re-run the failed jobs. If it fails again, check the feed URL and its credentials in nuget.config or .npmrc."),
                ("Não foi possível acessar o feed de pacotes ao restaurar as dependências. Normalmente é uma instabilidade temporária de rede ou do registro.",
                 "Rode de novo os jobs que falharam. Se falhar outra vez, confira a URL do feed e as credenciais no nuget.config ou .npmrc."),
                ("还原依赖时无法访问包源。这通常是网络或包注册表的临时故障。",
                 "重新运行失败的任务。如果仍然失败，请检查 nuget.config 或 .npmrc 中的源地址和凭据。"))),

        new("rate-limited", FailureCategory.Infrastructure,
            new Regex(@"API rate limit exceeded|429 Too Many Requests|You have reached your pull rate limit", RegexOptions.IgnoreCase),
            Texts(
                ("An external service rejected the requests because of its rate limit.",
                 "Re-run later, authenticate the requests (for example with GITHUB_TOKEN or a Docker Hub login) or cache what is downloaded."),
                ("Um serviço externo recusou as requisições por limite de taxa.",
                 "Rode de novo mais tarde, autentique as requisições (por exemplo com o GITHUB_TOKEN ou login no Docker Hub) ou faça cache do que é baixado."),
                ("外部服务因请求频率限制拒绝了请求。",
                 "稍后重新运行，为请求添加身份验证（例如使用 GITHUB_TOKEN 或登录 Docker Hub），或缓存需要下载的内容。")))
    ];

    /// <summary>The first known failure found in the log, or null.</summary>
    public static KnownFailure? Match(string log) => All.FirstOrDefault(rule => rule.Pattern.IsMatch(log));

    private static Dictionary<string, KnownFailureText> Texts(
        (string Explanation, string Fix) english, (string Explanation, string Fix) portuguese, (string Explanation, string Fix) chinese) => new()
    {
        ["en"] = new(english.Explanation, english.Fix),
        ["pt-BR"] = new(portuguese.Explanation, portuguese.Fix),
        ["zh-CN"] = new(chinese.Explanation, chinese.Fix)
    };
}
