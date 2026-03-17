[Back to README](../README.md)

# Architecture Documentation

This document provides detailed technical information about the Markdown Journal CLI architecture, design decisions, and implementation details.

## 🏗️ System Architecture

### High-Level Overview
```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   CLI Interface │    │   Command Layer │    │ Infrastructure  │
│  (Spectre.CLI)  │───▶│   (Commands/)   │───▶│   (Services)    │
└─────────────────┘    └─────────────────┘    └─────────────────┘
                                               │
                    ┌─────────────────────────────────────────┐
                    │  Core Services Layer                    │
                    │  • IJournalConfiguration               │
                    │  • IJournalConfigGenerator            │
                    │  • ITableOfContentsGenerator          │
                    │  • ITableOfContentsMarkdownParser     │
                    │  • IFileTracking / IHashService       │
                    │  • IEntryFormatterService             │
                    │  • IFileSystem                        │
                    │  • IMarkdownLinkRewriter              │
                    │  • ITemplateManager                   │
                    └─────────────────────────────────────────┘
```

### Dependency Flow
```
Program.cs
    ├── TypeRegistrar (DI Setup)
    ├── CommandApp (Spectre.Console.Cli)
    └── Commands/
            ├── NewCommand
            ├── InitCommand
            └── [Future Commands]
                    └── Infrastructure/
                                    ├── IFileSystem (File operations)
                                    ├── IJournalInitializer (Journal creation)
                                    ├── IInitJournalService (Journal adoption)
                                    ├── ITemplateManager (Template generation)
                                    ├── IJournalConfiguration (Configuration management)
                                    └── Custom Exceptions
```

## 🔧 Dependency Injection Deep Dive

### The TypeRegistrar Pattern

**Problem Solved:**
Spectre.Console.Cli uses its own DI abstractions (`ITypeRegistrar`/`ITypeResolver`) to remain framework-agnostic, but we want to use Microsoft's powerful DI container.

**Solution:**
The `TypeRegistrar` acts as an adapter/bridge pattern implementation:

```csharp
// Spectre.Console.Cli Interface
public interface ITypeRegistrar
{
    void Register(Type service, Type implementation);
    ITypeResolver Build();
}

// Our Implementation (located in markdown_journal_cli.Infrastructure.DependencyInjection)
public sealed class TypeRegistrar : ITypeRegistrar
{
    private readonly IServiceCollection _services; // Microsoft DI
    
    public void Register(Type service, Type implementation)
    {
        _services.AddSingleton(service, implementation); // Translation
    }
}
```

**Translation Layer:**
| Spectre.Console.Cli | Microsoft.Extensions.DI |
|-------------------|-------------------------|
| `ITypeRegistrar` | `IServiceCollection` |
| `ITypeResolver` | `IServiceProvider` |
| `Register()` | `AddSingleton()` |
| `Resolve()` | `GetService()` |

### Registration Flow
1. **Startup** - `Program.cs` creates `TypeRegistrar`
2. **Registration** - Core services registered:
   - `IFileSystem` → `FileSystem` (File operations)
   - `ITemplateManager` → `TemplateManager` (Template processing)
   - `IJournalConfiguration` → `JournalConfiguration` (Config management)
   - `IJournalInitializer` → `JournalInitializer` (Journal creation orchestration)
   - `ITableOfContentsGenerator` → `TableOfContentsGenerator` (TOC generation)
   - `IFileTracking` → `FileTracking` (Change detection)
   - `IHashService` → `HashService` (SHA256 hashing)
   - `IEntryFormatterService` → `EntryFormatterService` (Entry name formatting)
   - `IJournalFileUpdateService` → `JournalFileUpdateService` (Entry rename/move/ignore)
   - `IMarkdownLinkRewriter` → `MarkdownLinkRewriter` (Inline link rewriting)
3. **Building** - `registrar.Build()` creates `IServiceProvider`
4. **Resolution** - Commands receive dependencies via constructor injection

