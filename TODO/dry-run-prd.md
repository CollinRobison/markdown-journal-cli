# PRD: `--dry-run` Flag for `update journal`

## Problem Statement

Users running `mdjournal update journal` have no way to preview what changes will be applied before they happen. This creates friction for users who want to audit changes in important journals and makes it impossible to verify what a full sync would do without actually committing to it.

This is noted as a known future goal in `ARCHITECTURE.md` under `🔮 Future Architecture Considerations` (`--check` flag).

---

## Proposed Solution

Add `--dry-run` (with `--check` as an alias option) to `UpdateJournalSettings`. When set, all detection and preview logic runs but **zero writes occur**. Output is rendered as structured Spectre.Console tables, color-coded by change type. Additionally, create the foundational `IInMemoryFileBuffer` infrastructure for in-memory file staging — used for TOC preview now, and designed for a future transactional rollback mechanism across all write operations.

---

## Assumptions & Decisions

| # | Decision | Rationale |
|---|----------|-----------|
| 1 | Primary: `--dry-run`, alias: `--check` | `--dry-run` is the dominant CLI convention (git, terraform, rsync). `--check` retained as an alias per user preference. |
| 2 | Exit code `0` when dry-run completes successfully | `--dry-run` is an inspection tool, not a CI gate. If drift detection for CI is desired in future, `--exit-on-changes` is a natural extension. |
| 3 | TOC preview via `PreviewTableOfContents()` string return | Extracts private `GenerateTableOfContents` logic. No disk writes. `TableOfContentsService` stays decoupled from `IInMemoryFileBuffer`. |
| 4 | `IInMemoryFileBuffer` created now for TOC staging, designed for future rollback | Establishes the snapshot/stage/restore contract. Used in dry-run path to hold the generated TOC for diffing. Future work wires snapshot-before-write into `JournalUpdateService` for transactional safety. |
| 5 | `--rename-toc` is in scope for `--dry-run` | Uses `FindFilesWithLinkTo()` (read-only scan) to enumerate files that would have backlinks rewritten. No writes. |
| 6 | Output is user-friendly (filename + change type), hash-ready | `ChangeDetectionResult` already carries the file paths. Hash column can be added to the display model later without schema changes. |
| 7 | `update entry` is out of scope | Different command, different semantics. Separate PRD if needed. |

---

## Architecture Alignment

Follows patterns established in `ARCHITECTURE.md`:
- Commands remain thin orchestrators; delegate detection to services.
- All rendering uses `IAnsiConsole` via constructor injection.
- New infrastructure lives in `Infrastructure/FileSystem/`.
- New models live alongside existing models in `Infrastructure/Tracking/Models/`.
- All new interfaces registered in DI (`Program.cs`).
- `IAnsiConsole` stays in `UpdateCommand` for rendering — data flows up as models, not console output.

---

## New Infrastructure: `IInMemoryFileBuffer`

**Location:** `Infrastructure/FileSystem/IInMemoryFileBuffer.cs` + `InMemoryFileBuffer.cs`

**Purpose:** General-purpose in-memory file content store.
- **PRD scope**: Stage generated TOC content for preview/diffing without disk I/O.
- **Future scope**: Snapshot-before-write in `JournalUpdateService` for transactional rollback if a multi-step update fails partway through.

```csharp
public interface IInMemoryFileBuffer
{
    // Capture current disk content as a snapshot (for future rollback)
    void Snapshot(string absolutePath);

    // Store content in the staging area without writing to disk
    void Stage(string absolutePath, string content);

    // Retrieve staged content (null if not staged)
    string? GetStaged(string absolutePath);

    // Retrieve snapshot content (null if not snapshotted)
    string? GetSnapshot(string absolutePath);

    // Write staged content to disk
    void Commit(string absolutePath);

    // Restore snapshot content to disk (rollback)
    void Restore(string absolutePath);

    bool HasStaged(string absolutePath);
    bool HasSnapshot(string absolutePath);

    // Clear all staged and snapshot state
    void Clear();
}
```

**Lifecycle note:** Registered as singleton. `UpdateCommand` calls `Clear()` after dry-run rendering to avoid stale state on subsequent invocations.

---

## New `ITableOfContentsService` Method

```csharp
// Returns the generated TOC markdown content without writing to disk.
string PreviewTableOfContents(string journalDirectory);
```

**Implementation:** Extract the private `GenerateTableOfContents(config, createdDate, lastEditedDate)` body into the new public method. Reads existing TOC dates from disk (preserves them as `UpdateTableOfContents` does), generates content, returns string. No `_fileSystem.UpdateFile()` call.

