# ADR: Add `--rename-toc <name>` Flag to `update` Command

**Status:** Proposed  
**Date:** 2026-03-14  
**Branch:** (to be cut from `main`)

---

## Context

The TOC filename is set once at journal initialisation and currently cannot be changed without manually editing `.journalrc`, renaming the file, and hand-patching every markdown link that references the old filename. This is error-prone and leaves the tracking index stale.

We also have an upcoming need to rewrite markdown link references whenever _any_ tracked file is renamed (e.g. `update entry --name`). That cross-cutting concern should be extracted into a reusable infrastructure component rather than embedded in a single service method.

---

## Decision

Add a `--rename-toc <name>` option to the existing `update` command that:

1. Validates that no other file already occupies the desired name.  
2. Renames the TOC file on disk (skip if already named correctly).  
3. Updates the `tableOfContents.file` field in `.journalrc`.  
4. Rewrites all markdown link references to the old TOC filename across the journal's markdown files using a new, reusable `IMarkdownLinkRewriter` infrastructure service.  
5. Updates the `Last Edited:` metadata and tracking index for every file that was modified (do this by updating the specific files in the file tracking preferably doing both with existing methods).

---

## CLI Contract

```
journal update --rename-toc <name> 
```

- `<name>` — the desired stem of the new TOC file (e.g. `MyContents`). The `.md` extension is always appended automatically. The user must _not_ include the extension.
check the journal configuration .journalrc for existing toc file

### Behaviour Matrix

| Condition                                                              | Outcome                                                                     |
| ---------------------------------------------------------------------- | --------------------------------------------------------------------------- |
| `<name>.md` equals current TOC filename                                | Skip file rename; still run link-reference check and update any stale links |
| `<name>.md` does not exist yet                                         | Rename file, update `.journalrc`, rewrite links in other files              |
| A file named `<name>.md` already exists and is **not** the current TOC | Error: exit code 1 with clear message                                       |
| `.journalrc` not found                                                 | Error: exit code 1 (existing `JournalrcNotFoundException`)                  |
| Tracking index not found                                               | Error: exit code 1 (existing `TrackingIndexNotFoundException`)              |

### Example output

```
Renamed TOC: 1a-TableOfContents.md → MyContents.md
Updated .journalrc table-of-contents filename.
Updated links in: notes/intro.md
Updated links in: notes/chapter-1.md
Last Edited updated for 2 file(s).
```

---

## Architecture

### Principle: keep the command thin

`UpdateCommand` must not contain business logic. It reads settings, delegates to `IJournalUpdateService.RenameToc`, and handles exceptions for display.

### New Components

#### 1. `IMarkdownLinkRewriter` — `Infrastructure/FileSystem/`

Reusable, stateless interface for finding and rewriting markdown link references across files. Intentionally generic so it can serve future rename operations (e.g. `update entry --name`).

```csharp
namespace markdown_journal_cli.Infrastructure.FileSystem;

/// <summary>
/// Finds and rewrites markdown link references to a given filename inside file content.
/// Matches standard markdown links: [text](path/to/file.md) and [text](file.md)
/// where the final path segment equals the target filename.
/// </summary>
public interface IMarkdownLinkRewriter
{
    /// <summary>
    /// Rewrites all markdown link references whose final path segment matches
    /// <paramref name="oldFileName"/> to use <paramref name="newFileName"/> instead,
    /// preserving any leading path segments.
    /// Returns the updated content string (unchanged if no matches found).
    /// </summary>
    string RewriteLinks(string content, string oldFileName, string newFileName);

    /// <summary>
    /// Returns the relative paths (relative to <paramref name="directory"/>) of all
    /// markdown files whose content contains a markdown link to <paramref name="fileName"/>.
    /// </summary>
    IReadOnlyList<string> FindFilesWithLinkTo(string directory, string fileName);
}
```

**Implementation notes:**

- Lives in `Infrastructure/FileSystem/MarkdownLinkRewriter.cs`.  
- Use a regex that matches `[<text>](<anything>/<fileName>)` and `[<text>](<fileName>)`, capturing the leading path to preserve it.  
- `FindFilesWithLinkTo` calls `IFileSystem.GetMarkdownFiles(directory)` (see below) and returns only those whose content matches.  
- Must be registered in DI as `IMarkdownLinkRewriter → MarkdownLinkRewriter` (singleton / transient are both fine — it holds no state).

#### 2. `IFileSystem` extension — `GetMarkdownFiles`

Add one method to the existing interface so `MarkdownLinkRewriter` can enumerate files without coupling to `System.IO` directly:

