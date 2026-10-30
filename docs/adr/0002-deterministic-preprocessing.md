# ADR 0002: Reduce logs, redact secrets and match known failures before any agent

**Status:** accepted

## Context

Job logs are huge (tens of thousands of lines of restore, build and test output), may contain secrets GitHub did
not mask, and many failures have causes that never depend on the code: timeouts, runners lost, feeds down.

## Decision

Before any agent, deterministic code:

- isolates the failed step and keeps windows around error patterns plus the end of the step, within a size limit;
- redacts token formats, private keys and password assignments, in the log and the diff;
- matches known failure rules, which explain the failure in every language without a model;
- numbers the lines of the diff, because models miscount them from hunk headers.

## Consequences

- Fewer tokens, lower cost and faster answers; known failures cost nothing.
- These steps are unit-tested; prompts only do what needs judgment.
- A new log format may need new patterns in `LogExcerpt`. Rules must be precise, or they would hide failures caused
  by the code: every rule has evaluation and dataset tests.
