[Back to README](../README.md)

# Development Guide

This guide covers everything developers need to know to contribute to the Markdown Journal CLI project.

## 🚀 Getting Started

### Prerequisites
- .NET 10.0 SDK
- Git
- Your favorite C# IDE (VS Code, Visual Studio, Rider, etc.)

### First-Time Setup
```bash
git clone https://github.com/CollinRobison/markdown-journal-cli.git
cd markdown-journal-cli
dotnet restore
dotnet build
dotnet test
```

### Verify Installation
```bash
dotnet run --project markdown-journal-cli -- new TestJournal
# Should create a TestJournal directory
```

## 🏗️ Project Structure

```
markdown-journal-cli/
├── markdown-journal-cli/           # Main application
│   ├── Commands/                  # Command implementations
│   │   ├── Add/                   # Add commands (entry, config, toc)
│   │   │   ├── AddEntryCommand.cs
│   │   │   ├── AddFileTrackingCommand.cs
│   │   │   ├── AddJournalrcCommand.cs
│   │   │   ├── AddTableOfContentsCommand.cs
│   │   │   └── AddSettings.cs
│   │   ├── Init/                  # Init journal command
│   │   │   ├── InitCommand.cs
│   │   │   └── InitSettings.cs
│   │   ├── New/                   # New journal command
│   │   │   └── NewCommand.cs
│   │   ├── Remove/                # Remove journal entry command
│   │   │   ├── RemoveEntryCommand.cs
│   │   │   └── RemoveSettings.cs
│   │   ├── Update/                # Update journal/entry commands
│   │   │   ├── DryRunRenderer.cs       # Spectre.Console dry-run output renderer
│   │   │   ├── IDryRunRenderer.cs      # Renderer interface
│   │   │   ├── TextDiffer.cs           # LCS-based line-level diff (internal)
│   │   │   ├── UpdateCommand.cs
│   │   │   ├── UpdateEntryCommand.cs
│   │   │   └── UpdateSettings.cs
│   ├── Exceptions/                # Custom exceptions
│   │   └── JournalExceptions.cs
│   ├── Infrastructure/            # Core services
│   │   ├── Configuration/         # Journal configuration management
│   │   │   ├── IJournalConfiguration.cs
│   │   │   ├── JournalConfiguration.cs
│   │   │   ├── IJournalConfigGenerator.cs
│   │   │   ├── JournalConfigGenerator.cs
│   │   │   ├── ITableOfContentsMarkdownParser.cs
│   │   │   ├── TableOfContentsMarkdownParser.cs
│   │   │   └── Models/            # Configuration data models
│   │   ├── DependencyInjection/   # DI container setup
│   │   │   └── TypeRegistrar.cs
│   │   ├── FileSystem/           # File system abstraction
│   │   │   ├── IFileSystem.cs
│   │   │   ├── FileSystem.cs
│   │   │   ├── IInMemoryFileBuffer.cs  # In-memory staging (dry-run preview)
│   │   │   ├── InMemoryFileBuffer.cs   # Snapshot/Stage/Commit/Restore implementation
│   │   │   ├── IMarkdownLinkRewriter.cs   # Inline link rewriting interface
│   │   │   ├── MarkdownLinkRewriter.cs    # Compiled-regex link rewriter implementation
│   │   │   └── MarkdownMetadataParser.cs
│   │   ├── Transactions/         # File transaction and rollback infrastructure
│   │   │   ├── IFileTransactionCoordinator.cs   # Ambient scope factory
│   │   │   ├── FileTransactionCoordinator.cs    # Thread-local ambient scope implementation
│   │   │   ├── IFileTransactionScope.cs         # Track/Commit/Rollback contract
│   │   │   ├── FileTransactionScope.cs          # Execute-then-compensate implementation
│   │   │   ├── JoinedTransactionScope.cs        # Inner scope that delegates to root
│   │   │   ├── IDeletionRollbackStrategy.cs     # Snapshot/restore for deleted files
│   │   │   ├── InMemoryDeletionRollbackStrategy.cs
│   │   │   ├── IRollbackReporter.cs             # Console output for rollback events
│   │   │   ├── RollbackReporter.cs              # Spectre.Console rollback summary
│   │   │   ├── RollbackReporterExtensions.cs    # Extension helpers
│   │   │   ├── NoOpTransactionInfrastructure.cs # No-op impls for tests/dry-run
│   │   │   ├── RollbackCompletedException.cs    # Thrown after rollback; carries RollbackResult
│   │   │   └── Models/                          # RollbackEntry, RollbackEntryKind, RollbackResult, RollbackFailure
│   │   └── Tracking/             # File change detection
│   │       ├── IFileTracking.cs
│   │       ├── FileTracking.cs
│   │       ├── IHashService.cs
│   │       ├── HashService.cs
│   │       └── Models/
│   │           └── UpdateDryRunReport.cs  # Dry-run report aggregate + TocDiffResult + TocRenameDryRunResult
│   ├── JournalTemplates/          # Template and initialization services
│   │   ├── Templates/            # Template implementations
│   │   ├── IJournalInitializer.cs # Journal creation orchestration
│   │   ├── JournalInitializer.cs
│   │   ├── ITemplateManager.cs   # Template processing
│   │   ├── TemplateManager.cs
│   │   ├── ITableOfContentsGenerator.cs
│   │   └── TableOfContentsGenerator.cs
│   ├── Services/                  # Business logic services (each pair in its own subfolder)
│   │   ├── EntryFormatter/
│   │   │   ├── IEntryFormatterService.cs
│   │   │   └── EntryFormatterService.cs
│   │   ├── InitJournal/
│   │   │   ├── IInitJournalService.cs      # Journal adoption orchestration
│   │   │   └── InitJournalService.cs
│   │   ├── JournalEntry/
│   │   │   ├── IJournalEntryService.cs
│   │   │   └── JournalEntryService.cs
│   │   ├── JournalFileUpdate/
│   │   │   ├── IJournalFileUpdateService.cs
│   │   │   └── JournalFileUpdateService.cs
│   │   ├── JournalUpdate/
│   │   │   ├── IJournalUpdateService.cs    # + BuildDryRunReport method
│   │   │   └── JournalUpdateService.cs     # + BuildDryRunReport, RenameToc; IMarkdownLinkRewriter injected
│   │   ├── NewJournal/
│   │   │   ├── INewJournalService.cs
│   │   │   └── NewJournalService.cs
│   │   ├── RemoveEntry/
│   │   │   ├── IRemoveEntryService.cs      # Remove entry orchestration
│   │   │   └── RemoveEntryService.cs
│   │   └── TableOfContents/
│   │       ├── ITableOfContentsService.cs  # + PreviewTableOfContents overloads (no disk write)
│   │       └── TableOfContentsService.cs
│   ├── appsettings.json          # Application configuration
│   ├── JournalSettings.cs        # Settings model
│   └── Program.cs                # Entry point
├── markdown-journal-cli.Tests/    # Unit tests
│   ├── Commands/                 # Command tests
│   │   ├── NewCommandTests.cs
│   │   ├── Init/
│   │   │   └── InitCommandTests.cs
│   │   ├── Add/
│   │   │   ├── AddEntryCommandTests.cs
│   │   │   ├── AddFileTrackingCommandTests.cs
│   │   │   ├── AddJournalrcCommandTests.cs
│   │   │   ├── AddTableOfContentsCommandTests.cs
│   │   │   ├── AddTableOfContentsIntegrationTests.cs
│   │   │   ├── AddFileTrackingRollbackTests.cs    # rollback: fault-inject each write step
│   │   │   ├── AddJournalrcRollbackTests.cs
│   │   │   └── AddTableOfContentsRollbackTests.cs
│   │   ├── Remove/
│   │   │   └── RemoveEntryCommandTests.cs     # remove entry command tests
│   │   └── Update/
│   │       ├── UpdateCommandTests.cs          # + --rename-toc and --dry-run dispatch tests
│   │       └── UpdateEntryCommandTests.cs
│   ├── Infrastructure/           # Infrastructure service tests
│   │   ├── FileSystem/
│   │   │   ├── FileSystemTests.cs
│   │   │   ├── FaultInjectingFileSystem.cs    # test helper: fault injection for IFileSystem
│   │   │   ├── InMemoryFileBufferTests.cs     # new: Snapshot/Stage/Commit/Restore tests
│   │   │   ├── MarkdownLinkRewriterTests.cs   # extended: StripLinksInDirectory tests added
│   │   │   ├── MarkdownMetadataParserTests.cs
│   │   │   └── TestFileSystem.cs
│   │   ├── Transactions/
│   │   │   ├── FileTransactionScopeTests.cs       # Track*/Commit/Rollback + reverse-order tests
│   │   │   ├── FileTransactionCoordinatorTests.cs  # Begin/BeginOrJoin ambient scope tests
│   │   │   ├── JoinedTransactionScopeTests.cs      # joined scope delegation tests
│   │   │   ├── RollbackReporterTests.cs            # console output tests
│   │   │   └── TransactionEdgeCaseTests.cs         # idempotency, disposed scope, etc.
│   │   ├── FileTrackingTests.cs
│   │   ├── HashServiceTests.cs
│   │   ├── JournalConfigurationTests.cs
│   │   ├── JournalConfigGeneratorTests.cs
│   │   ├── TableOfContentsMarkdownParserTests.cs
│   │   └── TypeRegistrarTests.cs
│   ├── JournalTemplates/         # Template and initialization tests
│   │   ├── JournalInitializerTests.cs
│   │   ├── TableOfContentsGeneratorTests.cs
│   │   └── TemplateManagerTests.cs
│   └── Services/
│       ├── EntryFormatter/
│       │   └── EntryFormatterServiceTests.cs
│       ├── InitJournal/
│       │   └── InitJournalServiceTests.cs
│       ├── JournalEntry/
│       │   └── JournalEntryServiceTests.cs
│       ├── JournalFileUpdate/
│       │   └── JournalFileUpdateServiceTests.cs
│       ├── JournalUpdate/
│       │   └── JournalUpdateServiceTests.cs   # + RenameToc + BuildDryRunReport test cases
│       ├── NewJournal/
│       │   └── NewJournalServiceTests.cs
│       ├── RemoveEntry/
│       │   └── RemoveEntryServiceTests.cs     # remove entry service tests
│       ├── Rollback/
│       │   ├── ServiceRollbackTestBase.cs               # shared helpers for rollback tests
│       │   ├── InitJournalServiceRollbackTests.cs
│       │   ├── JournalEntryServiceRollbackTests.cs
│       │   ├── JournalFileUpdateServiceRollbackTests.cs
│       │   ├── JournalUpdateServiceRollbackTests.cs
│       │   ├── NewJournalServiceRollbackTests.cs
│       │   └── RemoveEntryServiceRollbackTests.cs
│       └── TableOfContents/
│           └── TableOfContentsServiceTests.cs  # extended: PreviewTableOfContents tests added
├── docs/                         # Documentation
└── README.md                     # Main documentation
```

