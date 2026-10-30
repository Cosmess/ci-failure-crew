# Security policy

## Supported versions

Security fixes are released for the latest `v1.x` version. The `v1` tag always points to it.

## Reporting a vulnerability

Please **do not open a public issue**. Report it privately through
[GitHub private vulnerability reporting](https://github.com/Cosmess/ci-failure-crew/security/advisories/new)
(*Security → Report a vulnerability*).

Include the version or commit, the steps to reproduce and the impact you expect. You will get an answer within
7 days, and a fix or a plan within 30 days for confirmed issues. Reporters are credited in the advisory unless they
prefer otherwise.

## What is in scope

CI Failure Crew handles API keys and reads untrusted logs and pull request content, so these reports are especially
welcome:

- leaks of the API key, the GitHub token or secrets printed in a job log (comments, job summary, container image);
- prompt injection in a log or a diff that makes the crew post content it should not, or misuse its tools;
- escaping the read-only MCP tools (reading outside the repository, `.git`, secret files or symbolic links);
- script injection through the Action inputs.

A wrong explanation or fix is a model quality issue, not a vulnerability: please open a regular issue.

## Using the Action safely

- Keep the OpenAI key in a repository or organization secret, never in the workflow file.
- Grant only the permissions the workflow needs: `actions: read`, `contents: read` and `pull-requests: write`.
- Pin a version (`@v1`, `@v1.0.0` or a commit SHA).
- Run it on `workflow_run`, as documented, and never add a checkout of the pull request code to that workflow: it
  runs with the default branch's secrets.
