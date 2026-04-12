# Tasks: Test Suite Deep Dive & Cleanup

**Input**: Design documents from `/specs/002-test-suite-cleanup/`
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/ ✅, quickstart.md ✅

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no outstanding dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- Exact file paths are included in every task description

---

## Phase 1: Setup

**Purpose**: Establish a verified baseline before any changes are made.

- [x] T001 Run `dotnet test markdown-journal-cli.Tests/markdown-journal-cli.Tests.csproj` and record the passing test count as the pre-cleanup baseline in `specs/002-test-suite-cleanup/tasks.md` (update the note at the bottom of this file)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Create the four shared test infrastructure classes that every subsequent phase depends on. All four files are new and can be written in parallel.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete — integration tests require `JournalIntegrationTestBase`; mock-migration tasks require `MockFactory`, `CommandTestBase`, and `ServiceTestBase`.

- [x] T002 [P] Create `MockFactory` static class with `CreateFileSystem()`, `CreateJournalConfiguration()`, `CreateFileTracking()`, `CreateTemplateManager()`, `CreateTableOfContentsService()`, `CreateEntryFormatterService()`, and `CreateJournalSettings()` factory methods per contracts/test-infrastructure-api.md in `markdown-journal-cli.Tests/Infrastructure/MockFactory.cs`
- [x] T003 [P] Create `CommandTestBase` abstract class with all six `Mock<T>` fields (`MockFileSystem`, `MockJournalConfiguration`, `MockFileTracking`, `MockTemplateManager`, `MockTableOfContentsService`, `MockEntryFormatterService`) and `IOptions<JournalSettings>`, a virtual `SetupDefaultBehaviors()` method, and a `BuildApp(Action<IConfigurator> configure)` protected helper that returns a fresh `CommandAppTester` per contracts/test-infrastructure-api.md in `markdown-journal-cli.Tests/Infrastructure/CommandTestBase.cs`
- [x] T004 [P] Create `ServiceTestBase` abstract class with the same six `Mock<T>` fields as `CommandTestBase`, plus `NoOpCoordinator` (`NoOpFileTransactionCoordinator.Instance`), `NoOpReporter` (`NoOpRollbackReporter.Instance`), and a `NullLogger<T>()` helper; add XML doc comment pointing developers to `ServiceRollbackTestBase` for rollback scenarios per contracts/test-infrastructure-api.md in `markdown-journal-cli.Tests/Infrastructure/ServiceTestBase.cs`
- [x] T005 [P] Create `JournalIntegrationTestBase` abstract class implementing `IDisposable` with `JournalRoot`, `JournalPath`, `FileSystem` (real `FileSystem` instance), and `IOptions<JournalSettings>` wired to `JournalPath`; implement `InitializeJournal()` seeding helper and `Dispose()` calling `Directory.Delete(JournalRoot, recursive: true)` per research.md section 4 and contracts/test-infrastructure-api.md in `markdown-journal-cli.Tests/Infrastructure/JournalIntegrationTestBase.cs`
- [x] T006 Run `dotnet build markdown-journal-cli.Tests/markdown-journal-cli.Tests.csproj` and confirm all four new Infrastructure files compile with zero errors before proceeding

**Checkpoint**: All four shared infrastructure classes compile — user story phases may now begin in parallel

---

## Phase 3: User Story 1 — Missing Command Integration Tests (Priority: P1) 🎯 MVP

**Goal**: Every CLI command (`init`, `new`, `add`, `update`, `remove`) has at least one integration test class exercising the full command pipeline against real disk I/O with no mocked dependencies (FR-001, SC-001).

**Independent Test**: Run `dotnet test --filter "Category=Integration"` and confirm `InitCommandIntegrationTests`, `NewCommandIntegrationTests`, `UpdateCommandIntegrationTests`, and `RemoveEntryCommandIntegrationTests` all exist and pass; confirm temp directories are absent after the run.

