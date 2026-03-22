# PRD: Journal Config Change Detection — Decoupling `UpdateJournalConfig` from Tracking Change Detection

## Overview

Introduce a config-specific change detection model (`JournalConfigSyncResult`) that compares what
is tracked in the tracking index (`.mdjournal`) against what is registered in `.journalrc`,
returning what needs to be added or removed from the config — independently of hash-based file
change detection. Update `UpdateJournalConfig` to consume this model, and update `UpdateCommand`
to call the new infra method directly.

---

## Problem

### Root Cause

`UpdateCommand` calls `DetectChangesWithoutUpdate` once to produce a `ChangeDetectionResult`.
When the `--tracking` (`-t`) flag is used alone:

1. `UpdateLastEditedDatesAndTracking(trackingOnly: true)` runs and calls
   `IFileTracking.UpdateFileInIndex` for every file in `fileResults.AddedFiles` — registering
   them in the tracking index.
2. `UpdateJournalConfig` is skipped because the user only requested a tracking update.
3. On the next run, those files are now "known" to the tracker, so `DetectChangesWithoutUpdate`
   no longer reports them in `AddedFiles`.
4. Because `UpdateJournalConfig` depends entirely on `fileResults.AddedFiles` to know what to
   add, **those files can never be automatically added to `.journalrc`**.

The comment left in `UpdateCommand.cs` confirms the bug was known:

```csharp
// found a bug here where if the -t flag runs first and the you try to run an update
// on the config it will not update since they both rely on file results to update.
```

### Why the Current Design Is Fragile

`UpdateJournalConfig` borrows a change signal (`ChangeDetectionResult`) that belongs to the
file-tracking subsystem. Once the tracker consumes and integrates that signal, the config layer
has no independent way to ask "what files am I missing?" The coupling of two orthogonal
concerns — tracking state and config state — through a shared, ephemeral result object is the
structural defect.

---

## Solution

Introduce a dedicated config-sync detection capability in the Configuration infrastructure that
answers a single question: **what is in the tracking index but not in `.journalrc`, and vice
versa?** This question can be asked at any time, regardless of whether tracking has been recently
updated, because it compares two durable, on-disk sources of truth directly.

---

## Architecture

### New Model

#### `markdown-journal-cli/Infrastructure/Configuration/Models/JournalConfigSyncResult.cs`

```csharp
namespace markdown_journal_cli.Infrastructure.Configuration.Models;

/// <summary>
/// Represents the difference between what is recorded in the tracking index
/// and what is registered in the journal configuration (.journalrc).
/// Used to drive incremental updates to the journal config independently of
/// hash-based file change detection.
/// </summary>
public class JournalConfigSyncResult
{
    /// <summary>
    /// Files present in the tracking index but absent from .journalrc.
    /// These should be added to the journal configuration.
    /// </summary>
    public IReadOnlyList<string> FilesToAdd { get; init; } = [];

    /// <summary>
    /// Files registered in .journalrc but absent from the tracking index.
    /// These should be removed from the journal configuration.
    /// </summary>
    public IReadOnlyList<string> FilesToRemove { get; init; } = [];

    public bool HasChanges => FilesToAdd.Count > 0 || FilesToRemove.Count > 0;
}
```

---

### Modified: `IJournalConfiguration`

Add one new method:

```csharp
/// <summary>
/// Compares the tracking index against the journal configuration and returns
/// the set of files that need to be added to or removed from .journalrc.
/// The TOC file is automatically excluded from <see cref="JournalConfigSyncResult.FilesToAdd"/>.
/// Returns an empty result (no changes) if the journal config does not exist.
/// </summary>
/// <param name="journalPath">The root path of the journal.</param>
JournalConfigSyncResult DetectConfigChanges(string journalPath);
```

**Implementation in `JournalConfiguration`:**

