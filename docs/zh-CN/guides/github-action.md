# 使用 GitHub Action

[English](../../en/guides/github-action.md) | [Português](../../pt-BR/guides/github-action.md) | **简体中文**

## 最简配置

1. 在仓库中添加 `OPENAI_API_KEY` secret（*Settings → Secrets and variables → Actions*）。
2. 创建 `.github/workflows/ci-failure-crew.yml`：

```yaml
name: CI Failure Crew

on:
  workflow_run:
    workflows: [CI]          # 要监视的工作流名称
    types: [completed]

permissions:
  actions: read              # 读取运行及其日志
  contents: read
  pull-requests: write       # 在 Pull Request 中评论

jobs:
  explain:
    if: github.event.workflow_run.event == 'pull_request' && github.event.workflow_run.conclusion != 'cancelled'
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: Cosmess/ci-failure-crew@v1
        with:
          openai-api-key: ${{ secrets.OPENAI_API_KEY }}
          language: zh-CN
```

3. 将其合并到默认分支：GitHub 只会从默认分支运行 `workflow_run` 工作流。

## 为什么使用 `workflow_run`

失败的工作流无法解释它自己：它的日志只有在结束后才完整，而且在来自 fork 的 Pull Request 中它无法获取 secret。
`workflow_run` 会在被监视的工作流完成后启动，运行在默认分支的上下文中，可以访问 secret。在这里，`actions/checkout`
检出的是默认分支，因此 Pull Request 的代码永远不会被执行。Pull Request 的修改只会以 diff 的形式作为数据传给智能体。

## 不同结论下的行为

| 被监视运行的结论 | Action 的行为 |
|---|---|
| `failure`、`timed_out` | 解释失败的任务，并创建或更新评论 |
| `success` | 将已有评论标记为已解决，并折叠之前的诊断 |
| 其他 | 不做任何事 |

请在 `if` 中保留 `success`（或去掉对结论的判断），以便 CI 通过时评论能被标记为已解决。

## 输入

| 输入 | 默认值 | 说明 |
|---|---|---|
| `openai-api-key` | 必填 | OpenAI API 密钥 |
| `github-token` | `github.token` | 用于读取运行和日志并发表评论的令牌 |
| `model` | `gpt-5-mini` | OpenAI 模型 |
| `language` | 空（使用配置文件，或 `en`） | `en`、`pt-BR` 或 `zh-CN` |
| `config` | `.ci-failure-crew.yml` | 配置文件（不存在时忽略） |
| `run-id` | `github.event.workflow_run.id` | 要解释的运行，用于在 `workflow_run` 之外使用 Action |
| `post-comment` | `true` | 在 Pull Request 中评论 |
| `read-repository` | `true` | 为智能体提供只读的仓库 MCP 工具 |

## 多个工作流

在 `workflows: [CI, E2E]` 中列出它们。每个工作流在 Pull Request 中各自维护一条评论。

## 运行器与启动时间

| 运行器 | Action 的运行方式 | 启动时间 |
|---|---|---|
| Linux（amd64 或 arm64）并使用版本 tag（`@v1`、`@v1.0.0`） | 拉取预构建镜像 `ghcr.io/cosmess/ci-failure-crew` | 几秒钟 |
| Windows、macOS，或使用分支 / 提交 SHA | 从源码构建工具 | 在 GitHub 托管运行器上约 15 秒 |

## 注意事项

- 通过的运行不会产生费用：不会调用任何模型。
- 已知失败（超时、运行器断开、包源不可用等）无需模型即可解释。
- 推送到没有 Pull Request 的分支时，解释只会写入任务摘要。
- 请固定版本（`@v1`、某个 tag 或提交 SHA），不要使用 `@main`。
