[Back to README](../README.md)

# Architecture Documentation

This document provides detailed technical information about the Markdown Journal CLI architecture, design decisions, and implementation details.

## 🏗️ System Architecture

### High-Level Overview

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   CLI Interface │    │   Command Layer │    │ Infrastructure  │
│  (Spectre.CLI)  │───▶│   (Commands/)   │───▶│  (Services)     │
└─────────────────┘    └─────────────────┘    └─────────────────┘
```

Commands are thin: they validate input, call services, and output results. All business logic lives in services under `Infrastructure/` and `Services/`. See `Program.cs` for the full list of registered services and commands.

### File Infrastructure

Three metadata artifacts (`.journalrc`, `.mdjournal/.journalindex`, and `.mdjournal/.journaltoc`) are kept in sync with the journal directory's markdown files via a continuous loop when `mdjournal update journal` runs:

![File infrastructure diagram](mdjournal_file_infrastructure.png)

| Component | Role |
|---|---|
| `.journalrc` | Config file. Defines the journal name, TOC file name, markdown extensions, `tableOfContents.ignoreFiles`, and `trackingIndex.noTrack`. |
| `.mdjournal/` | Hidden metadata directory. Contains `.journalindex` and `.journaltoc`. |
| `.mdjournal/.journalindex` | Tracking file. Stores SHA256 hashes and last-checked timestamps for every tracked `.md` file. |
| `.mdjournal/.journaltoc` | TOC structure file. Stores the topic hierarchy and root entries as JSON. |
| Journal Directory | The actual markdown entry files and generated Table of Contents on disk |

**Sync loop (automatic on `update journal`):**

1. **Disk → `.mdjournal/.journalindex`** — `FileTracking.DetectChangesWithoutUpdate()` walks the directory, filters out `.mdjournal/`, applies `.journalrc` `trackingIndex.noTrack`, and compares file hashes against the stored index to identify added, modified, and deleted files.
2. **`.mdjournal/.journalindex` → Journal Directory** — Last-edited metadata is stamped into modified entry files; `.mdjournal/.journalindex` is updated with the new hashes.
3. **`.mdjournal/.journalindex` → `.mdjournal/.journaltoc`** — `JournalConfiguration.DetectConfigChanges()` diffs the tracking index keys against the combined entry set from `.mdjournal/.journaltoc` (root entries + topic hierarchy) and `.journalrc` (`tableOfContents.ignoreFiles`) to find files that need to be added or removed. Files in `trackingIndex.noTrack` have already been excluded from the tracking index, so they do not participate in config drift detection. Structural adds and removes are written to `.mdjournal/.journaltoc` via `IJournalTocStructureRepository`. Config detection runs twice in the live path: once before the early-return check, and again after tracking is committed so that same-run file additions and deletions are captured.
4. **`.journalrc` + `.mdjournal/.journaltoc` → Journal Directory** — `TableOfContentsService` reads user settings from `.journalrc` and the topic structure from `.mdjournal/.journaltoc`, then previews the Table of Contents markdown. `JournalUpdateService` compares that preview to the current TOC after normalizing the `Last Edited` metadata date; the TOC file is rewritten and re-tracked only when the meaningful content differs.

**User-driven (explicit commands only):**

- **Journal Directory → `.journalrc`** — Ignore-file flags (`update entry --ignore`) are written to `.journalrc`'s `tableOfContents.ignoreFiles` list via explicit commands. `trackingIndex.noTrack` is user configuration that excludes files, relative paths, or directories from tracking entirely.
- **Journal Directory → `.mdjournal/.journaltoc`** — Entry heading/location changes (`update entry --headings`), display name changes (`update entry --name`/`--title`), and file renames are written to `.mdjournal/.journaltoc` via `IJournalTocStructureRepository`.

## 🔧 Dependency Injection

Spectre.Console.Cli uses its own DI abstractions (`ITypeRegistrar`/`ITypeResolver`) to remain framework-agnostic. `TypeRegistrar` (in `Infrastructure/DependencyInjection/`) bridges this to Microsoft's DI container by translating Spectre's `Register()` calls into `IServiceCollection.AddSingleton()` calls.

`Program.cs` is the source of truth for all DI registrations. All services and commands are singletons; commands receive their dependencies via constructor injection.

## 🚨 Exception Architecture

All exceptions inherit from `JournalException` and live in `Exceptions/JournalExceptions.cs`. See that file for the current hierarchy — new exceptions are added there as features grow.

All commands extend `JournalCommand<TSettings>` (not `Command<TSettings>` directly). The base class catches `RollbackCompletedException` and maps it to the standard exit codes (see [Exit Codes](#exit-codes) below). Concrete commands override `ExecuteCore()` instead of `Execute()`.

## 📁 File System Abstraction

All file and directory operations go through `IFileSystem` (`Infrastructure/FileSystem/IFileSystem.cs`) — never `System.IO` directly. This keeps commands and services testable (mock `IFileSystem` instead of touching real files) and cross-platform. Tests use `TestFileSystem`; production uses the real `FileSystem` wrapper.

When adding new file operations, check `IFileSystem` first for an existing method before adding a new one.

## 🏗️ Service Architecture

### Core Services Overview

The application follows a service-oriented architecture with clear separation of concerns. See each interface file in `Infrastructure/` and `Services/` for full signatures.

**`IRemoveEntryService`** — Orchestrates full removal of a journal entry.
- Validates `.journalrc` and tracking index exist before proceeding.
- Guards against protected infrastructure files (`.journalrc`, tracking index, TOC file).
- Deletes the entry file, removes it from config and tracking, regenerates the TOC.
- When `cleanRefs` is `true`, calls `IMarkdownLinkRewriter.StripLinksInDirectory` to remove dead links across the journal and re-hashes modified files.

**`IInitJournalService`** — Orchestrates adoption of existing directories as journals.
- Validates the directory exists and isn't already managed.
- Creates tracking index, config, and TOC from existing files — no template files.
- Accepts an optional custom TOC name; throws `TocFileAlreadyExistsException` on conflict.
- Distinct from `IJournalInitializer` which creates a new directory with starter templates.

**`IJournalInitializer`** — Orchestrates journal creation (`new` command).
- Coordinates file creation, templating, and configuration.
- Encapsulates journal initialization business logic so `NewCommand` stays thin.

**`ITemplateManager`** — Handles template processing.
- Generates content from named templates (table of contents, journal entries).
- Extensible: new templates implement `ITemplateGenerator` and register via `RegisterDefaultTemplates()`.

**`IJournalConfiguration`** — Manages all `.journalrc` CRUD operations.
- Supports complex nested topic/subtopic hierarchy.
- Provides entry find, rename, and file-reference update for rename workflows.
- Preserves `trackingIndex.noTrack` when journal updates regenerate user-facing configuration.

**`IJournalFileUpdateService`** — Orchestrates entry update operations.
- Handles renaming, relocation, title changes, and ignore-status toggling.
- Updates all references in one operation: file system, tracking index, config, TOC, and backlinks.
- `updateBacklinks` (default `true`): rewrites inline link references in all other entry files on rename; suppressed with `--no-backlinks`.

**`IMarkdownLinkRewriter`** — Reusable inline-link rewriting infrastructure. `ReplaceLinksInDirectory` rewrites links on rename; `StripLinksInDirectory` removes dead links on entry deletion. Matches only inline links `[text](path/file.md)`; reference-style links are out of scope.

**`IJournalUpdateService`** — Orchestrates `update journal` operations, including a pure read-only dry-run path that projects pending changes without any disk writes. Live TOC updates use the same preview capability to skip writes when the only potential difference is the TOC `Last Edited` date.

**`ITableOfContentsService`** — TOC generation with preview support. The preview path returns generated TOC markdown as a string without writing to disk, preserving existing `Created`/`Last Edited` dates.

**`IInMemoryFileBuffer`** — In-memory file staging used by the dry-run path to generate and read TOC content without touching disk.

**`IFileTransactionCoordinator`** — Singleton factory for per-operation file transaction scopes. Tracks write operations before they happen so they can be reversed in reverse order on failure.

**`IDeletionRollbackStrategy`** — Captures and restores file content for delete operations that need to be rolled back.

**`IRollbackReporter`** — Writes rollback progress and results to the terminal via Spectre.Console.

**`NoOpFileTransactionCoordinator` / `NoOpFileTransactionScope` / `NoOpRollbackReporter`** — Silent no-op implementations used in tests and dry-run contexts.

**`IDryRunRenderer`** — Renders a read-only dry-run report to the terminal (tracking changes, config changes, TOC diff, rename-toc preview) with no disk writes.

### Configuration Generation Strategy

When creating a `.journalrc` for an existing journal, the system attempts three sources in order, stopping at the first successful result:

1. **Table of contents file** - Uses `ITableOfContentsMarkdownParser` to extract entries and build config.
2. **Tracking index** - Uses the `.md-journal` index to infer known files.
3. **Directory scan** - Falls back to scanning the journal directory for markdown files.

This approach prioritizes the most user-curated source first (TOC), then known tracking data, and only scans the directory as a last resort.

## 🔑 Key Architectural Patterns

### Natural Sorting Algorithm

Implemented in `JournalConfiguration.cs` via the `NaturalStringComparer` class.

**Problem:** Lexicographic sorting places "file_10" before "file_5" because it compares character-by-character.

**Solution:** Custom `IComparer<string>` that treats consecutive digits as complete numbers, so `file_1 < file_5 < file_10 < file_100`. Used for both topic names and entry filenames.

### Parent-Child Topic Detection

Implemented in `TableOfContentsGenerator.cs` for smart TOC rendering.

**Problem:** When a topic has an entry with the same name AND subtopics, both need to be represented without duplication.

**Solution:** When a topic has exactly one visible entry whose name matches the topic name, the entry link is merged into the topic heading and subtopics are rendered below it:

```
## [Abc](abc.md)
  - Test 2
    - [test file 1](abc-test_2-test_file_1.md)
