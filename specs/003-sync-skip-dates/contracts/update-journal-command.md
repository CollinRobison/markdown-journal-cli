# CLI Command Contract: `update journal`

**Feature**: `003-sync-skip-dates`  
**Date**: 2026-04-15

This document describes the public command-line interface contract for `mdjournal update journal` after the `--sync` flag is added.

---

## Command

```
mdjournal update journal [OPTIONS]
```

---

## Flags

| Flag | Type | Default | Description |
|------|------|---------|-------------|
| `-p, --path <path>` | `string` | `.` | Root path of the journal to operate on |
| `-c, --config` | `bool` | `false` | Update `.journalrc` only |
| `-d, --date` | `bool` | `false` | Update "Last Edited:" metadata + tracking for modified files |
| `-t, --tracking` | `bool` | `false` | Update tracking index only (no date writes to entry files) |
| `--toc, --tableofcontents` | `bool` | `false` | Regenerate table of contents only |
| `--rename-toc <stem>` | `string` | `null` | Rename the TOC file to `<stem>.md`; stem must not include `.md` |
| `--dry-run, --check` | `bool` | `false` | Preview changes without writing any files |
| `--sync` *(new)* | `bool` | `false` | Update tracking + config + TOC without writing "Last Edited:" to user entry files |

---

## Behavior: No Flags (default)

When no scope flags are provided, all subsystems are updated including "Last Edited:" date writes. Equivalent to specifying `--date --config --toc` together.

---

## Behavior: `--sync`

- Updates tracking index (hashes updated; no "Last Edited:" writes to entry files)
- Updates `.journalrc` (new entries added, deleted entries removed)
- Regenerates table of contents (TOC file's own `Last Edited:` is still stamped)
- Prints `--sync active: Last Edited dates were not updated` after successful changes
- Does **not** print the summary line when the journal is already up to date

---

## Valid Flag Combinations

| Combination | Valid? | Notes |
|-------------|--------|-------|
| *(no flags)* | ✅ | Updates all subsystems including dates |
| `--sync` | ✅ | Updates all subsystems; no user-entry date writes |
| `--sync --dry-run` | ✅ | Dry-run preview of all three subsystems |
| `--sync --path <p>` | ✅ | Operates on a non-default journal path |
| `--sync --rename-toc <stem>` | ✅ | Both flags operate independently; no conflict |
| `--date` | ✅ | Updates tracking + "Last Edited:" only |
| `--tracking` | ✅ | Updates tracking index only; no date writes |
| `--config` | ✅ | Updates `.journalrc` only |
| `--toc` | ✅ | Regenerates TOC only |
| `--rename-toc <stem>` | ✅ | Renames TOC file; stem must not end in `.md` |
| `--dry-run` | ✅ | Preview changes for all requested sections |
| Any scope flag + `--path` | ✅ | `--path` is always composable |
| Any scope flag + `--dry-run` | ✅ | `--dry-run` is always composable |

---

## Invalid Flag Combinations (Validation Errors)

All of the following are rejected by `UpdateJournalSettings.Validate()` before any file I/O:

| Combination | Error Message |
|-------------|---------------|
| `--sync --date` | `--sync and --date are mutually exclusive. --sync suppresses date writes; --date requests them.` |
| `--sync --tracking` | `--sync and --tracking are mutually exclusive. --sync is an all-or-nothing preset; use --tracking alone to scope to one subsystem.` |
| `--sync --config` | `--sync and --config are mutually exclusive. --sync is an all-or-nothing preset; use --config alone to scope to one subsystem.` |
| `--sync --toc` | `--sync and --toc are mutually exclusive. --sync is an all-or-nothing preset; use --toc alone to scope to one subsystem.` |
| `--rename-toc MyFile.md` | `--rename-toc expects a stem only (no extension). The .md extension is appended automatically.` |

---

## Pre-flight Guards (before any I/O)

| Condition | Error | Notes |
|-----------|-------|-------|
| `.mdjournal` missing | `TrackingIndexNotFoundException` | Applies to all modes including `--sync` |
| `.mdjournal` malformed / unparseable | Descriptive parse error | Applies to all modes including `--sync` |
| `.journalrc` missing (when config/TOC required) | `JournalrcNotFoundException` | Applies when `--sync`, `--config`, `--toc`, or no-flags mode |

---

## Exit Codes

| Code | Meaning |
|------|---------|
| `0` | Success — changes applied, or journal already up to date |
| `1` | Known error — validation failure, file not found, rename conflict, or other handled exception |
| `2` | Operation failed — all writes rolled back atomically (safe to retry) |
| `3` | Operation failed — rollback itself encountered errors (manual inspection required) |

---

## Console Output Contract

### Changes applied (`--sync`)

```
[tracking output]
[config output]
[toc output]
--sync active: Last Edited dates were not updated
```

*(Last line is rendered dimmed via `[dim]...[/]` Spectre.Console markup.)*

### Already up to date

```
Everything is up to date.
```

*(The `--sync active` line does NOT appear in this case.)*

### Dry run

```
[preview report for requested sections]
[dry-run notice]
```

### Validation error

```
Error: <message identifying conflicting flags>
```

*(Exit code 1; no files read or written.)*

### Rollback (exit 2)

```
Error: <original failure message>
Rollback complete. All writes have been reversed.
```