- [x] T007 [US1] Migrate `AddEntryIntegrationTests` to extend `JournalIntegrationTestBase` — remove inline temp-dir creation and `Dispose()` boilerplate, wire real services via the base-class `FileSystem` and `JournalSettings`, confirm all existing tests still pass in `markdown-journal-cli.Tests/Commands/Add/AddEntryIntegrationTests.cs`
- [x] T008 [P] [US1] Migrate `AddTableOfContentsIntegrationTests` to extend `JournalIntegrationTestBase` — same migration as T007: remove inline temp-dir and dispose boilerplate, use base-class infrastructure in `markdown-journal-cli.Tests/Commands/Add/AddTableOfContentsIntegrationTests.cs`
- [x] T009 [P] [US1] Create `InitCommandIntegrationTests` extending `JournalIntegrationTestBase` — wire real `InitJournalService` and real `FileTransactionCoordinator`, invoke `init` command via `CommandAppTester` against `JournalRoot`, assert `.journalrc`, `.mdjournal`, and `1a-TableOfContents.md` exist on disk after the command completes in `markdown-journal-cli.Tests/Commands/Init/InitCommandIntegrationTests.cs`
- [x] T010 [P] [US1] Create `NewCommandIntegrationTests` extending `JournalIntegrationTestBase` — wire real `NewJournalService`, invoke `new` command via `CommandAppTester`, assert the expected journal subfolder and files are created on disk in `markdown-journal-cli.Tests/Commands/New/NewCommandIntegrationTests.cs`
- [x] T011 [P] [US1] Create `UpdateCommandIntegrationTests` extending `JournalIntegrationTestBase` — call `InitializeJournal()`, add an entry, invoke the `update` command via `CommandAppTester`, assert the entry file and TOC reflect the change in `markdown-journal-cli.Tests/Commands/Update/UpdateCommandIntegrationTests.cs`
- [x] T012 [P] [US1] Create `RemoveEntryCommandIntegrationTests` extending `JournalIntegrationTestBase` — call `InitializeJournal()`, add an entry, invoke the `remove` command via `CommandAppTester`, assert the entry file is absent and TOC is updated in `markdown-journal-cli.Tests/Commands/Remove/RemoveEntryCommandIntegrationTests.cs`
- [x] T013 [US1] Run all integration test classes (`dotnet test --filter "FullyQualifiedName~IntegrationTests"`) and confirm all pass, exit code 0, and no temp directories remain under `Path.GetTempPath()` matching the `journal-*` pattern

**Checkpoint**: All five commands have integration test coverage (SC-001) — this story is independently verifiable

---

## Phase 4: User Story 2 — Consistent Mock Usage in Unit Tests (Priority: P1)

**Goal**: Every unit test file uses Moq via the shared `CommandTestBase` / `ServiceTestBase` base classes, eliminating mixed strategies and per-class mock-wiring duplication (FR-002, FR-004, SC-002, SC-004).

**Independent Test**: Inspect all migrated unit test class declarations — each must extend `CommandTestBase` or `ServiceTestBase`; no test class in `Commands/` or `Services/` may declare its own `Mock<T>` fields independently for dependencies already covered by the base class.

### Command unit test migration to `CommandTestBase`

- [x] T014 [P] [US2] Migrate `AddEntryCommandTests` to extend `CommandTestBase` — remove inline `Mock<T>` field declarations and constructor wiring for all dependencies covered by the base class; update each test method to call `BuildApp(...)` for a fresh tester and keep only scenario-specific `Setup()` overrides in `markdown-journal-cli.Tests/Commands/Add/AddEntryCommandTests.cs`
- [x] T015 [P] [US2] Migrate `AddFileTrackingCommandTests` to extend `CommandTestBase` — same migration pattern as T014 in `markdown-journal-cli.Tests/Commands/Add/AddFileTrackingCommandTests.cs`
- [x] T016 [P] [US2] Migrate `AddJournalrcCommandTests` to extend `CommandTestBase` — same migration pattern as T014 in `markdown-journal-cli.Tests/Commands/Add/AddJournalrcCommandTests.cs`
- [x] T017 [P] [US2] Migrate `AddTableOfContentsCommandTests` to extend `CommandTestBase` — same migration pattern as T014 in `markdown-journal-cli.Tests/Commands/Add/AddTableOfContentsCommandTests.cs`
- [x] T018 [P] [US2] Migrate `InitCommandTests` to extend `CommandTestBase` — same migration pattern as T014 in `markdown-journal-cli.Tests/Commands/Init/InitCommandTests.cs`
- [x] T019 [P] [US2] Migrate `NewCommandTests` to extend `CommandTestBase` — same migration pattern as T014 in `markdown-journal-cli.Tests/Commands/New/NewCommandTests.cs`
- [x] T020 [P] [US2] Migrate `RemoveEntryCommandTests` to extend `CommandTestBase` — same migration pattern as T014 in `markdown-journal-cli.Tests/Commands/Remove/RemoveEntryCommandTests.cs`
- [x] T021 [P] [US2] Migrate `UpdateCommandTests` to extend `CommandTestBase` — same migration pattern as T014; note this file will receive a dedup audit in T034 (Phase 5) in `markdown-journal-cli.Tests/Commands/Update/UpdateCommandTests.cs`
- [x] T022 [P] [US2] Migrate `UpdateEntryCommandTests` to extend `CommandTestBase` — same migration pattern as T014; note this file will receive a dedup audit in T034 (Phase 5) in `markdown-journal-cli.Tests/Commands/Update/UpdateEntryCommandTests.cs`