## 🛠️ Development Workflow

### 1. Creating New Commands

#### Step 1: Create Command Class
```csharp
using System.ComponentModel;
using Spectre.Console.Cli;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.JournalTemplates;
using markdown_journal_cli.Infrastructure.Configuration;

namespace markdown_journal_cli.Commands.YourCommand;

[Description("TODO: Add your command description")]
public sealed class YourCommand : JournalCommand<YourCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly IFileSystem _fileSystem;
    private readonly IJournalInitializer _journalInitializer; // Example service injection

    public YourCommand(
        IAnsiConsole console, 
        IFileSystem fileSystem,
        IJournalInitializer journalInitializer)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _journalInitializer = journalInitializer ?? throw new ArgumentNullException(nameof(journalInitializer));
    }

    public sealed class Settings : CommandSettings
    {
        // TODO: Define your command arguments and options
        [CommandArgument(0, "[argument]")]
        [Description("TODO: Describe your argument")]
        public string? YourArgument { get; set; }

        public override ValidationResult Validate()
        {
            // TODO: Add validation logic
            return ValidationResult.Success();
        }
    }

    protected override int ExecuteCore(CommandContext context, Settings settings)
    {
        try
        {
            // TODO: Implement your command logic
            _console.MarkupLine("[green]Success:[/] Command completed");
            return 0;
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }
}
```

