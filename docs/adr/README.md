# Architecture decision records

| ADR | Decision |
|---|---|
| [0001](0001-reuse-code-review-crew-by-copy.md) | Reuse Code Review Crew's foundation by copying it, not through a shared library |
| [0002](0002-deterministic-preprocessing.md) | Reduce logs, redact secrets and match known failures before any agent |
| [0003](0003-conditional-routing.md) | Route each job only to the agents it needs, instead of fan-out to all |
| [0004](0004-workflow-run-trigger.md) | Run on workflow_run and never execute the pull request code |
| [0005](0005-sticky-comment-per-workflow.md) | One comment per workflow, updated and resolved, plus suggestions once per commit |
| [0006](0006-prebuilt-container-image.md) | Prebuilt container image for the Action, with a build-from-source fallback |
