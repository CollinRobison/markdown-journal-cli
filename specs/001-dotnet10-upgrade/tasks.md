# Tasks: .NET 10 Upgrade

**Input**: Design documents from `/specs/001-dotnet10-upgrade/`
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅ (N/A — no new entities), quickstart.md ✅

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Add the SDK pin before any project files are touched. This ensures `dotnet` resolves .NET 10 for all subsequent commands.

- [X] T001 Create `global.json` at repository root with `sdk.version: "10.0.201"` and `rollForward: "latestMinor"`

**Checkpoint**: `dotnet --version` resolves to a `10.0.x` SDK

---

## Phase 2: User Story 1 — Project Targets .NET 10 and Builds Clean (Priority: P1) 🎯 MVP

**Goal**: Both project files declare `net10.0` as `TargetFramework` and `dotnet build` succeeds with zero errors and zero warnings.

**Independent Test**: Run `dotnet build markdown-journal-cli/markdown-journal-cli.csproj` — expect exit 0, zero diagnostics.

### Implementation for User Story 1

- [X] T002 [P] [US1] Update `<TargetFramework>net9.0</TargetFramework>` → `net10.0` in `markdown-journal-cli/markdown-journal-cli.csproj`
- [X] T003 [P] [US1] Update `<TargetFramework>net9.0</TargetFramework>` → `net10.0` in `markdown-journal-cli.Tests/markdown-journal-cli.Tests.csproj`
- [X] T004 [US1] Validate: run `dotnet build markdown-journal-cli/markdown-journal-cli.csproj` and confirm exit 0, zero errors, zero warnings

**Checkpoint**: Both project files target `net10.0`. Build gate passes. User Story 1 is complete and independently verifiable.

---

## Phase 3: User Story 2 — NuGet Dependencies Are Compatible With .NET 10 (Priority: P2)

**Goal**: All NuGet packages are updated to their latest stable .NET 10-compatible versions. `dotnet build` and `dotnet test` both succeed with zero warnings and all tests green.

**Independent Test**: Run `dotnet test` — expect all existing tests to pass with zero failures and no "package is not compatible" warnings.

### Implementation for User Story 2

- [X] T005 [P] [US2] Update all package versions in `markdown-journal-cli/markdown-journal-cli.csproj`:
  - `Microsoft.Extensions.Configuration` `10.0.0` → `10.0.5`
  - `Microsoft.Extensions.Configuration.UserSecrets` `10.0.0` → `10.0.5`
  - `Microsoft.Extensions.DependencyInjection` `10.0.0` → `10.0.5`
  - `Microsoft.Extensions.Hosting` `10.0.0` → `10.0.5`
  - `Microsoft.Extensions.Options.DataAnnotations` `10.0.0` → `10.0.5`
  - `Spectre.Console` `0.50.0` → `0.55.0`
  - `Spectre.Console.Cli` `0.50.0` → `0.55.0`
- [X] T006 [P] [US2] Update all package versions in `markdown-journal-cli.Tests/markdown-journal-cli.Tests.csproj`:
  - `Microsoft.Extensions.DependencyInjection` `10.0.0` → `10.0.5`
  - `Microsoft.NET.Test.Sdk` `17.12.0` → `18.3.0`
  - `Spectre.Console.Testing` `0.50.0` → `0.55.0`
  - `xunit` `2.9.2` → `2.9.3`
  - `xunit.runner.visualstudio` `2.8.2` → `3.1.5`
  - `coverlet.collector` `6.0.2` → `8.0.1`
- [X] T007 [US2] Run `dotnet build` immediately after T005/T006; if `TestConsole` constructor errors appear (CS0117 or similar), update `new TestConsole()` instantiation in the 7 affected test files to match the Spectre.Console 0.55.0 API:
  - `markdown-journal-cli.Tests/Commands/Add/AddTableOfContentsCommandTests.cs`
  - `markdown-journal-cli.Tests/Commands/Add/AddTableOfContentsRollbackTests.cs`
  - `markdown-journal-cli.Tests/Commands/Add/AddJournalrcRollbackTests.cs`
  - `markdown-journal-cli.Tests/Commands/Init/InitCommandTests.cs`
  - `markdown-journal-cli.Tests/Commands/New/NewCommandTests.cs`
  - `markdown-journal-cli.Tests/Commands/Remove/RemoveEntryCommandTests.cs`
  - `markdown-journal-cli.Tests/Services/JournalUpdate/JournalUpdateServiceTests.cs`
