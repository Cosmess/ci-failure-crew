# AGENTS.md

Instructions for AI agents (Claude Code, Codex, Copilot, Cursor, etc.) working on this repository.
Read this whole file before changing anything. Details are in [`docs/`](docs/en/README.md).

## What the project is

**CI Failure Crew** explains failed CI runs with a crew of AI agents in .NET 10. Deterministic code reduces the job
logs, redacts secrets and explains known failures; then a triage agent classifies the failure, a flakiness agent or
an investigator plus a fixer find the cause and the fix, and a synthesizer writes the headline. The result is posted
as one comment per workflow on the pull request, with GitHub suggestions for one-line fixes. It is published as a
GitHub Action that runs on `workflow_run`.

## Commands

```bash
dotnet build                                  # builds the solution
dotnet test tests/CiFailureCrew.Tests         # unit tests (fast, no network)
dotnet test                                   # everything; integration tests are skipped without OPENAI_API_KEY
dotnet run --project src/CiFailureCrew.Cli -- --log samples/test.log --diff samples/test.diff   # needs OPENAI_API_KEY
dotnet run --project src/CiFailureCrew.Cli -- --help
dotnet run --project src/CiFailureCrew.Evals                                                    # needs OPENAI_API_KEY
```

## Mandatory rules

1. **Commit messages follow Conventional Commits and are written in Brazilian Portuguese** (maintainer preference).
   E.g. `feat(agentes): adiciona regra para falha de cache`. Types: `feat`, `fix`, `docs`, `test`, `refactor`,
   `perf`, `ci`, `chore`. The body explains why.
2. **No AI attribution** in commits or PRs: no `Co-Authored-By` lines for assistants, no "Generated with ...".
   Commits use the git identity configured on the machine.
3. **Languages:** code, comments, agent instructions, CLI messages and errors in English. Text the crew writes into
   a comment goes through `ReportStrings` or `KnownFailures` and must exist in `en`, `pt-BR` and `zh-CN`.
4. **Documentation exists in three languages.** When behavior changes, update `README.md`, `README.pt-BR.md`,
   `README.zh-CN.md` and the matching pages in `docs/en`, `docs/pt-BR` and `docs/zh-CN`. ADRs are English only.
5. **Tests come with every behavior change.** Unit tests never call an LLM or the network: use `FakeChatClient` and
   `FakeHttpHandler`. Run `dotnet test` before every commit; everything must pass.
6. **Deterministic first.** Parsing, reducing logs, redacting and matching known failures are code, not prompts.
   Agents only do what needs judgment.
7. **Core does not depend on an LLM provider.** `CiFailureCrew.Core` only knows `IChatClient`. Provider SDKs live
   in `CiFailureCrew.Providers`.
8. **An agent failure never takes the explanation down.** Every model call goes through `RetryPolicy`; errors go to
   `JobDiagnosis.Errors` and the diagnosis keeps what the previous agents found.
9. **Logs, diffs and files are untrusted.** Everything sent to a model goes through `SecretRedactor`; everything
   from a model shown on GitHub goes through `MarkdownText.Safe`; tools are read-only and confined to the
   repository. The Action never checks out or runs pull request code.
10. **Secrets only through environment variables.** Never write keys in code, tests, samples, eval cases or logs,
    not even fake keys in a real provider's format (GitHub push protection blocks them): build them at runtime.
11. **Changed prompts, agents or rules? Run the evaluation** (`src/CiFailureCrew.Evals`) and compare the results
    before and after. New kinds of failures get a case in `evals/cases/`.
12. **`.log` files are ignored by the .NET `.gitignore`**, except in `samples/` and `evals/cases/`. Folders named
    `Logs` are ignored too, which is why the log code lives in `LogAnalysis`.

## Code map

| Path | Responsibility |
|---|---|
| `src/CiFailureCrew.Core/LogAnalysis/` | `LogCleaner`, `LogExcerpt`, `SecretRedactor`, `KnownFailures` |
| `src/CiFailureCrew.Core/Agents/` | Agent definitions (`Crew.cs`) |
| `src/CiFailureCrew.Core/Orchestration/` | `FailureOrchestrator` (routing per job), `RetryPolicy`, token usage, `RepositoryPath` |
| `src/CiFailureCrew.Core/Diff/` | Unified diff parser and `DiffAnnotator` (line numbers for the agents) |
| `src/CiFailureCrew.Core/GitHub/` | `GitHubClient` and `FailurePublisher` (sticky comment, suggestions, resolution) |
| `src/CiFailureCrew.Core/Rendering/` | Markdown comment, `MarkdownText` (mentions and fences) |
| `src/CiFailureCrew.Core/Configuration/` | `.ci-failure-crew.yml` (`CrewConfig`) |
| `src/CiFailureCrew.Core/Localization/` | `CrewLanguage` and `ReportStrings` |
| `src/CiFailureCrew.Core/Telemetry/` | OpenTelemetry `ActivitySource` and metrics |
| `src/CiFailureCrew.Providers/` | `ChatClientFactory` (OpenAI, Anthropic, Ollama) |
| `src/CiFailureCrew.Cli/` | `cfc` command line, MCP tool host and OTLP export |
| `src/CiFailureCrew.McpServer/` | Read-only repository MCP server |
| `src/CiFailureCrew.Evals/` | Evaluation runner and scoring |
| `evals/cases/` | Labeled failed runs |
| `samples/` | A failed test log and the diff that caused it |
| `action.yml`, `Dockerfile` | Composite Action and the prebuilt image it uses on Linux |

## Guides

- [Architecture](docs/en/architecture.md), [coding standards](docs/en/coding-standards.md), [testing](docs/en/testing.md)
- [GitHub Action](docs/en/guides/github-action.md), [adding a known failure](docs/en/guides/adding-a-known-failure.md)
- [Evaluation](evals/README.md), [architecture decision records](docs/adr/README.md)
- [Contributing and releasing](CONTRIBUTING.md), [changelog](CHANGELOG.md)

## Before finishing a task

- [ ] `dotnet build` without errors or new warnings
- [ ] `dotnet test` passing
- [ ] README and docs updated in the three languages if behavior changed
- [ ] `CHANGELOG.md` updated under *Unreleased* for user-visible changes
- [ ] Commit following Conventional Commits, in Portuguese, without AI attribution
