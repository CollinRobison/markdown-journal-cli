# PRD: `remove entry` Command

## Overview

Add a `remove` branch command (alias `rm`) with an `entry` subcommand that fully removes a journal
entry: deletes the file, removes it from `.journalrc`, removes it from the tracking index, and
regenerates the table of contents. An optional `--clean-refs` flag strips dead inline-link
references to the deleted file across all other journal entries.

---

## Command Structure

```
md-journal remove entry <fileName> [options]
md-journal rm entry <fileName> [options]
```

### Arguments

| Argument | Required | Description |
|---|---|---|
| `<fileName>` | Yes | The filename of the entry to remove (with or without `.md` extension). |

### Options

| Flag | Short | Default | Description |
|---|---|---|---|
| `--path` | `-p` | `.` | Path to the journal directory (inherited from `RemoveSettings`). |
| `--force` | `-f` | `false` | Skip the confirmation prompt and remove immediately. |
| `--clean-refs` | | `false` | After deleting the file, scan all other entry files and strip any inline markdown links that pointed to the removed entry, preserving link text. |

> **Naming rationale for `--clean-refs`**: the operation cleans up dead references left behind by
> the deletion. `--remove-references` is accurate but verbose; `--clean-refs` is concise and
> idiomatic for a CLI.

---

## Behaviour

### Protected files

`remove entry` **must refuse** to remove any of the following journal-infrastructure files,
regardless of whether `--force` is supplied:

| File | How it's identified |
|---|---|
| Journal config | `_journalSettings.JournalConfigFileName` (default: `.journalrc`) |
| Tracking index | `$".{_journalSettings.AppName}"` (default: `.mdjournal`) |
| Table of Contents | `IJournalConfiguration.Read(journalPath).TableOfContents.File` (may be user-renamed) |

