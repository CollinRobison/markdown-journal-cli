# PRD: `mdjournal init` Command

## Problem Statement

The `new` command bootstraps a brand-new journal in an **empty** directory. Users with an existing folder of markdown notes need a way to retroactively enrol that directory as an mdjournal-managed journal without disturbing existing content. The `init` command fills this gap.

---

## Command Signature

```
mdjournal init [name] [-p|--path <directory>] [-n|--toc|--tableofcontents <tocFileName>]
```

---

## Differentiation from `new`

| Concern | `new` | `init` |
|---|---|---|
| Target directory | Must **not** exist — command creates it | Must **already** exist |
| Template files (Intro, Entry Template, All-My-Journals) | Created | **Not** created — existing content preserved |
| Table of Contents file | Created with default name from settings | Always created; name via `--toc`; error if name conflicts with existing file |
| `.journalrc` config | Created | Created; error if already present (already managed) |
| File-tracking index | Created (empty dir) | Created and pre-populated with existing `.md` files |

---

## Settings (`Commands/Init/InitSettings.cs`)

| Property | CLI flag(s) | Type | Default |
|---|---|---|---|
| `FilePath` | `-p \| --path` | `string` | `"."` |
| `JournalName` | `[name]` positional, optional | `string?` | directory name |
| `TableOfContentsName` | `-n \| --toc \| --tableofcontents` | `string?` | `JournalSettings.TableOfContentsFileName` |

**Validation:** Same rules as `NewCommand.Settings` — no empty/whitespace, no spaces, no path-invalid characters in `JournalName`.

---

## Command (`Commands/Init/InitCommand.cs`)

**Dependencies:** `IAnsiConsole`, `IFileSystem`, `IInitJournalService`, `IOptions<JournalSettings>`

**Execute flow:**
1. Resolve `journalName` → `settings.JournalName ?? Path.GetFileName(Path.GetFullPath(filePath)) ?? _journalSettings.DefaultJournalName`
2. Directory does not exist → `[red]Error:[/]` return `1`
3. `.journalrc` already exists → `[red]Error:[/] already a managed journal` return `1`
4. Call `_initJournalService.Initialize(filePath, journalName, settings.TableOfContentsName)`
5. Catch `TocFileAlreadyExistsException` → `[red]Error:[/]` return `1`
6. Catch `Exception` → `[red]Error:[/] unexpected` return `1`
7. Success → `[green]Success:[/]` return `0`

---

## Service (`Services/IInitJournalService.cs` / `InitJournalService.cs`)

```csharp
public interface IInitJournalService
{
    void Initialize(string journalDirectory, string journalName, string? tableOfContentsName);
}
```

**`Initialize` steps:**
1. Guard clauses for null/whitespace args
2. `CreateTableOfContents(dir, tocName)` — resolve name, throw `TocFileAlreadyExistsException` if file exists, else create blank TOC template
3. `CreateJournalConfiguration(dir, journalName, tocName)` — single root entry pointing to TOC file
4. `CreateFileTrackingIndex(dir)` — `LoadIndex` + `UpdateIndex` (pre-populates existing `.md` files)

> **Forward-compatibility:** Interface accepts `string? tableOfContentsName` (nullable today). When `--no-toc` ships, `InitCommand` passes `null` and `CreateTableOfContents` becomes a no-op for null. Service and interface stay unchanged.

---

## Exceptions (`Exceptions/JournalExceptions.cs`)

- `TocFileAlreadyExistsException(string directory, string fileName)` — thrown by service when TOC file name conflicts

---

## Files

| Action | Path |
|---|---|
| Create | `Commands/Init/InitSettings.cs` |
| Create | `Commands/Init/InitCommand.cs` |
| Create | `Services/IInitJournalService.cs` |
| Create | `Services/InitJournalService.cs` |
| Modify | `Exceptions/JournalExceptions.cs` — add `TocFileAlreadyExistsException` |
| Modify | `Program.cs` — register service + command |
| Create | `Tests/Commands/Init/InitCommandTests.cs` |
| Create | `Tests/Services/InitJournalServiceTests.cs` |

---

## Tests

### `InitCommandTests.cs`

- `Should_Initialize_Journal_With_Explicit_Name`
- `Should_Initialize_Journal_Using_Directory_Name_As_Default`
- `Should_Return_Error_When_Directory_Does_Not_Exist`
- `Should_Return_Error_When_Journal_Already_Initialized`
- `Should_Return_Error_When_Toc_File_Already_Exists`
- `Should_Pass_Custom_TOC_Name_To_Service`
- `Should_Pass_Null_TOC_Name_When_Not_Specified`
- `Should_Validate_Journal_Name_For_Invalid_Characters`
- `Should_Validate_Empty_Journal_Name`
- `Should_Initialize_With_Custom_Path`

### `InitJournalServiceTests.cs`

- `Initialize_ThrowsArgumentException_WhenDirectoryIsNull`
- `Initialize_ThrowsArgumentException_WhenJournalNameIsNull`
- `Initialize_DoesNotCreateDirectory`
- `Initialize_CreatesTocFile_WhenItDoesNotExist`
- `Initialize_ThrowsException_WhenTocFileAlreadyExists`
- `Initialize_UsesTocNameFromParameter_WhenProvided`
- `Initialize_UsesDefaultTocName_WhenParameterIsNull`
- `Initialize_CallsJournalConfigurationCreate`
- `Initialize_ConfigurationHasCorrectJournalName`
- `Initialize_ConfigurationTocFileReferenceMatchesResolvedTocName`
- `Initialize_CallsFileTrackingLoadIndex`
- `Initialize_CallsFileTrackingUpdateIndex`
- `Initialize_DoesNotCreateIntroductionFile`
- `Initialize_DoesNotCreateJournalEntryTemplateFile`
- `Initialize_DoesNotCreateAllMyJournalsFile`

---

## Open Questions (resolved)

1. **Named exception for TOC conflict:** Yes — `TocFileAlreadyExistsException` added.
2. **Config root entries:** TOC-only (no intro/template/all-journals entries since those files aren't created).
3. **TOC optionality:** Required now; designed for future `--no-toc` flag via nullable interface param + private helper decomposition.
4. **Tracking idempotency:** Tracking file existence is handled by the tracking infrastructure; service always calls LoadIndex + UpdateIndex.
