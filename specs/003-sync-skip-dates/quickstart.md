# Quickstart: Using `mdjournal update journal --sync`

**Feature**: `003-sync-skip-dates`  
**Date**: 2026-04-15

---

## When to Use `--sync`

Use `--sync` after a **git pull** or **merge conflict resolution** where your journal's tracking index (`.mdjournal`) may have stale hashes — the computed hashes no longer match the files on disk — but the entry content has not been genuinely edited by you.

Running the standard `update journal` in this situation would re-stamp "Last Edited:" on every entry that appears hash-changed, corrupting your journal history. `--sync` rebuilds tracking, config, and the table of contents without touching "Last Edited:" on any of your entries.

---

## Basic Usage

```bash
# Sync the journal in the current directory
mdjournal update journal --sync

# Sync a journal at a specific path
mdjournal update journal --sync --path /path/to/MyJournal
```

**Sample output when changes exist:**

```
Updated tracking: 3 modified, 1 added, 0 removed
Config updated: 1 added
Table of contents regenerated
--sync active: Last Edited dates were not updated
```

**Sample output when already up to date:**

```
Everything is up to date.
```

*(The `--sync active` line does not appear when there is nothing to do.)*

---

## Preview Changes Without Writing (Dry Run)

```bash
mdjournal update journal --sync --dry-run
```

Shows a preview report of what tracking, config, and TOC changes would be made. No files are written.

---

## What `--sync` Does

| Subsystem | Behavior |
|-----------|----------|
| Tracking index (`.mdjournal`) | Hashes updated for all changed / new / deleted files |
| Config (`.journalrc`) | New entries added; deleted entries removed |
| Table of Contents | Regenerated |
| Entry "Last Edited:" metadata | **Not changed** |
| TOC file's own `Last Edited:` | Still stamped (TOC is infrastructure, not a user entry) |

---

## What `--sync` Does NOT Do

- Does **not** write "Last Edited:" to user-authored entry files — that is the entire point.
- Does **not** suppress the TOC file's own `Last Edited:` stamp.
- Does **not** interact with `--rename-toc` — that flag works independently and continues to stamp dates on the files it modifies.

---

## Valid and Invalid Combinations

| Command | Result |
|---------|--------|
| `update journal --sync` | ✅ Syncs tracking + config + TOC; no entry date writes |
| `update journal --sync --dry-run` | ✅ Dry-run preview; no writes |
| `update journal --sync --path <p>` | ✅ Target a specific journal |
| `update journal --sync --rename-toc NewName` | ✅ Rename TOC + sync; both operate independently |
| `update journal --sync --date` | ❌ Validation error — mutually exclusive |
| `update journal --sync --tracking` | ❌ Validation error — mutually exclusive |
| `update journal --sync --config` | ❌ Validation error — mutually exclusive |
| `update journal --sync --toc` | ❌ Validation error — mutually exclusive |

> **Why are `--tracking`, `--config`, and `--toc` rejected?** These flags communicate "scope this operation to only one subsystem." `--sync` is an all-or-nothing preset. Allowing the combination would silently do "all three" when the user only named one, which is confusing.

---

## Pre-flight Guards

`--sync` respects the same pre-flight checks as `update journal`:

- If `.mdjournal` is missing → `TrackingIndexNotFoundException` (exit 1, no writes)
- If `.mdjournal` exists but is malformed → descriptive parse error (exit 1, no writes)
- If `.journalrc` is missing → `JournalrcNotFoundException` (exit 1, no writes)

If `--sync` partially fails (e.g., tracking updates succeed but TOC write fails), **all writes are rolled back atomically** (exit code 2). The journal is left in the same state as before the command ran.
