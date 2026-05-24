# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## Maintainer Notes

- Add entries under `Unreleased` as changes merge.
- Move `Unreleased` items into a new version section at release time.
- Use ISO date format: `YYYY-MM-DD`.
- Keep entries user-focused and grouped by type.

Suggested categories:

- `Added` for new features
- `Changed` for changes in existing functionality
- `Deprecated` for soon-to-be removed features
- `Removed` for now removed features
- `Fixed` for bug fixes
- `Security` for vulnerability-related updates

## [Unreleased]

### Added

- Open-source collaboration scaffolding:
  - `CODE_OF_CONDUCT.md`
  - `SECURITY.md`
  - `.github` issue/PR templates
  - `.github/CODEOWNERS`
  - `.github/workflows/ci.yml` (build + test on push/PR)
- `docs/COMMANDS.md` extracted as full command reference.
- `docs/TESTING.md` added as dedicated testing infrastructure guide.

### Changed

- Simplified `README.md` to focus on quick start and top-level docs navigation.
- Updated `docs/DEVELOPMENT.md` to align command/service patterns with current code.
- Updated `CONTRIBUTING.md` links and guidance for the new docs layout.

## [0.1.0] - 2026-05-23

### Added

- Initial public CLI capabilities for markdown journal management:
  - `new`
  - `init`
  - `add entry`
  - `add config`
  - `add toc`
  - `add tracking`
  - `update journal`
  - `update entry`
  - `remove entry` (`rm` alias)
- Metadata directory layout (`.mdjournal/.journalindex`, `.mdjournal/.journaltoc`).
- Rollback transaction infrastructure with standardized exit codes (`2`, `3`).
