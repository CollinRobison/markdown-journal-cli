# Tasks: Update --sync Flag (Skip Last-Edited Dates)

**Input**: Design documents from `/specs/003-sync-skip-dates/`
**Branch**: `003-sync-skip-dates`
**Date**: 2026-04-15

**Prerequisites consulted**: plan.md ✅ | spec.md ✅ | research.md ✅ | data-model.md ✅ | contracts/update-journal-command.md ✅ | quickstart.md ✅

---

## Phase 1: Setup

**Purpose**: No new project structure, packages, or infrastructure required. This feature modifies two existing files and adds tests to three existing test files. No setup tasks are needed.

*Phase 1 skipped — this feature has no setup work.*

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The `bool Sync` property and its `Validate()` conflict checks must exist in `UpdateJournalSettings` before the routing logic in `UpdateCommand` can reference `settings.Sync`. This single change unblocks all user story implementation and all tests.

- [X] T001 Add `bool Sync` property with `[CommandOption("--sync")]` and four `Validate()` conflict checks (`--sync`+`--date`, `--sync`+`--tracking`, `--sync`+`--config`, `--sync`+`--toc`) to `UpdateJournalSettings` in `markdown-journal-cli/Commands/Update/UpdateSettings.cs`

**Checkpoint**: `settings.Sync` is now available — all three user story phases can proceed.

---

## Phase 3: User Story 1 — Sync After Git Pull / Merge (Priority: P1) 🎯 MVP

**Goal**: `mdjournal update journal --sync` rebuilds tracking/config/TOC without writing "Last Edited:" to user entry files. Includes the `[dim]--sync active...[/]` summary line (FR-012) and the no-op early-return path.

**Independent Test**: Run `update journal --sync` against a journal with stale tracking hashes. Confirm: (1) tracking hashes updated, (2) config reflects adds/removes, (3) TOC regenerated, (4) no entry file "Last Edited:" changed.

### Implementation for User Story 1

- [X] T002 [US1] Exclude `settings.Sync` from the `bool all` local variable detection in `UpdateCommand.ExecuteCore`, update the outer change-detection guard to include `settings.Sync`, and add the `--sync` routing block (calls `UpdateLastEditedDatesAndTracking(trackingOnly: true)`, `UpdateJournalConfig`, `UpdateTableOfContents`, then prints the FR-012 summary line) in `markdown-journal-cli/Commands/Update/UpdateCommand.cs`

### Tests for User Story 1

- [X] T003 [P] [US1] Add unit test `ExecuteCore_Should_UpdateTrackingConfigToc_When_SyncFlagSet` verifying `UpdateLastEditedDatesAndTracking(trackingOnly: true)`, `UpdateJournalConfig`, and `UpdateTableOfContents` are all called and console output contains `--sync active` in `markdown-journal-cli.Tests/Commands/Update/UpdateCommandTests.cs`
- [X] T004 [P] [US1] Add unit test `ExecuteCore_Should_PrintSyncActiveLine_When_SyncFlagAndChangesExist` verifying the FR-012 summary line appears in output when `hasAnythingToDo` is true in `markdown-journal-cli.Tests/Commands/Update/UpdateCommandTests.cs`
- [X] T005 [P] [US1] Add unit test `ExecuteCore_Should_NotPrintSyncActiveLine_When_SyncFlagAndNoChanges` verifying the `--sync active` line does NOT appear when journal is already up to date in `markdown-journal-cli.Tests/Commands/Update/UpdateCommandTests.cs`
- [X] T006 [P] [US1] Add unit test `ExecuteCore_Should_NotCallUpdateLastEditedDates_When_SyncFlagSet` verifying `UpdateLastEditedDatesAndTracking` is never called with `trackingOnly: false` when `--sync` is active in `markdown-journal-cli.Tests/Commands/Update/UpdateCommandTests.cs`
- [X] T007 [P] [US1] Add integration test `UpdateJournal_Should_NotModifyEntryLastEditedDates_When_SyncFlag` on a journal with stale tracking — verifies entry file "Last Edited:" fields are unchanged after sync in `markdown-journal-cli.Tests/Commands/Update/UpdateCommandIntegrationTests.cs`
- [X] T008 [P] [US1] Add integration test `UpdateJournal_Should_UpdateTrackingAndConfig_When_SyncFlag` verifying tracking hashes and config are updated after sync in `markdown-journal-cli.Tests/Commands/Update/UpdateCommandIntegrationTests.cs`
- [X] T009 [P] [US1] Add integration test `UpdateJournal_Should_ReturnZeroAndPrintUpToDate_When_SyncFlagAndJournalCurrent` verifying exit 0 and "Everything is up to date." when nothing has changed in `markdown-journal-cli.Tests/Commands/Update/UpdateCommandIntegrationTests.cs`
- [X] T010 [P] [US1] Add integration test `UpdateJournal_Should_AddNewEntryToTracking_When_SyncFlagAndNewFile` verifying a new (untracked) entry file is added to the tracking index without writing a "Last Edited:" date in `markdown-journal-cli.Tests/Commands/Update/UpdateCommandIntegrationTests.cs`
- [X] T011 [P] [US1] Add integration test `UpdateJournal_Should_RemoveDeletedEntryFromTracking_When_SyncFlagAndDeletedFile` verifying deleted entries are removed from tracking/config without modifying remaining entry files in `markdown-journal-cli.Tests/Commands/Update/UpdateCommandIntegrationTests.cs`