### DI Registration (Program.cs)
```csharp
// Core services
host.Services.AddSingleton<IFileSystem, FileSystem>();
host.Services.AddSingleton<ITemplateManager, TemplateManager>();
host.Services.AddSingleton<IJournalConfiguration, JournalConfiguration>();
host.Services.AddSingleton<INewJournalService, NewJournalService>();
host.Services.AddSingleton<IInitJournalService, InitJournalService>();  // ← init command
host.Services.AddSingleton<IEntryFormatterService, EntryFormatterService>();
host.Services.AddSingleton<IHashService, HashService>();
host.Services.AddSingleton<IFileTracking, FileTracking>();
host.Services.AddSingleton<ITableOfContentsGenerator, TableOfContentsGenerator>();
host.Services.AddSingleton<IMarkdownLinkRewriter, MarkdownLinkRewriter>();

// Commands
host.Services.AddSingleton<NewCommand>();
host.Services.AddSingleton<InitCommand>();   // ← init command
host.Services.AddSingleton<AddEntry>();
host.Services.AddSingleton<AddJournalrc>();
host.Services.AddSingleton<AddTableOfContents>();
host.Services.AddSingleton<AddFileTracking>();
```

### Benefits of This Approach
- ✅ **Testability** - Easy to mock `IFileSystem` in tests
- ✅ **Flexibility** - Can swap implementations without changing commands
- ✅ **Separation of Concerns** - Commands focus on business logic
- ✅ **Future-Proof** - Easy to add new services (logging, config, etc.)

## 🚨 Exception Architecture

### Exception Hierarchy
```
System.Exception
    └── JournalException (Base for all journal errors)
            ├── JournalAlreadyExistsException
            ├── JournalrcNotFoundException
            ├── TocFileAlreadyExistsException  ← thrown when init target TOC filename already exists
            ├── TocRenameConflictException     ← thrown when --rename-toc target filename is already in use
            └── [other domain-specific exceptions]
```

## 📁 File System Abstraction

### Interface Design
```csharp
public interface IFileSystem
{
    bool DirectoryExists(string path);
    void CreateDirectory(string path);
    string CombinePaths(params string[] paths);
    void RenameFile(string oldPath, string newPath);
    string? GetFileName(string? path);
    string GetFullPath(string path);   // ← returns the absolute path for a relative input
    // ...
    /// <summary>
    /// Returns the relative paths of all markdown (.md) files found recursively
    /// under <paramref name="directory"/>, relative to that directory.
    /// Added to support IMarkdownLinkRewriter scanning without coupling to System.IO.
    /// </summary>
    IReadOnlyList<string> GetMarkdownFiles(string directory);
}
```

### Implementation Strategies

**Production Implementation:**
```csharp
public class FileSystem : IFileSystem
{
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);
    public string CombinePaths(params string[] paths) => Path.Combine(paths);
}
```

**Test Implementation:**
```csharp
public class TestFileSystem : IFileSystem
{
    public List<string> CreatedDirectories { get; } = new();
    
    public void CreateDirectory(string path) => CreatedDirectories.Add(path);
    // Mock other methods...
}
```

### Why Abstract the File System?
- ✅ **Unit Testing** - No actual files created during tests
- ✅ **Cross-Platform** - Abstraction handles OS differences
- ✅ **Security** - Can add validation/sandboxing later
- ✅ **Monitoring** - Can add logging/metrics without changing commands

## 🏗️ Service Architecture

### Core Services Overview

The application follows a service-oriented architecture with clear separation of concerns:

**`IInitJournalService`** - Orchestrates adoption of existing directories as journals
```csharp
public interface IInitJournalService
{
    void Initialize(string journalDirectory, string journalName, string? tableOfContentsName);
}
```
- Validates the directory exists and isn't already managed (done by `InitCommand` before calling)
- Creates tracking index, config, and TOC from existing files — no template files
- Accepts an optional custom TOC name; throws `TocFileAlreadyExistsException` on conflict
- Distinct from `IJournalInitializer` which creates a new directory with starter templates

**`IJournalInitializer`** - Orchestrates journal creation
```csharp
public interface IJournalInitializer
{
    void Initialize(string journalDirectory, string journalName);
}
```
- Coordinates file creation, templating, and configuration
- Encapsulates journal initialization business logic
- Makes NewCommand focus solely on CLI concerns