1. Inject `IFileTracking` via constructor (no circular dependency: `FileTracking` depends only on
   `IFileSystem`, `IOptions<JournalSettings>`, and `IHashService`).
2. Load the tracking index via `_fileTracking.LoadIndex(journalPath)`.
3. Read `.journalrc` via `Read(journalPath)`. If `null`, return an empty `JournalConfigSyncResult`.
4. Extract all config entry file paths by flattening:
   - `config.TableOfContents.RootEntries` (`.File` property)
   - All `Topic.Entries` and recursively all `Topic.Subtopics` entries
   - Collect into a `HashSet<string>` for O(1) lookup (case-insensitive, same as existing comparisons)
5. Collect all `.md` file paths from the tracking index into a `HashSet<string>`.
6. Resolve the TOC filename from `config.TableOfContents.File`.
7. Compute:
   - `FilesToAdd` = tracked files **not** in the config set, **excluding** the TOC file
   - `FilesToRemove` = config files **not** in the tracked set
8. Return `new JournalConfigSyncResult { FilesToAdd = ..., FilesToRemove = ... }`.

> **Design note:** TOC exclusion lives inside `DetectConfigChanges` (not in the service layer)
> because it is a config-domain invariant — the TOC is never a config entry. Keeping this rule
> close to the model prevents it from being forgotten at any future call site.

> **Architectural tradeoff:** Placing this method on `IJournalConfiguration` rather than a
> dedicated `IJournalConfigChangeDetector` interface keeps related config concerns together and
> avoids interface proliferation. The cost is a slightly heavier mock in tests that only need
> `IJournalConfiguration` for other operations — acceptable given the existing test pattern
> already uses the concrete `JournalConfiguration` implementation backed by `TestFileSystem`.

---

### Modified: `IJournalUpdateService`

Change the signature of `UpdateJournalConfig`:

**Before:**
```csharp
public void UpdateJournalConfig(string journalPath, ChangeDetectionResult fileResults);
```

**After:**
```csharp
/// <summary>
/// Incrementally updates the .journalrc configuration using a pre-computed config sync result:
/// adds new entries and removes deleted entries.
/// </summary>
public void UpdateJournalConfig(string journalPath, JournalConfigSyncResult syncResult);
```

**Implementation in `JournalUpdateService.UpdateJournalConfig`:**

- Replace iteration over `fileResults.AddedFiles` → iterate over `syncResult.FilesToAdd`
- Replace iteration over `fileResults.DeletedFiles` → iterate over `syncResult.FilesToRemove`
- No TOC-file exclusion guard needed here — the model already handles it
- Logging and console output patterns remain identical to existing implementation

---

### Modified: `UpdateCommand`

#### Dependency change

Inject `IJournalConfiguration` directly into `UpdateCommand` (in addition to existing
dependencies) so it can call `DetectConfigChanges`.

#### Logic changes

**Current early-return check (fragile):**
```csharp
if (!fileResults.HasChanges)
{
    if (settings.RenameToc is null)
        _console.MarkupLine("[green]Everything is up to date.[/]");
    return 0;
}
```

**Updated logic:**

Compute config sync result before the early-return check when applicable, so that config drift
is detected even when no tracked files have changed hashes:

```csharp
var configSyncResult = (all || settings.ConfigFlag)
    ? _journalConfiguration.DetectConfigChanges(settings.FilePath)
    : null;

var hasAnythingToDo = fileResults.HasChanges
    || (configSyncResult?.HasChanges ?? false);

if (!hasAnythingToDo)
{
    if (settings.RenameToc is null)
        _console.MarkupLine("[green]Everything is up to date.[/]");
    return 0;
}
```

Then in the config update block:
```csharp
if (all || settings.ConfigFlag)
{
    // configSyncResult already computed above
    _journalUpdateService.UpdateJournalConfig(settings.FilePath, configSyncResult!);
}
```

Remove the bug comment.