### Service unit test migration to `ServiceTestBase`

- [x] T023 [P] [US2] Migrate `EntryFormatterServiceTests` to extend `ServiceTestBase` — remove inline mock declarations, use `MockFileSystem`, `MockEntryFormatterService`, `NoOpCoordinator`, `NoOpReporter` from base class; keep per-test `CreateSut()` factory method in `markdown-journal-cli.Tests/Services/EntryFormatter/EntryFormatterServiceTests.cs`
- [x] T024 [P] [US2] Migrate `InitJournalServiceTests` to extend `ServiceTestBase` — replace any `TestFileSystem` usage mixed with `Mock<>` for non-transaction dependencies; use base-class mocks consistently; keep `NoOpCoordinator` / `NoOpReporter` for transaction opt-out in `markdown-journal-cli.Tests/Services/InitJournal/InitJournalServiceTests.cs`
- [x] T025 [P] [US2] Migrate `JournalEntryServiceTests` to extend `ServiceTestBase` — same migration as T023 in `markdown-journal-cli.Tests/Services/JournalEntry/JournalEntryServiceTests.cs`
- [x] T026 [P] [US2] Migrate `JournalFileUpdateServiceTests` to extend `ServiceTestBase` — same migration as T023 in `markdown-journal-cli.Tests/Services/JournalFileUpdate/JournalFileUpdateServiceTests.cs`
- [x] T027 [P] [US2] Migrate `JournalUpdateServiceTests` to extend `ServiceTestBase` — replace any mixed `TestFileSystem`/`Mock<>` patterns found per research.md section 6; use base-class mocks consistently in `markdown-journal-cli.Tests/Services/JournalUpdate/JournalUpdateServiceTests.cs`
- [x] T028 [P] [US2] Migrate `NewJournalServiceTests` to extend `ServiceTestBase` — same migration as T023 in `markdown-journal-cli.Tests/Services/NewJournal/NewJournalServiceTests.cs`
- [x] T029 [P] [US2] Migrate `RemoveEntryServiceTests` to extend `ServiceTestBase` — same migration as T023 in `markdown-journal-cli.Tests/Services/RemoveEntry/RemoveEntryServiceTests.cs`
- [x] T030 [P] [US2] Migrate `TableOfContentsServiceTests` to extend `ServiceTestBase` — same migration as T023 in `markdown-journal-cli.Tests/Services/TableOfContents/TableOfContentsServiceTests.cs`
- [x] T031 [US2] Run `dotnet test markdown-journal-cli.Tests/markdown-journal-cli.Tests.csproj` and resolve any compilation errors or `MockBehavior.Strict` unexpected-call failures introduced during the migration; all tests from the T001 baseline must continue to pass

**Checkpoint**: All command and service unit tests extend shared base classes with consistent Moq usage (SC-002, SC-004) — story 2 independently verifiable

---

## Phase 5: User Story 3 — Test Project Maintainability & Quality Pass (Priority: P2)