---

## New Model: `UpdateDryRunReport`

**Location:** `Infrastructure/Tracking/Models/UpdateDryRunReport.cs`

```csharp
public class UpdateDryRunReport
{
    public ChangeDetectionResult? TrackingChanges { get; init; }
    public JournalConfigSyncResult? ConfigChanges { get; init; }
    public TocDiffResult? TocPreview { get; init; }
    public TocRenameDryRunResult? RenamePreview { get; init; }
}

public class TocDiffResult
{
    public string CurrentContent { get; init; } = string.Empty;
    public string PreviewContent { get; init; } = string.Empty;
    // Derived at render time: lines added, removed
}

public class TocRenameDryRunResult
{
    public string CurrentName { get; init; } = string.Empty;
    public string NewName { get; init; } = string.Empty;
    public IReadOnlyList<string> FilesWithBacklinks { get; init; } = [];
}
```

---

## `IJournalUpdateService` — New Method

```csharp
UpdateDryRunReport BuildDryRunReport(
    string journalPath,
    ChangeDetectionResult? trackingChanges,
    JournalConfigSyncResult? configChanges,
    bool includeToc,
    string? renameTocTarget
);
```

- Calls `_tableOfContentsService.PreviewTableOfContents()` when `includeToc` is true.
- Reads current TOC content from disk for diff via `_fileSystem.GetFileContent()`.
- Calls `_markdownLinkRewriter.FindFilesWithLinkTo()` when `renameTocTarget` is not null.
- Builds and returns `UpdateDryRunReport`. No writes.

---

## `UpdateCommand.Execute()` — Dry-Run Path

When `settings.DryRun` is true:

1. Guard checks (tracking file, journalrc) — same as live path.
2. `fileResults = _fileTracking.DetectChangesWithoutUpdate(settings.FilePath)` — already non-mutating.
3. `configDrift = _journalConfiguration.DetectConfigChanges(settings.FilePath)` — already non-mutating.
4. Call `_journalUpdateService.BuildDryRunReport(...)`.
5. Render report using Spectre.Console tables via `IAnsiConsole`.
6. Print footer: `[dim]No changes were applied. Re-run without --dry-run to apply.[/]`
7. Return `0`.

---

## Spectre.Console Output Design

### Tracking Changes Table
```
┌─ Tracking Changes ─────────────────────────────────────────┐
│ File                              │ Change                  │
├─────────────────────────────────────────────────────────────┤
│ entries/new-entry.md              │ ✚ added                 │
│ entries/modified.md               │ ~ modified              │
│ entries/deleted.md                │ ✖ removed               │
└─────────────────────────────────────────────────────────────┘
3 tracked file change(s) detected.
```
Colors: added = green, modified = yellow, removed = red.

### Config Changes Table (`.journalrc`)
```
┌─ Config Changes (.journalrc) ──────────────────────────────┐
│ File                              │ Change                  │
├─────────────────────────────────────────────────────────────┤
│ entries/new-entry.md              │ ✚ will be added         │
│ entries/deleted.md                │ ✖ will be removed       │
└─────────────────────────────────────────────────────────────┘
```

### TOC Preview Panel (line-level diff)
```
┌─ Table of Contents Preview ────────────────────────────────┐
│ --- current (1a-TableOfContents.md)                        │
│ +++ generated                                              │
│   # Table of Contents                                      │
│ - [Old Entry](old-entry.md)                                │
│ + [New Entry](new-entry.md)                                │
│   [Unchanged Entry](other.md)                              │
└─────────────────────────────────────────────────────────────┘
1 line(s) added · 1 line(s) removed
```
Removed lines = red, added lines = green, unchanged lines = dim.

### Rename TOC Preview
```
┌─ TOC Rename Preview ───────────────────────────────────────┐
│ 1a-TableOfContents.md  →  my-notes.md                      │
└─────────────────────────────────────────────────────────────┘
┌─ Files With Backlinks to Update ───────────────────────────┐
│ File                              │ Change                  │
├─────────────────────────────────────────────────────────────┤
│ entries/entry1.md                 │ ~ backlinks             │
│ entries/entry2.md                 │ ~ backlinks             │
└─────────────────────────────────────────────────────────────┘
```

### Nothing To Do
```
Everything is up to date. No changes detected. (--dry-run active, no writes made)
```

### Footer (always shown after dry-run output)
```
No changes were applied. Re-run without --dry-run to apply.
```

