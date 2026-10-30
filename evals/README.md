# Evaluation

**English** | [Português](README.pt-BR.md) | [简体中文](README.zh-CN.md)

The evaluation runs the crew over labeled failed runs and measures how often the explanation is right. It calls the
real model, so it runs on demand: locally or with the **Evaluation** workflow.

```bash
export OPENAI_API_KEY=sk-...
dotnet run --project src/CiFailureCrew.Evals                       # every case, explanations in English
dotnet run --project src/CiFailureCrew.Evals -- --filter flaky     # cases whose name contains "flaky"
dotnet run --project src/CiFailureCrew.Evals -- --language pt-BR --output report.md
```

## Metrics

Each check is counted only on the jobs labeled for it.

| Metric | A job passes when |
|---|---|
| Category | the category is the expected one or one of `acceptCategories` |
| Root cause located | the file is one of `rootCause` and the line is within its `tolerance` |
| Origin | `relatedToPullRequest` matches |
| Explanation mentions the cause | the explanation or the fix contains one of `keywords` (case and accents ignored) |
| Known failures without AI | the job was explained by the rule in `knownRule` |
| Fully correct | every labeled check passes |

The suggested fix is not scored: judging code automatically needs running it.

## A case

```
evals/cases/<name>/
  <job>.log       one per failed job, in GitHub's format (timestamps, ##[group] headers)
  diff.patch      the pull request diff (optional)
  history.json    the job history on the default branch (optional): {"test": {"runs": 10, "failures": 3}}
  expected.json   the labels
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

Keywords should cover English, Portuguese and Chinese when the word changes across languages, so the case scores
explanations in any of them.

## Adding a case

1. Create the folder with the files above. Real logs are best: download them from a failed run and remove anything
   private.
2. Check the hunk headers of `diff.patch`: wrong counts shift the line numbers the agents see.
3. Run `dotnet test tests/CiFailureCrew.Tests`: the dataset tests check that every labeled line exists in the diff
   or the log, that rule cases are matched by their rule and the others are not.
4. Run the evaluation and put the results in the pull request.
