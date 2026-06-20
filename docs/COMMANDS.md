[Back to README](../README.md)

# Command Reference

This document is the complete command reference for `mdjournal`.

## Command Tree

```text
mdjournal new
mdjournal init
mdjournal add entry|config|toc|tracking
mdjournal update journal|entry
mdjournal remove entry
mdjournal rm entry          # alias of remove
```

## General CLI Pattern

```bash
mdjournal <command> [options]
mdjournal <branch> [branch-options] <subcommand> [subcommand-options]
```

Examples:

```bash
mdjournal new MyJournal
mdjournal add --path ~/Documents/MyJournal entry Daily_Notes
mdjournal update --path ~/Documents/MyJournal journal --sync
```

## `new`

Create a new markdown journal directory with templates, config, TOC, and tracking metadata.

Syntax:

```bash
mdjournal new [name] [options]
```

Arguments:

- `name` (optional): journal name; defaults to configured `DefaultJournalName`

Options:

- `-p|--path <filePath>`: parent directory where the journal directory is created (default `.`)

Examples:

```bash
mdjournal new
mdjournal new WorkJournal --path ~/Documents/Journals
```

## `init`

Initialize an **existing** directory as an `mdjournal`-managed journal.

Syntax:

```bash
mdjournal init [name] [options]
```

Arguments:

- `name` (optional): journal name; defaults to target directory name

Options:

- `-p|--path`: existing directory to initialize (default `.`)
- `--toc|--tableofcontents`: TOC markdown file stem (no `.md`)

Behavior:

- Fails if target directory does not exist
- Fails if `.journalrc` already exists
- Fails if the resolved TOC filename already exists
- Creates `.journalrc`, `.mdjournal/.journalindex`, `.mdjournal/.journaltoc`, and TOC markdown file

Examples:

```bash
mdjournal init
mdjournal init MyNotes --path ~/Documents/Notes
mdjournal init --path ~/Documents/Notes --toc Contents
```

## `add entry`

Create a new journal entry file and update config, tracking index, and TOC.

Syntax:

```bash
mdjournal add entry <name> [options]
```

Arguments:

- `<name>`: entry name

Options:

- `-p|--path`: journal directory path (default `.`)
- `-t|--title`: custom TOC/display title
- `--he|--heading`: top-level heading
- `--sh|--subheading`: nested headings, `-` separated
- `--ignore`: add entry but exclude from TOC

Examples:

```bash
mdjournal add entry Daily_Standup --path ~/Documents/MyJournal
mdjournal add entry meeting_notes --title "Team Meeting" --he Work
mdjournal add entry api_design --he Tech --sh Backend-API
mdjournal add entry draft_notes --ignore
```

## `add config`

Create `.journalrc` for an existing journal if missing.

Syntax:

```bash
mdjournal add config [options]
```

Options:

- `-p|--path`: journal directory path (default `.`)
- `--toc|--tableofcontents`: TOC file stem to parse
- `-n|--name|--journalname`: journal name override

Generation order:

1. Parse TOC file
2. Parse tracking index
3. Fallback to directory scan

Examples:

```bash
mdjournal add config --path ~/Documents/MyJournal
mdjournal add config --path ~/Documents/MyJournal --toc TableOfContents --name "My Journal"
```

## `add toc`

Create TOC artifacts for an existing journal.

Syntax:

```bash
mdjournal add toc [options]
```

Options:

- `-p|--path`: journal directory path (default `.`)
- `-n|--name|--toc|--tableofcontents`: TOC markdown file stem
- `--structure-only`: create only `.mdjournal/.journaltoc`
- `--md-only`: create only markdown TOC file

Rules:

- `--structure-only` and `--md-only` are mutually exclusive
- `--name` cannot be combined with `--structure-only`

Return behavior:

- `0` when creation succeeds (including partial creation)
- `1` when all requested artifacts already exist

Examples:

```bash
mdjournal add toc --path ~/Documents/MyJournal
mdjournal add toc --path ~/Documents/MyJournal --name Contents
mdjournal add toc --path ~/Documents/MyJournal --structure-only
mdjournal add toc --path ~/Documents/MyJournal --md-only
```

## `add tracking`

Create the tracking index file for an existing journal.

Syntax:

```bash
mdjournal add tracking [options]
```

Options:

- `-p|--path`: journal directory path (default `.`)
- `--ignoreconfig` (alias `--ic`): skip `.journalrc` existence guard

Notes:

- If tracking already exists, command exits `0` with warning and does not overwrite
- The generated tracking index excludes markdown files matched by `.journalrc` `trackingIndex.noTrack`.
- `trackingIndex.noTrack` can contain a specific file name (`scratch.md`), a relative file path (`private/secret.md`), or a directory (`archive` or `archive/`).

Examples:

```bash
mdjournal add tracking --path ~/Documents/MyJournal
mdjournal add tracking --path ~/Documents/MyJournal --ignoreconfig
```

## `update journal`

Synchronize journal metadata and TOC from file changes.

Syntax:

```bash
mdjournal update [branch-options] journal [options]
```

Branch options:

- `-p|--path`: journal directory path (default `.`)

Options:

- `-c|--config`: update config only
- `-d|--date`: update Last Edited metadata + tracking for modified files
- `-t|--tracking`: update tracking only (overrides `--date` behavior)
- `--toc|--tableofcontents`: regenerate TOC markdown
- `--rename-toc <name>`: rename TOC file stem and rewrite backlinks
- `--dry-run|--check`: preview changes without writes
- `--sync`: update tracking + config + TOC without writing Last Edited in user entries

Behavior:

- With no flags, runs full update set
- `--sync` is mutually exclusive with `--date`, `--tracking`, `--config`, and `--toc`
- `--rename-toc` runs independently and can be combined with other operations
- `--dry-run` always exits `0`
- Tracking and sync operations respect `.journalrc` `trackingIndex.noTrack`: matching files are skipped before hash comparison and are not saved in `.mdjournal/.journalindex`.
- `tableOfContents.ignoreFiles` only hides entries from the generated TOC; `trackingIndex.noTrack` excludes files from tracking entirely.
- TOC regeneration is skipped when the generated TOC matches the current TOC except for the `Last Edited` metadata date. In that case the TOC file and tracking index are left unchanged.

No-track examples in `.journalrc`:

```json
{
  "trackingIndex": {
    "noTrack": ["scratch.md", "private/secret.md", "archive"]
  }
}
```

Matching is case-insensitive, normalizes slashes, and does not currently support glob patterns.

Examples:

```bash
mdjournal update --path ~/Documents/MyJournal journal
mdjournal update --path ~/Documents/MyJournal journal --config
mdjournal update --path ~/Documents/MyJournal journal --date
mdjournal update --path ~/Documents/MyJournal journal --tracking
mdjournal update --path ~/Documents/MyJournal journal --toc
mdjournal update --path ~/Documents/MyJournal journal --sync
mdjournal update --path ~/Documents/MyJournal journal --rename-toc Contents
mdjournal update --path ~/Documents/MyJournal journal --dry-run --config --tracking
```

## `update entry`

Update an entry's filename, display title, heading placement, or ignore state.

Syntax:

```bash
mdjournal update [branch-options] entry <fileName> [options]
```

Branch options:

- `-p|--path`: journal directory path (default `.`)

Arguments:

- `<fileName>`: entry filename with or without `.md`

Options:

- `-n|--name`: new entry name (filename stem)
- `-t|--title`: new display title in TOC
- `--he|--headings`: new heading path (`-` separated, `_` for spaces)
- `--ignore`: add to ignore list
- `--unignore`: remove from ignore list
- `--nb|--no-backlinks`: skip backlink rewrites on rename

Rules:

- `--ignore` and `--unignore` are mutually exclusive

Examples:

```bash
mdjournal update --path ~/Documents/MyJournal entry draft --name api_design
mdjournal update --path ~/Documents/MyJournal entry api_design --title "API Design Doc"
mdjournal update --path ~/Documents/MyJournal entry api_design --he Tech-Backend
mdjournal update --path ~/Documents/MyJournal entry api_design --ignore
mdjournal update --path ~/Documents/MyJournal entry api_design --unignore
```

## `remove entry` / `rm entry`

Remove an entry file, then synchronize config/tracking/TOC. Optionally clean dead inline references.

Syntax:

```bash
mdjournal remove [branch-options] entry <fileName> [options]
mdjournal rm [branch-options] entry <fileName> [options]
```

Branch options:

- `-p|--path`: journal directory path (default `.`)

Arguments:

- `<fileName>`: entry filename with or without `.md`

Options:

- `-f|--force`: skip confirmation prompt
- `--clean-refs`: remove dead inline links to the removed file in other entries

Protected files:

- `.journalrc`
- `.mdjournal/.journalindex`
- Current TOC markdown file

Examples:

```bash
mdjournal remove --path ~/Documents/MyJournal entry old_notes
mdjournal remove --path ~/Documents/MyJournal entry old_notes --force
mdjournal remove --path ~/Documents/MyJournal entry old_notes --clean-refs --force
mdjournal rm --path ~/Documents/MyJournal entry old_notes --force
```

## Exit Codes

All commands use this contract:

| Code | Meaning |
| --- | --- |
| `0` | Success |
| `1` | Error (validation/guard/unexpected) |
| `2` | Failed mid-write, rollback fully restored all tracked changes |
| `3` | Failed mid-write, rollback also had failures |

`--dry-run` / `--check` always returns `0`.