> **`--tracking` flag behavior:** When `-t` is run alone, `configSyncResult` is `null` (not
> computed). Config update is skipped silently. On any subsequent run that includes config
> (`all` or `--config`), `DetectConfigChanges` will correctly detect drift against the updated
> tracking index and add the missing entries. No warning is surfaced — the user explicitly
> requested a tracking-only update.

---

## Behaviour

### Happy path: `update --tracking` followed by `update`

| Step | Action | Result |
|---|---|---|
| 1 | `update --tracking` | New file added to tracking index; `.journalrc` unchanged. |
| 2 | `update` (or `update --config`) | `DetectConfigChanges` compares tracking index vs `.journalrc`; new file appears in `FilesToAdd`; file is added to `.journalrc`. |

### Happy path: `update` (all flags)

| Step | Action | Result |
|---|---|---|
| 1 | `DetectChangesWithoutUpdate` | Returns hash-based changes for date/tracking updates. |
| 2 | `DetectConfigChanges` | Returns independent config sync for config updates. |
| 3 | Both results used | Date/tracking and config updated correctly and independently. |

### Edge cases

| Case | Expected behaviour |
|---|---|
| Tracking index is empty | `FilesToAdd` = empty; `FilesToRemove` = all current config entries |
| `.journalrc` does not exist | `DetectConfigChanges` returns empty result; command throws `JournalrcNotFoundException` before reaching config update |
| TOC file is in tracking but not config | Excluded from `FilesToAdd` — never added as a config entry |
| File deleted from disk | Absent from tracking index → appears in `FilesToRemove`; removed from `.journalrc` |
| Config and tracking fully in sync | `HasChanges = false`; no console output |
| No tracked files, no config entries | `HasChanges = false`; "Everything is up to date." shown |

---

## Console Output

Existing output patterns are preserved. No new output lines are required. The bug fix is
transparent to the user — the correct entries now appear in config after a tracking-only run
followed by a full update.

| Event | Output |
|---|---|
| File added to config | `[dim]  + {relativePath}[/]` |
| File removed from config | `[dim]  - {relativePath}[/]` |
| Config updated | `[green]Journal configuration updated.[/]` |
| No config changes | `[dim]No configuration changes needed.[/]` |
| Warning (file in config but not tracked) | `[yellow]Warning:[/] config entry not found for deleted file: {relativePath}` |

---

## Logging

Existing `ILogger<JournalUpdateService>` log calls are preserved. No new log calls are required
beyond what already exists in `UpdateJournalConfig`. Add one `LogDebug` call at the top of
`DetectConfigChanges`:

```csharp
_logger.LogDebug(
    "Detecting config changes: {TrackedCount} tracked files, {ConfigCount} config entries",
    trackedFiles.Count,
    configFiles.Count
);
```

---

## Modified Files

| File | Change |
|---|---|
| `Infrastructure/Configuration/Models/JournalConfigSyncResult.cs` | **New** — sync result model |
| `Infrastructure/Configuration/IJournalConfiguration.cs` | Add `DetectConfigChanges` method |
| `Infrastructure/Configuration/JournalConfiguration.cs` | Implement `DetectConfigChanges`; inject `IFileTracking` |
| `Services/JournalUpdate/IJournalUpdateService.cs` | Change `UpdateJournalConfig` signature |
| `Services/JournalUpdate/JournalUpdateService.cs` | Update `UpdateJournalConfig` implementation |
| `Commands/Update/UpdateCommand.cs` | Inject `IJournalConfiguration`; call `DetectConfigChanges`; fix early-return logic; remove bug comment |
| `Program.cs` | No change needed (all deps already registered) |

---

## Testing

### `JournalConfigurationTests.cs` — new scenarios for `DetectConfigChanges`

Tooling: `TestFileSystem` + `FileTracking` + concrete `JournalConfiguration` (existing pattern)