#### Step 2: Register Command in Program.cs
```csharp
config.AddCommand<YourCommand>("your-command");
```

#### Step 3: Create Tests
```csharp
// TODO: Create YourCommandTests.cs in markdown-journal-cli.Tests/Commands/
```

### 2. Adding New Services

#### Step 1: Define Interface
```csharp
namespace markdown_journal_cli.Infrastructure.FileSystem;

public interface IYourService
{
    // TODO: Define your service contract
    Task<string> DoSomethingAsync(string input);
}
```

#### Step 2: Implement Service
```csharp
namespace markdown_journal_cli.Infrastructure.Services;

public class YourService : IYourService
{
    // TODO: Implement your service
    public async Task<string> DoSomethingAsync(string input)
    {
        // Implementation here
        return await Task.FromResult(input);
    }
}
```

#### Step 3: Register in DI Container
```csharp
// In Program.cs
registrar.Register(typeof(IYourService), typeof(YourService));
```

### 3. Error Handling

#### Adding New Exception Types
```csharp
namespace markdown_journal_cli.Exceptions;

public class YourSpecificException : JournalException
{
    public string AdditionalProperty { get; }

    public YourSpecificException(string message, string additionalInfo)
        : base($"TODO: Format your error message: {message}")
    {
        AdditionalProperty = additionalInfo;
    }
}
```

