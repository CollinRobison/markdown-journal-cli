# Implementation Plan: Test Suite Deep Dive & Cleanup

**Branch**: `002-test-suite-cleanup` | **Date**: 2026-04-11 | **Spec**: [spec.md](spec.md)  
**Input**: Feature specification from `/specs/002-test-suite-cleanup/spec.md`

## Summary

Perform a comprehensive test suite cleanup across three streams: (1) add integration tests for the four commands (`init`, `new`, `update`, `remove`) that currently have none, exercising the full CLI pipeline against real disk I/O; (2) consolidate repeated mock-setup boilerplate into layer-scoped shared infrastructure (`CommandTestBase`, `ServiceTestBase`, `MockFactory`, `JournalIntegrationTestBase`) so constructor changes require a single-point update; and (3) execute a quality pass — fixing vacuous assertions, renaming misleading test names, and removing verified duplicate coverage. No production source code changes are required.

## Technical Context

**Language/Version**: C# 13 / .NET 10 (`net10.0`)  
**Primary Dependencies**: Spectre.Console 0.55.0, Spectre.Console.Cli 0.55.0, Microsoft.Extensions.* 10.0.5  
**Storage**: Real `System.IO` file system for integration tests; `TestFileSystem` (in-memory) for unit/rollback tests  
**Testing**: xUnit 2.9.3 + Moq 4.20.72 + Shouldly 4.3.0 (no new packages required)  
**Target Platform**: Local development + CI (macOS / Linux)  
**Project Type**: Test project (no production changes; scope is `markdown-journal-cli.Tests/` only)  
**Performance Goals**: N/A — test execution speed not a primary concern; suite should finish in < 60s  
**Constraints**: All existing passing tests must remain passing; no new NuGet packages; no production code changes unless strictly required for testability  
**Scale/Scope**: ~50 test files, 5 CLI commands, 7+ service test classes, 6 rollback test classes

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| **I. Thin Command Layer** | ✅ PASS | No command code changes; this is test-only |
| **II. Service-Oriented Architecture** | ✅ PASS | No service interface changes |
| **III. File System Abstraction** | ✅ PASS | Integration tests use real `FileSystem`; unit tests use `TestFileSystem`. No `System.IO` calls added outside the integration test base class setup |
| **IV. Transactional Integrity** | ✅ PASS | `NoOpFileTransactionCoordinator` preserved for unit tests; real coordinator used for integration and rollback tests |
| **V. Test Coverage (NON-NEGOTIABLE)** | ✅ PASS | This feature is entirely in service of Principle V. Mirror structure preserved (FR-005). xUnit + Moq + Shouldly — no substitutions. |
| **VI. Rich Terminal UI** | ✅ PASS | `TestConsole` used in all command tests; no static `AnsiConsole` calls introduced |
| **Technology Stack** | ✅ PASS | No new runtime or test packages; no framework substitutions |
| **Security: Path Traversal** | ✅ PASS | Integration test temp directories use `Guid`-named subdirs under `Path.GetTempPath()` — no user-supplied paths |
| **Quality Gate: all tests pass** | ✅ ENFORCED | Acceptance criteria requires zero new failures introduced |

**Post-design re-check (Phase 1):** No constitution violations introduced by the design. `JournalIntegrationTestBase` uses `Directory.Delete(recursive: true)` in `Dispose()` — synchronous, standard .NET, not a security concern in test tear-down. No production registrations extracted to avoid out-of-scope changes.

## Project Structure

### Documentation (this feature)

```text
specs/002-test-suite-cleanup/
├── plan.md                          # This file
├── research.md                      # Phase 0 output
├── data-model.md                    # Phase 1 output
├── quickstart.md                    # Phase 1 output
├── contracts/
│   └── test-infrastructure-api.md   # Phase 1 output
└── tasks.md                         # Phase 2 output (speckit.tasks)
```

### Source Code (test project only)

```text
markdown-journal-cli.Tests/
├── Infrastructure/
│   ├── CommandAppTester.cs              (existing — unchanged)
│   ├── CommandTestBase.cs               (NEW — abstract base for command unit tests)
│   ├── ServiceTestBase.cs               (NEW — abstract base for service unit tests)
│   ├── MockFactory.cs                   (NEW — static pre-configured Mock<T> factory)
│   ├── JournalIntegrationTestBase.cs    (NEW — abstract base for command integration tests)
│   ├── Configuration/                   (existing — unchanged)
│   ├── DependencyInjection/             (existing — unchanged)
│   ├── FileSystem/                      (existing — unchanged)
│   ├── JournalTemplates/                (existing — unchanged)
│   ├── Tracking/                        (existing — unchanged)
│   └── Transactions/                    (existing — unchanged)
├── Commands/
│   ├── Add/                             (existing — migrated to CommandTestBase)
│   ├── Init/
│   │   ├── InitCommandTests.cs          (existing — migrated to CommandTestBase)
│   │   └── InitCommandIntegrationTests.cs  (NEW)
│   ├── New/
│   │   ├── NewCommandTests.cs           (existing — migrated)
│   │   └── NewCommandIntegrationTests.cs   (NEW)
│   ├── Remove/
│   │   ├── RemoveEntryCommandTests.cs   (existing — migrated)
│   │   └── RemoveEntryCommandIntegrationTests.cs  (NEW)
│   └── Update/
│       ├── UpdateCommandTests.cs        (existing — migrated + dedup audit)
│       ├── UpdateEntryCommandTests.cs   (existing — migrated + dedup audit)
│       └── UpdateCommandIntegrationTests.cs  (NEW)
├── Services/
│   ├── EntryFormatter/                  (existing — migrated to ServiceTestBase)
│   ├── InitJournal/                     (existing — migrated)
│   ├── JournalEntry/                    (existing — migrated)
│   ├── JournalFileUpdate/               (existing — migrated)
│   ├── JournalUpdate/                   (existing — migrated)
│   ├── NewJournal/                      (existing — migrated)
│   ├── RemoveEntry/                     (existing — migrated)
│   ├── Rollback/                        (existing — PRESERVED, no modifications)
│   └── TableOfContents/                 (existing — migrated)
└── Exceptions/                          (existing — unchanged)
```

**Structure decision**: Single test project mirroring the existing `markdown-journal-cli.Tests/` structure. No new projects. New files added to `Infrastructure/` for shared base classes.

## Complexity Tracking

> No constitution violations to justify. This feature adds test infrastructure only.
