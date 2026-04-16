# Implementation Plan: Update --sync Flag (Skip Last-Edited Dates)

**Branch**: `003-sync-skip-dates` | **Date**: 2026-04-15 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/003-sync-skip-dates/spec.md`

## Summary

Add a `--sync` flag to `mdjournal update journal` that runs the tracking, config, and TOC update subsystems without writing "Last Edited:" date changes to user-authored journal entry files. This is designed for post-git-pull / post-merge scenarios where file hashes may be stale but the user has not genuinely edited the entries. The implementation reuses the existing `trackingOnly: true` path in `UpdateLastEditedDatesAndTracking`, adds `--sync` conflict-validation in `UpdateJournalSettings.Validate()`, and updates the `all` detection logic in `UpdateCommand.ExecuteCore`.

## Technical Context

**Language/Version**: C# 13 / .NET 10 (`net10.0`)  
**Primary Dependencies**: Spectre.Console 0.55.0, Spectre.Console.Cli 0.55.0, Microsoft.Extensions.* 10.0.5, xUnit + Moq + Shouldly  
**Storage**: Files — `.journalrc` and `.mdjournal` (JSON); markdown `.md` entry files  
**Testing**: xUnit + Moq + Shouldly; `TestFileSystem` (in-memory) for unit tests; real `System.IO` for integration tests  
**Target Platform**: macOS / Linux / Windows (.NET 10)  
**Project Type**: CLI tool (Spectre.Console.Cli)  
**Performance Goals**: Comparable wall-clock time to `--tracking --config --toc` (within 10%) per SC-001  
**Constraints**: Atomic rollback on partial failure (Constitution IV); zero "Last Edited:" writes to user entry files (SC-002); `--sync` + granular flag combos rejected before any I/O (SC-003)  
**Scale/Scope**: Single-user local journal CLI; one-command surface area change

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Thin Command Layer | ✅ PASS | Flag validation in `UpdateJournalSettings.Validate()`; routing in `ExecuteCore`; no business logic in command |
| II. Service-Oriented Architecture | ✅ PASS | No new service needed; `IJournalUpdateService.UpdateLastEditedDatesAndTracking(trackingOnly: true)` already exists |
| III. File System Abstraction | ✅ PASS | No new file I/O paths; existing `IFileSystem` usage unchanged |
| IV. Transactional Integrity | ✅ PASS | `--sync` uses the existing `FileTransactionScope` wrapping all three subsystems |
| V. Test Coverage | ✅ PASS | FR-011 mandates unit + integration tests; rollback + malformed-index as `TestFileSystem` unit tests |
| VI. Rich Terminal UI | ✅ PASS | FR-012 summary line `[dim]--sync active: Last Edited dates were not updated[/]` uses Spectre.Console markup |

**Pre-Phase-0 Gate**: PASS — no violations. Proceed to research.

**Post-Phase-1 Re-check**: PASS — no new violations introduced by design. All changes confined to `UpdateSettings.cs` and `UpdateCommand.cs`; no new services, infrastructure, or direct `System.IO` usage.

## Project Structure

### Documentation (this feature)

```text
specs/003-sync-skip-dates/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks — NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
# Files changed by this feature
markdown-journal-cli/
└── Commands/Update/
    ├── UpdateSettings.cs          # ← add bool Sync + Validate() conflict checks
    └── UpdateCommand.cs           # ← update all-detection; add sync routing; add FR-012 summary line

markdown-journal-cli.Tests/
├── Commands/Update/
│   ├── UpdateCommandTests.cs      # ← new unit tests (sync routing, validation rejections, dry-run)
│   └── UpdateCommandIntegrationTests.cs  # ← new integration tests (stale-tracking, no-op, new-file, deleted-file)
└── Services/JournalUpdate/
    └── JournalUpdateServiceTests.cs  # ← new dry-run sync tests
```

**Structure Decision**: Single-project CLI (Option 1). Only two source files change; mirror test files already exist. No new services, infrastructure, templates, or project files.
