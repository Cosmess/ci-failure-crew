# ADR 0001: Reuse Code Review Crew's foundation by copying it, not through a shared library

**Status:** accepted

## Context

CI Failure Crew needs much of what [Code Review Crew](https://github.com/Cosmess/code-review-crew) already has: the
provider factory, `RetryPolicy`, telemetry, the read-only MCP server, the diff parser, localization and the
evaluation approach. Extracting a shared NuGet package first would delay the project and couple two products that
are still changing.

## Decision

Copy the pieces into this repository, rename them and adapt them freely (for example, the MCP host only starts the
repository server, and the diff parser gained `DiffAnnotator`). Both projects keep Microsoft Agent Framework on
top of `Microsoft.Extensions.AI`.

## Consequences

- Each project evolves at its own pace and is published as an independent Action.
- A bug fixed in a copied piece (for example in `RepositoryReader`) must be fixed in both repositories.
- If a third project needs the same pieces, extracting a shared package becomes worth it.
