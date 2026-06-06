# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

This changelog is updated automatically by Release Please.
<<<<<<< HEAD
=======

## [0.1.3](https://github.com/CollinRobison/markdown-journal-cli/compare/v0.1.2...v0.1.3) (2026-06-06)


### Documentation

* changelog stale info ([#79](https://github.com/CollinRobison/markdown-journal-cli/issues/79)) ([038723e](https://github.com/CollinRobison/markdown-journal-cli/commit/038723ed4031802ef2260f51a5bdfee776752123))

## [0.1.2](https://github.com/CollinRobison/markdown-journal-cli/compare/v0.1.1...v0.1.2) (2026-06-06)


### Bug Fixes

* release pipelines and test the release process ([#77](https://github.com/CollinRobison/markdown-journal-cli/issues/77)) ([8c48943](https://github.com/CollinRobison/markdown-journal-cli/commit/8c489438b9133d281bc10108848a0697af73702c))
>>>>>>> d44f81e2bbce104cad9723f88c6c714e802c474e

## [0.1.1](https://github.com/CollinRobison/markdown-journal-cli/compare/v0.1.0...v0.1.1) (2026-06-06)


### Bug Fixes

* remove docs and release fixes ([#76](https://github.com/CollinRobison/markdown-journal-cli/issues/76)) ([6d726b4](https://github.com/CollinRobison/markdown-journal-cli/commit/6d726b49a1139f22675ab887d191c696cabf5cb7))

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
