# Tasks: Delete Entry --clean-refs Tolerates Already-Deleted Files

**Input**: Design documents from `/specs/004-delete-clean-refs-missing-file/`
**Prerequisites**: plan.md ✅ | spec.md ✅ | data-model.md ✅ | quickstart.md ✅

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to

---

## Phase 1: Setup

No new dependencies or project structure changes required — this is a pure code modification feature targeting existing files.

**Checkpoint**: Ready to begin implementation immediately.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Thread `cleanRefs` through the validation layer so the file-existence guard is conditionally relaxed. All user-story work depends on this foundation being in place first.

**⚠️ CRITICAL**: No user story can be verified end-to-end until T001–T003 are complete.

- [X] T001 Update `IRemoveEntryService.ValidatePreconditions` signature in `markdown-journal-cli/Services/RemoveEntry/IRemoveEntryService.cs` to accept `bool cleanRefs = false` as a third parameter with a default value of `false`, so existing call sites compile unchanged
- [X] T002 Update `ResolveAndValidate` private helper in `markdown-journal-cli/Services/RemoveEntry/RemoveEntryService.cs` to accept `bool cleanRefs = false`, skip the file-existence assertion (step 5) when `cleanRefs` is `true`, and return a third tuple element `bool fileExists` instead of always throwing `FileNotFoundException`
- [X] T003 Update `RemoveEntryCommand.ExecuteCore` in `markdown-journal-cli/Commands/Remove/RemoveEntryCommand.cs`: change `ValidatePreconditions(settings.FilePath, settings.FileName)` to `ValidatePreconditions(settings.FilePath, settings.FileName, settings.CleanRefs)` so the pre-flight check mirrors the relaxed guard

**Checkpoint**: Foundation ready — the `--clean-refs` non-force path no longer throws `FileNotFoundException` when the file is absent.

---

## Phase 3: User Story 1 — Clean Refs After Manual File Deletion (Priority: P1) 🎯 MVP

**Goal**: A manually deleted file with orphaned references can be fully cleaned up via `remove entry <file> --clean-refs` (with or without `--force`).

**Independent Test**: Create journal, add two entries where Beta links to Alpha. Delete `Alpha.md` from disk. Run `remove entry Alpha --clean-refs` without `--force`, confirm the prompt. Expect exit 0, dead link stripped in Beta.md, Alpha absent from config and tracking.

- [X] T004 Update `RemoveEntryService.RemoveEntry` in `markdown-journal-cli/Services/RemoveEntry/RemoveEntryService.cs` to skip `tx.TrackDelete` and `_fileSystem.DeleteFile` when `fileExists` is `false` (file already absent from disk), while still running config removal, tracking removal, TOC regeneration, and link stripping
- [X] T005 [P] Add unit test `Execute_Should_PassCleanRefsToValidatePreconditions_When_ForceNotSet` in `markdown-journal-cli.Tests/Commands/Remove/RemoveEntryCommandTests.cs`: push `"y"` to input, run with `CleanRefs=true` and `Force=false`, verify `ValidatePreconditions` called with `cleanRefs: true`
- [X] T006 [P] Add unit test `Execute_Should_ShowConfirmationAndSucceed_When_FileAbsentAndCleanRefsSet` in `markdown-journal-cli.Tests/Commands/Remove/RemoveEntryCommandTests.cs`: configure `ValidatePreconditions` to not throw (file absent but cleanRefs=true), push `"y"`, assert exit 0 and output contains "Success:" and "Stripped links:"
- [X] T007 Add integration test `Execute_Should_Succeed_When_EntryAlreadyDeletedAndCleanRefsSet` in `markdown-journal-cli.Tests/Commands/Remove/RemoveEntryCommandIntegrationTests.cs`: seed Alpha and Beta where Beta links to Alpha, delete `Alpha.md`, run `--clean-refs --force`, assert exit 0, "Success:", and Beta.md no longer contains "Alpha.md"
- [X] T008 Add integration test `Execute_Should_ShowPromptNotError_When_FileAlreadyDeletedAndCleanRefsSetWithoutForce` in `markdown-journal-cli.Tests/Commands/Remove/RemoveEntryCommandIntegrationTests.cs`: delete Alpha.md, push `"y"` to `_app.Console.Input`, run `--clean-refs` (no `--force`), assert exit 0 and no "Error:" in output

**Checkpoint**: US1 fully functional — `--clean-refs` on a missing file works with and without `--force`.

---

## Phase 4: User Story 2 — Partial Cleanup After Interrupted Removal (Priority: P2)

**Goal**: A second run of `remove entry <file> --clean-refs --force` completes a previously interrupted cleanup (file deleted, config/tracking still reference it).

