# Feature Specification: Update --sync Flag (Skip Last-Edited Dates)

**Feature Branch**: `003-sync-skip-dates`
**Created**: 2026-04-15
**Status**: Draft (Clarified)
**Input**: User description: "add a mdjournal journal --sync or similar command that does mdjournal update journal but doesn't update the last edited date. For example when people pull from a git repo and have to merge changes so the entry hash tracking might be messed up so you don't want entries looking like they were edited but weren't. The point of this one is to update everything except the last edited dates."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Sync After Git Pull / Merge (Priority: P1)

A developer stores their journal in a git repository. After pulling from a remote or resolving a merge conflict, some entry file hashes in the tracking index no longer match the files on disk. The developer wants to resync the tracking index, config, and table of contents without stamping today's date on entries that were not genuinely edited.

**Why this priority**: This is the core motivating use case. Without this flag, every post-merge `update journal` would corrupt the "Last Edited:" metadata on entries the user never touched, making journal history unreliable.

**Independent Test**: Can be tested standalone by running `update journal --sync` against a journal whose tracking index is stale and confirming that:
1. Tracking index hashes are updated
2. Config reflects any added/removed entries
3. Table of contents is regenerated
4. No "Last Edited:" field in any entry file is changed

**Acceptance Scenarios**:

1. **Given** a journal with stale tracking hashes, **When** the user runs `mdjournal update journal --sync`, **Then** tracking, config, and TOC are updated; no entry file has its "Last Edited:" date modified.
2. **Given** a journal with all hashes current, **When** the user runs `mdjournal update journal --sync`, **Then** the command exits 0 with "Everything is up to date." and makes no file writes.
3. **Given** a journal with new entry files not yet tracked, **When** the user runs `mdjournal update journal --sync`, **Then** the new files are added to the tracking index and config; their "Last Edited:" metadata is not written.
4. **Given** a journal with deleted entry files, **When** the user runs `mdjournal update journal --sync`, **Then** the deleted files are removed from the tracking index and config; no other file is modified.

---

### User Story 2 - Dry-Run Preview of Sync (Priority: P2)

A developer wants to preview what `--sync` would change before committing to the operation.

**Why this priority**: The existing `--dry-run` / `--check` mechanism is already present on the update command and should compose naturally with `--sync` so users can audit changes safely before running.

**Independent Test**: Can be tested by running `update journal --sync --dry-run` and confirming the output shows only tracking/config/TOC changes with no date-update rows, and that no files are written.

**Acceptance Scenarios**:

1. **Given** a journal with stale tracking, **When** the user runs `mdjournal update journal --sync --dry-run`, **Then** the command prints a preview report of tracking/config/TOC changes and exits 0 without writing any files.
2. **Given** a journal that is up to date, **When** the user runs `mdjournal update journal --sync --dry-run`, **Then** the command prints "Everything is up to date." with a dry-run notice.

---

### User Story 3 - Contradictory Flag Rejection (Priority: P3)

A developer accidentally combines `--sync` with `--date`. These flags are contradictory: `--sync` explicitly skips date writes while `--date` explicitly requests them. The tool must reject the combination before performing any I/O.

**Why this priority**: Providing explicit feedback for contradictory flags prevents silent, hard-to-debug behavior and teaches users the correct mental model.

**Independent Test**: Can be tested by running `update journal --sync --date` and confirming a validation error is returned before any writes are made.

**Acceptance Scenarios**:

1. **Given** any journal, **When** the user runs `mdjournal update journal --sync --date`, **Then** the command exits non-zero with a human-readable validation error explaining the conflict; no files are read or written.

---

### Edge Cases

- What happens when `--sync` is combined with `--tracking`, `--config`, or `--toc`? These combinations are rejected as validation errors. `--sync` is an all-or-nothing preset; the granular flags exist precisely to scope the update to a single subsystem. Mixing the two sends contradictory intent — "only this one thing" vs. "all three" — and the tool must call that out rather than silently do "all three".
- What happens when the tracking index file (`.mdjournal`) is missing? The command must throw `TrackingIndexNotFoundException` before any writes — same as the regular update path.
- What happens when `.journalrc` is missing? The command must throw `JournalrcNotFoundException` before any writes, because `--sync` always includes config and TOC operations.
- What happens when `--sync` is combined with `--rename-toc`? Both operations execute independently without conflict. `--rename-toc` retains its normal date-stamping behavior on the files it modifies — `--sync` does not suppress `--rename-toc` date writes.
- Does `--sync` suppress the TOC file's own `Last Edited:` stamp during regeneration? No. The TOC file is infrastructure, not a user-authored journal entry. It continues to receive `lastEditedDate: DateTime.Now` as in the existing `UpdateTableOfContents` path. Only user entry files are protected.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The `update journal` command MUST accept a new `--sync` flag.
- **FR-002**: When `--sync` is specified, the command MUST update the tracking index (tracking-only path, no date writes) — equivalent in effect to `--tracking`.
- **FR-003**: When `--sync` is specified, the command MUST update the journal configuration — equivalent in effect to `--config`.
- **FR-004**: When `--sync` is specified, the command MUST regenerate the table of contents — equivalent in effect to `--toc`.
- **FR-005**: When `--sync` is specified, the command MUST NOT write "Last Edited:" date changes to user-authored journal entry files. The TOC file's own `Last Edited:` metadata is exempt — it is infrastructure and continues to be stamped with today's date during TOC regeneration.
- **FR-006**: When `--sync` is combined with any of `--date`, `--tracking`, `--config`, or `--toc`, the command MUST return a validation error before performing any I/O. Each of these flags contradicts `--sync`: `--date` requests date writes that `--sync` explicitly suppresses; `--tracking`, `--config`, and `--toc` scope the update to a single subsystem while `--sync` is an all-or-nothing preset.
- **FR-007**: When `--sync` is combined with `--dry-run`, the command MUST produce a preview report consistent with what `--tracking --config --toc --dry-run` would show, without writing any files.
- **FR-008**: The `--sync` flag MUST compose cleanly with `--path` to target a non-default journal location.
- **FR-009**: When `--sync` is specified and the journal is already up to date, the command MUST print "Everything is up to date." and exit 0.
- **FR-010**: All existing unit and integration tests for the `update journal` command MUST continue to pass without modification.
- **FR-011**: New tests MUST cover: sync updates tracking/config/TOC; sync does not write dates on entry files (TOC date stamp is allowed); sync + dry-run shows preview without writes; sync + date, sync + tracking, sync + config, and sync + toc are each rejected at validation.
- **FR-012**: When `--sync` is active and file changes are detected, the command MUST print a single summary line indicating that Last Edited date updates were skipped (e.g., `[dim]--sync active: Last Edited dates were not updated[/]`). This line MUST NOT appear when the journal is already up to date.