```

### Ignore Files Pattern

**Purpose:** Allow entries to exist in configuration but be excluded from TOC.

**Implementation:**
- `.journalrc` contains `ignoreFiles` array
- Files added with `--ignore-file` flag
- Filtered at TOC generation time
- Still tracked in file system and configuration

**Use Cases:**
- Draft entries not ready for publication
- Private notes
- Template files
- Work-in-progress documentation

**Example:**
```json
{
  "tableOfContents": {
    "ignoreFiles": ["draft.md", "private-notes.md"]
  }
}
```

### Tracking No-Track Pattern

**Purpose:** Allow markdown files or directories to exist inside the journal directory without becoming part of the tracking index.

This is different from `tableOfContents.ignoreFiles`: ignored files are still tracked and can remain in configuration, while no-track files are omitted before hashing and are not written to `.mdjournal/.journalindex`.

**Implementation:**
- `.journalrc` contains `trackingIndex.noTrack`.
- `FileTracking.GetCurrentMarkdownFiles()` reads the journal configuration before change detection or index refresh.
- Matches are filtered before hashes are computed, before added/modified/deleted results are calculated, and before a new index is saved.
- Matching accepts:
  - file-name-only entries, e.g. `scratch.md` matches `scratch.md` and `notes/scratch.md`
  - exact relative paths, e.g. `private/secret.md`
  - directory entries, e.g. `archive` or `archive/` excludes all markdown files under that directory
- Matching is case-insensitive and normalizes `\` to `/`; glob patterns are not supported.

**Use Cases:**
- Private folders that should never be indexed
- Imported archives kept near a journal but outside active tracking
- Scratch files that should not create update noise
- Generated markdown files managed by another tool

**Example:**
```json
{
  "trackingIndex": {
    "noTrack": ["scratch.md", "private/secret.md", "archive"]
  }
}
```

### File Change Detection

**Architecture:**
```
IFileTracking
    └── IHashService (SHA256)
            └── .mdjournal/.journalindex file
