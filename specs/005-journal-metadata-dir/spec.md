# Feature Specification: Journal Metadata Directory

**Feature Branch**: `005-journal-metadata-dir`
**Created**: 2026-04-22
**Status**: Ready for Planning
**Input**: User description: "Reorganize the file structure so .journalrc no longer handles TOC structure (give it its own file), move the TOC structure file and .mdjournal into their own hidden directory (like .git), and rename the .mdjournal tracking file."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - New Journals Use the New Layout (Priority: P1)

A user creates a brand-new journal with `mdjournal new`. The journal is created with the new directory structure from the start.

**Why this priority**: All new journals should reflect the canonical structure immediately so the migration burden decreases over time.

**Independent Test**: Run `mdjournal new MyJournal`. Verify the metadata directory is created with the correct files inside it, and `.journalrc` does not include TOC structure.

**Acceptance Scenarios**:

1. **Given** no existing journal at the target path, **When** `mdjournal new <name>` is run, **Then** the journal is created with the metadata directory, the tracking file, and the TOC structure file all in the correct locations.
2. **Given** a newly created journal, **When** an entry is added and the TOC is regenerated, **Then** the TOC structure file inside the metadata directory is correctly updated.

---

### User Story 2 - `mdjournal init` Uses the New Layout (Priority: P2)

A user adopts an existing directory as a journal with `mdjournal init`. The resulting metadata files are placed in the new directory structure.

**Why this priority**: Consistent with the new journal creation experience; lower priority because fewer users rely on `init` than `new`.

**Independent Test**: Run `mdjournal init` on an existing directory with markdown files. Verify the metadata directory is created with the tracking and TOC structure files, and `.journalrc` is created without TOC structure embedded.

**Acceptance Scenarios**:

1. **Given** an existing directory of markdown files, **When** `mdjournal init <path>` is run, **Then** the metadata directory and all required files are created using the new layout.

---

### User Story 3 - All Commands Function Correctly with New Layout (Priority: P1)

All existing commands (`add`, `update entry`, `update journal`, `remove`) continue to work identically from the user's perspective. No command signatures or flags change.

**Why this priority**: Zero regression is required — users must not need to learn anything new to keep using the tool.

**Independent Test**: Run the full command suite against a journal in the new layout. All commands produce identical output and file results compared to the old layout.

**Acceptance Scenarios**:

1. **Given** a journal in the new layout, **When** `mdjournal add entry <name>` is run, **Then** the entry is created, `.mdjournal/.journalindex` is updated with the new entry hash, and `.mdjournal/.journaltoc` is updated with the new entry's TOC position.
2. **Given** a journal in the new layout, **When** `mdjournal update journal` is run, **Then** the sync loop reads and writes the split files from the metadata directory correctly.
3. **Given** a journal in the new layout, **When** `mdjournal remove entry <name>` is run, **Then** the entry is deleted and all metadata files are updated.

---

### User Story 4 - `mdjournal add toc` Creates Both TOC Artifacts (Priority: P2)

A user runs `mdjournal add toc` to set up the TOC plumbing for a journal. Both the internal JSON structure file (`.mdjournal/.journaltoc`) and the human-readable markdown TOC file are created together as a single logical operation. Optional flags allow targeting only one artifact when needed.

**Why this priority**: Consistent with the existing `add` command philosophy where users can explicitly create individual journal artifacts. Grouping both under `add toc` reflects that from the user's perspective they are two parts of the same feature.

**Independent Test**: Run `mdjournal add toc` on a journal that has the metadata directory but no `.journaltoc` and no markdown TOC file. Verify both files are created. Run again with `--structure-only` and confirm only `.journaltoc` is created (markdown file untouched). Run with `--md-only` and confirm only the markdown TOC file is created.

**Acceptance Scenarios**:

1. **Given** a journal with the metadata directory but neither `.journaltoc` nor the markdown TOC file, **When** `mdjournal add toc` is run (no flags), **Then** both `.mdjournal/.journaltoc` and the markdown TOC file are created.
2. **Given** a journal where `.journaltoc` already exists but the markdown TOC file does not, **When** `mdjournal add toc` is run (no flags), **Then** the markdown TOC file is created and `.journaltoc` is left unchanged.
3. **Given** a journal where both files already exist, **When** `mdjournal add toc` is run, **Then** the command warns that both already exist and returns exit code 1 without modifying either file.
4. **Given** a journal, **When** `mdjournal add toc --structure-only` is run, **Then** only `.mdjournal/.journaltoc` is created or validated; the markdown TOC file is not touched.
5. **Given** a journal, **When** `mdjournal add toc --md-only` is run, **Then** only the markdown TOC file is created; `.journaltoc` is not touched.

---

### Edge Cases