| Test | Scenario |
|---|---|
| `DetectConfigChanges_ReturnsFilesToAdd_WhenTrackedFilesNotInConfig` | File in tracking, absent from `.journalrc` → `FilesToAdd` contains it |
| `DetectConfigChanges_ReturnsFilesToRemove_WhenConfigFilesNotInTracking` | File in `.journalrc`, absent from tracking → `FilesToRemove` contains it |
| `DetectConfigChanges_ExcludesTocFile_FromFilesToAdd` | TOC file in tracking, absent from config → NOT in `FilesToAdd` |
| `DetectConfigChanges_ReturnsEmpty_WhenConfigAndTrackingInSync` | Both sources agree → `HasChanges = false` |
| `DetectConfigChanges_ReturnsEmpty_WhenJournalrcDoesNotExist` | No `.journalrc` → returns empty result without throwing |
| `DetectConfigChanges_HandlesEmptyTrackingIndex` | No tracked files → all config entries appear in `FilesToRemove` |
| `DetectConfigChanges_HandlesTopicEntries` | Entry nested under a topic in `.journalrc` → correctly included in config file set |
| `DetectConfigChanges_HandlesSubtopicEntries` | Entry nested under a subtopic → correctly included in config file set |
| `DetectConfigChanges_IsCaseInsensitive_ForFileComparison` | Same file path with different casing → treated as the same entry |

### `JournalUpdateServiceTests.cs` — updated `UpdateJournalConfig` scenarios

All existing `UpdateJournalConfig` tests are updated to pass `JournalConfigSyncResult` instead of
`ChangeDetectionResult`. No new test scenarios are required — the observable behaviour is
identical; only the input type changes.

### `UpdateCommandTests.cs` — regression and new scenarios

| Test | Scenario |
|---|---|
| `Execute_TrackingOnly_ThenConfig_AddsNewFilesToConfig` | **Regression test.** Run tracking-only first (updates index), then run config-only — new file must appear in `.journalrc` |
| `Execute_All_CallsDetectConfigChanges_WithJournalConfiguration` | `DetectConfigChanges` is called when `all` is true |
| `Execute_ConfigFlag_CallsDetectConfigChanges` | `DetectConfigChanges` is called when `--config` flag is set |
| `Execute_TrackingFlag_DoesNotCallDetectConfigChanges` | `DetectConfigChanges` is NOT called when only `--tracking` is set |
| `Execute_NoChanges_InBothTrackingAndConfig_ReturnsZeroWithMessage` | `fileResults.HasChanges = false` AND `configSyncResult.HasChanges = false` → "Everything is up to date." |
| `Execute_NoTrackingChanges_ButConfigDrift_StillUpdatesConfig` | `fileResults.HasChanges = false` but `configSyncResult.HasChanges = true` → config is updated, does not return early |

---

## Assumptions & Open Questions

| # | Assumption | Risk |
|---|---|---|
| 1 | `DetectConfigChanges` does **not** detect drift between the name stored in a config entry and the filename on disk — add/remove only for v1. | Low — drift detection can be added in a future iteration |
| 2 | `IFileTracking` injected into `JournalConfiguration` introduces no circular dependency. Verified: `FileTracking` → `IFileSystem` + `IOptions<JournalSettings>` + `IHashService` only. | Resolved ✅ |
| 3 | The early-return "Everything is up to date." message only fires when both `fileResults.HasChanges` and `configSyncResult.HasChanges` are false (when applicable). | Low |
| 4 | `--tracking` alone silently skips config sync. No warning is surfaced. | Low — this is intentional per spec |
| 5 | `Program.cs` requires no change — `IJournalConfiguration` is already registered. | Resolved ✅ |

---

## Out of Scope

- Detecting renamed entries (config path ≠ tracked path)
- Warning the user about config drift when `--tracking` is used alone
- Batch config regeneration from scratch (`RegenerateStructure` already handles this)
- Detecting entries whose display name in `.journalrc` is stale relative to the filename