## 🧪 Testing Guidelines

### Test Naming Conventions
```csharp
[Fact]
public void Should_DoExpectedThing_When_ConditionMet()
{
    // Arrange - Set up test data
    // Act - Execute the code under test
    // Assert - Verify the results
}
```

### Mock Services in Tests
```csharp
public class YourCommandTests
{
    private readonly TestConsole _console;
    private readonly TestFileSystem _fileSystem;
    private readonly TestJournalInitializer _journalInitializer;
    private readonly CommandAppTester _app;

    public YourCommandTests()
    {
        _console = new TestConsole();
        _fileSystem = new TestFileSystem(); 
        _journalInitializer = new TestJournalInitializer();
        
        var registrar = new markdown_journal_cli.Tests.Infrastructure.TypeRegistrar()
            .RegisterInstance(_console)
            .RegisterInstance<IFileSystem>(_fileSystem)
            .RegisterInstance<IJournalInitializer>(_journalInitializer);

        _app = new CommandAppTester(registrar);
        _app.Configure(config =>
        {
            config.AddCommand<YourCommand>("your-command");
            config.PropagateExceptions();
        });
    }
}
```

## 🎯 Code Standards

### General Guidelines
- Follow standard C# naming conventions
- Use `sealed` classes where appropriate
- Enable nullable reference types
- Keep methods focused and testable

### Documentation Strategy

**For CLI projects, focus documentation efforts where they provide the most value:**

#### ✅ **DO Document:**
- **Public interfaces** and their contracts (what the abstraction provides)
- **Complex business logic** that isn't self-explanatory
- **Custom exception types** and when they're thrown
- **Any code you might extract into a library later**
- **Non-obvious design decisions** (use inline comments)

#### ❌ **DON'T Document:**
- Simple wrapper methods or obvious operations
- Private implementation details
- Framework plumbing (commands, DI setup, etc.)
- Getters/setters for simple properties

#### Example - Good Documentation:
```csharp
/// <summary>
/// Provides file system abstraction for testability and cross-platform support.
/// </summary>
public interface IFileSystem
{
    /// <summary>
    /// Creates directory structure, including parent directories if needed.
    /// </summary>
    void CreateDirectory(string path);
}

// Complex business logic deserves explanation
public JournalEntry ParseEntry(string markdown)
{
    // Parse frontmatter first - supports both YAML and TOML
    // TODO: Add support for JSON frontmatter
    var frontmatterEnd = markdown.IndexOf("---", 3);
    // ...
}
```

#### Example - Skip Documentation:
```csharp
// These are obvious and don't need XML docs
public string JournalName { get; set; }
public bool DirectoryExists(string path) => Directory.Exists(path);

private void InternalHelperMethod() { } // Private - no docs needed
```

### Documentation Priorities for CLI Projects

**Most Valuable Documentation (in order of importance):**

1. **User-facing documentation** (README, usage examples, command help)
2. **Architecture decisions** (why certain patterns were chosen)
3. **Development setup** (how to contribute, build, test)
4. **Complex business logic** (inline comments for tricky algorithms)
5. **Public interfaces** (XML docs for contracts that might be reused)

**Remember:** For CLI tools, good user documentation and clear code structure are far more valuable than extensive API documentation.

### Specific Patterns

#### Command Classes
```csharp
[Description("Clear, concise description")]
public sealed class CommandName : Command<CommandName.Settings>
{
    // Constructor injection only
    public CommandName(IDependency dependency) { }
    
    // Nested Settings class
    public sealed class Settings : CommandSettings
    {
        // Always include validation
        public override ValidationResult Validate() { }
    }
    
    // Return meaningful exit codes
    public override int Execute(CommandContext context, Settings settings)
    {
        try { return 0; }
        catch (SpecificException ex) { return 1; }
        catch (Exception ex) { return 1; }
    }
}
```

#### Service Classes
```csharp
public interface IService
{
    // Use async for I/O operations
    Task<Result> DoWorkAsync(Parameters params);
}

public class Service : IService
{
    // Validate inputs
    public async Task<Result> DoWorkAsync(Parameters params)
    {
        if (params == null) throw new ArgumentNullException(nameof(params));
        // Implementation
    }
}
```

## 🔍 Debugging Tips

### Common Issues