**Checkpoint**: US1 is fully functional — `--sync` syncs all three subsystems without date writes. All 9 tests above pass.

---

## Phase 4: User Story 2 — Dry-Run Preview of Sync (Priority: P2)

**Goal**: `update journal --sync --dry-run` shows a preview report covering tracking/config/TOC changes and exits without writing any files.

**Independent Test**: Run `update journal --sync --dry-run` against a journal with stale tracking. Confirm: output shows tracking/config/TOC preview sections; no "Last Edited:" update rows; no files are written.

### Implementation for User Story 2

- [X] T012 [US2] Update `ExecuteDryRun` include-flag expressions to include `settings.Sync` for `includeTracking`, `includeConfig`, and `includeToc` in `markdown-journal-cli/Commands/Update/UpdateCommand.cs`

### Tests for User Story 2

- [X] T013 [P] [US2] Add unit test `ExecuteDryRun_Should_IncludeAllSections_When_SyncFlag` verifying `BuildDryRunReport` is called with non-null `trackingChanges`, non-null `configChanges`, and `includeToc: true` when `--sync --dry-run` is used in `markdown-journal-cli.Tests/Commands/Update/UpdateCommandTests.cs`
- [X] T014 [P] [US2] Add unit test `ExecuteDryRun_Should_WriteNoFiles_When_SyncDryRun` verifying no write methods on `IFileSystem` are called when `--sync --dry-run` is used in `markdown-journal-cli.Tests/Commands/Update/UpdateCommandTests.cs`

**Checkpoint**: US2 is fully functional — `--sync --dry-run` shows all three preview sections and makes no writes.

---

## Phase 5: User Story 3 — Contradictory Flag Rejection (Priority: P3)

**Goal**: Combining `--sync` with `--date`, `--tracking`, `--config`, or `--toc` returns a validation error before any I/O.

**Independent Test**: Run each of the four invalid combinations. Confirm: each exits non-zero with a human-readable error identifying the conflicting flag; no files are read or written.

*Note: The `Validate()` implementation is part of T001 (foundational). This phase adds only the corresponding unit tests.*

### Tests for User Story 3

- [ ] T015 [P] [US3] Add unit test `Validate_Should_ReturnError_When_SyncAndDateCombined` verifying `ValidationResult.Error` with message naming `--date` in `markdown-journal-cli.Tests/Commands/Update/UpdateCommandTests.cs`
- [ ] T016 [P] [US3] Add unit test `Validate_Should_ReturnError_When_SyncAndTrackingCombined` verifying `ValidationResult.Error` with message naming `--tracking` in `markdown-journal-cli.Tests/Commands/Update/UpdateCommandTests.cs`
- [ ] T017 [P] [US3] Add unit test `Validate_Should_ReturnError_When_SyncAndConfigCombined` verifying `ValidationResult.Error` with message naming `--config` in `markdown-journal-cli.Tests/Commands/Update/UpdateCommandTests.cs`
- [ ] T018 [P] [US3] Add unit test `Validate_Should_ReturnError_When_SyncAndTocCombined` verifying `ValidationResult.Error` with message naming `--toc` in `markdown-journal-cli.Tests/Commands/Update/UpdateCommandTests.cs`