---

## Flag-Scoping Rules

`--dry-run` mirrors the scoping logic of the live `update journal` command exactly — it previews only the sections that would run. Any combination of specific flags is supported; each flag adds its corresponding section to the dry-run output.

### Single-Flag Combinations

| Flags used | Sections shown |
|---|---|
| `--dry-run` alone (implicit `all`) | Tracking + Config + TOC |
| `--dry-run --tracking` | Tracking only |
| `--dry-run --date` | Tracking only (date updates are downstream of tracking) |
| `--dry-run --config` | Config only |
| `--dry-run --toc` | TOC only |
| `--dry-run --rename-toc <name>` | Rename preview only |

### Multi-Flag Combinations

Multiple specific flags are additive — each flag includes its own section:

| Flags used | Sections shown |
|---|---|
| `--dry-run --tracking --config` | Tracking + Config |
| `--dry-run --tracking --toc` | Tracking + TOC |
| `--dry-run --config --toc` | Config + TOC |
| `--dry-run --tracking --config --toc` | Tracking + Config + TOC (equivalent to `--dry-run` alone) |
| `--dry-run --date --config` | Tracking + Config (`--date` implies tracking detection) |

**Implementation note:** The scoping logic for `--dry-run` is derived from the same `all` / individual-flag boolean evaluation used by the live path. No separate branching needed — the dry-run path reads the same `settings.ConfigFlag`, `settings.TocFlag`, `settings.Tracking`, `settings.DateFlag` booleans and calls `BuildDryRunReport()` with the appropriate include flags.

**Config changes depend on tracking:** When `--config` is run without `--tracking`, `JournalConfiguration.DetectConfigChanges()` compares the existing tracking index against `.journalrc`. The dry-run shows accurate config drift based on the current committed tracking index — it does not simulate a pending tracking update first.

---

## Exit Codes

| Scenario | Code |
|---|---|
| Dry-run completed, changes detected | `0` |
| Dry-run completed, nothing to do | `0` |
| Error (missing tracking file, missing journalrc, etc.) | `1` |

---

## Logging

Follow existing pattern in `JournalUpdateService`:
- `LogDebug` for per-file operations
- `LogWarning` for unexpected states
- No `LogInformation` in dry-run (console output is the user-facing signal)

Add to `UpdateCommand`:
- `LogDebug("Dry-run mode active, skipping all writes")`
- `LogDebug("Dry-run report: {TrackingCount} tracking, {ConfigCount} config, toc={TocIncluded}, rename={RenameTarget}")` structured params.

---

## Testing Strategy

### Unit Tests — `InMemoryFileBuffer`
- `Snapshot()` reads and stores current disk content via `IFileSystem`
- `Stage()` stores content without calling `IFileSystem.UpdateFile()`
- `GetStaged()` returns null if nothing staged
- `GetSnapshot()` returns null if nothing snapshotted
- `Commit()` calls `IFileSystem.UpdateFile()` with staged content
- `Restore()` calls `IFileSystem.UpdateFile()` with snapshot content
- `Clear()` resets all state; subsequent `GetStaged`/`GetSnapshot` return null
- `HasStaged()` / `HasSnapshot()` reflect correct state

### Unit Tests — `TableOfContentsService.PreviewTableOfContents()`
- Returns correct markdown without calling `IFileSystem.UpdateFile()`
- Preserves existing Created/Last Edited dates from current TOC file
- Returns valid content when no entries exist
- Output matches `GenerateTableOfContents()` result (parity with live path)

### Unit Tests — `UpdateCommand` dry-run paths
- `--dry-run` (all) — `BuildDryRunReport` called, no mutating methods called, returns `0`
- `--dry-run --tracking` — only tracking section in report, returns `0`
- `--dry-run --config` — only config section in report, returns `0`
- `--dry-run --toc` — only TOC preview in report, returns `0`
- `--dry-run --rename-toc <name>` — rename preview with backlink list, returns `0`
- `--dry-run` with no changes — "everything up to date" output, returns `0`
- `--dry-run` + missing tracking file — returns `1` with error message
- `--dry-run` + missing journalrc (when config/toc needed) — returns `1` with error message
- Verify `UpdateLastEditedDatesAndTracking`, `UpdateJournalConfig`, `UpdateTableOfContents` are **never called** when `--dry-run` is set

### Integration Tests
- `--dry-run` leaves all files byte-for-byte identical on disk
- TOC content from `PreviewTableOfContents()` matches file written by a live `--toc` run on identical starting state (parity test)

---

