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