The check is **case-insensitive** and runs before the confirmation prompt. The service throws
`ProtectedJournalFileException` (see [New exceptions](#new-exceptions)); the command maps it to
`[red]Error:[/] ... exit 1`.

> **Why read the TOC name from live config?** The TOC filename is user-configurable via
> `update journal --rename-toc`. Hardcoding the settings default would silently allow removal of a
> renamed TOC file.

### Happy path (without `--force`)

1. Validate `<fileName>` resolves to an existing `.md` file in the journal directory.
2. Display a `ConfirmationPrompt` via `IAnsiConsole`:
   > `"Are you sure you want to remove '[fileName]'? This action cannot be undone. [y/n]"`
   - If the user answers **no** → print `"Removal cancelled."` and exit `0`.
3. Delete the file via `IFileSystem.DeleteFile`.
4. Remove the config entry via `IJournalConfiguration.RemoveEntry`.
5. Remove the tracking entry via `IFileTracking.RemoveFileFromIndex`.
6. Regenerate the TOC via `ITableOfContentsService.UpdateTableOfContents`.
7. If `--clean-refs` was supplied:
   a. Update last edited date -> Strip dead links via `IMarkdownLinkRewriter.StripLinksInDirectory` → returns `modifiedFiles` 
   b. Re-hash each modified file via `IFileTracking.UpdateFileInIndex` so `update journal` does not
      flag them as changed on the next run (dead-link cleanup is housekeeping, not a content edit)
8. Print success confirmation.

### Happy path (with `--force`)

Same as above but step 2 is skipped entirely.

### Error cases

| Condition | Exit Code | Message |
|---|---|---|
| Journal `.journalrc` not found | `1` | `[red]Error:[/] ...JournalrcNotFoundException message` |
| Tracking index not found | `1` | `[red]Error:[/] ...TrackingIndexNotFoundException message` |
| **Protected file targeted** | `1` | `[red]Error:[/] '{fileName}' is a protected journal file and cannot be removed with 'remove entry'.` |
| Entry file not found | `1` | `[red]Error:[/] Entry file '{fileName}' not found at '{path}'.` |
| Unexpected exception | `1` | `[red]Error:[/] An unexpected error occurred: {ex.Message}` |

---

## Architecture

### New files

#### `markdown-journal-cli/Commands/Remove/RemoveSettings.cs`

Base settings class, mirrors `AddSettings` / `UpdateSettings`:

```csharp
public class RemoveSettings : CommandSettings
{
    [CommandOption("-p|--path")]
    [DefaultValue(".")]
    public string FilePath { get; set; } = ".";
}

public class RemoveEntrySettings : RemoveSettings
{
    [CommandArgument(0, "<fileName>")]
    [Description("The name of the file to remove (with or without .md extension).")]
    public required string FileName { get; set; }

    [CommandOption("-f|--force")]
    [Description("Skip the confirmation prompt and remove immediately.")]
    public bool Force { get; set; }

    [CommandOption("--clean-refs")]
    [Description("Scan all other entry files and strip inline links pointing to the removed entry.")]
    public bool CleanRefs { get; set; }

    public override ValidationResult Validate()
    {
        // FileName must be a valid markdown filename (letters, digits, underscores, hyphens, optional .md)
    }
}
```

#### `markdown-journal-cli/Commands/Remove/RemoveEntryCommand.cs`

Thin command, mirrors `AddEntry`. Injects `IAnsiConsole` and `IRemoveEntryService`.

- Without `--force`: calls `AnsiConsole.Confirm(...)` before delegating.
- With `--force`: delegates immediately, skipping prompt.
- Maps `JournalrcNotFoundException`, `TrackingIndexNotFoundException`, `FileNotFoundException`, and
  generic `Exception` to `[red]Error:[/]` console output + return code `1`.
- Uses `.EscapeMarkup()` on all user-provided strings interpolated into markup.
- `ILogger<RemoveEntryCommand>` for debug logging of entry point and exit.

#### `markdown-journal-cli/Services/RemoveEntry/IRemoveEntryService.cs`

```csharp
public interface IRemoveEntryService
{
    /// <summary>
    /// Removes a journal entry: deletes the file, removes it from .journalrc,
    /// removes it from the tracking index, regenerates the TOC, and optionally
    /// strips dead inline-link references across the journal.
    /// </summary>
    void RemoveEntry(string journalPath, string fileName, bool cleanRefs);
}
```

#### `markdown-journal-cli/Services/RemoveEntry/RemoveEntryService.cs`

Constructor-injected dependencies:
- `IFileSystem`
- `IJournalConfiguration`
- `IFileTracking`
- `ITableOfContentsService`
- `IMarkdownLinkRewriter`
- `IOptions<JournalSettings>`
- `ILogger<RemoveEntryService>`

Orchestration steps (each preceded by `_logger.LogDebug(...)`):

1. Normalise `fileName` — append `.md` if missing.
2. Validate `.journalrc` exists → throw `JournalrcNotFoundException` if not.
3. Validate tracking index exists → throw `TrackingIndexNotFoundException` if not.
4. **Guard against protected files** — resolve the live TOC filename from config; compare
   normalised `fileName` (case-insensitive) against `.journalrc`, `.<AppName>`, and the TOC file
   → throw `ProtectedJournalFileException` if matched.
5. Resolve absolute entry path; validate file exists → throw `FileNotFoundException` if not.
6. `IFileSystem.DeleteFile(absoluteEntryPath)`
7. `IJournalConfiguration.RemoveEntry(journalPath, resolvedFileName)`
8. `IFileTracking.RemoveFileFromIndex(journalPath, resolvedFileName)`
9. `ITableOfContentsService.UpdateTableOfContents(journalPath, lastEditedDate: DateTime.Now)`
10. If `cleanRefs`:
    a. `IMarkdownLinkRewriter.StripLinksInDirectory(journalPath, resolvedFileName)` → `modifiedFiles`
    b. For each file in `modifiedFiles`: `IFileTracking.UpdateFileInIndex(journalPath, relativePath)`
       — re-hashes so the file isn't flagged as modified on the next `update journal` run
11. `_logger.LogDebug("Successfully removed entry ...")`.

### New exceptions

#### `markdown-journal-cli/Exceptions/JournalExceptions.cs`

Add alongside the existing hierarchy:

```csharp
/// <summary>
/// Exception thrown when attempting to remove a protected journal infrastructure file
/// (e.g. .journalrc, tracking index, or the table of contents).
/// </summary>
/// <param name="fileName">The protected filename that was targeted.</param>
public class ProtectedJournalFileException(string fileName)
    : JournalException(
        $"'{fileName}' is a protected journal file and cannot be removed with 'remove entry'."
    )
{
    /// <summary>Gets the protected filename that was targeted.</summary>
    public string FileName { get; } = fileName;
}
```

### Modified files

#### `markdown-journal-cli/Infrastructure/FileSystem/IMarkdownLinkRewriter.cs`

Add a new method:

```csharp
/// <summary>
/// Scans every markdown file under <paramref name="directory"/>, strips all inline markdown
/// links whose final path segment matches <paramref name="fileName"/> — replacing
/// [text](file.md) with just the link text — and writes the file back if it changed.
/// Files listed in <paramref name="excludeFiles"/> are skipped.
/// Returns the relative paths of every file that was modified.
/// </summary>
IReadOnlyList<string> StripLinksInDirectory(
    string directory,
    string fileName,
    IReadOnlyCollection<string>? excludeFiles = null
);
```

#### `markdown-journal-cli/Infrastructure/FileSystem/MarkdownLinkRewriter.cs`

Implement `StripLinksInDirectory` using the existing `BuildLinkPattern` helper; replace each match
with `m.Groups["text"].Value` (link text only, URL discarded).

#### `markdown-journal-cli/Program.cs`

1. Register `IRemoveEntryService` / `RemoveEntryService` as singleton.
2. Register `RemoveEntryCommand` as singleton.
3. Add the `remove` branch (and a duplicate `rm` branch pointing to the same command):

```csharp
config.AddBranch<RemoveSettings>("remove", remove =>
{
    remove.SetDescription("Removes a specified file from an existing journal.");
    remove.AddCommand<RemoveEntryCommand>("entry")
        .WithExample("remove", "--path", "Source/Repos/TestJournal", "entry", "old_notes")
        .WithExample("remove", "--path", "Source/Repos/TestJournal", "entry", "old_notes", "--force")
        .WithExample("remove", "--path", "Source/Repos/TestJournal", "entry", "old_notes", "--clean-refs");
})
.WithAlias("rm"); // IBranchConfigurator.WithAlias — native Spectre.Console.Cli support (v0.49.1+)
```

---

## Console output (Spectre.Console conventions)

All user-provided strings interpolated into markup **must** be escaped with `.EscapeMarkup()`.

| Event | Output |
|---|---|
| Confirmation prompt | `AnsiConsole.Confirm("Are you sure you want to remove '[fileName]'? This action cannot be undone.")` |
| File deleted | `[green]Removed:[/] '{fileName}'` |
| Config updated | `[dim]  - {fileName} removed from config[/]` |
| Tracking updated | `[dim]  Removed from tracking: {fileName}[/]` |
| TOC updated | `[green]Table of contents updated.[/]` |
| Links stripped (per file) | `[dim]  Stripped links: {relativePath}[/]` |
| Links stripped (summary) | `[green]Cleaned dead references in {n} file(s).[/]` |
| Removal cancelled | `Removal cancelled.` |
| Success | `[green]Success:[/] Entry '{fileName}' removed.` |
| Error | `[red]Error:[/] {message}` |

---

## Testing

### `RemoveEntryCommandTests.cs` (unit — mirrors `AddEntryCommandTests`)

Tooling: `CommandAppTester` + `TestConsole` + `Moq` + `Shouldly` + `NullLogger`

Scenarios:
- `Execute_WithForce_RemovesEntryWithoutPrompt`
- `Execute_WithoutForce_UserConfirms_RemovesEntry`
- `Execute_WithoutForce_UserDenies_CancelsAndReturnsZero`
- `Execute_WithCleanRefs_CallsCleanRefs_OnService`
- `Execute_JournalrcNotFound_ReturnsOneWithErrorMessage`
- `Execute_ProtectedFile_ReturnsOneWithErrorMessage`
- `Execute_FileNotFound_ReturnsOneWithErrorMessage`
- `Execute_UnexpectedException_ReturnsOneWithErrorMessage`
- `Execute_FileNameContainsMarkup_EscapesCorrectly`

### `RemoveEntryServiceTests.cs` (unit)

Tooling: `Moq` + `Shouldly` + `NullLogger`

Scenarios:
- `RemoveEntry_DeletesFile_UpdatesConfig_UpdatesTracking_UpdatesToc`
- `RemoveEntry_WithCleanRefs_CallsStripLinksInDirectory`
- `RemoveEntry_WithCleanRefs_UpdatesTrackingForEachModifiedFile`
- `RemoveEntry_WithoutCleanRefs_DoesNotCallStripLinksInDirectory`
- `RemoveEntry_FileNotFound_ThrowsFileNotFoundException`
- `RemoveEntry_JournalrcNotFound_ThrowsJournalrcNotFoundException`
- `RemoveEntry_TrackingIndexNotFound_ThrowsTrackingIndexNotFoundException`
- `RemoveEntry_TargetsJournalConfig_ThrowsProtectedJournalFileException`
- `RemoveEntry_TargetsTrackingIndex_ThrowsProtectedJournalFileException`
- `RemoveEntry_TargetsTocFile_ThrowsProtectedJournalFileException`
- `RemoveEntry_TargetsTocFile_CaseInsensitive_ThrowsProtectedJournalFileException`
- `RemoveEntry_TargetsRenamedTocFile_ThrowsProtectedJournalFileException` (reads live config, not settings default)
- `RemoveEntry_FileNameWithoutExtension_NormalisesAndResolvesCorrectly`
- `RemoveEntry_FileNameWithExtension_ResolvesCorrectly`
- `RemoveEntry_CallOrder_FollowsOrchestrationSequence` (verify protected-file check before delete)

### `MarkdownLinkRewriterTests.cs` additions (unit)

- `StripLinksInDirectory_RemovesLinksToFile_KeepsLinkText`
- `StripLinksInDirectory_SkipsExcludedFiles`
- `StripLinksInDirectory_ReturnsEmptyList_WhenNoLinksFound`
- `StripLinksInDirectory_HandlesMultipleLinksInSingleFile`

---

## Documentation updates

### `README.md`
- Add `remove entry` command section (syntax, arguments, options, examples)
- Add `remove entry` usage examples in the Usage section
- Move `remove | rm` from Planned Features → ✅ in Development Status

### `docs/ARCHITECTURE.md`
- Update **Dependency Flow** diagram to include `RemoveEntryCommand`
- Update **DI Registration** code block to include `IRemoveEntryService` / `RemoveEntryService` and `RemoveEntryCommand`
- Add `IRemoveEntryService` to **Service Architecture → Core Services Overview**
- Update **Service Interaction Flow** to show the remove flow
- Document `StripLinksInDirectory` in the `IMarkdownLinkRewriter` description

### `docs/DEVELOPMENT.md`
- Update **Project Structure** tree to show `Commands/Remove/` and `Services/RemoveEntry/`
- Update **Service Registration** code block to include `IRemoveEntryService` and `RemoveEntryCommand`

---

## Assumptions & open questions

| # | Assumption | Risk |
|---|---|---|
| 1 | `--clean-refs` strips the link but preserves link text (`[Meeting Notes](entry.md)` → `Meeting Notes`). | Low |
| 2 | `--clean-refs` does not stamp `Last Edited` on modified files. Tracking hash **is** updated per modified file to prevent false-positive change detection on next `update journal` run. | Resolved ✅ |
| 3 | `rm` alias uses `IBranchConfigurator.WithAlias("rm")` — native Spectre.Console.Cli support confirmed in v0.49.1. | Resolved ✅ |
| 4 | Removal of the TOC file itself is out of scope. | Low |
| 5 | `<fileName>` accepts with or without `.md` extension; service normalises. | Low |

---

## Out of scope

- Removing a journal directory entirely (`remove journal`)
- Batch / glob removal of multiple entries
- Undo / recycle-bin behaviour
- Removing the TOC or tracking file