## Files to Create / Modify

| File | Action |
|---|---|
| `Commands/Update/UpdateSettings.cs` | Add `--dry-run` / `--check` to `UpdateJournalSettings` |
| `Commands/Update/UpdateCommand.cs` | Add dry-run execution path + Spectre.Console rendering |
| `Infrastructure/FileSystem/IInMemoryFileBuffer.cs` | **New** interface |
| `Infrastructure/FileSystem/InMemoryFileBuffer.cs` | **New** implementation |
| `Infrastructure/Tracking/Models/UpdateDryRunReport.cs` | **New** model file (`UpdateDryRunReport`, `TocDiffResult`, `TocRenameDryRunResult`) |
| `Services/TableOfContents/ITableOfContentsService.cs` | Add `PreviewTableOfContents()` |
| `Services/TableOfContents/TableOfContentsService.cs` | Implement `PreviewTableOfContents()` (extract private method) |
| `Services/JournalUpdate/IJournalUpdateService.cs` | Add `BuildDryRunReport()` |
| `Services/JournalUpdate/JournalUpdateService.cs` | Implement `BuildDryRunReport()` |
| `Program.cs` | Register `IInMemoryFileBuffer → InMemoryFileBuffer` |
| `docs/ARCHITECTURE.md` | Add new components, decisions, DI registrations |
| `Tests/Commands/Update/UpdateCommandTests.cs` | New dry-run test cases |
| `Tests/Infrastructure/FileSystem/InMemoryFileBufferTests.cs` | **New** test file |
| `Tests/Services/TableOfContents/TableOfContentsServiceTests.cs` | New `PreviewTableOfContents` tests |

---

## Todos

1. Add `--dry-run` / `--check` flag to `UpdateJournalSettings`
2. Create `IInMemoryFileBuffer` interface
3. Create `InMemoryFileBuffer` implementation
4. Register `IInMemoryFileBuffer` in DI (`Program.cs`)
5. Add `PreviewTableOfContents()` to `ITableOfContentsService`
6. Implement `PreviewTableOfContents()` in `TableOfContentsService` (extract `GenerateTableOfContents`)
7. Create `UpdateDryRunReport`, `TocDiffResult`, `TocRenameDryRunResult` models
8. Add `BuildDryRunReport()` to `IJournalUpdateService`
9. Implement `BuildDryRunReport()` in `JournalUpdateService`
10. Add dry-run execution path to `UpdateCommand.Execute()` with Spectre.Console rendering
11. Write unit tests for `InMemoryFileBuffer`
12. Write unit tests for `TableOfContentsService.PreviewTableOfContents()`
13. Write unit tests for `UpdateCommand` dry-run paths (9 scenarios)
14. Write integration test: `--dry-run` makes zero disk writes
15. Write integration test: `PreviewTableOfContents()` parity with live `UpdateTableOfContents()`
16. Update `docs/ARCHITECTURE.md` — add `IInMemoryFileBuffer` to service overview, DI registration table, service interaction flow for dry-run path, and a new Design Decision entry for `--dry-run` / `IInMemoryFileBuffer`
17. Update `README.md` — add `--dry-run` / `--check` to the `update journal` flag table and usage examples
18. Update `docs/DEVELOPMENT.md` — add `--dry-run` to the `update journal` command reference section and note `IInMemoryFileBuffer` in any infrastructure or service documentation

---

## Bug Fix: Config and TOC Preview Must Reflect Pending Tracking Changes

### Problem

When running `update journal --dry-run` with all sections active, the **Config Changes** table and **TOC Preview** are both computed from the *current* committed tracking index and `.journalrc` — not from the *projected* state after tracking is applied. This means:

