# Implementation Plan: Delete Entry --clean-refs Tolerates Already-Deleted Files

**Branch**: `004-delete-clean-refs-missing-file` | **Date**: 2026-04-18 | **Spec**: [spec.md](./spec.md)  
**Input**: Feature specification from `/specs/004-delete-clean-refs-missing-file/spec.md`

## Summary

When `--clean-refs` is set, the `remove entry` command must tolerate a target file that no longer exists on disk. Currently `ResolveAndValidate` throws `FileNotFoundException` before any cleanup occurs. The fix conditionally relaxes the file-existence guard when `cleanRefs` is true, skips the delete step for an already-absent file, and still performs all remaining cleanup: config removal, tracking removal, TOC regeneration, and dead-link stripping.

## Technical Context

**Language/Version**: C# 13 / .NET 10 (`net10.0`)  
**Primary Dependencies**: Spectre.Console 0.55.0, Spectre.Console.Cli 0.55.0, Microsoft.Extensions.* 10.0.5  
**Storage**: Files — `.journalrc` (JSON config), `.mdjournal` (JSON tracking index), `.md` entry files  
**Testing**: xUnit + Moq + Shouldly  
**Target Platform**: Cross-platform CLI (macOS / Linux / Windows)  
**Project Type**: CLI tool  
**Performance Goals**: < 2 seconds for `remove entry --clean-refs` on a typical journal  
**Constraints**: No `System.IO` calls outside `FileSystem`; all multi-file writes in `FileTransactionScope`  
**Scale/Scope**: Single-user local journal; tens to hundreds of `.md` entries

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Thin Command Layer | ✅ PASS | Command delegates to service; only routing logic changes |
| II. Service-Oriented Architecture | ✅ PASS | Logic stays in `RemoveEntryService`; interface updated |
| III. File System Abstraction | ✅ PASS | All file checks via `IFileSystem.FileExists` — no `System.IO` |
| IV. Transactional Integrity | ✅ PASS | `TrackDelete` skipped when file absent; no phantom rollback entries |
| V. Test Coverage | ✅ PASS | New unit + integration tests added for the already-deleted scenario |
| VI. Rich Terminal UI | ✅ PASS | No new console output patterns; existing markup escaping preserved |

No violations — Complexity Tracking section not required.

## Project Structure

### Documentation (this feature)

```text
specs/004-delete-clean-refs-missing-file/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
└── tasks.md             # Phase 2 output (/speckit.tasks — NOT created here)
```

### Source Code (files touched)

```text
markdown-journal-cli/
├── Commands/Remove/
│   └── RemoveEntryCommand.cs           # Pass CleanRefs to ValidatePreconditions
├── Services/RemoveEntry/
│   ├── IRemoveEntryService.cs          # Add cleanRefs param to ValidatePreconditions
│   └── RemoveEntryService.cs           # Core logic: ResolveAndValidate + RemoveEntry

markdown-journal-cli.Tests/
├── Commands/Remove/
│   └── RemoveEntryCommandTests.cs      # New tests for relaxed precondition path
├── Services/RemoveEntry/
│   └── RemoveEntryServiceTests.cs      # New tests for already-deleted + cleanRefs
└── Commands/Remove/
    └── RemoveEntryCommandIntegrationTests.cs  # New integration test
```
