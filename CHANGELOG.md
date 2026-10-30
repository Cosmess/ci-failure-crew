# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and versions follow [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Fixed

- The timeout message of an agent call showed "0s" for timeouts under one second; it now shows fractions of a
  second.

## [1.0.0] - 2026-10-30

First public release.

### Added

- Log analysis without a model: failed step isolation, log excerpts within a size limit, secret redaction and
  known failure rules (job timeout, runner lost, disk full, out of memory, package feed down, rate limits), which
  never apply when the log shows a failed test or a compiler error.
- TriageAgent, FlakinessAgent, InvestigatorAgent, FixerAgent and SynthesizerAgent, called per job only when needed.
- One comment per workflow on the pull request, updated on every run and resolved when the workflow passes, with
  GitHub suggestions for one-line fixes.
- Pull requests from forks found by owner and branch, and job history on the default branch to detect flaky tests.
- `cfc` command line to explain a run (`--run`) or local logs (`--log`, `--diff`).
- GitHub Action for `workflow_run`, with a prebuilt image on Linux runners.
- Explanations in English, Brazilian Portuguese and Simplified Chinese.
- Evaluation suite with 11 labeled failed runs.

[Unreleased]: https://github.com/Cosmess/ci-failure-crew/compare/v1.0.0...main
[1.0.0]: https://github.com/Cosmess/ci-failure-crew/releases/tag/v1.0.0