### Key Entities

- **`UpdateJournalSettings`**: Gains a new `bool Sync` property decorated with `[CommandOption("--sync")]`. Its `Validate()` method gains checks rejecting `--sync` combined with any of `--date`, `--tracking`, `--config`, or `--toc`.
- **`UpdateCommand.ExecuteCore`**: The `all` detection logic must exclude `Sync`-active invocations from the "update all including dates" path. A sync-active command routes through the tracking-only path for tracking, and independently triggers config and TOC updates.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: `mdjournal update journal --sync` on a journal with stale tracking completes in comparable wall-clock time to the equivalent `--tracking --config --toc` invocation (within 10%).
- **SC-002**: Zero user-authored journal entry files have their "Last Edited:" field modified when `--sync` is used, verifiable by file-content diff before and after the command. The TOC file's `Last Edited:` may be updated and is not included in this constraint.
- **SC-003**: Combining `--sync` with any of `--date`, `--tracking`, `--config`, or `--toc` is rejected at validation time (before any file I/O), returning exit code 1 with a human-readable error message identifying the conflicting flag.
- **SC-004**: All pre-existing tests (unit + integration) pass unmodified on the feature branch.
- **SC-005**: New tests cover the primary acceptance scenarios: stale-tracking sync, up-to-date no-op, new-file sync, deleted-file sync, dry-run preview, and rejection of `--sync` combined with each of `--date`, `--tracking`, `--config`, and `--toc`.

## Clarifications

### Session 2026-04-15

- Q: When `--sync` regenerates the TOC, should the TOC file's own `Last Edited:` metadata be stamped with today's date, or should it be preserved/blanked? → A: TOC gets today's date — it is infrastructure, not a user-authored entry (FR-005, SC-002, Edge Cases updated).
- Q: When `--sync` is combined with `--rename-toc`, should `--rename-toc`'s date stamps on modified files be suppressed? → A: No — `--rename-toc` retains its normal date-stamping behavior regardless of `--sync` (Edge Cases updated).
- Q: When `--sync` runs and file changes are detected, should the console indicate that date updates were skipped? → A: Yes — a single summary line `[dim]--sync active: Last Edited dates were not updated[/]` after the tracking/config/TOC output; no per-file noise (FR-012 added).
- Q: Should `--sync` combined with granular flags (`--tracking`, `--config`, `--toc`) be allowed as redundant but valid, or rejected? → A: Rejected — the granular flags communicate "only this subsystem", which contradicts `--sync`'s all-or-nothing preset. Allowing the combination silently does more than the user named, which is confusing UX (FR-006, SC-003, Edge Cases, Assumptions updated).

## Assumptions

- `--sync` is added to the existing `update journal` sub-command rather than introduced as a new top-level `sync journal` command. This minimizes surface area and stays consistent with the existing flag-based customization model.
- `--sync` always implies all three subsystems (tracking + config + TOC). It is not a partial selector; users needing only one subsystem should use `--tracking`, `--config`, or `--toc` individually.
- Combining `--sync` with `--tracking`, `--config`, or `--toc` is a validation error. These granular flags communicate "scope this operation to one subsystem", which directly contradicts the all-or-nothing nature of `--sync`.
- `--rename-toc` remains independent of `--sync`; they can be combined without conflict, and `--rename-toc` continues to stamp dates on affected files per its existing behavior.
- The existing `trackingOnly: true` path in `UpdateLastEditedDatesAndTracking` is the correct low-level primitive to reuse — `--sync` routes through that path for the tracking subsystem.
- `--dry-run` composability is expected to work naturally because the dry-run path already branches on individual flag values; `--sync` follows the same pattern as other flags.