**`ITemplateManager`** - Handles template processing
```csharp
public interface ITemplateManager
{
    string GenerateFromTemplate(string templateName, Dictionary<string, object>? parameters);
    void RegisterTemplate(ITemplateGenerator template);
}
```
- Generates content from templates (table of contents, journal entries)
- Extensible template system for custom journal formats
- Parameters support for dynamic content generation

**`IJournalConfiguration`** - Manages journal configuration files
```csharp
public interface IJournalConfiguration
{
    void Create(string directory, JournalConfig config);
    JournalConfig? Read(string directory);
    void Update(string directory, Action<JournalConfig> config);
    void AddEntry(string directory, string name, string file, ...);
    bool RemoveEntry(string directory, string file);
    bool UpdateEntryName(string directory, string file, string newEntryName);
    void UpdateFileReferences(string directory, string oldFile, string newFile);
    (Entries? entry, string[] topicPath) FindEntry(string directory, string fileName);
}
```
- Handles all `.journalrc` CRUD operations
- Supports complex nested topic/subtopic hierarchy
- Provides entry find, rename, and file-reference update for rename workflows

**`IJournalFileUpdateService`** - Orchestrates entry update operations```csharp
public interface IJournalFileUpdateService
{
    void UpdateEntry(string directory, string currentFileName, ...);
    void RenameEntry(string directory, string oldFile, string newFile);
    void UpdateEntryLocation(string directory, string fileName, string[] newTopicPath, string displayName);
    void UpdateEntryDisplayName(string directory, string fileName, string newDisplayName);
    void SetIgnoreStatus(string directory, string fileName, bool ignored);
}
```
- Orchestrates renaming, relocation, title changes, and ignore-status toggling
- Updates all references: file system, tracking index, config, and TOC in a single operation

**`IMarkdownLinkRewriter`** - Reusable inline-link rewriting infrastructure
```csharp
public interface IMarkdownLinkRewriter
{
    string RewriteLinks(string content, string oldFileName, string newFileName);
    IReadOnlyList<string> FindFilesWithLinkTo(string directory, string fileName);
    IReadOnlyList<string> ReplaceLinksInDirectory(
        string directory, string oldFileName, string newFileName,
        IReadOnlyCollection<string>? excludeFiles = null);
}
```
- Stateless, reusable — designed to serve any future file-rename operation
- `RewriteLinks` is a pure string transformation (regex, no I/O)
- `ReplaceLinksInDirectory` is the preferred bulk API: scans, rewrites, and persists all changed files in one call, returning the list of modified relative paths
- Matches only inline links `[text](path/file.md)`; reference-style links are out of scope for this iteration
- Uses `RegexOptions.Compiled` — the pattern is JIT-compiled once and reused across every `.md` file in the journal

