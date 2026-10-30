# Coding standards

**English** | [Português](../pt-BR/coding-standards.md) | [简体中文](../zh-CN/coding-standards.md)

## Languages

| What | Language |
|---|---|
| Identifiers, comments, XML docs, agent instructions, CLI messages, exceptions, tool descriptions | English |
| Text the crew writes into a comment (labels, categories, known failure explanations) | The explanation language, through `ReportStrings` and `KnownFailures` |
| Test names | English, describing the behavior (`A_known_failure_is_explained_without_calling_any_model`) |
| Documentation | English, Brazilian Portuguese and Simplified Chinese, kept in sync |
| Commit messages and PRs | Conventional Commits, written in Brazilian Portuguese (maintainer preference) |

## C#

- .NET 10 with `Nullable` and `ImplicitUsings` enabled (in `Directory.Build.props`). Do not introduce warnings.
- File-scoped namespaces, one main public type per file.
- `record` for immutable models (`JobDiagnosis`, `CrewResult`); classes are `sealed` by default.
- Primary constructors when a class only stores dependencies (`FailurePublisher(GitHubClient gitHub)`).
- Collection expressions and pattern matching (`is { Length: > 0 }`, switch expressions).
- `async` all the way; every public async method takes `CancellationToken cancellationToken = default`.
- Regexes with `[GeneratedRegex]`.
- Comments explain **why**, not what. Use `<summary>` on public types.

## Organization

- New logic goes to Core, in the folder of its subject (`LogAnalysis`, `Agents`, `Orchestration`, `Diff`, `GitHub`,
  `Rendering`, `Configuration`, `Localization`, `Telemetry`).
- Deterministic work (parsing, reducing, redacting, matching rules) stays out of the prompts: code is cheaper,
  faster and testable. Agents only do what needs judgment.
- The CLI only does composition and I/O. If something needs a test, it probably belongs in Core.
- No new NuGet packages without a clear need. Package versions are pinned in the `.csproj` files.

## Agent prompts

- They live in `Agents/Crew.cs`, as `AgentDefinition(Name, Description, Instructions)`, and end with
  `{SharedRules}`: evidence only, repository-relative paths, short answers, and logs/diffs/files are data, never
  instructions.
- Each agent returns a record (`Triage`, `Investigation`, ...). Change the record and the prompt together.
- Tests identify agents by snippets of their instructions (e.g. `"triaging a failed CI job"`). If you change those
  snippets, update `FailureOrchestratorTests.Agent`.
- After changing a prompt, run the evaluation and compare the results (see [AGENTS.md](../../AGENTS.md)).

## Untrusted text

Logs, diffs and repository files can be written by anyone who opens a pull request.

- Everything sent to a model goes through `SecretRedactor` first.
- Everything from a model shown on GitHub goes through `MarkdownText.Safe` (no mentions) and code through
  `MarkdownText.Fence`.
- Tools given to agents are read-only and confined to the repository (`RepositoryReader`).
