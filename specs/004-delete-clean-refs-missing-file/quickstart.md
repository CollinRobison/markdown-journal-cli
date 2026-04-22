# Quickstart: Delete Entry --clean-refs Tolerates Already-Deleted Files

## What changed

When `--clean-refs` is set, `remove entry` now succeeds even if the target file has already been deleted from disk. Previously the command failed immediately with `FileNotFoundException` — now it skips the delete step and still cleans up config, tracking, TOC, and dead links.

## Use cases

```bash
# File was manually deleted; clean up orphaned references
mdjournal remove entry old-notes.md --clean-refs

# Same, skip confirmation prompt
mdjournal remove entry old-notes.md --clean-refs --force

# File still exists — behaviour is unchanged (delete, then clean refs)
mdjournal remove entry old-notes.md --clean-refs
```

## Behaviour matrix

| File exists on disk | `--clean-refs` | Result |
|---|---|---|
| Yes | No | Delete file; update config/tracking/TOC |
| Yes | Yes | Delete file; update config/tracking/TOC; strip dead links |
| **No** | **Yes** | **Skip delete; update config/tracking/TOC; strip dead links** ← NEW |
| **No (also absent from config/tracking)** | **Yes** | **No-op; report 0 stripped links; no false "removed from config/tracking" output** ← NEW (US3) |
| No | No | `FileNotFoundException` error (unchanged) |

## Contracts: CLI command schema

```text
remove entry <fileName> [--path <dir>] [--force] [--clean-refs]

--clean-refs  Scan all other entry files and strip inline links pointing to
              the removed entry. Now also works when the entry file is already
              absent from disk.
```