### Service Interaction Flow
```
NewCommand
    └── IJournalInitializer.Initialize()
            ├── IFileSystem.CreateDirectory()
            ├── ITemplateManager.GenerateFromTemplate() (4x)
            ├── IJournalConfiguration.Create()
            ├── ITableOfContentsGenerator.UpdateTableOfContents()
            └── IFileTracking.UpdateIndex()

InitCommand
    └── IInitJournalService.Initialize()
            ├── IFileSystem.FileExists()              (TOC conflict check)
            ├── IFileTracking.LoadIndex()             (load or create index)
            ├── IFileTracking.UpdateIndex()           (index all existing .md files)
            ├── IJournalConfigGenerator.GenerateFromTrackingIndex()  (write .journalrc)
            ├── ITableOfContentsService.UpdateTableOfContents()      (create TOC)
            └── IFileTracking.UpdateIndex()           (re-index to include newly created TOC)
            ✗ Does NOT create template files (unlike NewCommand)

AddEntry
    ├── IEntryFormatterService.FormatEntryName()
    ├── IFileSystem.CreateMarkdownFile()
    ├── ITemplateManager.GenerateFromTemplate()
    ├── IJournalConfiguration.AddEntry()
    ├── IFileTracking.UpdateFileInIndex()
    └── ITableOfContentsGenerator.UpdateTableOfContents()

UpdateJournal
    ├── IFileTracking.DetectChangesWithoutUpdate()
    ├── MarkdownMetadataParser.UpdateLastEditedDate()
    ├── IJournalConfiguration.AddEntry() / RemoveEntry()
    └── ITableOfContentsGenerator.UpdateTableOfContents()

UpdateEntry
    └── IJournalFileUpdateService.UpdateEntry()
            ├── IFileSystem.RenameFile()             (when renaming)
            ├── IFileTracking.RenameFileInIndex()    (when renaming)
            ├── IJournalConfiguration.UpdateFileReferences() (when renaming)
            ├── IJournalConfiguration.UpdateEntryLocation()  (when moving heading)
            ├── IJournalConfiguration.UpdateEntryName()      (when changing title)
            ├── IJournalConfiguration.AddIgnoreEntry() / RemoveEntry() (ignore toggle)
            └── ITableOfContentsGenerator.UpdateTableOfContents()

UpdateJournal --rename-toc
    └── IJournalUpdateService.RenameToc()
            ├── IJournalConfiguration.Read()           (get current TOC filename)
            ├── IFileSystem.FileExists()               (conflict check)
            ├── IFileSystem.RenameFile()               (rename on disk)
            ├── IJournalConfiguration.Update()         (update .journalrc)
            ├── IFileTracking.RenameFileInIndex()      (update tracking)
            ├── IMarkdownLinkRewriter.ReplaceLinksInDirectory()  (bulk rewrite)
            │       └── IFileSystem.GetMarkdownFiles() (enumerate .md files)
            │       └── IFileSystem.UpdateFile()       (persist each changed file)
            ├── MarkdownMetadataParser.UpdateLastEditedDate() (stamp modified files)
            └── IFileTracking.UpdateFileInIndex()      (per modified file)

AddJournalrc
    └── IJournalConfigGenerator.GenerateFromTableOfContents()
            └── ITableOfContentsMarkdownParser.ParseTableOfContents()
    └── IJournalConfigGenerator.GenerateFromTrackingIndex()
    └── IJournalConfigGenerator.GenerateFromDirectory()

AddTableOfContents
    ├── IJournalConfiguration.Read()
    ├── IJournalConfiguration.Update() (when TOC name differs)
    └── ITableOfContentsGenerator.UpdateTableOfContents()

AddFileTracking
    └── IFileTracking.UpdateIndex()
```

### Configuration Generation Strategy

When creating a `.journalrc` for an existing journal, the system attempts three sources in order, stopping at the first successful result:

1. **Table of contents file** - Uses `ITableOfContentsMarkdownParser` to extract entries and build config.
2. **Tracking index** - Uses the `.md-journal` index to infer known files.
3. **Directory scan** - Falls back to scanning the journal directory for markdown files.

This approach prioritizes the most user-curated source first (TOC), then known tracking data, and only scans the directory as a last resort.

### Benefits of Service Architecture
- ✅ **Single Responsibility** - Each service has one clear purpose
- ✅ **Testability** - Services can be tested in isolation
- ✅ **Maintainability** - Changes to one service don't affect others
- ✅ **Extensibility** - Easy to add new services or modify existing ones
- ✅ **Reusability** - Services can be used by multiple commands

## � Key Architectural Patterns

### Natural Sorting Algorithm

Implemented in `JournalConfiguration.cs` via the `NaturalStringComparer` class:

**Problem:** Lexicographic string sorting places "file_10" before "file_5" because it compares character-by-character ('1' < '5').

**Solution:** Custom `IComparer<string>` that treats consecutive digits as complete numbers:

```csharp
internal class NaturalStringComparer : IComparer<string>
{
    public int Compare(string? x, string? y)
    {
        // Parse and compare numeric segments as integers
        // Example: "file_5" < "file_10" < "file_100"
    }
    
    private static long ExtractNumber(string str, ref int index)
    {
        // Extracts consecutive digits as a long integer
    }
}
```

**Benefits:**
- Natural ordering matches file system behavior
- Works with any numeric values (handles leading zeros)
- Case-insensitive alphabetic comparison
- Used for both topic names and entry filenames

**Example Output:**
- Input: `["file_10", "file_5", "file_100", "file_1"]`
- Sorted: `["file_1", "file_5", "file_10", "file_100"]`

### Parent-Child Topic Detection

Implemented in `TableOfContentsGenerator.cs` for smart TOC rendering:

**Problem:** When a topic has an entry with matching name AND subtopics, should we render both or merge them?

