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
                    │  • ITableOfContentsGenerator          │
                    │  • IFileTracking / IHashService       │
                    │  • IEntryFormatterService             │
                    │  • IFileSystem                        │
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
            └── [Future Commands]
                    └── Infrastructure/
                                    ├── IFileSystem (File operations)
                                    ├── IJournalInitializer (Journal creation)
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
   - `IJournalInitializer` → `JournalInitializer` (Journal creation orchestration)   - `ITableOfContentsGenerator` → `TableOfContentsGenerator` (TOC generation)
   - `IFileTracking` → `FileTracking` (Change detection)
   - `IHashService` → `HashService` (SHA256 hashing)
   - `IEntryFormatterService` → `EntryFormatterService` (Entry name formatting)3. **Building** - `registrar.Build()` creates `IServiceProvider`
4. **Resolution** - Commands receive dependencies via constructor injection

### Benefits of This Approach
- ✅ **Testability** - Easy to mock `IFileSystem` in tests
- ✅ **Flexibility** - Can swap implementations without changing commands
- ✅ **Separation of Concerns** - Commands focus on business logic
- ✅ **Future-Proof** - Easy to add new services (logging, config, etc.)

### Alternative Approaches Considered

**1. No DI (Direct Instantiation)**
```csharp
public NewCommand() 
{
    _fileSystem = new FileSystem(); // Tightly coupled
}
```
❌ Hard to test, not flexible

**2. Service Locator Pattern**
```csharp
public NewCommand() 
{
    _fileSystem = ServiceLocator.Get<IFileSystem>();
}
```
❌ Hidden dependencies, anti-pattern

**3. Manual Factory Pattern**
```csharp
public static class CommandFactory 
{
    public static NewCommand CreateNewCommand() => new(new FileSystem());
}
```
❌ Boilerplate, doesn't scale

## 🚨 Exception Architecture

### Exception Hierarchy
```
System.Exception
    └── JournalException (Base for all journal errors)
            └── JournalAlreadyExistsException (Specific error type)
            └── [Future: JournalNotFoundException]
            └── [Future: InvalidJournalFormatException]
```

### Constructor Chaining Explanation
```csharp
public class JournalException : Exception
{
    // This constructor calls Exception(string message)
    public JournalException(string message) : base(message) { }
    
    // This constructor calls Exception(string message, Exception innerException)
    public JournalException(string message, Exception inner) : base(message, inner) { }
}
```

**Why `: base()` is Required:**
- Constructors are **NOT inherited** in C#
- Must manually "expose" parent constructors you want to use
- `: base()` calls parent constructor **before** your constructor body
- Without it, `Exception` properties (Message, StackTrace, etc.) wouldn't be initialized

### Exception Handling Strategy
```csharp
try 
{
    // Command logic
}
catch (JournalAlreadyExistsException ex) // Most specific first
{
    _console.MarkupLine($"[red]Error:[/] {ex.Message}");
    return 1; // Specific exit code
}
catch (JournalException ex) // General journal errors
{
    _console.MarkupLine($"[red]Error:[/] {ex.Message}");
    return 1;
}
catch (Exception ex) // Unexpected errors
{
    _console.MarkupLine($"[red]Error:[/] Unexpected error: {ex.Message}");
    return 1;
}
```

## 📁 File System Abstraction

### Interface Design
```csharp
public interface IFileSystem
{
    bool DirectoryExists(string path);
    void CreateDirectory(string path);
    string CombinePaths(params string[] paths);
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
    JournalConfig Read(string directory);
    void Update(string directory, Action<JournalConfig> config);
}
```
- Handles `.journalrc` file operations
- Supports complex journal configuration objects
- Enables journal metadata and settings management

### Service Interaction Flow
```
NewCommand
    └── IJournalInitializer.Initialize()
            ├── IFileSystem.CreateDirectory()
            ├── ITemplateManager.GenerateFromTemplate() (4x)
            ├── IJournalConfiguration.Create()
            ├── ITableOfContentsGenerator.UpdateTableOfContents()
            └── IFileTracking.UpdateIndex()

AddEntry
    ├── IEntryFormatterService.FormatEntryName()
    ├── IFileSystem.CreateMarkdownFile()
    ├── ITemplateManager.GenerateFromTemplate()
    ├── IJournalConfiguration.AddEntry()
    ├── IFileTracking.UpdateFileInIndex()
    └── ITableOfContentsGenerator.UpdateTableOfContents()
```

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