```csharp
/// <summary>
/// Returns the relative paths of all markdown files found recursively under
/// <paramref name="directory"/>, relative to that directory.
/// </summary>
IReadOnlyList<string> GetMarkdownFiles(string directory);
```

#### 3. `TocRenameConflictException` — `Exceptions/JournalExceptions.cs`

```csharp
/// <summary>
/// Thrown when the desired new TOC filename is already in use by another file.
/// </summary>
public class TocRenameConflictException(string directory, string fileName)
    : JournalException(
        $"Cannot rename TOC: '{fileName}' already exists in '{directory}'. " +
        "Choose a different name or remove the conflicting file first.")
{
    public string Directory { get; } = directory;
    public string FileName { get; } = fileName;
}
```

#### 4. `IJournalUpdateService` — new method

```csharp
/// <summary>
/// Renames the journal's table-of-contents file to <paramref name="newTocName"/>,
/// updates the .journalrc configuration, and rewrites all markdown link references
/// to the old TOC filename in other journal files, updating Last Edited dates and
/// the tracking index for each modified file.
/// If the TOC is already named correctly, only the link-reference check is performed.
/// </summary>
void RenameToc(string journalPath, string newTocName);
```

#### 5. `JournalUpdateService` — `RenameToc` implementation

Orchestration steps (no business logic leaks into the command):

```
RenameToc(journalPath, newTocName):
  1. Read config → get currentTocFile
  2. Compute newTocFile = newTocName + ".md"
  3. Compute newTocAbsPath = CombinePaths(journalPath, newTocFile)
  4. isAlreadyNamed = currentTocFile.Equals(newTocFile, OrdinalIgnoreCase)
  5. If !isAlreadyNamed:
       a. If FileExists(newTocAbsPath) → throw TocRenameConflictException
       b. RenameFile(CombinePaths(journalPath, currentTocFile), newTocAbsPath)
       c. Console: "Renamed TOC: {currentTocFile} → {newTocFile}"
       d. UpdateConfig: config.TableOfContents.File = newTocFile
       e. UpdateFileInIndex(journalPath, newTocFile)  // track under new name
       f. RemoveFileFromIndex(journalPath, currentTocFile)
  6. Find all .md files with links to currentTocFile (exclude TOC file itself)
  7. For each affected file:
       a. Read content
       b. Rewrite links (oldFileName → newTocFile)
       c. UpdateLastEditedDate in content
       d. WriteFile
       e. UpdateFileInIndex(journalPath, relativePath)
       f. Console: "Updated links in: {relativePath}"
  8. If no files had links → Console: "No link references needed updating."
```

#### 6. `UpdateJournalSettings` — new option

```csharp
[CommandOption("--rename-toc")]
[Description(
    "Rename the table-of-contents file to <name> (stem only, no extension). " +
    "Updates .journalrc, rewrites all markdown references, and stamps Last Edited " +
    "on modified files."
)]
public string? RenameToc { get; set; }
```

#### 7. `UpdateCommand.Execute` — dispatch

The existing `all` guard becomes:

```csharp
bool all =
    !settings.DateFlag
    && !settings.ConfigFlag
    && !settings.TocFlag
    && !settings.Tracking
    && settings.RenameToc is null;
```

Add a new block after the existing `if (all || settings.TocFlag)` block:

```csharp
if (settings.RenameToc is not null)
{
    _journalUpdateService.RenameToc(settings.FilePath, settings.RenameToc);
}
```

Catch and display `TocRenameConflictException` alongside the existing catches, returning exit code 1.

---

## Dependency Injection

Register in the DI composition root (`Program.cs` / extension method):

```csharp
services.AddSingleton<IMarkdownLinkRewriter, MarkdownLinkRewriter>();
```

`MarkdownLinkRewriter` takes `IFileSystem` as its sole constructor dependency, which is already registered.

---

## Testing Strategy

All test projects follow the pattern of concrete infrastructure with in-memory `TestFileSystem` / `TestHashService` doubles. Maintain that convention.

### `MarkdownLinkRewriterTests` — `Tests/Infrastructure/FileSystem/`

Unit-level; no mocks needed — `RewriteLinks` is pure string transformation.

| Scenario                                                            | Expected                                                 |
| ------------------------------------------------------------------- | -------------------------------------------------------- |
| Content with inline link `[TOC](1a-TableOfContents.md)`             | Replaced with `[TOC](MyContents.md)`                     |
| Content with path-prefixed link `[TOC](../1a-TableOfContents.md)`   | Replaced with `[TOC](../MyContents.md)` (path preserved) |
| Content with no matching link                                       | Content returned unchanged                               |
| Content with multiple matching links                                | All occurrences replaced                                 |
| `FindFilesWithLinkTo` with two files — one containing link, one not | Returns only the matching file's relative path           |
| `FindFilesWithLinkTo` with no markdown files                        | Returns empty list                                       |