**Issue: DI Container Can't Resolve Service**
```
System.InvalidOperationException: Unable to resolve service for type 'IYourService'
```
**Solution:** Make sure service is registered in `Program.cs`

**Issue: Command Not Found**
```
Unknown command 'your-command'
```
**Solution:** Verify command is added in `Program.cs` configuration

**Issue: Tests Failing with DI Issues**
```
TypeRegistrar ambiguous reference error
```
**Solution:** Use fully qualified name: `markdown_journal_cli.Tests.Infrastructure.TypeRegistrar`

### Debugging Commands
```bash
# Run with detailed output
dotnet run --project markdown-journal-cli -- your-command --help

# Debug specific test
dotnet test --filter "YourTestMethod"

# Run in debug configuration
dotnet run --configuration Debug --project markdown-journal-cli -- your-command
```

## 📦 Release Process

### TODO: Document Release Process
- [ ] Version numbering strategy
- [ ] Changelog maintenance
- [ ] NuGet package creation
- [ ] Global tool publishing
- [ ] GitHub releases
- [ ] Documentation updates

### Current Build Targets
```bash
# Debug build (development)
dotnet build --configuration Debug

# Release build (production)
dotnet build --configuration Release

# Run tests
dotnet test

# TODO: Pack as global tool
# dotnet pack --configuration Release
# dotnet tool install -g --add-source ./nupkg markdown-journal-cli
```

## 📋 TODO: Areas Needing Documentation

The following areas need detailed documentation (you should write these based on your vision for the project):

### Project Vision & Goals
- [ ] **Project Mission Statement** - What problem does this solve?
- [ ] **Target Users** - Who is this for?
- [ ] **Use Cases** - What scenarios should this handle?
- [ ] **Success Metrics** - How do we measure success?

### Feature Specifications
- [ ] **Journal Structure** - What does a journal look like on disk?
- [ ] **Entry Format** - What markdown format/template for entries?
- [ ] **Metadata Handling** - How do we store journal metadata?
- [ ] **Search Requirements** - What search capabilities are needed?

### User Experience Design
- [ ] **CLI UX Principles** - What makes a good CLI experience?
- [ ] **Error Message Guidelines** - How should errors be presented?
- [ ] **Help Text Standards** - What information should help include?
- [ ] **Progress Indication** - When/how to show progress?

### Technical Decisions
- [ ] **File Organization Strategy** - How should journals be structured?
- [ ] **Configuration Approach** - How should users configure the tool?
- [ ] **Plugin Architecture** - Should we support plugins? How?
- [ ] **Performance Goals** - What are acceptable performance limits?

### Security & Privacy
- [ ] **Data Privacy Policy** - How do we handle user data?
- [ ] **Security Considerations** - What security measures are needed?
- [ ] **Encryption Strategy** - Should we support encrypted journals?

### Operational Concerns
- [ ] **Logging Strategy** - What should we log? Where?
- [ ] **Error Reporting** - How do we handle crash reports?
- [ ] **Telemetry** - What usage data (if any) should we collect?
- [ ] **Backup Strategy** - How do we help users backup journals?

**IJournalConfiguration Pattern**
- **Purpose**: Manages journal configuration CRUD operations and topic hierarchy
- **Benefits**: Centralized configuration management, supports complex nested structures
- **Features**: Natural sorting, ignore files, parent-child topic detection, entry removal
- **Example**: `AddEntry` uses this to update `.journalrc` with new entry metadata
- **TOC Protection**: Auto-removes TOC file if accidentally added as entry (multi-layer defense)

**ITableOfContentsGenerator Pattern**
- **Purpose**: Generates markdown table of contents from journal configuration
- **Benefits**: Automated TOC updates, smart parent-child detection, ignore file support
- **Features**: Natural alphanumeric sorting, nested topic rendering, date preservation, TOC self-exclusion
- **Example**: Automatically updates TOC when new entries are added
- **Protection**: Automatically excludes TOC file from being listed in itself

**IFileTracking Pattern**
- **Purpose**: Tracks file changes using SHA256 hashing for change detection
- **Benefits**: Detects added, modified, and deleted files without manual intervention
- **Features**: Index persistence (`.md-journal` file), hash-based comparison
- **Example**: Used to sync journal state and detect external file modifications

**IHashService Pattern**
- **Purpose**: Computes SHA256 hashes for file content comparison
- **Benefits**: Reliable change detection, cryptographically secure
- **Example**: Used by `IFileTracking` to determine if file content has changed