**Checkpoint**: US3 is fully functional — all four contradictory combinations are rejected at validation before any I/O.

---

## Phase 6: Edge Case & Resilience Tests (FR-011)

**Goal**: Cover atomic rollback on partial failure and malformed tracking-index abort — both as unit tests using `TestFileSystem` per FR-011 and spec clarification (Q8/Q9 session 2026-04-15).

- [ ] T019 [P] Add unit test `ExecuteCore_Should_RollbackAllWrites_When_SyncPartiallyFails` using `TestFileSystem` + `FaultInjectingFileSystem` to simulate TOC write failure mid-sync, verifying exit code 2 and no partial file state in `markdown-journal-cli.Tests/Commands/Update/UpdateCommandTests.cs`
- [ ] T020 [P] Add unit test `ExecuteCore_Should_AbortBeforeWrites_When_SyncAndTrackingIndexMalformed` using `TestFileSystem` with malformed `.mdjournal` content, verifying a descriptive error is returned and no writes are performed in `markdown-journal-cli.Tests/Commands/Update/UpdateCommandTests.cs`

---

## Phase 7: Polish & Cross-Cutting Concerns

**Goal**: Verify FR-010 (no pre-existing tests regressed) and validate the full implementation against the CLI contract in `contracts/update-journal-command.md`.

- [ ] T021 Run the full test suite (`dotnet test`) and confirm all pre-existing tests still pass (FR-010 / SC-004)
- [ ] T022 [P] Verify console output format against the contract in `specs/003-sync-skip-dates/contracts/update-journal-command.md` — confirm `[dim]--sync active: Last Edited dates were not updated[/]` markup renders correctly and the no-op path shows only "Everything is up to date." with no extra lines

---

## Dependencies

```
T001 (foundational: Sync property + Validate)
  └── T002 (US1 implementation: all routing)
        ├── T003–T011 (US1 tests — can all run in parallel after T002)
  └── T012 (US2 dry-run implementation)
        ├── T013–T014 (US2 tests — can run in parallel after T012)
  └── T015–T018 (US3 validation tests — test Validate() added in T001; can all run in parallel)
  └── T019–T020 (edge case tests — depend on T002)
  └── T021–T022 (polish — depend on all above)
```

**Most stories are independent** (T003–T011 can all be written in parallel after T002).

---

## Parallel Execution Examples

### After T001 + T002 complete

```
T003, T004, T005, T006  ← US1 unit tests (all independent, different test methods)
T007, T008, T009        ← US1 integration tests (all independent)
T010, T011              ← US1 integration tests (new-file, deleted-file — independent)
T015, T016, T017, T018  ← US3 validation tests (all test different flag combos)
T019, T020              ← Edge case tests (independent of US1 story tests)
```

### After T012 completes

```
T013, T014  ← US2 dry-run tests (independent of each other)
```

---

## Implementation Strategy

**MVP Scope**: Complete Phase 2 (T001) + Phase 3 (T002–T011) to deliver the core sync behavior with full test coverage. US1 is independently testable and delivers the primary motivating use case.

**Increment 2**: Phase 4 (T012–T014) — dry-run composability.  
**Increment 3**: Phase 5 (T015–T018) — validation rejection tests (Validate() code is already in T001; these are tests only).  
**Increment 4**: Phase 6 (T019–T020) + Phase 7 (T021–T022) — edge cases and polish.

---

## Summary

| Phase | Tasks | Story |
|-------|-------|-------|
| 2: Foundational | T001 | — |
| 3: US1 Sync After Git Pull | T002–T011 | US1 (P1) |
| 4: US2 Dry-Run Preview | T012–T014 | US2 (P2) |
| 5: US3 Flag Rejection | T015–T018 | US3 (P3) |
| 6: Edge Cases | T019–T020 | — |
| 7: Polish | T021–T022 | — |
| **Total** | **22 tasks** | |

**Parallel opportunities**: 16 of 22 tasks carry `[P]` — most test tasks after T002 are fully independent.  
**Independent test criteria**: Each user story phase ends with an explicit checkpoint describing how to verify the story in isolation before proceeding.  
**Suggested MVP**: T001 + T002 + T003–T011 (12 tasks) — delivers the complete core sync behavior with full test coverage.