- What happens when the metadata directory exists but is empty or partially populated? → Fail with a clear error message listing the missing file(s) and direct the user to run `mdjournal init` to reinitialize.
- What happens when a user manually deletes the metadata directory? → Same as above — detected as missing required files, fail with actionable error.
- What happens when a journal is in a read-only location and a command is run?
- What happens when a command is run against an uninitialized directory (no `.mdjournal` file or directory)? → Fail with a clear error message directing the user to run `mdjournal init` first.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The tool MUST create a hidden metadata directory inside the journal directory to house internal tracking and TOC structure files.
- **FR-002**: The tracking file (currently `.mdjournal`) MUST be moved into the metadata directory and renamed to `.journalindex`.
- **FR-003**: The TOC structure (currently embedded in `.journalrc`) MUST be extracted into its own dedicated file inside the metadata directory named `.journaltoc`. The file MUST use JSON format, consistent with `.journalrc` and `.journalindex`.
- **FR-004**: The metadata directory MUST be named `.mdjournal` and hidden by default on macOS/Linux (prefixed with `.`).
- **FR-004a**: The metadata directory name, tracking file name, and TOC structure file name MUST each be configurable via `JournalSettings` (matching the pattern of existing file name settings such as `JournalConfigFileName` and `TableOfContentsFileName`).
- **FR-005**: After the restructure, `.journalrc` MUST contain only user-configurable settings (custom entry names, ignore lists, display preferences) — no TOC structure.
- **FR-006**: The `mdjournal new` command MUST create journals in the new layout only.
- **FR-007**: The `mdjournal init` command MUST create the metadata directory and split files when initializing an existing directory.
- **FR-007a**: When a command is run against a journal whose `.mdjournal/` directory exists but is missing required files (`.journalindex` or `.journaltoc`), the tool MUST fail with a clear error message listing the missing file(s) and instruct the user to run `mdjournal init` to reinitialize.
- **FR-008**: All existing commands MUST function correctly against the new layout with no changes to their public interface (flags, arguments, exit codes).
- **FR-008a**: The `mdjournal add toc` command MUST support creating both the internal JSON structure file (`.mdjournal/.journaltoc`) and the human-readable markdown TOC file as a single logical operation. A `--structure-only` flag MUST limit the operation to `.journaltoc` only; a `--md-only` flag MUST limit it to the markdown TOC file only. With no flags, both artifacts are created (skipping any that already exist with a warning).
- **FR-008b**: The `mdjournal add entry` command MUST update `.mdjournal/.journaltoc` (not `.journalrc`) when updating TOC structure after adding an entry.
- **FR-008c**: All `add` subcommands (`add entry`, `add toc`, `add tracking`) MUST be updated to use the new metadata directory paths. Any path references previously derived from `.journalrc` or the journal root for internal metadata files MUST be updated to resolve from `.mdjournal/`.
- **FR-009**: All documentation (ARCHITECTURE.md, README, development.md, drawio diagram) MUST be updated to reflect the new layout. update the png that is in the architecture doc which is created from the drawio doc.
- **FR-010**: All tests MUST be updated to reflect the new file paths and layout.
- **FR-011**: The test suite MUST include tests for creating journals and running all commands against the new layout.
- **FR-012**: These file names are set in the configuration JournalSettings.cs just like they are currently. make sure they are not hardcoded in the project but can be configured for whoever compiles the source code to match their naming needs. 

### Key Entities

- **Metadata Directory**: The new hidden directory that houses internal journal files (tracking and TOC structure). Lives inside the journal directory.
- **Tracking File**: Previously `.mdjournal` at the journal root. Stores SHA256 hashes and last-checked timestamps per entry file. Moves to the metadata directory with a new name.
- **TOC Structure File**: New file extracted from `.journalrc`. Defines the topic hierarchy and TOC ordering. Lives in the metadata directory.
- **`.journalrc`**: Retained at the journal root as the user-facing config file. Stripped of TOC structure; retains custom entry names, `ignoreFiles`, and display preferences.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All existing tests pass after updating to the new layout; no test is deleted purely to avoid updating it.
- **SC-002**: No existing command flag, argument, or exit code changes. The new `--structure-only` and `--md-only` flags on `add toc` are additive and do not affect the default (no-flag) behavior.
- **SC-003**: New and init-created journals never contain TOC structure inside `.journalrc` after this change.
- **SC-004**: The hidden metadata directory is invisible in standard directory listings (standard `ls` without flags on macOS/Linux) — users are less likely to accidentally edit internal files.

## Clarifications

### Session 2026-04-22

- Q: How should the tool detect that a journal uses the old layout? → A: `.mdjournal` is a *file* at the journal root → old layout; `.mdjournal` is a directory (or absent) → new layout. Detection is file-vs-directory, no content parsing required.
- Q: What is the migration scope given the current user base? → A: There are no existing users. The automatic migration path (User Story 1 / FR-006–007) is removed from scope. All commands target the new layout only; there is no backwards compatibility requirement for old-layout journals.
- Q: What format should `.journaltoc` use to store the TOC structure? → A: JSON — matching the format of `.journalrc` and `.journalindex`; no new parser required.
- Q: When a command runs against an uninitialized directory (no `.mdjournal` file or directory), what should the tool do? → A: Fail with a clear error message directing the user to run `mdjournal init` first.
- Q: When `.mdjournal/` exists but required files inside are missing, what should the tool do? → A: Fail with a clear error message listing the missing file(s) and direct the user to run `mdjournal init` to reinitialize; same user-facing pattern as fully uninitialized. The `add` command must also write `.journaltoc` (not `.journalrc`) when adding entries.
- Q: Should `AddTableOfContentsCommand` (`add toc`) also create `.journaltoc`, or is that a separate command? → A: Yes — `add toc` manages both the internal JSON structure file (`.mdjournal/.journaltoc`) and the human-readable markdown TOC file as a single logical operation (two parts of the same user-facing feature). Flags `--structure-only` and `--md-only` allow targeting a single artifact. This is consistent with the existing `add` command philosophy. `.journaltoc` is also always kept up to date by `add entry`. All `add` subcommands must be updated to resolve metadata paths from `.mdjournal/` rather than the journal root or `.journalrc`.

## Assumptions

- Only macOS and Linux hidden-directory conventions are in scope (`.` prefix). Windows support is deferred.
- No migration path is required — there are no existing users with old-layout journals.
- The test suite uses both the real file system (integration tests) and `TestFileSystem` (unit tests); both must be updated.
- `.journalrc` will retain its name and root-level position; only TOC structure content is removed from it.
- The existing rollback/transaction infrastructure (`IFileTransactionCoordinator`) is used for atomicity of new journal creation and init, not migration.