**Goal**: Fix vacuous assertions, rename misleading test names, deduplicate verbatim coverage, and confirm rollback tests remain intact (FR-011, SC-007, SC-005).

**Independent Test**: Running `dotnet test` produces zero warnings about always-passing tests; all test method names follow `Method_Should_Behavior_When_Condition`; no two test methods exercise identical code paths with identical assertions.

- [x] T032 [P] [US3] Read all test methods in each file under `markdown-journal-cli.Tests/Commands/` and fix every vacuous assertion (e.g., `Assert.True(true)`, `Should.NotThrow(...)` wrapping no real operation) by replacing them with Shouldly assertions (`.ShouldBe()`, `.ShouldContain()`, `.ShouldNotBeNull()`, etc.) that would fail if the production method were deleted — every assertion MUST produce a meaningful failure message without a debugger (FR-006) in `markdown-journal-cli.Tests/Commands/`
- [x] T033 [P] [US3] Read all test methods in each file under `markdown-journal-cli.Tests/Services/` (excluding `Rollback/`) and fix every vacuous assertion using the same Shouldly replacement rule as T032 — every assertion MUST produce a meaningful failure message without a debugger (FR-006) in `markdown-journal-cli.Tests/Services/`
- [x] T034 [US3] Open `UpdateCommandTests.cs` and `UpdateEntryCommandTests.cs` side-by-side; identify test methods that are verbatim duplicates (same scenario, same assertions); for each confirmed duplicate verify the surviving test covers the code path, then remove the redundant test method; update any test method names that become misleading after deduplication in `markdown-journal-cli.Tests/Commands/Update/UpdateCommandTests.cs` and `markdown-journal-cli.Tests/Commands/Update/UpdateEntryCommandTests.cs`
- [x] T035 [P] [US3] Rename every test method across all command test files that does not follow `Method_Should_ExpectedBehavior_When_Condition` — use the method under test or scenario name as the prefix, describe observable output in `_Should_`, describe the triggering condition in `_When_` per data-model.md Entity 7 in `markdown-journal-cli.Tests/Commands/`
- [x] T036 [P] [US3] Rename every test method across all service test files (excluding `Rollback/`) that does not follow `Method_Should_ExpectedBehavior_When_Condition` — same naming rules as T035 in `markdown-journal-cli.Tests/Services/`
- [x] T037 [US3] Run `dotnet test --filter "FullyQualifiedName~Rollback"` and confirm all six rollback test classes (`InitJournalServiceRollbackTests`, `JournalEntryServiceRollbackTests`, `JournalFileUpdateServiceRollbackTests`, `JournalUpdateServiceRollbackTests`, `NewJournalServiceRollbackTests`, `RemoveEntryServiceRollbackTests`) pass without any modification to `ServiceRollbackTestBase` in `markdown-journal-cli.Tests/Services/Rollback/`
- [x] T038 [US3] Verify the XML doc comment added to `ServiceTestBase` in T004 explicitly references `ServiceRollbackTestBase` for rollback scenarios; update the comment if it was modified during T031 migration work in `markdown-journal-cli.Tests/Infrastructure/ServiceTestBase.cs`

**Checkpoint**: Quality pass complete — no vacuous assertions, consistent naming, rollback tests unmodified (SC-007)

---

## Phase 6: Polish & Final Verification

**Purpose**: Full suite green, folder structure verified, quickstart scenarios validated.

