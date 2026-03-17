# PRD: Backlink Update on Entry Rename

**Feature Area:** `update entry` command  
**Status:** Proposed

---

## Problem Statement

When a user renames a journal entry (via `--name` or `--headings` flags on `update entry`), the physical file and all internal configuration artifacts (`.journalrc`, tracking index, TOC) are updated automatically. However, other markdown entry files in the journal that contain inline links pointing to the renamed file are left with stale references. This causes broken links in the journal.

---

## Proposed Solution

After any rename that changes the physical filename, the `update entry` command should automatically scan all markdown entry files in the journal for inline links to the old filename and rewrite them to the new filename. This reuse the existing `IMarkdownLinkRewriter` infrastructure (already used by `update journal --rename-toc`).

A new opt-out flag `--no-backlinks` (`--nb`) allows users to skip this behavior if desired.

---

## Scope

### In Scope

- Trigger backlink update when `--name` or `--headings` produce a different physical filename.
- Scan all `.md` files under the journal directory (recursive).
- Exclude the TOC file (it is managed by `UpdateTableOfContents`).
- Exclude the renamed entry itself (it has the new name now; self-links are not expected).
- Add `--no-backlinks` / `--nb` flag to `update entry`; default is **update enabled**.
- Add new parameter `updateBacklinks` (default `true`) to `IJournalFileUpdateService.UpdateEntry()`.
- Comprehensive unit tests covering: update occurs on rename, update is skipped on `--no-backlinks`, update does not occur when only display name metadata changes (no file rename), and TOC file is excluded.
- must update the readme.md(s) architecture.md and the development.md where needed. update docs.
- Reference-style markdown links (only inline `[text](url)` links are handled by `IMarkdownLinkRewriter`). you can look into extending `IMarkdownLinkRewriter` to handle these.
- Stamping `Last Edited` metadata on files whose links were rewritten (consistent with targeted rename behavior; the linked files are incidentally modified, not "edited" by the user).

### Out of Scope
- Updating links in non-entry files (TOC, tracking index, `.journalrc`).

---

## Behavioral Specification

### Trigger Condition

Backlink update fires **only** when `isRenaming == true` inside `UpdateEntry` — i.e., when `--name` or `--headings` produce a target filename that differs (case-insensitively) from the current filename.

It does **not** fire for:
- `--title` only (display name change, no file rename)
- `--ignore` / `--unignore`
- `--headings` changes that result in the same physical filename (no-op rename)

### Exclusion List

Files excluded from the `ReplaceLinksInDirectory` scan:
1. The renamed file itself (`newFile`) — it now has the correct name.
2. The TOC file — managed independently by `UpdateTableOfContents`.

### `--no-backlinks` / `--nb` Flag

| Scenario | Behavior |
|---|---|
| Flag absent (default) | Backlinks are updated after rename |
| `--no-backlinks` present | Backlink scan is skipped; rename still proceeds |

---

## API Changes

### `UpdateEntrySettings` (UpdateSettings.cs)

Add:

```csharp
[CommandOption("--nb|--no-backlinks")]
[Description("Skip updating inline link references in other entry files after a rename. Backlink updates are enabled by default.")]
public bool NoBacklinks { get; set; }
```

### `IJournalFileUpdateService`

Update signature:

```csharp
void UpdateEntry(
    string directory,
    string currentFileName,
    string? newEntryName = null,
    string? newEntryTitle = null,
    string? newHeadings = null,
    bool ignoreFile = false,
    bool unignoreFile = false,
    bool updateBacklinks = true   // NEW
);
```

### `JournalFileUpdateService`

- Constructor: inject `IMarkdownLinkRewriter markdownLinkRewriter`.
- `UpdateEntry()`: after `ApplyFileRename(...)`, if `isRenaming && updateBacklinks`, call `_markdownLinkRewriter.ReplaceLinksInDirectory(...)` with appropriate exclusions.

### `UpdateEntryCommand`

Pass `!settings.NoBacklinks` as `updateBacklinks` argument.

---

## Testing Requirements

### Unit Tests — `UpdateEntryCommandTests`

- `Execute_PassesUpdateBacklinks_True_ByDefault` — verify service called with `updateBacklinks: true` when `--no-backlinks` is absent.
- `Execute_PassesUpdateBacklinks_False_WhenNoBACKLINKSFlagSet` — verify service called with `updateBacklinks: false` when `--no-backlinks` is present.

### Unit Tests — `JournalFileUpdateServiceTests`

- `UpdateEntry_CallsReplaceLinksInDirectory_WhenRenameOccurs` — verifies rewriter is called with correct old/new filenames and exclusions.
- `UpdateEntry_DoesNotCallReplaceLinksInDirectory_WhenNoRenameOccurs` — title-only update must not trigger rewriter.
- `UpdateEntry_DoesNotCallReplaceLinksInDirectory_WhenUpdateBacklinksFalse` — opt-out flag respected.
- `UpdateEntry_ExcludesTocFile_FromReplaceLinks` — TOC file is in the exclusions list.
- `UpdateEntry_ExcludesRenamedFile_FromReplaceLinks` — renamed entry itself is excluded.

---

## Implementation Notes

- `IMarkdownLinkRewriter` is already registered in DI (`Program.cs`) and injected into `JournalUpdateService` — no new DI registration required.
- Pattern mirrors `JournalUpdateService.RenameToc()` which already calls `ReplaceLinksInDirectory`.
