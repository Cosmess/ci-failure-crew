# Using the GitHub Action

**English** | [Português](../../pt-BR/guides/github-action.md) | [简体中文](../../zh-CN/guides/github-action.md)

## Minimal setup

1. Add the `OPENAI_API_KEY` secret to the repository (*Settings → Secrets and variables → Actions*).
2. Create `.github/workflows/ci-failure-crew.yml`:

```yaml
name: CI Failure Crew

on:
  workflow_run:
    workflows: [CI]          # the names of the workflows to watch
    types: [completed]

permissions:
  actions: read              # read the run and its logs
  contents: read
  pull-requests: write       # comment on the pull request

jobs:
  explain:
    if: github.event.workflow_run.event == 'pull_request' && github.event.workflow_run.conclusion != 'cancelled'
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: Cosmess/ci-failure-crew@v1
        with:
          openai-api-key: ${{ secrets.OPENAI_API_KEY }}
```

3. Merge it into the default branch: GitHub only runs `workflow_run` workflows from there.

## Why `workflow_run`

The workflow that failed cannot explain itself: its logs are only complete after it ends, and on pull requests
from forks it has no secrets. `workflow_run` starts after the watched workflow completes, in the context of the
default branch, with access to secrets. `actions/checkout` there checks out the default branch, so the pull
request code is never executed. The pull request changes reach the agents only as the diff, as data.

## What it does on each conclusion

| Conclusion of the watched run | What the Action does |
|---|---|
| `failure`, `timed_out` | Explains the failed jobs and creates or updates the comment |
| `success` | Marks an existing comment as resolved, folding the old diagnosis |
| anything else | Nothing |

Keep `success` in the `if` (or remove the condition on the conclusion) so the comment is resolved when CI passes.

## Inputs

| Input | Default | Description |
|---|---|---|
| `openai-api-key` | required | OpenAI API key |
| `github-token` | `github.token` | Token to read the run and its logs and to comment |
| `model` | `gpt-5-mini` | OpenAI model |
| `language` | empty (uses the configuration, or `en`) | `en`, `pt-BR` or `zh-CN` |
| `config` | `.ci-failure-crew.yml` | Configuration file (ignored if missing) |
| `run-id` | `github.event.workflow_run.id` | Run to explain, to use the Action outside `workflow_run` |
| `post-comment` | `true` | Comment on the pull request |
| `read-repository` | `true` | Read-only MCP tools over the checked-out repository |

## Several workflows

List them in `workflows: [CI, E2E]`. Each workflow keeps its own comment on the pull request.

## Runners and startup time

| Runner | How the Action runs | Startup |
|---|---|---|
| Linux (amd64 or arm64) with a version tag (`@v1`, `@v1.0.0`) | Pulls the prebuilt image `ghcr.io/cosmess/ci-failure-crew` | a few seconds |
| Windows, macOS, or a branch / commit SHA | Builds the tool from source | about 15 seconds on GitHub-hosted runners |

## Notes

- Runs that pass cost nothing: no model is called.
- Known failures (timeouts, runner lost, feed down...) are explained without a model.
- A push to a branch without a pull request only writes the explanation to the job summary.
- Pin a version (`@v1`, a tag or a commit SHA) instead of `@main`.