## �🧪 Testing Architecture

### Test Structure
```
markdown-journal-cli.Tests/ (509 tests)
├── Commands/
│   ├── NewCommandTests.cs          # New journal command tests
│   └── Add/
│       ├── AddEntryCommandTests.cs     # Entry creation tests
│       ├── AddJournalrcCommandTests.cs # Config creation tests
│       └── AddTableOfContentsCommandTests.cs
├── Infrastructure/
│   ├── FileSystemTests.cs          # File operations
│   ├── FileTrackingTests.cs        # Change detection
│   ├── HashServiceTests.cs         # SHA256 hashing
│   ├── JournalConfigurationTests.cs # Config CRUD, natural sort
│   ├── MarkdownMetadataParserTests.cs
│   ├── TestFileSystem.cs           # Mock file system
│   └── TypeRegistrarTests.cs       # DI container
├── JournalTemplates/
│   ├── JournalInitializerTests.cs  # Journal creation
│   ├── TableOfContentsGeneratorTests.cs # TOC generation, parent-child
│   └── TemplateManagerTests.cs     # Template processing
└── Services/
    └── EntryFormatterServiceTests.cs # Entry name formatting
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

### Planned Enhancements

**1. Configuration System**
```csharp
public interface IConfiguration
{
    string DefaultJournalPath { get; }
    string DefaultEditor { get; }
    JournalSettings GetJournalSettings(string name);
}
```

**2. Plugin Architecture**
```csharp
public interface IJournalPlugin
{
    string Name { get; }
    void ProcessEntry(JournalEntry entry);
}
```

**3. Template System**
```csharp
public interface ITemplateEngine
{
    string RenderTemplate(string templateName, object data);
}
```

**4. Async Operations**
```csharp
public interface IFileSystemAsync
{
    Task<bool> DirectoryExistsAsync(string path);
    Task CreateDirectoryAsync(string path);
}
```

### Scalability Considerations
- **Command Organization** - May need command groups/categories as features grow
- **Shared Services** - Logging, configuration, metrics services
- **Performance** - Async operations for large journal operations
- **Extensibility** - Plugin system for custom journal formats/processors

## 📋 Design Decisions Log

### Decision: Use Spectre.Console.Cli
**Rationale:** Rich terminal UI, excellent command parsing, built-in help generation
**Alternatives:** System.CommandLine, custom argument parsing
**Trade-offs:** Additional dependency, learning curve

### Decision: File System Abstraction
**Rationale:** Testability, cross-platform compatibility
**Alternatives:** Direct file system calls
**Trade-offs:** Additional complexity, slight performance overhead

### Decision: Custom Exception Hierarchy
**Rationale:** Clear error categorization, better error handling
**Alternatives:** Using generic exceptions with error codes
**Trade-offs:** More classes to maintain, but much clearer error handling

### Decision: Natural Sorting for Entries
**Rationale:** Matches file system behavior, user expectations
**Alternatives:** Lexicographic sorting (default string comparison)
**Trade-offs:** Custom comparer implementation (~50 lines), but much better UX

### Decision: Parent-Child Topic Detection
**Rationale:** Cleaner TOC when topic name matches single entry
**Alternatives:** Always render entries separately from topic headings
**Trade-offs:** More complex rendering logic, but eliminates redundant entries

### Decision: SHA256 for File Hashing
**Rationale:** Cryptographically secure, collision-resistant, standard library support
**Alternatives:** MD5 (faster but deprecated), CRC32 (not secure)
**Trade-offs:** Slightly slower than MD5, but appropriate for file integrity

### Decision: Ignore Files in Configuration
**Rationale:** Flexible control over TOC without deleting files
**Alternatives:** Separate ignore file like .gitignore, file naming conventions
**Trade-offs:** Centralized in .journalrc, easier to manage but less discoverable

### Decision: Constructor Injection over Property Injection
**Rationale:** Explicit dependencies, immutable after construction
**Alternatives:** Property injection, service locator
**Trade-offs:** More verbose constructors, but much clearer dependencies

## 🤔 Architectural Questions for Future Discussion

- Should we implement a repository pattern for journal storage?
- How should we handle journal metadata (creation date, tags, etc.)?
- Should we support multiple journal formats (Markdown, Org-mode, etc.)?
- How should we handle journal encryption/security?
- Should we implement a journal indexing/search system?
- How should we handle journal templates and customization?
