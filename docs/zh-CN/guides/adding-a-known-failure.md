# 添加已知失败

[English](../../en/guides/adding-a-known-failure.md) | [Português](../../pt-BR/guides/adding-a-known-failure.md) | **简体中文**

有些失败的原因总是相同的，而且从来不是代码的问题：任务超时、运行器断开连接、包源不可用。这些失败由
[`KnownFailures.cs`](../../../src/CiFailureCrew.Core/LogAnalysis/KnownFailures.cs) 中的规则解释，无需调用模型。

## 何时添加规则

- 日志中有能识别原因的固定信息（`No space left on device`、`error NU1301`）。
- 原因在代码之外，因此每次的修复方法都相同。
- 该信息不会出现在由代码引起的失败中。匹配到真实代码失败的规则会把它掩盖掉。作为保护措施，当该步骤中出现失败的测试或
  编译错误时（`LogExcerpt.IsCodeFailure`），任何规则都不会生效；但仍请匹配带标点的完整信息，而不是其中的片段。

如果原因取决于代码，请交给智能体处理。

## 步骤

1. 在 `KnownFailures.All` 中添加一个 `KnownFailure`：kebab-case 格式的 id、分类、匹配模式，以及英文、葡萄牙文和中文的
   解释与修复方法。规则按顺序匹配：请把更具体的规则放在前面。
2. 在 `LogTests.Recognizes_known_failures` 中添加该信息。
3. 添加一个带有 `"knownRule": "<id>"` 的评估用例（参见 [evals/README.zh-CN.md](../../../evals/README.zh-CN.md)）。
   数据集测试会检查该规则能识别这个用例，并且不会识别其他用例。
4. 运行 `dotnet test`，并更新各 README 和[架构](../architecture.md)中的已知失败列表。