**Independent Test**: Seed a journal entry. Delete the `.md` file manually. Confirm `.journalrc` still references it. Run `remove entry <file> --clean-refs --force`. Assert config and tracking no longer reference the file, exit 0.

- [X] T009 Add integration test `Execute_Should_CompletePartialCleanup_When_FileDeletedButMetadataIntact` in `markdown-journal-cli.Tests/Commands/Remove/RemoveEntryCommandIntegrationTests.cs`: seed Alpha, delete `Alpha.md`, assert `.journalrc` still contains Alpha, run `--clean-refs --force`, assert `.journalrc` and tracking index no longer reference Alpha, exit 0

**Checkpoint**: US2 fully functional — re-running after an interrupted removal completes without error.

---

## Phase 5: User Story 3 — Fully-Cleaned State: Honest Output When Already Gone (Priority: P2)

**Goal**: When a second run finds the file absent from disk, config, and tracking, the command must produce honest output: no false "removed from config/tracking" lines, no unnecessary TOC write, a clear "no dead references" signal, and a distinct "nothing to remove" final message rather than "Success: Entry removed." The service must return a structured `RemoveEntryResult` so the command layer can derive all of this without guesswork.

**Independent Test**: Run `remove entry Alpha.md --clean-refs --force` twice. First run exits 0, cleans up, says "Success:". Second run exits 0, output does NOT contain "removed from config", "removed from tracking", or "Table of contents updated.", DOES contain "No dead references found.", and ends with "was not found in the journal — nothing to remove."

### Implementation for User Story 3

- [X] T010 Introduce `RemoveEntryResult` record in `markdown-journal-cli/Services/RemoveEntry/RemoveEntryService.cs` (top of file, before the class): `record RemoveEntryResult(bool FileExistedOnDisk, bool RemovedFromConfig, bool RemovedFromTracking, IReadOnlyList<string> StrippedLinkFiles)`
- [X] T011 Update `markdown-journal-cli/Services/RemoveEntry/IRemoveEntryService.cs`: change `RemoveEntry` return type from `IReadOnlyList<string>` to `RemoveEntryResult`; update XML `<returns>` doc accordingly
- [X] T012 Update `RemoveEntryService.RemoveEntry` in `markdown-journal-cli/Services/RemoveEntry/RemoveEntryService.cs`:
  - Capture `bool removedFromConfig = _journalConfiguration.RemoveEntry(journalPath, resolvedFileName)`
  - Before calling `RemoveFileFromIndex`, call `_fileTracking.LoadIndex(journalPath)` and check `index.Files.ContainsKey(resolvedFileName)` to get `bool removedFromTracking`
  - Call `UpdateTableOfContents` only when `removedFromConfig` is `true` (skip the TOC write when the entry was already absent from config — nothing changed in the TOC)
  - Return `new RemoveEntryResult(fileExists, removedFromConfig, removedFromTracking, modifiedFiles)` in the cleanRefs branch and `new RemoveEntryResult(fileExists, removedFromConfig, removedFromTracking, [])` otherwise
- [X] T013 Update `RemoveEntryCommand.ExecuteCore` in `markdown-journal-cli/Commands/Remove/RemoveEntryCommand.cs`:
  - Derive `bool anythingRemoved = result.FileExistedOnDisk || result.RemovedFromConfig || result.RemovedFromTracking`
  - Print "Removed:" header, "removed from config/tracking" lines, and "Table of contents updated." only when `anythingRemoved` is `true` (FR-007)
  - When `--clean-refs` is set and `result.StrippedLinkFiles.Count > 0`: print each "Stripped links:" line then "Cleaned dead references in N file(s)."
  - When `--clean-refs` is set and `result.StrippedLinkFiles.Count == 0`: print `[dim]No dead references found.[/]` (FR-008 revised)
  - When `anythingRemoved`: print `[green]Success:[/] Entry '...' removed.`
  - When NOT `anythingRemoved`: print `[dim]'...' was not found in the journal — nothing to remove.[/]`
- [X] T014 [P] Update `markdown-journal-cli.Tests/Commands/Remove/RemoveEntryCommandTests.cs`:
  - Change default mock setup from `Returns(Array.Empty<string>())` to `Returns(new RemoveEntryResult(true, true, true, Array.Empty<string>()))`
  - Update the `Execute_Should_CallCleanRefsOnService_When_CleanRefsSet` test mock to `Returns(new RemoveEntryResult(true, true, true, modifiedFiles))`
  - Update the `Execute_Should_ShowConfirmationAndSucceed_When_FileAbsentAndCleanRefsSet` test mock to `Returns(new RemoveEntryResult(false, true, true, modifiedFiles))`
  - Update the `Execute_Should_EscapeMarkupCorrectly` test mock to use `RemoveEntryResult`
  - Add test `Execute_Should_NotPrintRemovedFromConfig_When_AlreadyAbsent`: mock returning `RemoveEntryResult(false, false, false, [])`, assert no "removed from config/tracking", no "Success:", output contains "not found in the journal" and "No dead references found."
  - Add test `Execute_Should_PrintNoDeadRefsMessage_When_CleanRefsSetAndNoLinksFound`: mock returning `RemoveEntryResult(true, true, true, [])`, run with `CleanRefs=true`, assert output contains "No dead references found." and does NOT contain "Cleaned dead references in 0"