- [x] T039 [P] Run the complete test suite (`dotnet test markdown-journal-cli.sln`) and confirm the final passing test count is ≥ the T001 baseline count with zero new failures (SC-003); record the final count in the baseline note below
- [x] T040 [P] Verify that `markdown-journal-cli.Tests/` folder layout maps 1:1 to `markdown-journal-cli/` source folders — specifically confirm `Commands/Init/`, `Commands/New/`, `Commands/Update/`, `Commands/Remove/`, `Commands/Add/` and all `Services/` subdirectories each have corresponding test files per FR-005 (SC-005)
- [x] T041 Create `markdown-journal-cli.Tests/Infrastructure/QuickstartValidationTests.cs` with four `[Fact]` tests — one per quickstart pattern (command unit test, service unit test, integration test, rollback test) — based on the code samples in `specs/002-test-suite-cleanup/quickstart.md` sections 1–4; run `dotnet test --filter "FullyQualifiedName~QuickstartValidationTests"` and confirm all four pass with no manual steps required

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately
- **Phase 2 (Foundational)**: Depends on Phase 1 — **BLOCKS all user story phases**
- **Phase 3 (US1)**: Depends on Phase 2 (specifically T005 `JournalIntegrationTestBase`) — can run in parallel with Phase 4
- **Phase 4 (US2)**: Depends on Phase 2 (specifically T002 `MockFactory`, T003 `CommandTestBase`, T004 `ServiceTestBase`) — can run in parallel with Phase 3
- **Phase 5 (US3)**: Depends on Phase 4 completion (T031 clean build) — quality pass needs migrated tests
- **Phase 6 (Polish)**: Depends on Phases 3, 4, and 5 all complete

### User Story Dependencies

- **User Story 1 (P1 — Integration Tests)**: Unblocked after T006; no dependency on US2 or US3
- **User Story 2 (P1 — Mock Consistency)**: Unblocked after T006; no dependency on US1 or US3
- **User Story 3 (P2 — Quality Pass)**: Depends on US2 (T031) completing so tests are in their migrated state before naming/dedup work begins

### Within Each Phase

- All tasks in Phase 2 marked [P] can be written simultaneously (separate files)
- All tasks in Phase 3 marked [P] can be written simultaneously after T006
- All tasks in Phase 4 marked [P] can be written simultaneously after T006
- T031 (migration health check) must run after ALL T014–T030 are complete
- T035 and T036 (naming) can run after T032 and T033 (vacuous fixes) but may be done in parallel with them

---

## Parallel Execution Examples

### Example: Phase 2 (4 parallel file authors)

```
Author 1: T002 MockFactory.cs
Author 2: T003 CommandTestBase.cs
Author 3: T004 ServiceTestBase.cs
Author 4: T005 JournalIntegrationTestBase.cs
→ All merge, T006 verifies
```

### Example: Phase 3, US1 Integration Tests (5 parallel)

```
After T006:
Author 1: T007 + T008 (Add migrations)
Author 2: T009 (Init integration test)
Author 3: T010 (New integration test)
Author 4: T011 (Update integration test)
Author 5: T012 (Remove integration test)
→ All merge, T013 verifies
```

### Example: Phase 4, US2 Command Migration (9 parallel)

```
After T006 (and US1 merged):
Author 1: T014 AddEntryCommandTests
Author 2: T015 AddFileTrackingCommandTests
Author 3: T016 AddJournalrcCommandTests
Author 4: T017 AddTableOfContentsCommandTests
Author 5: T018 InitCommandTests
Author 6: T019 NewCommandTests
Author 7: T020 RemoveEntryCommandTests
Author 8: T021 UpdateCommandTests
Author 9: T022 UpdateEntryCommandTests
→ Concurrently:
Author 1: T023 EntryFormatterServiceTests
Author 2: T024 InitJournalServiceTests
...
→ All merge, T031 verifies
```

---

## Implementation Strategy

**MVP (Phase 1 → Phase 2 → Phase 3 → T031)**: Verified shared infrastructure + all integration tests passing. This alone satisfies FR-001, SC-001, and FR-003. Delivers the highest-value safety net before touching existing tests.

**Increment 2 (Phase 4 T014–T031)**: Consistent mock usage across all unit tests. Satisfies FR-002, FR-004, SC-002, SC-004.

**Increment 3 (Phase 5 + Phase 6)**: Quality pass and final polish. Satisfies FR-011, SC-007, SC-003, SC-005.

---

## Baseline Note

> **T001 pre-cleanup baseline**: **1045 tests passing**  
> **T039 post-cleanup final count**: _(record passing test count here after running T039)_

---

## Baseline Note

> **Pre-cleanup baseline (T001)**: **1045 tests passing**  
> **After T022 (command migration complete)**: **1056 tests passing** (+11 from new integration tests)  
> **Post-cleanup final (T039)**: **1060 tests passing** (+4 from QuickstartValidationTests in T041)
