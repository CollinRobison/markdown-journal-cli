# Data Model: --sync Flag (Skip Last-Edited Dates)

**Feature**: `003-sync-skip-dates`  
**Phase**: 1 — Design  
**Date**: 2026-04-15

---

## Overview

This feature introduces no new data entities, no new files, and no schema changes to `.journalrc` or `.mdjournal`. The only structural change is the addition of one property to `UpdateJournalSettings` and corresponding updates to `UpdateCommand.ExecuteCore` routing logic.

---

## Changed Entities

### `UpdateJournalSettings` — `Commands/Update/UpdateSettings.cs`

**New property**:

| Property | Type | Attribute | Description |
|----------|------|-----------|-------------|
| `Sync` *(new)* | `bool` | `[CommandOption("--sync")]` | When true, updates tracking (no date writes), config, and TOC without writing "Last Edited:" to user entry files. |

**New validation rules in `Validate()`**:

| Condition | Error Message |
|-----------|---------------|
| `Sync && DateFlag` | `"--sync and --date are mutually exclusive. --sync suppresses date writes; --date requests them."` |
| `Sync && Tracking` | `"--sync and --tracking are mutually exclusive. --sync is an all-or-nothing preset; use --tracking alone to scope to one subsystem."` |
| `Sync && ConfigFlag` | `"--sync and --config are mutually exclusive. --sync is an all-or-nothing preset; use --config alone to scope to one subsystem."` |
| `Sync && TocFlag` | `"--sync and --toc are mutually exclusive. --sync is an all-or-nothing preset; use --toc alone to scope to one subsystem."` |

**Existing `Validate()` check** (unchanged): rejects `--rename-toc` value that ends in `.md`.

---

## `UpdateCommand.ExecuteCore` Routing Changes

### Change 1 — `bool all` detection

**Before**:
```csharp
bool all = !settings.DateFlag
    && !settings.ConfigFlag
    && !settings.TocFlag
    && !settings.Tracking
    && settings.RenameToc is null;
```

**After**:
```csharp
bool all = !settings.DateFlag
    && !settings.ConfigFlag
    && !settings.TocFlag
    && !settings.Tracking
    && !settings.Sync           // ← new exclusion
    && settings.RenameToc is null;
```

### Change 2 — Sync routing block

Inside the `if (all || settings.DateFlag || settings.Tracking || settings.ConfigFlag || settings.TocFlag || settings.Sync)` guard, add a `--sync` branch alongside the existing flag branches:

```csharp
if (settings.Sync)
{
    _journalUpdateService.UpdateLastEditedDatesAndTracking(
        settings.FilePath,
        fileResults,
        trackingOnly: true         // ← key: no date writes to entry files
    );

    var configSyncResult = _journalConfiguration.DetectConfigChanges(settings.FilePath);
    _journalUpdateService.UpdateJournalConfig(settings.FilePath, configSyncResult);

    _journalUpdateService.UpdateTableOfContents(settings.FilePath);

    _console.MarkupLine("[dim]--sync active: Last Edited dates were not updated[/]");
}
```

> **Note**: The `[dim]...[/]` summary line is printed inside this block, so it only appears when `hasAnythingToDo` is true (the block is only reached when changes exist). If the journal is already up to date the early-return path fires before this block.

### Change 3 — `ExecuteDryRun` include-flags

**Before**:
```csharp
var includeTracking = all || settings.DateFlag || settings.Tracking;
var includeConfig   = all || settings.ConfigFlag;
var includeToc      = all || settings.TocFlag;
```

**After**:
```csharp
var includeTracking = all || settings.DateFlag || settings.Tracking || settings.Sync;
var includeConfig   = all || settings.ConfigFlag                    || settings.Sync;
var includeToc      = all || settings.TocFlag                      || settings.Sync;
```

---

## Unchanged Entities

| Entity | Location | Change? |
|--------|----------|---------|
| `.mdjournal` (tracking index) | `Infrastructure/Tracking/` | None — `trackingOnly: true` writes the same JSON schema |
| `.journalrc` (config) | `Infrastructure/Configuration/` | None |
| Markdown entry files | user-authored `.md` | None — `--sync` explicitly prevents date writes |
| TOC file | `1a-TableOfContents.md` (or custom) | `Last Edited:` still stamped during `UpdateTableOfContents` |
| `IJournalUpdateService` | `Services/JournalUpdate/` | None — no interface changes |
| `JournalUpdateService` | `Services/JournalUpdate/` | None — no new methods |
| `IFileSystem` / `FileSystem` | `Infrastructure/FileSystem/` | None |
| `FileTransactionScope` / `IFileTransactionCoordinator` | `Infrastructure/Transactions/` | None |

---

## Test Coverage Required (FR-011)

| Test | Type | File |
|------|------|------|
| `--sync` updates tracking/config/TOC; no date writes on entry files | Integration | `UpdateCommandIntegrationTests.cs` |
| `--sync` no-op when journal is up to date | Integration | `UpdateCommandIntegrationTests.cs` |
| `--sync` adds new (previously untracked) entry files | Integration | `UpdateCommandIntegrationTests.cs` |
| `--sync` removes deleted entry files from tracking | Integration | `UpdateCommandIntegrationTests.cs` |
| `--sync --dry-run` shows preview without writes | Unit | `UpdateCommandTests.cs` |
| `--sync --date` rejected at validation (exit 1) | Unit | `UpdateCommandTests.cs` |
| `--sync --tracking` rejected at validation (exit 1) | Unit | `UpdateCommandTests.cs` |
| `--sync --config` rejected at validation (exit 1) | Unit | `UpdateCommandTests.cs` |
| `--sync --toc` rejected at validation (exit 1) | Unit | `UpdateCommandTests.cs` |
| Atomic rollback on partial failure with `--sync` | Unit (`TestFileSystem`) | `UpdateCommandTests.cs` |
| Abort on malformed `.mdjournal` with `--sync` | Unit (`TestFileSystem`) | `UpdateCommandTests.cs` |
| Dry-run report with sync (all three sections populated) | Unit | `JournalUpdateServiceTests.cs` |