### `JournalUpdateServiceTests` — additions to existing class

Use the existing `TestFileSystem` + `TestHashService` setup.

| Scenario                              | Expected                                                                  |
| ------------------------------------- | ------------------------------------------------------------------------- |
| Happy path: new name, no conflict     | File renamed, config updated, links rewritten, tracking updated           |
| Already named correctly               | No rename, links still checked and rewritten if stale                     |
| Conflict: `newTocFile` already exists | Throws `TocRenameConflictException`                                       |
| No other files reference the TOC      | RenameToc completes without modifying other files; prints no-link message |
| Other files reference TOC             | Links rewritten; `Last Edited` stamps updated; tracking index updated     |

### `UpdateCommandTests` — `Tests/Commands/Update/`

Light integration tests using `TestConsole` + mocked `IJournalUpdateService`.

| Scenario                                       | Expected                                             |
| ---------------------------------------------- | ---------------------------------------------------- |
| `--rename-toc MyContents` passed               | `RenameToc(".", "MyContents")` called once           |
| `TocRenameConflictException` thrown by service | Exit code 1; error message written to console        |
| `--rename-toc` combined with other flags       | `RenameToc` called; other update methods also called |
| No flags at all (`all = true`)                 | `RenameToc` NOT called                               |

---

## File Change Summary

| File                                                           | Change                                                      |
| -------------------------------------------------------------- | ----------------------------------------------------------- |
| `Infrastructure/FileSystem/IFileSystem.cs`                     | Add `GetMarkdownFiles(string directory)`                    |
| `Infrastructure/FileSystem/FileSystem.cs`                      | Implement `GetMarkdownFiles`                                |
| `Infrastructure/FileSystem/IMarkdownLinkRewriter.cs`           | **New** interface                                           |
| `Infrastructure/FileSystem/MarkdownLinkRewriter.cs`            | **New** implementation                                      |
| `Exceptions/JournalExceptions.cs`                              | Add `TocRenameConflictException`                            |
| `Services/IJournalUpdateService.cs`                            | Add `RenameToc` method signature                            |
| `Services/JournalUpdateService.cs`                             | Implement `RenameToc`; inject `IMarkdownLinkRewriter`       |
| `Commands/Update/UpdateSettings.cs`                            | Add `RenameToc` option to `UpdateJournalSettings`           |
| `Commands/Update/UpdateCommand.cs`                             | Dispatch `--rename-toc`; catch `TocRenameConflictException` |
| `Program.cs` / DI registration                                 | Register `IMarkdownLinkRewriter → MarkdownLinkRewriter`     |
| `Tests/Infrastructure/FileSystem/MarkdownLinkRewriterTests.cs` | **New** unit tests                                          |
| `Tests/Services/JournalUpdateServiceTests.cs`                  | Add `RenameToc` test cases                                  |
| `Tests/Commands/Update/UpdateCommandTests.cs`                  | Add `--rename-toc` test cases                               |

---

## Risks & Open Questions

| #   | Risk / Question                                                                                            | Mitigation                                                                                                                                                                |
| --- | ---------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | Regex false-positives matching filename fragments (e.g. `TableOfContents` inside `Old-TableOfContents.md`) | Match on full final path segment: regex asserts `/` or start-of-target before filename and `)` after                                                                      |
| 2   | Markdown reference-style links `[text][ref]` / `[ref]: url` not covered by inline-link regex               | ADR scope is inline links only; reference-style support can be added to `IMarkdownLinkRewriter` later without interface changes                                           |
| 3   | Partial failure: file renamed but config update crashes                                                    | Rollback system is tracked separately in TODO; for now surface the error and let the user re-run — idempotent because "already named correctly" path still rewrites links |
| 4   | Very large journals with many files                                                                        | `GetMarkdownFiles` + `FindFilesWithLinkTo` scan is O(n×m); acceptable for personal journal scale; no action needed                                                        |
| 5   | Name normalisation: should the user be allowed to pass `MyContents.md` with the extension?                 | Spec says stem only; `UpdateJournalSettings.Validate()` should return error if value ends with `.md`                                                                      |

---

## Assumptions

- The TOC file always resides at the root of the journal directory (no subdirectory).  
- New name validation (length, invalid path characters) is delegated to `IFileSystem.RenameFile` throwing `IOException`; we do not add a separate pre-validation step.  
- `Last Edited:` metadata updating reuses the existing `MarkdownMetadataParser.UpdateLastEditedDate` helper.  
- Only inline markdown links `[text](filename.md)` are rewritten in this iteration.
