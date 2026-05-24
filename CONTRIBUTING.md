# Contribution Guidelines

Thanks for your interest in contributing to `markdown-journal-cli`.

## Contents

- [Prerequisites](#prerequisites)
- [Project-specific coding rules](#project-specific-coding-rules)
- [Development setup](#development-setup)
- [Contribution workflow](#contribution-workflow)
- [Testing expectations](#testing-expectations)
- [Pull request checklist](#pull-request-checklist)
- [Security reporting](#security-reporting)

## Prerequisites

By contributing, you confirm that:

- The contribution is your original work.
- You have rights to submit the contribution (including employer permissions when required).
- You license your contribution under the repository's [MIT License](./LICENSE).
- You agree to follow the [Code of Conduct](./CODE_OF_CONDUCT.md).

## Project-specific Coding Rules

This project is intentionally strict about architecture.

- Commands stay thin: validate input, call services, print results.
- Business logic lives in services.
- Services have interfaces and DI registration in `Program.cs`.
- All file operations go through `IFileSystem` (no direct `System.IO` usage in production code).
- Use `IAnsiConsole.MarkupLine()` for output; no `Console.WriteLine()`.
- Escape user-provided strings with `.EscapeMarkup()`.

Canonical engineering rules are maintained in `AGENTS.md`.

## Development Setup

Requirements:

- .NET 10 SDK
- Git

```bash
git clone https://github.com/CollinRobison/markdown-journal-cli
cd markdown-journal-cli
dotnet restore
dotnet build
dotnet test
```

Additional docs:

- Development workflow: `docs/DEVELOPMENT.md`
- Full command reference: `docs/COMMANDS.md`
- Testing guide: `docs/TESTING.md`
- Architecture details: `docs/ARCHITECTURE.md`

## Contribution Workflow

1. Open or find an issue describing the work.
2. Fork the repository.
3. Create a focused feature branch.
4. Implement changes with tests.
5. Open a pull request against `main`.

Recommendations:

- Keep each commit a small logical unit.
- Avoid unrelated reformatting/noise.
- Do not bundle unrelated features in one PR.

## Testing Expectations

Before opening a PR:

```bash
dotnet build
dotnet test
```

Also ensure:

- New behavior has tests (unit minimum, integration when appropriate).
- New command/service files have mirrored test files in `markdown-journal-cli.Tests/`.
- Rollback-sensitive write paths are covered with failure/rollback tests where relevant.

See `docs/TESTING.md` for base classes and patterns.

## Commit Message Convention

This project uses [Conventional Commits](https://www.conventionalcommits.org/).
PR titles must follow the format because squash merges use the PR title as
the commit message on `main`, and release-please reads those commits to
determine the next version bump.

Format: `<type>: <description>` (e.g., `feat: add export command`)

| Type | Version bump | When to use |
|---|---|---|
| `feat` | minor | New feature or command |
| `fix` | patch | Bug fix |
| `docs` | none | Documentation only |
| `test` | none | Adding or fixing tests |
| `refactor` | none | Code change, not a fix or feature |
| `chore` | none | Dependencies, build tooling |
| `ci` | none | CI/CD changes |
| `perf` | patch | Performance improvement |

For breaking changes, append `!`: `feat!: rename --path flag to --directory`

PR title format is enforced by CI — the PR will fail the lint check if
the title doesn't match the convention.

## Pull Request Checklist

- [ ] Branch is up to date with `main`
- [ ] `dotnet build` passes
- [ ] `dotnet test` passes
- [ ] PR title follows conventional commits format (`feat:`, `fix:`, etc.)
- [ ] Commands remain thin; logic resides in services
- [ ] New services include interface + `Program.cs` registration
- [ ] User-facing output uses `IAnsiConsole.MarkupLine()`
- [ ] User-provided markup strings use `.EscapeMarkup()`
- [ ] Docs/changelog updated when behavior changes

PR description should include:

- What changed
- Why it changed
- How it was tested
- Related issue/discussion links

## Security Reporting

Do not open public issues for vulnerabilities.

- Use private reporting in `SECURITY.md`:
  https://github.com/CollinRobison/markdown-journal-cli/security/advisories/new

## Notes

- Maintainers may request changes before merge.
- Lack of immediate response does not mean a PR is ignored; reviews are best-effort.
- Even if a PR is not merged, feedback and discussion are welcome.
