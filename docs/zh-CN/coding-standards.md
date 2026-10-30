# 编码规范

[English](../en/coding-standards.md) | [Português](../pt-BR/coding-standards.md) | **简体中文**

## 语言

| 内容 | 语言 |
|---|---|
| 标识符、注释、XML 文档、智能体指令、CLI 消息、异常、工具描述 | 英文 |
| 智能体写入评论的固定文本（标签、分类、已知失败的解释） | 解释所用的语言，通过 `ReportStrings` 和 `KnownFailures` |
| 测试名称 | 英文，描述行为（`A_known_failure_is_explained_without_calling_any_model`） |
| 文档 | 英文、巴西葡萄牙文和简体中文，保持同步 |
| 提交信息和 PR | 遵循 Conventional Commits，使用巴西葡萄牙文撰写（维护者偏好） |

## C#

- .NET 10，启用 `Nullable` 和 `ImplicitUsings`（在 `Directory.Build.props` 中）。不要引入新的警告。
- 使用文件范围的命名空间，每个文件一个主要的公共类型。
- 不可变模型使用 `record`（`JobDiagnosis`、`CrewResult`）；类默认声明为 `sealed`。
- 当类只保存依赖项时使用主构造函数（`FailurePublisher(GitHubClient gitHub)`）。
- 使用集合表达式和模式匹配（`is { Length: > 0 }`、switch 表达式）。
- 全程使用 `async`；每个公共异步方法都接收 `CancellationToken cancellationToken = default`。
- 正则表达式使用 `[GeneratedRegex]`。
- 注释解释**为什么**，而不是做了什么。公共类型使用 `<summary>`。

## 组织方式

- 新逻辑放在 Core 中对应主题的文件夹（`LogAnalysis`、`Agents`、`Orchestration`、`Diff`、`GitHub`、`Rendering`、
  `Configuration`、`Localization`、`Telemetry`）。
- 确定性的工作（解析、精简、隐藏、匹配规则）不写进提示词：代码更便宜、更快且可测试。智能体只做需要判断的事情。
- CLI 只负责组装和 I/O。如果某段逻辑需要测试，它很可能应该放在 Core 中。
- 没有明确需要时不引入新的 NuGet 包。包版本固定在 `.csproj` 文件中。

## 智能体提示词

- 它们位于 `Agents/Crew.cs`，形式为 `AgentDefinition(Name, Description, Instructions)`，并以 `{SharedRules}` 结尾：
  只基于证据、使用相对于仓库根目录的路径、回答简短，并且日志/diff/文件都是数据，而不是指令。
- 每个智能体返回一个 record（`Triage`、`Investigation` 等）。修改 record 时同时修改提示词。
- 测试通过指令中的片段识别智能体（例如 `"triaging a failed CI job"`）。如果修改了这些片段，请更新
  `FailureOrchestratorTests.Agent`。
- 修改提示词后，运行评估并比较结果（参见 [AGENTS.md](../../AGENTS.md)）。

## 不可信的文本

日志、diff 和仓库文件可能由任何提交 Pull Request 的人编写。

- 发送给模型的所有内容都先经过 `SecretRedactor`。
- 来自模型并显示在 GitHub 上的所有内容都经过 `MarkdownText.Safe`（不会提及用户），代码经过 `MarkdownText.Fence`。
- 提供给智能体的工具是只读的，并限制在仓库范围内（`RepositoryReader`）。