**Solution:** Three-part detection algorithm:

1. **Name Matching**: Check if topic name equals entry name (case-insensitive)
2. **File Prefix Matching**: Verify all subtopic files start with entry file path
3. **Edge Case Handling**: Merge entry link into topic heading, render subtopics below

```csharp
// Edge case detection
if (visibleEntries.Length == 1 && 
    string.Equals(topic.Name, visibleEntries[0].Name, StringComparison.OrdinalIgnoreCase))
{
    // Render as: ## [Topic](topic.md)
    //            - Subtopic 1
    //            - Subtopic 2
}
```

**Example:**
```
Config:
  Topic: "abc"
  Entry: "abc.md"
  Subtopics: ["test 2"]

TOC Output:
  ## [Abc](abc.md)
    - Test 2
      - [test file 1](abc-test_2-test_file_1.md)
```

### Ignore Files Pattern

**Purpose:** Allow entries to exist in configuration but be excluded from TOC.

**Implementation:**
- `.journalrc` contains `ignoreFiles` array
- Files added with `--ignore-file` flag
- Filtered at TOC generation time
- Still tracked in file system and configuration

**Use Cases:**
- Draft entries not ready for publication
- Private notes
- Template files
- Work-in-progress documentation

**Example:**
```json
{
  "tableOfContents": {
    "ignoreFiles": ["draft.md", "private-notes.md"]
  }
}
```

### File Change Detection

**Architecture:**
```
IFileTracking
    └── IHashService (SHA256)
            └── .md-journal index file
```

**Process:**
1. **Index Creation**: Hash all markdown files on journal initialization
2. **Storage**: Save index to `.md-journal` JSON file
3. **Detection**: Compare current file hashes with stored hashes
4. **Results**: Return added/modified/deleted file lists

**Index Structure:**
```json
{
  "files": {
    "intro.md": "a3f2b8c...",
    "topic-entry.md": "d4e9c1a..."
  }
}
```

**Benefits:**
- Detects external file modifications
- No need for file system watchers
- Works across sessions
- Cryptographically secure (SHA256)

### Metadata Update Pattern

**Purpose:** Automatically maintain "Last Edited:" dates in markdown files when content changes.

**Implementation:**
- Located in `MarkdownMetadataParser.UpdateLastEditedDate()`
- Searches metadata header (first 6 non-empty lines before heading)
- Replaces existing "Last Edited:" line or inserts after "Created:" line
- Preserves file structure and existing metadata

**Metadata Header Format:**
```markdown
Created: 01/15/2025
Last Edited: 02/11/2026

# Entry Title
Content here...
```

**Update Algorithm:**
1. Split content into lines
2. Search metadata header (stops at first heading with `#`)
3. If "Last Edited:" line exists, replace it
4. If not found, insert after "Created:" line (or at top if no "Created:" line)
5. Format date according to journal settings

**Benefits:**
- Automatic change tracking
- Preserves existing metadata
- Configurable date format
- Works with manual file edits (detected via hash changes)

### TOC File Exclusion Pattern

**Problem:** The table of contents file can accidentally be added to `.journalrc` as an entry, causing it to appear in its own contents (circular reference).

**Multi-Layer Solution:**

**1. Prevention at Entry Time (`AddEntry`):**
```csharp
public void AddEntry(string directory, string name, string file, ...)
{
    // Check if file is TOC file - skip it
    var config = Read(directory);
    if (config != null && string.Equals(file, config.TableOfContents.File, ...))
    {
        return; // Never add TOC file as entry
    }
    // ... rest of add logic
}
```

**2. Auto-Cleanup on TOC Change (`JournalConfiguration.Update`):**
```csharp
public void Update(string directory, Action<JournalConfig> config)
{
    var oldTocFile = existingConfig.TableOfContents?.File;
    config(existingConfig); // Apply user changes
    var newTocFile = existingConfig.TableOfContents?.File;
    
    // If TOC file changed, remove new TOC file from entries
    if (newTocFile != oldTocFile)
    {
        RemoveEntryFromConfig(existingConfig, newTocFile);
    }
}
```

