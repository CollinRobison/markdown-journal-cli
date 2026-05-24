# Security Policy

## Supported Versions

This project is currently pre-1.0. Security fixes are developed on `main` and
released in the next available version.

| Version                   | Supported          |
| ------------------------- | ------------------ |
| `main`                    | :white_check_mark: |
| `< 1.0.0` historical tags | :x:                |

## Reporting a Vulnerability

Please do **not** open public GitHub issues for security vulnerabilities.

Use GitHub Private Vulnerability Reporting:

- https://github.com/CollinRobison/markdown-journal-cli/security/advisories/new

Include as much detail as possible:

- A clear description of the issue and impact
- Reproduction steps or proof-of-concept
- Affected version/commit and platform details
- Any suggested mitigation (if known)

## Response Expectations

- Initial triage response target: within 7 days
- A remediation plan is shared after triage when impact is confirmed
- Fixes are released as soon as practical and may include coordinated disclosure

## Disclosure Process

When a vulnerability is confirmed:

1. The maintainer validates impact and scope.
2. A fix is prepared in a private workflow when possible.
3. A release is published with a changelog entry.
4. A public advisory may be published after a fix is available.

## Security Best Practices for Contributors

- Do not commit secrets, credentials, or personal tokens.
- Prefer the `IFileSystem` abstraction in production code; avoid direct `System.IO` calls.
- Add tests for all security-sensitive behavior changes.
- Follow `AGENTS.md` command/service separation rules to reduce risky logic in CLI commands.