- [X] T015 [P] Update `markdown-journal-cli.Tests/Services/RemoveEntry/RemoveEntryServiceTests.cs`:
  - Add default mock setups in `SetupDefaultMockBehaviors`: `MockJournalConfiguration.Setup(RemoveEntry).Returns(true)` and `MockFileTracking.Setup(LoadIndex).Returns(new JournalIndex { Files = { [EntryFileName] = ... } })`
  - Update `RemoveEntry_Should_DeleteFileAndUpdateConfigAndTrackingAndToc` assertions to use `result.StrippedLinkFiles.ShouldBeEmpty()`, `result.FileExistedOnDisk.ShouldBeTrue()`, `result.RemovedFromConfig.ShouldBeTrue()`, `result.RemovedFromTracking.ShouldBeTrue()`
  - Update `RemoveEntry_Should_UpdateTrackingForEachModifiedFile_When_CleanRefsIsTrue` assertion from `result.ShouldBe(modifiedFiles)` to `result.StrippedLinkFiles.ShouldBe(modifiedFiles)`
  - Update `RemoveEntry_Should_SucceedWithoutDeleting_When_FileAbsentAndCleanRefsIsTrue` assertion to use `result.StrippedLinkFiles.ShouldBeEmpty()` and `result.FileExistedOnDisk.ShouldBeFalse()`
  - Update `RemoveEntry_Should_StillStripLinksInDirectory_When_FileAbsentAndCleanRefsIsTrue` assertion to `result.StrippedLinkFiles.ShouldBe(modifiedFiles)`
  - Add test `RemoveEntry_Should_ReturnRemovedFromConfigFalse_When_EntryNotInConfig`: setup `MockJournalConfiguration.RemoveEntry` to return `false`; assert `result.RemovedFromConfig == false`
  - Add test `RemoveEntry_Should_ReturnRemovedFromConfigTrue_When_EntryWasInConfig`: setup to return `true`; assert `result.RemovedFromConfig == true`
  - Add test `RemoveEntry_Should_ReturnRemovedFromTrackingFalse_When_EntryNotInIndex`: setup `MockFileTracking.LoadIndex` to return empty `JournalIndex`; assert `result.RemovedFromTracking == false`
- [X] T016 Add integration test `Execute_Should_NotReportFalseRemovals_When_SecondRunOnFullyCleanedJournal` in `markdown-journal-cli.Tests/Commands/Remove/RemoveEntryCommandIntegrationTests.cs`: run `--clean-refs --force` (first run cleans up), capture output length as offset, run same command again, slice output from offset, assert second-run output does NOT contain "removed from config" or "removed from tracking", DOES contain "No dead references found.", and ends without "Success:"

**Checkpoint**: All three user stories fully functional — output is honest, TOC skipped when nothing changes, and users get a clear "nothing to remove" signal on idempotent re-runs.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T017 [P] Update `README.md` `remove entry` examples: make `--clean-refs` usage (without `--force`) the primary example; keep `--force` variant as a secondary note clarifying it skips the confirmation prompt; place non-`--clean-refs` variants after

---

## Dependencies

```
T001 → T002 → T003 (foundational; all US work depends on this chain)
T003 → T004 → T007, T008 (US1 implementation and tests)
T010 → T011 → T012 → T013 (US3 RemoveEntryResult chain)
T012 → T014, T015 (test updates depend on service signature change)
T013 → T016 (integration test depends on command output shape)
T017 independent
```

## Parallel Execution per Story

```
US1:  T005, T006 can run in parallel (both in RemoveEntryCommandTests, no shared state)
US3:  T014, T015 can run in parallel (different test files)
T017  independent of everything
```

## Implementation Strategy

- **MVP (US1 only)**: T001 → T002 → T003 → T004 — gets the core fix in place with no output changes
- **Full US1 with tests**: add T005, T006, T007, T008
- **US2**: T009 (single integration test, no service changes)
- **US3 (honest output)**: T010 → T011 → T012 → T013 → T014 → T015 → T016
- **Polish**: T017 independently at any point after US1

All tasks are implemented. ✅
