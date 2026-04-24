# Tasks: Journal Metadata Directory

**Input**: Design documents from `/specs/005-journal-metadata-dir/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, quickstart.md, contracts/cli-contracts.md

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1=New Journals, US2=Init Layout, US3=All Commands, US4=Add Toc)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Baseline Verification)

**Purpose**: Establish a clean, passing baseline before any changes are made.

- [ ] T001 Verify build and test suite pass by running `dotnet build` + `dotnet test` — confirm no pre-existing failures before any changes

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented. All changes in this phase are additive or internal-only — no user-visible CLI behavior changes yet.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

### 2a. Settings & Configuration

- [x] T002 [P] Add `MetadataDirName` (default `".mdjournal"`), `TrackingFileName` (default `".journalindex"`), and `TocStructureFileName` (default `".journaltoc"`) properties to `markdown-journal-cli/JournalSettings.cs`
- [x] T003 [P] Add `"MetadataDirName": ".mdjournal"`, `"TrackingFileName": ".journalindex"`, and `"TocStructureFileName": ".journaltoc"` entries to `markdown-journal-cli/appsettings.json`

### 2b. Domain Models

- [x] T004 [P] Create `JournalTocStructure` model with JSON-serializable `Structure` and `RootEntries` properties (extracted from existing `TableOfContents`). Use a static `Empty()` factory method (or equivalent constructor) to initialize both to safe defaults (`Structure` with empty `Topics` array; `RootEntries = []`) for use when the `.journaltoc` file is absent. File: `markdown-journal-cli/Infrastructure/Configuration/Models/JournalTocStructure.cs`
- [x] T005 *(depends on T004)* Remove `Structure` and `RootEntries` properties from the `TableOfContents` class, keeping only `File`, `Extensions`, and `IgnoreFiles` in `markdown-journal-cli/Infrastructure/Configuration/Models/TableOfContents.cs`

### 2c. File System Abstraction

- [x] T006 [P] Add `bool IsDirectory(string path)` declaration to `markdown-journal-cli/Infrastructure/FileSystem/IFileSystem.cs`
- [x] T007 [P] Implement `IsDirectory(string path)` returning `Directory.Exists(path)` in `markdown-journal-cli/Infrastructure/FileSystem/FileSystem.cs`
- [x] T008 [P] Add `IsDirectory(string path)` implementation to `markdown-journal-cli.Tests/Infrastructure/FileSystem/TestFileSystem.cs` (in-memory: return true when the path is a tracked directory prefix, false otherwise)

### 2d. New Repository & Validator Interfaces

- [x] T009 [P] Create `IJournalTocStructureRepository` interface with `JournalTocStructure Load(string metadataDir)` and `void Save(JournalTocStructure structure, string metadataDir)` in `markdown-journal-cli/Infrastructure/Configuration/IJournalTocStructureRepository.cs`
- [x] T010 Implement `JournalTocStructureRepository` — JSON read/write of `.journaltoc` inside `metadataDir` using `JsonSerializer` (WriteIndented = true); when the file is absent return `JournalTocStructure.Empty()` (per T004: `Structure` with empty `Topics` array and `RootEntries = []`). File: `markdown-journal-cli/Infrastructure/Configuration/JournalTocStructureRepository.cs`
- [x] T011 [P] Create `IJournalValidator` interface with `IReadOnlyList<string> ValidateMetadataDirectory(string journalDir)` in `markdown-journal-cli/Infrastructure/Validation/IJournalValidator.cs`
- [x] T012 Implement `JournalValidator` — check that `.mdjournal/` exists and is a directory; check that `.journalindex` and `.journaltoc` exist inside it; return a list of missing file/directory names (empty list = valid) in `markdown-journal-cli/Infrastructure/Validation/JournalValidator.cs`

### 2e. Existing Infrastructure Updates

- [x] T013 Update `FileTracking` to build the tracking file path as `Path.Combine(journalDir, settings.MetadataDirName, settings.TrackingFileName)` and update `GetCurrentMarkdownFiles` exclusion to skip the entire `.mdjournal/` metadata directory in `markdown-journal-cli/Infrastructure/Tracking/FileTracking.cs`
- [x] T014 Update `JournalConfiguration.Create()` and `Update()` to omit `structure` and `rootEntries` from `.journalrc` writes — these fields now live in `.journaltoc` in `markdown-journal-cli/Infrastructure/Configuration/JournalConfiguration.cs`
- [x] T015 Update `JournalConfigGenerator` to load TOC structure from `IJournalTocStructureRepository` and save it back via `IJournalTocStructureRepository` rather than reading from `JournalConfig.TableOfContents.Structure` in `markdown-journal-cli/Infrastructure/Configuration/JournalConfigGenerator.cs`
- [x] T016 Update `JournalCommand<TSettings>` base class: (a) add `protected virtual bool SkipMetadataValidation => false;`, (b) call `IJournalValidator.ValidateMetadataDirectory(journalDir)` before delegating — only when `SkipMetadataValidation` is `false`, (c) print an actionable error via `IAnsiConsole` listing missing files and return exit code 1 on any failure. Note: `NewCommand` and `InitCommand` must override this to `true` — see T018 and T034. File: `markdown-journal-cli/Commands/JournalCommand.cs`

### 2f. Dependency Registration

- [x] T017 Register `IJournalTocStructureRepository → JournalTocStructureRepository` and `IJournalValidator → JournalValidator` with the DI container in `markdown-journal-cli/Program.cs`

**Checkpoint**: Foundational infrastructure is complete. All user story phases can now begin.

---

## Phase 3: User Story 1 — New Journals Use the New Layout (Priority: P1) 🎯 MVP

**Goal**: `mdjournal new <name>` creates a journal with a `.mdjournal/` directory containing `.journalindex` and `.journaltoc`, and `.journalrc` contains no structure fields.

**Independent Test**: Run `mdjournal new MyJournal --path /tmp/test-new`. Verify `.mdjournal/` is a directory, `.mdjournal/.journalindex` and `.mdjournal/.journaltoc` both exist inside it, and `.journalrc` has no `structure` or `rootEntries` keys.

### Implementation for User Story 1

- [ ] T018 [US1] Update `NewJournalService` to call `EnsureDirectoryExists` for the metadata directory, write `.journalindex` and `.journaltoc` into it inside a `FileTransactionScope`, and write `.journalrc` without structure fields. Also add `protected override bool SkipMetadataValidation => true;` to `NewCommand` — the journal directory does not exist yet when this command runs. Files: `markdown-journal-cli/Services/NewJournal/NewJournalService.cs`, `markdown-journal-cli/Commands/New/NewCommand.cs` *(NewJournalService is done; NewCommand is missing `SkipMetadataValidation` override — blocked on T016)*
- [ ] T019 [US1] Update `NewJournal` service tests to assert: metadata directory exists, `.journalindex` exists inside it, `.journaltoc` exists inside it, and `.journalrc` JSON contains no `structure` or `rootEntries` keys in `markdown-journal-cli.Tests/Services/NewJournal/NewJournalServiceTests.cs`
- [ ] T020 [US1] Update `QuickstartValidationTests` to expect the new `.mdjournal/` directory layout for all journal-creation scenarios in `markdown-journal-cli.Tests/Infrastructure/QuickstartValidationTests.cs`

**Checkpoint**: `mdjournal new` creates journals in the new layout. User Story 1 is independently testable.

---

## Phase 4: User Story 3 — All Commands Function Correctly with New Layout (Priority: P1)

**Goal**: `add entry`, `update journal`, `update entry`, and `remove entry` all read and write from `.mdjournal/.journalindex` and `.mdjournal/.journaltoc` with no change to their public interface (flags, arguments, exit codes).

**Independent Test**: Create a journal with `mdjournal new`, then run the full command suite against it. All commands produce correct output, and `.mdjournal/.journalindex` and `.mdjournal/.journaltoc` are updated correctly after each operation.

### Implementation for User Story 3

- [x] T021 [P] [US3] Update `TableOfContentsService` to write TOC structure to `.mdjournal/.journaltoc` via `IJournalTocStructureRepository` instead of embedding it in `.journalrc` in `markdown-journal-cli/Services/TableOfContents/TableOfContentsService.cs`
- [x] T022 [P] [US3] Update `JournalUpdateService` to read TOC structure from `IJournalTocStructureRepository` (instead of `JournalConfig.TableOfContents.Structure`) in `markdown-journal-cli/Services/JournalUpdate/JournalUpdateService.cs`
- [ ] T023 [P] [US3] Update `JournalEntryService` to resolve the journal index path from `.mdjournal/.journalindex` and update `.mdjournal/.journaltoc` (via `IJournalTocStructureRepository`) after creating an entry in `markdown-journal-cli/Services/JournalEntry/JournalEntryService.cs` *(still uses `$".{AppName}"` for tracking path)*
- [ ] T024 [P] [US3] Update `RemoveEntryService` to resolve index and TOC structure paths from the metadata directory in `markdown-journal-cli/Services/RemoveEntry/RemoveEntryService.cs` *(still uses `$".{AppName}"` for tracking path)*
- [x] T025 [P] [US3] Update `JournalFileUpdateService` to resolve any internal metadata file paths from the `.mdjournal/` metadata directory in `markdown-journal-cli/Services/JournalFileUpdate/JournalFileUpdateService.cs` *(delegates to IFileTracking/ITableOfContentsService which are already updated)*
- [ ] T026 [US3] Update all command handlers under `markdown-journal-cli/Commands/Add/` to pass the metadata directory path correctly to their underlying services. Explicitly covers: `AddEntryCommand`, `AddTableOfContentsCommand`, `AddFileTrackingCommand`, and `AddJournalrcCommand` — any path previously derived from the journal root or `.journalrc` for internal metadata files must resolve from `.mdjournal/` instead *(AddFileTrackingCommand still uses `$".{AppName}"` for tracking path)*
- [ ] T027 [US3] Update all command handlers under `markdown-journal-cli/Commands/Update/` to pass the metadata directory path correctly to their underlying services *(UpdateCommand still uses `$".{AppName}"` for tracking path)*
- [ ] T028 [US3] Update all command handlers under `markdown-journal-cli/Commands/Remove/` to pass the metadata directory path correctly to their underlying services *(depends on T024)*
- [ ] T029 [US3] Update all existing service unit tests that set up journal state to use the new `.mdjournal/` metadata directory layout in `markdown-journal-cli.Tests/Services/`
- [x] T030 [US3] Update `JournalIntegrationTestBase` to create journals in the new metadata directory layout so all integration tests start from a valid new-layout journal in `markdown-journal-cli.Tests/Infrastructure/JournalIntegrationTestBase.cs`
- [ ] T031 [US3] Update or add tests in `markdown-journal-cli.Tests/Commands/Add/AddEntryIntegrationTests.cs` to verify `add entry` updates `.mdjournal/.journalindex` and `.mdjournal/.journaltoc`
- [ ] T032 [US3] Update or add tests in `markdown-journal-cli.Tests/Commands/Update/UpdateCommandIntegrationTests.cs` to verify `update journal` reads and writes split files from `.mdjournal/`
- [ ] T033 [US3] Update or add tests in `markdown-journal-cli.Tests/Commands/Remove/RemoveEntryCommandIntegrationTests.cs` to verify `remove entry` updates all metadata files inside `.mdjournal/`

**Checkpoint**: All existing commands work correctly with the new layout. No regression from original behavior.

---

## Phase 5: User Story 2 — `mdjournal init` Uses the New Layout (Priority: P2)

**Goal**: `mdjournal init <path>` creates the `.mdjournal/` directory containing `.journalindex` and `.journaltoc`, and creates `.journalrc` at the journal root without any structure fields.

**Independent Test**: Run `mdjournal init` on an existing directory of markdown files. Verify the metadata directory and all required files are created and `.journalrc` has no embedded structure.

### Implementation for User Story 2

- [ ] T034 [US2] Update `InitJournalService` to call `EnsureDirectoryExists` for the metadata directory, write `.journalindex` and `.journaltoc` into it inside a `FileTransactionScope`, and write `.journalrc` without structure fields. Also add `protected override bool SkipMetadataValidation => true;` to `InitCommand` — the metadata directory is being created by this command and will not exist on entry. Files: `markdown-journal-cli/Services/InitJournal/InitJournalService.cs`, `markdown-journal-cli/Commands/Init/InitCommand.cs` *(InitJournalService is done; InitCommand is missing `SkipMetadataValidation` override — blocked on T016)*
- [ ] T035 [US2] Update `InitJournal` service tests to assert: metadata directory exists, `.journalindex` and `.journaltoc` exist inside it, and `.journalrc` JSON has no `structure` or `rootEntries` keys in `markdown-journal-cli.Tests/Services/InitJournal/InitJournalServiceTests.cs`
- [ ] T036 [US2] Add integration test verifying that when `.mdjournal/` exists as a directory but required files are missing, any subsequent command prints a clear error listing the missing files and returns exit code 1 in `markdown-journal-cli.Tests/Commands/Init/InitCommandIntegrationTests.cs`

**Checkpoint**: `mdjournal init` creates journals in the new layout. User Story 2 is independently testable.

---

## Phase 6: User Story 4 — `mdjournal add toc` Creates Both TOC Artifacts (Priority: P2)

**Goal**: `mdjournal add toc` creates both `.mdjournal/.journaltoc` and the markdown TOC file as a single logical operation. `--structure-only` limits the operation to `.journaltoc` only; `--md-only` limits it to the markdown TOC file only. When both already exist the command warns and returns exit code 1.

**Independent Test**: Run `mdjournal add toc` on a journal with `.mdjournal/` but neither `.journaltoc` nor a markdown TOC file. Verify both are created. Run again with `--structure-only` and confirm only `.journaltoc` is created. Run with `--md-only` and confirm only the markdown TOC file is created. Run with no flags when both exist and confirm exit code 1.

### Implementation for User Story 4

- [ ] T037 [P] [US4] Create `IAddTocService` interface with `AddTocResult Execute(string journalDir, bool structureOnly = false, bool mdOnly = false)` and `AddTocResult` enum (`Created`, `PartiallyCreated`, `AlreadyExists`) in `markdown-journal-cli/Services/AddToc/IAddTocService.cs`
- [ ] T038 [US4] Implement `AddTocService` with dual-artifact creation logic: check existence of each artifact independently, create missing ones, wrap all writes in `FileTransactionScope`, and return the appropriate `AddTocResult` in `markdown-journal-cli/Services/AddToc/AddTocService.cs`
- [ ] T039 [US4] Update `AddTableOfContentsCommand` to add `--structure-only` and `--md-only` command settings, delegate to `IAddTocService.Execute`, warn via `IAnsiConsole` when `AlreadyExists`, and return exit code 1 in that case in `markdown-journal-cli/Commands/Add/AddTableOfContentsCommand.cs`
- [ ] T040 [US4] Register `IAddTocService → AddTocService` with the DI container in `markdown-journal-cli/Program.cs`
- [ ] T041 [US4] Add unit tests for `AddTocService` covering: both artifacts created, only structure created (`structureOnly: true`), only markdown created (`mdOnly: true`), both already exist (returns `AlreadyExists`), one already exists and the other is created (returns `PartiallyCreated`) in `markdown-journal-cli.Tests/Services/AddToc/AddTocServiceTests.cs`
- [ ] T042 [US4] Add integration tests for `add toc` command: no-flags both-created, `--structure-only`, `--md-only`, both-already-exist returns exit code 1 in `markdown-journal-cli.Tests/Commands/Add/AddTableOfContentsIntegrationTests.cs`

**Checkpoint**: `add toc` command handles all artifact-creation combinations correctly. User Story 4 is independently testable.

---

## Final Phase: Polish & Cross-Cutting Concerns

**Purpose**: New infrastructure tests, documentation, constitution patch, and final validation.

### New Infrastructure Unit Tests

- [ ] T043 [P] Add `IsDirectory` unit tests to `markdown-journal-cli.Tests/Infrastructure/FileSystem/FileSystemTests.cs` — assert true for a real directory, false for a file, false for an absent path
- [ ] T044 [P] Add `JournalTocStructureRepository` unit tests (Load returns empty structure when file absent; Load reads existing file correctly; Save round-trip produces identical deserialized output) in `markdown-journal-cli.Tests/Infrastructure/Configuration/JournalTocStructureRepositoryTests.cs`
- [ ] T045 [P] Add `JournalValidator` unit tests (valid layout returns empty list; missing `.mdjournal/` directory; missing `.journalindex`; missing `.journaltoc`; both index and toc missing) in `markdown-journal-cli.Tests/Infrastructure/Validation/JournalValidatorTests.cs`

### Documentation

- [ ] T046 [P] Update `docs/ARCHITECTURE.md` — update the journal directory tree to show `.mdjournal/` as a directory containing `.journalindex` and `.journaltoc`, and update component descriptions to match the new layout
- [ ] T047 [P] Update `README.md` — update the journal layout section to show the `.mdjournal/` directory structure; explicitly note that the directory is dot-prefixed and therefore hidden in standard `ls` output on macOS/Linux (SC-004)
- [ ] T048 [P] Update `docs/DEVELOPMENT.md` — update "Adding a Service" (and related sections) to describe the metadata directory pattern and the new `IJournalTocStructureRepository` / `IJournalValidator` infrastructure
- [ ] T049a Update `docs/mdjournal_file_infrastructure.drawio` — add `.mdjournal/` as a directory node containing `.journalindex` and `.journaltoc`; relabel or remove the old root-level `.mdjournal` file node. Must be completed before T049.
- [ ] T049 Update the architecture diagram PNG embedded in `docs/ARCHITECTURE.md` by regenerating it from the updated `docs/mdjournal_file_infrastructure.drawio` *(depends on T049a)*

### Constitution

- [ ] T050 Update the `Serialization` row in `.specify/memory/constitution.md` from `System.Text.Json for .journalrc and .mdjournal` to `System.Text.Json for .journalrc, .journalindex, and .journaltoc (inside .mdjournal/ metadata directory)`; bump the version field from `1.0.0` to `1.0.1`; update the "Last Amended" date. Run this last after all other Polish tasks (T046–T049) are complete.

### Final Validation

- [ ] T051 Run `dotnet build` + `dotnet test` and confirm all tests pass — no test may be deleted solely to avoid updating it (SC-001)
- [ ] T052 Confirm `QuickstartValidationTests` passes end-to-end against the new layout in `markdown-journal-cli.Tests/Infrastructure/QuickstartValidationTests.cs`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 passing — **BLOCKS all user stories**
- **US1 (Phase 3)**: Depends on Foundational completion
- **US3 (Phase 4)**: Depends on Foundational completion; benefits from US1 (a working `new` creates valid test journals)
- **US2 (Phase 5)**: Depends on Foundational completion; can run in parallel with US3
- **US4 (Phase 6)**: Depends on Foundational completion; can run independently of US1, US2, and US3
- **Polish (Final Phase)**: Depends on all user story phases being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational — no dependency on other stories
- **User Story 3 (P1)**: Can start after Foundational — no dependency on US1 (same foundational infrastructure)
- **User Story 2 (P2)**: Can start after Foundational — no dependency on US1 or US3
- **User Story 4 (P2)**: Can start after Foundational — no dependency on any other story

### Within Each User Story

- Models/interfaces before implementations
- Implementations before commands
- Core implementation before integration tests

---

## Parallel Opportunities

### Foundational Phase (Phase 2)

The following tasks touch different files and can run simultaneously:

```
T002  JournalSettings.cs             — add new settings properties
T003  appsettings.json               — add new setting defaults
T004  JournalTocStructure.cs         — new model (new file)
T006  IFileSystem.cs                 — add IsDirectory declaration
T007  FileSystem.cs                  — implement IsDirectory
T008  TestFileSystem.cs              — add IsDirectory stub
T009  IJournalTocStructureRepository.cs — new interface (new file)
T011  IJournalValidator.cs           — new interface (new file)
```

### User Story 3 (Phase 4)

The following service updates touch different files:

```
T021  TableOfContentsService.cs
T022  JournalUpdateService.cs
T023  JournalEntryService.cs
T024  RemoveEntryService.cs
T025  JournalFileUpdateService.cs
```

### Final Phase

```
T043  FileSystemTests.cs             — IsDirectory tests
T044  JournalTocStructureRepositoryTests.cs — new test file
T045  JournalValidatorTests.cs       — new test file
T046  docs/ARCHITECTURE.md
T047  README.md
T048  docs/DEVELOPMENT.md
T050  .specify/memory/constitution.md
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete **Phase 1**: Baseline verification
2. Complete **Phase 2**: Foundational (CRITICAL — blocks all stories)
3. Complete **Phase 3**: User Story 1 (T018–T020)
4. **STOP and VALIDATE**: Run `mdjournal new MyJournal --path /tmp/test-new`, inspect directory layout, confirm `.mdjournal/.journalindex` and `.mdjournal/.journaltoc` exist
5. Proceed to Phase 4 (US3) for full command regression

### Suggested Full Delivery Order

Phase 1 → Phase 2 → Phase 3 (US1) + Phase 5 (US2) + Phase 6 (US4) in parallel → Phase 4 (US3) → Final Phase
