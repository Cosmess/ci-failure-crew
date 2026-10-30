# ADR 0003: Route each job only to the agents it needs

**Status:** accepted

## Context

Code Review Crew runs every specialist on every diff (fan-out), because every diff may have security, performance
and style issues. A failed job is different: a timeout needs no code investigation, a flaky test needs the history
and not a fix, and a compilation error needs the diff.

## Decision

A pipeline per job: known failure rules → TriageAgent → FlakinessAgent when the job may be flaky (triaged as such,
or it also failed on the default branch) → InvestigatorAgent for code-related categories → FixerAgent when a file
was found. Jobs run in parallel and SynthesizerAgent writes the headline over all of them.

## Consequences

- Each agent has a narrow job and a structured output, which keeps prompts short and testable.
- Cost follows the failure: from zero calls (known failure) to five (code failure with a fix).
- A wrong triage sends the job down the wrong path; the evaluation measures the category for this reason.