**3. Skip During Update Command (`UpdateCommand`):**
```csharp
private void UpdateJournalConfig(string journalPath, ChangeDetectionResult fileResults)
{
    var tocFile = config?.TableOfContents.File;
    
    foreach (var relativePath in fileResults.AddedFiles)
    {
        // Skip TOC file when processing added files
        if (string.Equals(relativePath, tocFile, ...)) continue;
        
        _journalConfiguration.AddEntry(...);
    }
}
```

**4. Filter During TOC Generation (`TableOfContentsGenerator`):**
```csharp
public string GenerateTableOfContents(JournalConfig config)
{
    var tocFile = config.TableOfContents.File;
    var ignoreFiles = config.TableOfContents.IgnoreFiles ?? [];
    
    // Auto-append TOC file to ignore list during rendering
    var ignoreFilesWithToc = ignoreFiles.Append(tocFile).ToArray();
    
    // Filter entries using expanded ignore list
    // ...
}
```

**Benefits:**
- **Defense in Depth**: Multiple layers prevent the issue
- **Auto-Recovery**: If TOC file somehow becomes an entry, it's automatically removed
- **User-Proof**: Works even if user manually edits configuration
- **No Breaking Changes**: Works with existing journals

**Edge Cases Handled:**
- User changes TOC filename → Old entry removed, new file excluded
- Manual config edit adds TOC → Update() cleans it up
- External file sync adds TOC → UpdateCommand skips it
- Direct AddEntry call with TOC → Rejected at entry point

## �🧪 Testing Architecture

### Test Structure
```
markdown-journal-cli.Tests/ (846 tests)
├── Commands/
│   ├── NewCommandTests.cs
│   ├── Init/
│   │   └── InitCommandTests.cs          ← new: init command integration tests
│   ├── Add/
│   │   ├── AddEntryCommandTests.cs
│   │   ├── AddJournalrcCommandTests.cs
│   │   ├── AddTableOfContentsCommandTests.cs
│   │   └── AddTableOfContentsIntegrationTests.cs
│   └── Update/
│       ├── UpdateCommandTests.cs        ← extended: --rename-toc dispatch tests added
│       └── UpdateEntryCommandTests.cs
├── Infrastructure/
│   ├── FileSystem/
│   │   ├── FileSystemTests.cs
│   │   ├── MarkdownLinkRewriterTests.cs  ← new: unit tests for inline-link rewriting
│   │   ├── MarkdownMetadataParserTests.cs
│   │   └── TestFileSystem.cs
│   ├── FileTrackingTests.cs
│   ├── HashServiceTests.cs
│   ├── JournalConfigurationTests.cs
│   ├── JournalConfigGeneratorTests.cs
│   ├── TableOfContentsMarkdownParserTests.cs
│   └── TypeRegistrarTests.cs
├── JournalTemplates/
│   ├── JournalInitializerTests.cs
│   ├── TableOfContentsGeneratorTests.cs
│   └── TemplateManagerTests.cs
└── Services/
    ├── EntryFormatter/
    │   └── EntryFormatterServiceTests.cs
    ├── InitJournal/
    │   └── InitJournalServiceTests.cs         ← new: unit tests for InitJournalService
    ├── JournalEntry/
    │   └── JournalEntryServiceTests.cs
    ├── JournalFileUpdate/
    │   └── JournalFileUpdateServiceTests.cs
    ├── JournalUpdate/
    │   └── JournalUpdateServiceTests.cs      ← extended: RenameToc test cases added
    ├── NewJournal/
    │   └── NewJournalServiceTests.cs
    └── TableOfContents/
        └── TableOfContentsServiceTests.cs
```

### Testing Strategy

**Command Testing Pattern:**
```csharp
public class NewCommandTests
{
    private readonly TestConsole _console;
    private readonly TestFileSystem _fileSystem;
    private readonly CommandAppTester _app;

    public NewCommandTests()
    {
        _console = new TestConsole();
        _fileSystem = new TestFileSystem();
        
        // Set up test DI container
        var registrar = new Tests.Infrastructure.TypeRegistrar()
            .RegisterInstance(_console)
            .RegisterInstance<IFileSystem>(_fileSystem);

        _app = new CommandAppTester(registrar);
    }
}
```

