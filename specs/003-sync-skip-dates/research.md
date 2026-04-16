# Research: --sync Flag (Skip Last-Edited Dates)

**Feature**: `003-sync-skip-dates`  
**Phase**: 0 — Pre-design context gathering  
**Date**: 2026-04-15

---

## 1. Implementation Approach: Where Does `--sync` Live?

**Decision**: `--sync` is added to the existing `update journal` sub-command, not introduced as a new top-level command.

**Rationale**: The feature is a behavioral variant of `update journal` — same subsystems (tracking, config, TOC), different date-write behavior. Adding a flag stays consistent with the existing `--tracking`, `--config`, `--toc`, and `--date` customization model. A top-level `sync journal` command would duplicate the routing logic without adding clarity, and would widen the public CLI surface unnecessarily.

**Alternatives considered**:
- New top-level command `sync journal`: rejected per spec Assumptions — minimizes surface area and stays consistent with flag-based model.

---

## 2. Tracking-Only Path Reuse

**Decision**: `--sync` routes through the existing `UpdateLastEditedDatesAndTracking(journalPath, fileResults, trackingOnly: true)` path. No new service method is needed.

**Rationale**: The `trackingOnly` parameter already exists on `IJournalUpdateService.UpdateLastEditedDatesAndTracking`. When `true`, it updates hashes in the tracking index without writing "Last Edited:" metadata to entry files. This is exactly the behavior `--sync` requires for the tracking subsystem. The interface and implementation are unchanged; only the command routing decides which value to pass.

**Alternatives considered**:
- New `SyncTracking(path, changes)` method on `IJournalUpdateService`: rejected — would duplicate the existing `trackingOnly: true` path with no additional value.

---

## 3. "All" Detection Logic Update

**Decision**: The `bool all` local variable in `UpdateCommand.ExecuteCore` must explicitly exclude `settings.Sync`. The updated expression is:

```csharp
bool all = !settings.DateFlag
    && !settings.ConfigFlag
    && !settings.TocFlag
    && !settings.Tracking
    && !settings.Sync           // ← new exclusion
    && settings.RenameToc is null;
```

**Rationale**: Without this exclusion, a bare `update journal --sync` invocation would set `all = true` and route through the "update all including dates" branch, defeating the entire purpose of `--sync`. The `all` flag is the catch-all path that includes date writes; `--sync` must be excluded from it and routed explicitly.

**Alternatives considered**:
- Treating `--sync` as a superset alias of `all` via flag aliasing: not possible in Spectre.Console.Cli's `CommandOption` model without custom post-processing; the routing in `ExecuteCore` is the canonical place.

---

## 4. Dry-Run Composition

**Decision**: In `ExecuteDryRun`, when `settings.Sync` is true, set `includeTracking = true`, `includeConfig = true`, `includeToc = true` — the same coverage as `all`.

```csharp
var includeTracking = all || settings.DateFlag || settings.Tracking || settings.Sync;
var includeConfig   = all || settings.ConfigFlag                    || settings.Sync;
var includeToc      = all || settings.TocFlag                      || settings.Sync;
```

**Rationale**: `--sync` is an all-or-nothing preset (spec Assumptions). Its dry-run preview must reflect all three subsystems. The dry-run path never writes date updates anyway, so `--sync` in dry-run mode is naturally "show tracking + config + TOC changes, without date-update rows".

**Alternatives considered**:
- Checking individual flags inside `ExecuteDryRun` when `settings.Sync` is true as a conditional block: equivalent outcome but less explicit and harder to read.

---

## 5. Validation: Contradictory Flag Combinations

**Decision**: `UpdateJournalSettings.Validate()` rejects four combinations before any I/O:

| Combination | Error |
|-------------|-------|
| `--sync` + `--date` | Mutually exclusive — `--sync` suppresses date writes; `--date` requests them |
| `--sync` + `--tracking` | Contradicts all-or-nothing preset — use `--tracking` alone to scope to one subsystem |
| `--sync` + `--config` | Same — use `--config` alone |
| `--sync` + `--toc` | Same — use `--toc` alone |

**Rationale**: Per spec FR-006 and SC-003. Spectre.Console.Cli calls `Validate()` before `ExecuteCore`, guaranteeing these checks run before any file I/O. The user gets a clear, actionable error message identifying the conflicting flag.

**Alternatives considered**:
- Allowing `--sync` + `--tracking/--config/--toc` as redundant-but-valid: rejected per spec clarification (Q4 session 2026-04-15) — silently doing "all three" when the user named only one subsystem is confusing UX.

---

## 6. FR-012 Summary Line Placement

**Decision**: The `[dim]--sync active: Last Edited dates were not updated[/]` line is printed in `UpdateCommand.ExecuteCore` immediately after the tracking/config/TOC updates complete, and only when `hasAnythingToDo` was `true`.

**Rationale**: Per spec clarification (Q7 session 2026-04-15) — the line must not appear when the journal is already up to date. The command layer owns all console output (Constitution I), so this belongs in `ExecuteCore`, not in any service. Printing it unconditionally (including on the no-op path) would be noise.

**Alternatives considered**:
- Printing the line from `JournalUpdateService`: rejected — violates Constitution I (thin command layer; services must not own terminal output the command can provide).

---

## 7. Atomicity / Rollback Behavior

**Decision**: `--sync` participates in the existing `FileTransactionScope` (`using var outerTx = _txCoordinator.Begin()`). No changes to the rollback infrastructure are needed.

**Rationale**: Per spec Edge Cases and FR-011. The existing transaction pattern already wraps all three subsystems. `--sync` simply routes to a subset of the existing update paths without changing the transactional contract. A partial failure (e.g., tracking succeeds but TOC write fails) triggers the same `RollbackCompletedException` path as any other update failure.

---

## 8. TOC Date Stamp Behavior

**Decision**: The TOC file's own "Last Edited:" metadata continues to be stamped during `UpdateTableOfContents` regeneration. `--sync` does not suppress TOC date writes.

**Rationale**: Per spec FR-005 and clarification (Q1 session 2026-04-15). The TOC is infrastructure generated by the tool — not a user-authored entry. SC-002 explicitly excludes the TOC file from the zero-date-writes constraint. No changes to `UpdateTableOfContents` are needed.

---

## 9. Abort on Malformed / Missing Tracking Index

**Decision**: When `.mdjournal` is missing, `--sync` throws `TrackingIndexNotFoundException` before any writes — same as the existing `update journal` pre-flight guard. When `.mdjournal` exists but is malformed, the deserialization layer throws a descriptive error that propagates before any writes.

**Rationale**: Per spec Edge Cases and clarification (Q8 session 2026-04-15). Pre-flight guards are already in place in `ExecuteCore`. `--sync` follows the same code path, so both guards apply automatically without additional code.

---

## 10. `--rename-toc` Composability

**Decision**: `--sync` and `--rename-toc` can be combined without conflict. `--rename-toc` retains its normal date-stamping behavior on the files it modifies; `--sync` does not suppress those writes.

**Rationale**: Per spec Edge Cases and clarification (Q2 session 2026-04-15). The two operations are independent: `--rename-toc` is not a "user entry date" operation; it modifies TOC-related infrastructure and updates backlinks. There is no semantic contradiction between "skip user entry dates" and "rename the TOC file". The validation in FR-006 only blocks granular scope-limiting flags (`--tracking`, `--config`, `--toc`, `--date`), not `--rename-toc`.
