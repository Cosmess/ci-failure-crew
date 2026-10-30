# ADR 0005: One comment per workflow, updated and resolved

**Status:** accepted

## Context

A pull request can fail many times while it is fixed, and several workflows can fail on the same commit. A new
comment per failure would bury the conversation.

## Decision

- One comment per workflow, found by a hidden marker in its first line with the workflow id, the commit, whether
  suggestions were posted and whether it is resolved.
- Each run of the workflow updates it. When the workflow passes, it is marked resolved and the previous diagnosis is
  folded in a `<details>` block.
- One-line fixes on lines of the diff are posted as a `COMMENT` review with GitHub suggestions, once per commit, so
  re-runs do not repeat them. If GitHub rejects the line, the fix stays in the comment.

## Consequences

- The pull request shows the current state of each workflow in one place.
- The marker format is a contract: changing it orphans existing comments, which would get a new comment instead
  of being updated.