- Files shown as `✚ added` in the Tracking section are **absent** from the Config Changes table (because they aren't in the tracking index yet).
- Those same files are **absent** from the TOC preview (because they aren't in `.journalrc` yet).
- Files shown as `✖ removed` in the Tracking section still appear in the Config Changes and TOC preview as if they still exist.

**Observed example:**

```
Tracking Changes
bbb.md            ✚ added
abc-...-file_1.md ✖ removed

TOC Preview                        ← bbb.md missing; abc-...-file_1.md still present
```

In the live path this is correct because execution is sequential:
1. `UpdateLastEditedDatesAndTracking` — commits the tracking index with the new state.
2. `DetectConfigChanges` is **re-run** after step 1 (see comment in `UpdateCommand.Execute()`: _"Re-detect after tracking update so same-run additions/deletions are captured"_).
3. `UpdateJournalConfig` — adds/removes config entries based on the now-accurate index.
4. `UpdateTableOfContents` — regenerates the TOC from the updated `.journalrc`.

The dry-run skips all writes, so steps 1–3 never happen — leaving `BuildDryRunReport` reading stale state for both the config and TOC sections.

### Root Cause

In `JournalUpdateService.BuildDryRunReport`:

- `configChanges` is the result of `DetectConfigChanges()` called against the **committed** tracking index (pre-pending changes).
- `PreviewTableOfContents(journalPath)` reads the **current `.journalrc`** directly (also pre-pending changes).

Neither receives any awareness of `trackingChanges.AddedFiles` or `trackingChanges.DeletedFiles`.

### Fix Approach

**Projected tracking set:** When `trackingChanges` is non-null, compute a projected view of the tracking index:

```
projected = (committed tracking index files)
          + trackingChanges.AddedFiles
          - trackingChanges.DeletedFiles
```

**Projected config drift:** Compare `projected` against current `.journalrc` entries to produce a `JournalConfigSyncResult` that reflects what sync would look like *after* tracking is committed. Use this instead of the naively-detected `configChanges` for both the Config Changes display and the TOC preview inputs.

**Projected TOC preview:** Add a second overload to `ITableOfContentsService`:

```csharp
// Generates TOC preview using a caller-supplied projected config (no disk reads for config).
string PreviewTableOfContents(string journalDirectory, JournalConfig projectedConfig);
```

In `BuildDryRunReport`, when `includeToc` is true and `trackingChanges` is non-null:
1. Load the current `JournalConfig`.
2. Apply the projected config sync result to an in-memory clone of the config:
   - Append `FilesToAdd` files as new `RootEntries` (skip files already present; skip the TOC file).
   - Remove any `RootEntries` / topic `Entries` whose `File` matches a path in `FilesToRemove`.
3. Call `PreviewTableOfContents(journalDirectory, projectedConfig)`.

**Scope:** This only applies when `trackingChanges` is present. When `includeToc` is true but `includeTracking` is false (e.g., `--dry-run --toc`), the current behavior (preview from current config) is correct and unchanged.

### Files to Modify (additions to existing table)

| File | Change |
|---|---|
| `Services/TableOfContents/ITableOfContentsService.cs` | Add `PreviewTableOfContents(string journalDirectory, JournalConfig projectedConfig)` overload |
| `Services/TableOfContents/TableOfContentsService.cs` | Implement the overload — pass `projectedConfig` directly to `GenerateTableOfContents`, preserving existing TOC dates from disk |
| `Services/JournalUpdate/JournalUpdateService.cs` | Update `BuildDryRunReport` to compute projected config drift and projected config, using the new overload when tracking changes are present |

### New Tests (additions to existing testing strategy)

- `BuildDryRunReport` config section uses projected tracking when `trackingChanges` is non-null — added files appear in `FilesToAdd`, deleted files appear in `FilesToRemove`
- `BuildDryRunReport` TOC preview includes pending-added files and excludes pending-deleted files
- `BuildDryRunReport` with `--toc` only (no tracking) continues to use current config unchanged
- `PreviewTableOfContents(journalDirectory, projectedConfig)` overload generates correct output from the supplied config without reading `.journalrc` from disk

### New Todos (additions to existing list)

19. Add `PreviewTableOfContents(string, JournalConfig)` overload to `ITableOfContentsService` and implement in `TableOfContentsService`
20. Update `BuildDryRunReport` in `JournalUpdateService` to compute projected config drift from pending tracking changes when `trackingChanges` is non-null
21. Update `BuildDryRunReport` to call the projected-config `PreviewTableOfContents` overload when both `includeToc` and `trackingChanges` are active
22. Add unit tests: `BuildDryRunReport` config and TOC sections reflect pending tracking additions and deletions
23. Add unit test: `PreviewTableOfContents(string, JournalConfig)` overload generates correct output without reading `.journalrc`
24. Add unit test: `--dry-run --toc` (no tracking) continues to produce preview from current config (no regression)

---

## Open Questions / Future Work

- **Rollback wiring** (future, tracked separately): Wire `IInMemoryFileBuffer.Snapshot()` before each write in `JournalUpdateService` so partial failures can restore prior state. `IInMemoryFileBuffer` is designed for this — just not wired yet.
- **`--exit-on-changes`** (future): Exit code `1` when drift detected — for CI pipeline integration.
- **`update entry --dry-run`**: Out of scope. Separate PRD when needed.
