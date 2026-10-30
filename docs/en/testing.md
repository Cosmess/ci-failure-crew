# Testing

**English** | [Português](../pt-BR/testing.md) | [简体中文](../zh-CN/testing.md)

## Projects

| Project | What it covers | Network/LLM |
|---|---|---|
| `tests/CiFailureCrew.Tests` | Log analysis, rules, orchestration and routing, retry, rendering, configuration, GitHub, MCP, evaluation | Never |
| `tests/CiFailureCrew.IntegrationTests` | End-to-end explanations with the real OpenAI API | Yes, only with `OPENAI_API_KEY` |
| `src/CiFailureCrew.Evals` (not a test) | Explanation quality on `evals/cases` | Yes, on demand |

In CI, unit tests run on every push and pull request. Integration tests only run on pushes to `main`, because
they cost money, and are skipped when the secret is missing.

The unit tests also cover:

- **Routing**: which agents each kind of failure calls, and that known failures call none.
- **Resilience**: a failing triage, investigator or synthesizer still produces a diagnosis.
- **Secrets**: no prompt contains a password from the log or the diff.
- **The real MCP protocol** and the `RepositoryReader` sandbox (path traversal, symbolic links, secret files).
- **Dataset consistency**: every case loads, rule cases are matched by their rule and the others are not, labeled
  lines exist in the diff or the log, and hunk headers match their content.

## Fakes

- **`FakeChatClient`** implements `IChatClient`. It takes a `prompt → response` function (the prompt includes the
  agent's instructions, so you can answer per agent), records the prompts and reports 1000 input and 100 output
  tokens per call. Throw from the function to simulate failures.
- **`FakeHttpHandler`** records every request and answers with the given function.
- **`SampleLogs`** builds logs in GitHub's format, with timestamps and step headers.

## Conventions

- The test name describes the behavior, in English with `_`.
- Arrange / act / assert separated by blank lines.
- Create the orchestrator with `retryBackoff: _ => TimeSpan.Zero`.
- Never write strings that look like real credentials in tests: build them at runtime (`"gh" + "p_" + ...`), or
  GitHub push protection blocks the push.
- Every fixed bug gets a test that would have failed before the fix.
