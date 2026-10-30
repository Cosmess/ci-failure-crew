# ADR 0004: Run on workflow_run and never execute the pull request code

**Status:** accepted

## Context

To explain a failure the Action needs the complete logs of the failed workflow, an API key and permission to
comment. A step inside the failed workflow has incomplete logs, and on pull requests from forks it has no secrets.
`pull_request_target` has secrets but is dangerous when combined with a checkout of the pull request code.

## Decision

The Action is meant for a separate workflow triggered by `workflow_run` when the watched workflows complete. It
reads the run, its logs and the pull request diff through the API. The checkout is the default branch, used only
by the read-only repository tools. The pull request is found by the run's link or, for forks, by owner and branch.

## Consequences

- Safe for forks: the pull request code reaches the agents only as data (the diff), never executed.
- The workflow file must be merged into the default branch before it runs.
- The repository tools see the default branch, not the pull request version of the files; the diff covers the
  changes.