```

**Process:**
1. **Index Creation**: Hash all tracked markdown files on journal initialization
2. **Storage**: Save index to `.mdjournal/.journalindex` JSON file
3. **Detection**: Load `.journalrc`, exclude `trackingIndex.noTrack` matches, then compare current file hashes with stored hashes
4. **Results**: Return added/modified/deleted file lists

**Index Structure:**
```json
{
  "files": {
    "intro.md": "a3f2b8c...",
    "topic-entry.md": "d4e9c1a..."
  }
}
```

**Benefits:**
- Detects external file modifications
- No need for file system watchers
- Works across sessions
- Cryptographically secure (SHA256)

### Metadata Update Pattern

**Purpose:** Automatically maintain "Last Edited:" dates in markdown files when content changes.

**Implementation:**
- Located in `MarkdownMetadataParser.UpdateLastEditedDate()`
- Searches metadata header (first 6 non-empty lines before heading)
- Replaces existing "Last Edited:" line or inserts after "Created:" line
- Preserves file structure and existing metadata

**Metadata Header Format:**
```markdown
Created: 01/15/2025
Last Edited: 02/11/2026

# Entry Title
Content here...
```

### TOC File Exclusion Pattern

**Problem:** The table of contents file can accidentally be added to `.journalrc` as an entry, causing it to appear in its own contents (circular reference).

**Solution — Defense in Depth:** Four independent layers prevent this:

1. **Prevention at entry time** (`IJournalConfiguration.AddEntry`) — silently skips the TOC file if passed as an entry.
2. **Auto-cleanup on TOC rename** (`JournalConfiguration.Update`) — when the TOC filename changes, the new filename is removed from the entries list.
3. **Skip during update** (`UpdateCommand`) — the TOC file is skipped when processing `AddedFiles` from change detection.
4. **Filter at render time** (`TableOfContentsGenerator`) — the TOC filename is appended to the ignore list during TOC generation regardless of config state.

This means even if one layer is bypassed (e.g. a manual config edit), the others catch it.

## 🧪 Testing Architecture

### Test Structure

Tests mirror the source layout under `markdown-journal-cli.Tests/`. See that directory for the current test inventory — the structure grows with the codebase and is always authoritative.

### Testing Strategy

**Test Categories:**
1. **Happy Path Tests** — Valid inputs produce expected outputs
2. **Error Handling Tests** — Invalid inputs produce proper error messages
3. **Integration Tests** — Full command execution with mocked dependencies
4. **Validation Tests** — Command argument validation
5. **Edge Case Tests** — Parent-child detection, natural sorting, ignore files
6. **Change Detection Tests** — File tracking with hash comparison
7. **Format Tests** — Entry name formatting with various separators
8. **Rollback Tests** — Fault injection at each write step via `FaultInjectingFileSystem`; asserts all prior writes were reversed and `RollbackCompletedException` was thrown with the expected `RollbackResult`

## 🔮 Future Architecture Considerations

- **Async file operations** - For large journals with many files
- **Global configuration** - User-level defaults (default editor, date format, etc.)
- **Plugin/extension points** - Custom template generators and entry processors

## 📋 Design Decisions Log

### Decision: Use Spectre.Console.Cli
**Rationale:** Rich terminal UI, excellent command parsing, built-in help generation  
**Alternatives:** System.CommandLine, custom argument parsing

### Decision: File System Abstraction
**Rationale:** Testability, cross-platform compatibility  
**Alternatives:** Direct file system calls

### Decision: Custom Exception Hierarchy
**Rationale:** Clear error categorization, better error handling  
**Alternatives:** Generic exceptions with error codes

### Decision: Natural Sorting for Entries
**Rationale:** Matches file system behavior and user expectations (`file_5` before `file_10`)  
**Alternatives:** Default lexicographic sorting

### Decision: SHA256 for File Hashing
**Rationale:** Collision-resistant, standard library support, appropriate for file integrity  
**Alternatives:** MD5 (deprecated), CRC32 (not secure)

### Decision: Multi-Layer TOC Exclusion
**Rationale:** Defense in depth prevents the TOC file from appearing in its own contents  
**Alternatives:** Single check at render time

### Decision: `IMarkdownLinkRewriter` as a Dedicated Infrastructure Service
**Rationale:** Link rewriting is a cross-cutting concern needed today for `--rename-toc` and tomorrow for `update entry --name`. Extracting it into a stateless interface keeps `JournalUpdateService` focused on orchestration and allows the rewriter to be tested in complete isolation with pure string inputs.  
**Alternatives:** Inline regex directly in `JournalUpdateService`; this would duplicate logic when entry rename is implemented

### Decision: Automatic Last Edited Updates
**Rationale:** Reduces manual maintenance, leverages existing change detection  
**Alternatives:** Manual date updates, file system modification times

### Decision: `init` vs `new` — No Template Files
**Rationale:** `init` adopts a directory that already contains content. Creating intro/template files would pollute an existing collection and conflict with existing filenames. The command focuses purely on adding management metadata: `.journalrc`, a TOC, and a tracking index.  
**Alternatives:** Re-use `NewJournalService` and skip template creation via a flag — rejected because it couples two semantically distinct operations and makes the flag surface of `NewJournalService` grow for unrelated reasons.

### Decision: `ProtectedJournalFileException` for Infrastructure File Guard
**Rationale:** Infrastructure files (`.journalrc`, tracking index, TOC) must never be deleted via the `remove entry` command. A dedicated exception gives the command a precise catch target and produces a user-friendly error message that clearly identifies the file as protected, rather than surfacing a generic `FileNotFoundException` or silent no-op.  
**Alternatives:** Silent skip (poor discoverability); generic `InvalidOperationException` (less precise error messaging).

### Decision: `--clean-refs` as Opt-In on `remove entry`
**Rationale:** Stripping dead links across every `.md` file is a write-heavy operation that is often unnecessary (e.g. when removing a draft that was never linked to). Making it opt-in with `--clean-refs` keeps the default fast and non-destructive. The `--force` flag already bypasses the confirmation prompt — combining it with `--clean-refs` gives a fully non-interactive removal pipeline for scripted use.  
**Alternatives:** Always strip dead links on remove — too aggressive; leaves user no escape hatch if the regex rewrite produces unexpected results.

### Decision: `--dry-run` (alias `--check`) on `update journal`
**Rationale:** Users need a way to audit pending changes before they are applied, especially on large or shared journals. `--dry-run` is the dominant CLI convention (git, terraform, rsync). `--check` is retained as an alias per UX preference. The flag is a pure read path: detection helpers (`DetectChangesWithoutUpdate`, `DetectConfigChanges`) are already non-mutating; `BuildDryRunReport` builds a structured model; `UpdateCommand.ExecuteDryRun` renders it via `IAnsiConsole`. Zero writes occur. Rendering is scoped by the same flags as the live path (`--tracking`, `--config`, `--toc`, `--rename-toc`). Exit code is always `0` on success.  
**Alternatives:** A separate `mdjournal diff` command — more discoverable but requires duplicating all detection logic and a separate command registration.

### Decision: Execute-Then-Compensate via `IFileTransactionScope`
**Rationale:** When a multi-step operation (e.g. `update journal` touches `.mdjournal`, `.journalrc`, TOC, and multiple entry files) fails partway through, the journal is left in an inconsistent state. The `IInMemoryFileBuffer` already had a `Snapshot/Restore` stub for this purpose; rather than overloading it, a dedicated `IFileTransactionScope` / `FileTransactionCoordinator` pair was introduced. Services call `Track*` before each write; the scope stores snapshots and reverses them in reverse-registration order on `Rollback()`. A `JoinedTransactionScope` wrapper allows inner services to participate in an outer command-level transaction without owning its lifetime. This maps to the execute-then-compensate Unit of Work pattern used by `TxFileManager` (ChinhDo), without any external dependencies or OS-specific kernel transactions.  
**Alternatives:** Extend `IInMemoryFileBuffer` directly (Option A) — mixed staging + rollback concerns, no explicit transaction boundary. WAL-style sentinel file (Option D) — would survive process crashes but is significantly more complex and overkill for a single-user CLI.

### Decision: `JournalCommand<TSettings>` Base Class for Exit-Code Mapping
**Rationale:** All commands need to translate `RollbackCompletedException` into the standard exit codes (2 = fully rolled back, 3 = partial rollback) without duplicating a try/catch in every `Execute()` override. A thin abstract base class seals `Execute()` and delegates to `ExecuteCore()` in each concrete command. This keeps exit-code semantics centralized and ensures future commands get correct rollback exit codes automatically.  
**Alternatives:** Duplicate the try/catch in every command — error-prone and harder to maintain.


## Exit Codes

| Code | Meaning |
|---|---|
| `0` | Command succeeded |
| `1` | Command failed — pre-flight check or unexpected error; no writes started |
| `2` | Command failed mid-write; all writes fully rolled back (safe to retry) |
| `3` | Command failed mid-write; rollback had errors (manual inspection required) |
