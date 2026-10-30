# Adding a known failure

**English** | [Português](../../pt-BR/guides/adding-a-known-failure.md) | [简体中文](../../zh-CN/guides/adding-a-known-failure.md)

Some failures always have the same cause and are never the fault of the code: a job timeout, a runner that lost
connection, a package feed that is down. They are explained by rules in
[`KnownFailures.cs`](../../../src/CiFailureCrew.Core/LogAnalysis/KnownFailures.cs), without calling a model.

## When to add a rule

- The log has a fixed message that identifies the cause (`No space left on device`, `error NU1301`).
- The cause is outside the code, so the fix is the same every time.
- The message does not appear in failures caused by the code. A rule that matches a real code failure hides it.
  As a safety net, no rule applies when the step shows a failed test or a compiler error
  (`LogExcerpt.IsCodeFailure`), but match the full message, with its punctuation, rather than a fragment.

If the cause depends on the code, let the agents handle it.

## Steps

1. Add a `KnownFailure` to `KnownFailures.All`: an id in kebab-case, the category, the pattern and the explanation
   and fix in English, Portuguese and Chinese. Rules are matched in order: put specific ones first.
2. Add the message to `LogTests.Recognizes_known_failures`.
3. Add an evaluation case with `"knownRule": "<id>"` (see [evals/README.md](../../../evals/README.md)). The dataset
   tests check that the rule matches it and that no other case is matched by it.
4. Run `dotnet test` and update the list of known failures in the READMEs and in
   [architecture.md](../architecture.md).
