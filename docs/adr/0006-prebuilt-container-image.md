# ADR 0006: Prebuilt container image for the Action, with a build-from-source fallback

**Status:** accepted

## Context

Building the tool on every run adds the .NET SDK setup and a build (about 15 seconds on GitHub-hosted runners, more
when the SDK has to be downloaded) before every explanation. Building from the user's repository folder would also
pick up a `global.json` there and could fail. Code Review Crew had exactly this bug and fixed it with the same
design (its ADR 0008).

A Docker container action (`runs.using: docker`) starts fast, but only runs on Linux runners and needs the image to
exist before the version can be used.

## Decision

- The Action is a **composite** action.
- A `Dockerfile` builds a framework-dependent image (amd64 and arm64). The `Container image` workflow publishes it to
  `ghcr.io/cosmess/ci-failure-crew` with the name of every version tag, including the moving `v1` tag.
- On Linux runners the Action pulls the image matching the ref it was used with (`github.action_ref`) and runs the
  CLI in it, mounting the workspace and the runner's temp folder at the same paths.
- Without an image (Windows, macOS, a branch or a commit SHA, or this repository's own `uses: ./`), the Action
  installs the SDK and publishes the CLI from the Action's own folder, away from the user's `global.json`.

## Consequences

- Linux runs start in seconds; other runners keep working, only slower.
- This repository's own workflow explains failures with the code of the default branch, because `uses: ./` builds
  from source.
- Every release must push its version tag so the image is published (see [CONTRIBUTING.md](../../CONTRIBUTING.md)).
- The GHCR package must be public, or other repositories fall back to building from source.