**IEntryFormatterService Pattern**
- **Purpose**: Formats entry names with configurable separators
- **Benefits**: Consistent file naming, handles heading/subheading hierarchy
- **Features**: Space separator conversion, heading separator management
- **Example**: Converts "My Entry" to "My_Entry" or parses "Tech-Backend-API"

**MarkdownMetadataParser Pattern**
- **Purpose**: Updates markdown file metadata (Created/Last Edited dates)
- **Benefits**: Automatic change tracking, preserves file structure
- **Features**: Searches metadata header (first 6 lines), inserts after "Created:" line
- **Example**: Used by `UpdateCommand` to update "Last Edited:" dates for modified files

### Service Registration (Program.cs)
```csharp
// Core services
host.Services.AddSingleton<IFileSystem, FileSystem>();
host.Services.AddSingleton<IInMemoryFileBuffer, InMemoryFileBuffer>();  // ← dry-run staging

// Rollback infrastructure
host.Services.AddSingleton<IDeletionRollbackStrategy, InMemoryDeletionRollbackStrategy>();
host.Services.AddSingleton<IFileTransactionCoordinator, FileTransactionCoordinator>();
host.Services.AddSingleton<IRollbackReporter, RollbackReporter>();

host.Services.AddSingleton<ITemplateManager, TemplateManager>();
host.Services.AddSingleton<IJournalConfiguration, JournalConfiguration>();
host.Services.AddSingleton<INewJournalService, NewJournalService>();
host.Services.AddSingleton<IInitJournalService, InitJournalService>();  // ← init command
host.Services.AddSingleton<IEntryFormatterService, EntryFormatterService>();
host.Services.AddSingleton<IHashService, HashService>(); 
host.Services.AddSingleton<IFileTracking, FileTracking>();
host.Services.AddSingleton<ITableOfContentsService, TableOfContentsService>();
host.Services.AddSingleton<ITableOfContentsGenerator, TableOfContentsGenerator>();
host.Services.AddSingleton<ITableOfContentsMarkdownParser, TableOfContentsMarkdownParser>();
host.Services.AddSingleton<IJournalConfigGenerator, JournalConfigGenerator>();
host.Services.AddSingleton<IJournalUpdateService, JournalUpdateService>();
host.Services.AddSingleton<IMarkdownLinkRewriter, MarkdownLinkRewriter>();
host.Services.AddSingleton<IRemoveEntryService, RemoveEntryService>();  // ← remove command
host.Services.AddSingleton<IDryRunRenderer, DryRunRenderer>();          // ← dry-run rendering

// Commands
host.Services.AddSingleton<NewCommand>();
host.Services.AddSingleton<InitCommand>();   // ← init command
host.Services.AddSingleton<AddEntry>();
host.Services.AddSingleton<AddJournalrc>();
host.Services.AddSingleton<AddTableOfContents>();
host.Services.AddSingleton<AddFileTracking>();
host.Services.AddSingleton<UpdateCommand>();
host.Services.AddSingleton<UpdateEntryCommand>();
host.Services.AddSingleton<RemoveEntryCommand>();  // ← remove command
```

### Key Architectural Patterns

**Natural Sorting Algorithm**
Implemented in `JournalConfiguration.cs` via `NaturalStringComparer`:
- Treats consecutive digits as numbers for proper ordering
- Example: `file_1, file_5, file_10, file_100` (not `file_1, file_10, file_100, file_5`)
- Used for both topic names and entry filenames

**Parent-Child Detection**
Implemented in `TableOfContentsGenerator.cs`:
- Detects when a topic has an entry with matching name AND subtopics
- Merges entry link into topic heading: `## [Topic](topic.md)` with subtopics listed below
- Uses file path prefix matching to determine parent-child relationships
- Example: `abc.md` is parent of `abc-test_2-test_file_1.md`

**Ignore Files Pattern**
- Files in `.journalrc` `ignoreFiles` array are excluded from TOC
- Still tracked in file system and configuration
- Useful for draft entries or non-public content

**IMarkdownLinkRewriter Pattern**
- **Purpose**: Stateless infrastructure service for finding and rewriting inline markdown links `[text](file.md)` across a journal directory
- **Benefits**: Reusable across future rename operations (e.g. `update entry --name`); keeps service orchestrators free of file-enumeration and regex details
- **Features**: Pure string `RewriteLinks`, read-only `FindFilesWithLinkTo`, bulk `ReplaceLinksInDirectory` that scans/rewrites/persists in one call; `RegexOptions.Compiled` for multi-file performance
- **Scope**: Inline links only — reference-style links (`[text][ref]`) are out of scope for this iteration
- **Example**: Used by `JournalUpdateService.RenameToc` to rewrite all references to the old TOC filename; also used by `BuildDryRunReport` (via `FindFilesWithLinkTo`) to list affected backlink files in the rename-toc dry-run preview