**Test Categories:**
1. **Happy Path Tests** - Valid inputs produce expected outputs
2. **Error Handling Tests** - Invalid inputs produce proper error messages
3. **Integration Tests** - Full command execution with mocked dependencies
4. **Validation Tests** - Command argument validation
5. **Edge Case Tests** - Parent-child detection, natural sorting, ignore files
6. **Change Detection Tests** - File tracking with hash comparison
7. **Format Tests** - Entry name formatting with various separators

## 🔮 Future Architecture Considerations

- **Async file operations** - For large journals with many files
- **Global configuration** - User-level defaults (default editor, date format, etc.)
- **Plugin/extension points** - Custom template generators and entry processors
- **`--check` flag** - Dry-run preview of changes before applying them

## 📋 Design Decisions Log

### Decision: Use Spectre.Console.Cli
**Rationale:** Rich terminal UI, excellent command parsing, built-in help generation  
**Alternatives:** System.CommandLine, custom argument parsing

### Decision: File System Abstraction
**Rationale:** Testability, cross-platform compatibility  
**Alternatives:** Direct file system calls

### Decision: Custom Exception Hierarchy
**Rationale:** Clear error categorization, better error handling  
**Alternatives:** Generic exceptions with error codes

### Decision: Natural Sorting for Entries
**Rationale:** Matches file system behavior and user expectations (`file_5` before `file_10`)  
**Alternatives:** Default lexicographic sorting

### Decision: SHA256 for File Hashing
**Rationale:** Collision-resistant, standard library support, appropriate for file integrity  
**Alternatives:** MD5 (deprecated), CRC32 (not secure)

### Decision: Multi-Layer TOC Exclusion
**Rationale:** Defense in depth prevents the TOC file from appearing in its own contents  
**Alternatives:** Single check at render time

### Decision: `IMarkdownLinkRewriter` as a Dedicated Infrastructure Service
**Rationale:** Link rewriting is a cross-cutting concern needed today for `--rename-toc` and tomorrow for `update entry --name`. Extracting it into a stateless interface keeps `JournalUpdateService` focused on orchestration and allows the rewriter to be tested in complete isolation with pure string inputs.  
**Alternatives:** Inline regex directly in `JournalUpdateService`; this would duplicate logic when entry rename is implemented

### Decision: `ReplaceLinksInDirectory` as the Preferred Bulk API
**Rationale:** Encapsulates the scan-rewrite-persist loop inside the infrastructure layer, keeping `JournalUpdateService.RenameToc` free of file-enumeration details. `FindFilesWithLinkTo` is retained for read-only queries.  
**Alternatives:** Let the service call `FindFilesWithLinkTo` + `RewriteLinks` + `UpdateFile` in a loop

### Decision: `RegexOptions.Compiled` in `MarkdownLinkRewriter`
**Rationale:** The same pattern is applied across every `.md` file in the journal directory in a single `ReplaceLinksInDirectory` call. `Compiled` JIT-compiles the regex once and amortizes that cost across all file reads, making it worthwhile even for modest journal sizes.  
**Alternatives:** `RegexOptions.None` (simpler, negligibly slower for small file counts)

### Decision: Automatic Last Edited Updates
**Rationale:** Reduces manual maintenance, leverages existing change detection  
**Alternatives:** Manual date updates, file system modification times

### Decision: `init` vs `new` — No Template Files
**Rationale:** `init` adopts a directory that already contains content. Creating intro/template files would pollute an existing collection and conflict with existing filenames. The command focuses purely on adding management metadata: `.journalrc`, a TOC, and a tracking index.  
**Alternatives:** Re-use `NewJournalService` and skip template creation via a flag — rejected because it couples two semantically distinct operations and makes the flag surface of `NewJournalService` grow for unrelated reasons.

### Decision: Double `UpdateIndex` call in `InitJournalService`
**Rationale:** The first `UpdateIndex` call indexes all pre-existing markdown files before the TOC is created. The second call runs after TOC creation so the new TOC file is also included in the index. This ensures every file the user would encounter in the journal is tracked from day one.  
**Alternatives:** Manually add the TOC path to the index — this would duplicate internal `FileTracking` logic and increase coupling.

### Decision: Constructor Injection over Property Injection
**Rationale:** Explicit dependencies, immutable after construction  
**Alternatives:** Property injection, service locator
