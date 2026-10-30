# 评估

[English](README.md) | [Português](README.pt-BR.md) | **简体中文**

评估会在带标注的失败运行上运行这组智能体，并衡量解释正确的频率。它会调用真实的模型，因此按需运行：在本地运行，
或通过 **Evaluation** 工作流运行。

```bash
export OPENAI_API_KEY=sk-...
dotnet run --project src/CiFailureCrew.Evals                       # 所有用例，英文解释
dotnet run --project src/CiFailureCrew.Evals -- --filter flaky     # 名称包含 "flaky" 的用例
dotnet run --project src/CiFailureCrew.Evals -- --language zh-CN --output report.md
```

## 指标

每项检查只统计标注了该项的任务。

| 指标 | 任务通过的条件 |
|---|---|
| 分类 | 分类为预期分类，或属于 `acceptCategories` 之一 |
| 定位到根本原因 | 文件是 `rootCause` 中的某一个，且行号在其 `tolerance` 范围内 |
| 来源 | `relatedToPullRequest` 一致 |
| 解释提到了原因 | 解释或修复中包含某个 `keywords`（忽略大小写和重音） |
| 无需 AI 的已知失败 | 该任务由 `knownRule` 中的规则解释 |
| 完全正确 | 所有标注的检查都通过 |

修复建议本身不评分：自动判断代码是否正确需要实际运行它。

## 一个用例

```
evals/cases/<名称>/
  <job>.log       每个失败的任务一个，使用 GitHub 的格式（时间戳、##[group] 标题）
  diff.patch      Pull Request 的 diff（可选）
  history.json    任务在默认分支上的历史（可选）：{"test": {"runs": 10, "failures": 3}}
  expected.json   标注
```

```json
{
  "description": "A method renamed in the pull request is still called by a controller outside the diff.",
  "jobs": {
    "build": {
      "category": "Compilation",
      "acceptCategories": [],
      "relatedToPullRequest": true,
      "rootCause": [{ "file": "src/Shop.Core/OrderService.cs", "line": 8, "tolerance": 1 }],
      "keywords": ["GetTotal", "CalculateTotal"],
      "knownRule": null
    }
  }
}
```

当某个词在不同语言中写法不同时，关键词应覆盖英文、葡萄牙文和中文，这样用例才能对任意语言的解释进行评分。

## 添加用例

1. 按上述结构创建文件夹。最好使用真实的日志：从失败的运行中下载，并删除所有私密信息。
2. 检查 `diff.patch` 的 hunk 标题：错误的行数会使智能体看到的行号发生偏移。
3. 运行 `dotnet test tests/CiFailureCrew.Tests`：数据集测试会检查每个标注的行都存在于 diff 或日志中，规则用例会被对应
   规则识别而其他用例不会。
4. 运行评估，并将结果写入 Pull Request。