**IInMemoryFileBuffer Pattern**
- **Purpose**: In-memory file staging and snapshot service; two use cases: (1) stage generated content for preview/diff without disk I/O (dry-run), (2) snapshot-before-write for transactional rollback (future)
- **Benefits**: Separates content generation from disk I/O; enables testable dry-run logic; lays groundwork for rollback semantics
- **Registered as**: singleton — keys are case-insensitive absolute paths; call `Clear()` between operations if reused
- **Example**: `JournalUpdateService.BuildDryRunReport` calls `ITableOfContentsService.PreviewTableOfContents` which uses the buffer internally to stage the generated TOC string

**IDryRunRenderer Pattern**
- **Purpose**: Renders an `UpdateDryRunReport` to the terminal using Spectre.Console — no file writes
- **Benefits**: Keeps rendering concerns out of `UpdateCommand` and `JournalUpdateService`; independently testable
- **Features**: Color-coded tables for tracking (✚/~/✖), config (will add/remove), TOC diff (LCS panel), rename-toc preview with backlink list; each section rendered only when non-null and has changes
- **Example**: `UpdateCommand.ExecuteDryRun` calls `_dryRunRenderer.Render(report, journalPath)` after building the report

## 🏗️ Service Architecture Patterns

### Current Service Implementations

**IJournalInitializer Pattern**
- **Purpose**: Orchestrates complex journal creation process
- **Benefits**: Commands stay focused on CLI concerns, business logic is testable
- **Example**: `NewCommand` delegates all initialization to `IJournalInitializer`

**ITemplateManager Pattern**  
- **Purpose**: Handles template processing and content generation
- **Benefits**: Extensible template system, parameterized content generation
- **Example**: Generates table of contents, intro, and journal entry templates

**IJournalConfiguration Pattern**
- **Purpose**: Manages `.journalrc` configuration files
- **Benefits**: Centralized config management, supports complex configuration objects
- **Example**: Creates and manages journal metadata and settings

**IJournalConfigGenerator Pattern**
- **Purpose**: Generates `.journalrc` configuration from existing journal sources
- **Benefits**: Enables retroactive config creation, supports multiple generation strategies
- **Features**: TOC parsing, tracking index parsing, directory scanning fallback
- **Example**: `AddJournalrc` uses priority-based generation (TOC → tracking → directory)

**ITableOfContentsMarkdownParser Pattern**
- **Purpose**: Parses markdown TOC files to extract entry structure
- **Benefits**: Converts human-readable TOC into structured configuration data
- **Features**: Handles nested topics, preserves hierarchy, extracts file links
- **Example**: Used by `IJournalConfigGenerator` to build config from existing TOC

### Testing Service Dependencies

**Integration Testing with Real Services:**
```csharp
[Fact]
public void JournalInitializer_Should_Create_Complete_Journal()
{
    // Uses real implementations for integration testing
    var fileSystem = new TestFileSystem();
    var templateManager = new TemplateManager();
    var configuration = new TestJournalConfiguration();
    var initializer = new JournalInitializer(fileSystem, templateManager, configuration);
    
    initializer.Initialize("/test/journal", "TestJournal");
    
    // Verify complete journal structure was created
    Assert.True(fileSystem.DirectoryExists("/test/journal"));
    Assert.Contains("TestJournal", configuration.CreatedConfigurations.Values.First().JournalName);
}
```

**Unit Testing with Mocked Dependencies:**
```csharp
[Fact] 
public void NewCommand_Should_Handle_InitializationFailure()
{
    // Mock initializer to throw exception
    var mockInitializer = new TestJournalInitializer();
    mockInitializer.ShouldThrow = true;
    mockInitializer.ExceptionToThrow = new InvalidOperationException("Test error");
    
    var command = new NewCommand(_console, _fileSystem, mockInitializer);
    var result = command.Execute(context, settings);
    
    Assert.Equal(1, result);
    Assert.Contains("Test error", _console.Output);
}
```

### Service Design Guidelines

1. **Single Responsibility**: Each service should have one clear purpose
2. **Interface Segregation**: Keep interfaces focused and minimal  
3. **Dependency Inversion**: Depend on abstractions, not concretions
4. **Immutable After Construction**: Services should be configured via constructor injection
5. **Async-Ready**: Design interfaces to support future async operations

## 🤝 Contribution Guidelines