- [X] T008 [US2] Validate: run `dotnet test` and confirm exit 0, all tests pass, zero failures, zero skipped

**Checkpoint**: All packages resolved and compatible with `net10.0`. Full test suite green. User Story 2 is complete and independently verifiable.

---

## Phase 4: User Story 3 — Developer Tooling and Documentation Reflect .NET 10 (Priority: P3)

**Goal**: All `net9.0` path references in VS Code configuration files and all `.NET 9` references in user-facing documentation are replaced with the correct .NET 10 equivalents.

**Independent Test**: Grep for `net9.0` across `.vscode/`, `README.md`, `docs/DEVELOPMENT.md`, `.instructions.md` — expect zero matches.

### Implementation for User Story 3

- [X] T009 [P] [US3] Replace all `net9.0` output path occurrences with `net10.0` in `.vscode/launch.json` (14 path references)
- [X] T010 [P] [US3] Replace the `net9.0` path reference with `net10.0` in `.vscode/tasks.json` (rollback-test task command)
- [X] T011 [P] [US3] Update `README.md` line 52: `.NET 9.0 or later` → `.NET 10.0 or later`
- [X] T012 [P] [US3] Update `docs/DEVELOPMENT.md` line 10: `.NET 9.0 SDK` → `.NET 10.0 SDK`
- [X] T013 [P] [US3] Update `.instructions.md` line 8: `A .NET 9 CLI application` → `A .NET 10 CLI application`

**Checkpoint**: No `net9.0` or `.NET 9` text remains in VS Code config or user-facing docs. User Story 3 is complete and independently verifiable.

---

## Phase 5: Polish & Final Validation

**Purpose**: Confirm all three user stories are coherent and the full success criteria (SC-001 through SC-004) are satisfied.

- [X] T014 [P] Verify zero residual references: run `grep -r "net9.0\|\.NET 9" --include="*.csproj" --include="*.json" --include="*.md" --include="*.cs" --exclude-dir=".git" --exclude-dir="TODO" --exclude-dir="bin" --exclude-dir="obj" .` — expect zero matches
- [X] T015 Final validation: run `dotnet build` (exit 0, zero diagnostics) then `dotnet test` (exit 0, all tests green) against the complete updated codebase

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **User Story 1 (Phase 2)**: Depends on Phase 1 (global.json must exist before `dotnet` commands)
- **User Story 2 (Phase 3)**: Depends on User Story 1 (packages target `net10.0`)
- **User Story 3 (Phase 4)**: Independent of US1 and US2 — can run in parallel with Phase 2 or 3
- **Polish (Phase 5)**: Depends on all three user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Starts after Phase 1 — no dependencies on other stories
- **User Story 2 (P2)**: Starts after User Story 1 — requires `net10.0` TFM to be in place
- **User Story 3 (P3)**: Independent — can be worked in parallel with US1 or US2 (touches different files)

### Within Each User Story

- T002 and T003 are independently parallelizable (separate `.csproj` files)
- T005 and T006 are independently parallelizable (separate `.csproj` files)
- T009 through T013 are all independently parallelizable (all different files)
- T007 is sequential — depends on T005 and T006 completing first

---

## Parallel Execution Example: US1 + US3 Concurrent

```
Phase 1: T001                                    # global.json
          ↓
Phase 2:  T002 ──┐ (parallel)
          T003 ──┘                               # Both TFM changes
          T004                                   # Build gate
          ↓
Phase 3:  T005 ──┐ (parallel)
          T006 ──┘                               # Both package updates
          T007                                   # TestConsole fix (if needed)
          T008                                   # Test gate
          ↓
Phase 4:  T009 ──┐
          T010   │
          T011   │ (all parallel — different files)
          T012   │
          T013 ──┘
          ↓
Phase 5:  T014 ─ T015                            # Final validation
```

**US3 parallel opportunity**: T009–T013 touch only `.vscode/` and documentation files. They can be started immediately after T001 (or even concurrently with Phase 2/3) since they have no compile-time dependencies.

---

## Implementation Strategy

**MVP Scope**: Phase 1 + Phase 2 (T001–T004)
- Delivers: Both project files target `net10.0`, build gate passes
- Completeness: Satisfies `FR-001`, `FR-002`, `SC-001`

**Full Delivery**: All phases in priority order
- P1 → P2 → P3 → Polish
- Satisfies all functional requirements (FR-001 through FR-012) and success criteria (SC-001 through SC-004)
