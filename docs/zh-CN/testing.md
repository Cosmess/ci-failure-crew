# 测试

[English](../en/testing.md) | [Português](../pt-BR/testing.md) | **简体中文**

## 项目

| 项目 | 覆盖内容 | 网络/LLM |
|---|---|---|
| `tests/CiFailureCrew.Tests` | 日志分析、规则、编排和路由、重试、渲染、配置、GitHub、MCP、评估 | 从不 |
| `tests/CiFailureCrew.IntegrationTests` | 使用真实 OpenAI API 的端到端解释 | 是，仅在设置了 `OPENAI_API_KEY` 时 |
| `src/CiFailureCrew.Evals`（不是测试） | 在 `evals/cases` 上衡量解释质量 | 是，按需运行 |

在 CI 中，单元测试在每次推送和 Pull Request 时运行。集成测试会产生费用，因此只在推送到 `main` 时运行，并且在缺少 secret
时会被跳过。

单元测试还覆盖：

- **路由**：每种失败会调用哪些智能体，以及已知失败不会调用任何智能体。
- **容错**：分类、调查或汇总智能体失败时，仍然会生成诊断。
- **密钥**：任何提示词中都不包含来自日志或 diff 的密码。
- **真实的 MCP 协议**以及 `RepositoryReader` 的沙箱（路径穿越、符号链接、密钥类文件）。
- **数据集一致性**：每个用例都能加载，规则用例会被对应规则识别而其他用例不会，标注的行存在于 diff 或日志中，
  并且 hunk 标题与其内容一致。

## 模拟对象

- **`FakeChatClient`** 实现了 `IChatClient`。它接收一个 `提示词 → 响应` 的函数（提示词包含智能体的指令，因此可以按智能体
  分别回答），记录所有提示词，并且每次调用报告 1000 个输入 token 和 100 个输出 token。在函数中抛出异常即可模拟失败。
- **`FakeHttpHandler`** 记录每个请求，并使用给定的函数返回响应。
- **`SampleLogs`** 按 GitHub 的格式构建日志，包含时间戳和步骤标题。

## 约定

- 测试名称描述行为，使用英文并以 `_` 分隔。
- Arrange / act / assert 之间用空行分隔。
- 创建编排器时使用 `retryBackoff: _ => TimeSpan.Zero`。
- 测试中不要写出看起来像真实凭据的字符串：在运行时拼接（`"gh" + "p_" + ...`），否则 GitHub 的推送保护会阻止推送。
- 每个修复的缺陷都要补充一个在修复前会失败的测试。