### TODO: Define Contribution Process
- [ ] Issue templates
- [ ] Pull request templates  
- [ ] Code review process
- [ ] Contributor onboarding
- [ ] Code of conduct
- [ ] Recognition/attribution

### Current Status
- ✅ Basic project structure established
- ✅ Core `new` command implemented
- ✅ **`init` command** — adopt an existing markdown directory as a journal (creates `.journalrc`, TOC, and tracking index; no template files)
- ✅ `add` command branch with entry, config, toc, and tracking subcommands
- ✅ `update journal` command for journal synchronization (config, dates, TOC)
- ✅ `update entry` command for renaming, relocating, and ignoring entries
- ✅ `--no-backlinks` flag on `update entry` — backlink rewriting on rename enabled by default; opt-out via `--nb|--no-backlinks`
- ✅ `--rename-toc` flag on `update journal` — rename TOC file, update `.journalrc`, rewrite all link references
- ✅ `--dry-run|--check` flag on `update journal` — preview all pending changes without any writes; color-coded Spectre.Console tables for tracking, config, TOC diff, and rename-toc preview; scoped by the same flags as the live path (`--tracking`, `--config`, `--toc`, `--rename-toc`)
- ✅ **`remove entry` command** — delete an entry file, remove config/tracking records, regenerate TOC; `--clean-refs` strips dead inline links across the journal; `rm` alias supported
- ✅ `IMarkdownLinkRewriter` infrastructure service — reusable inline-link rewriting and link stripping
- ✅ Exception handling architecture
- ✅ Testing framework setup (941 tests passing)
- ✅ Configuration system with generation from multiple sources
- ✅ TOC markdown parser for config generation
- ✅ File change detection with SHA256 hashing
- ✅ Automatic metadata date updates
- ✅ Multi-layer TOC self-reference prevention
- ✅ **Rollback system** — `IFileTransactionScope` / `IFileTransactionCoordinator` provide ambient execute-then-compensate transactions for all write commands; `IRollbackReporter` renders rollback summaries; `JournalCommand<TSettings>` maps `RollbackCompletedException` to exit codes 2/3
- ⏳ Additional commands (list, open, search, rename)
- ⏳ Documentation completion


### Rollback Infrastructure (`Infrastructure/Transactions/`)

All write commands participate in a file transaction that can be rolled back if any step fails mid-operation.

**Usage pattern in services:**
```csharp
using var tx = _coordinator.BeginOrJoin();
try
{
    tx.Track(fileA);          // snapshot before write
    WriteFileA(...);

    tx.TrackNew(fileB);       // record new file before create
    CreateFileB(...);

    tx.TrackDelete(fileC);    // snapshot before delete
    DeleteFileC(...);

    tx.Commit();
}
catch (Exception ex)
{
    _reporter.ReportRollbackStarting("update journal", ex);
    var result = tx.Rollback();
    _reporter.ReportRollbackComplete(result, journalPath);
    throw new RollbackCompletedException(result, ex);
}
```

**Key types:**
- `IFileTransactionCoordinator` — singleton factory; `Begin()` creates a root scope; `BeginOrJoin()` joins an existing ambient scope or creates a new one; thread-local ambient scope
- `IFileTransactionScope` — tracks write operations (`Track`, `TrackNew`, `TrackRename`, `TrackDelete`, `TrackNewDirectory`); `Commit()` finalizes; `Rollback()` reverses in reverse-registration order; auto-rolls back on `Dispose()` if not committed
- `JoinedTransactionScope` — returned by `BeginOrJoin()` when a scope already exists; delegates to the root; `Commit()` is a local no-op (only root commit finalizes)
- `IDeletionRollbackStrategy` / `InMemoryDeletionRollbackStrategy` — captures file content before `TrackDelete`; restores on rollback
- `IRollbackReporter` / `RollbackReporter` — prints Spectre.Console rollback summary table to the terminal
- `RollbackCompletedException` — thrown after rollback; carries `RollbackResult` (restored + failed entries); caught by `JournalCommand<TSettings>` and mapped to exit codes 2/3
- `NoOpFileTransactionCoordinator`, `NoOpFileTransactionScope`, `NoOpRollbackReporter` — no-op implementations (static `Instance` singleton) for tests and dry-run contexts

**Testing:**
Inject `FaultInjectingFileSystem` (in `markdown-journal-cli.Tests/Infrastructure/FileSystem/`) to trigger failures at specific write steps and assert all prior writes were reversed. Use `ServiceRollbackTestBase` for shared setup across service rollback tests.
