# CLI Architecture Patterns & Best Practices

## Q1: Should I put logic in commands or abstract to functions/classes?

### Answer: Move complex business logic to service classes

Your commands currently follow different patterns:

**Pattern 1: Direct Logic in Commands**
- `UpdateCommand` has lots of logic directly in `Execute()`
- Private methods like `UpdateJournalConfig()`, `UpdateFileTracking()`, etc.

**Pattern 2: Delegated to Services**
- `NewJournalCommand` delegates to services
- Uses `_journalInitializer.Initialize()`, `_tableOfContentsGenerator.UpdateTableOfContents()`

### ✅ Recommended: Service Abstraction

**Benefits:**
1. **Testability** - Services are easier to unit test in isolation
2. **Reusability** - Logic can be shared across multiple commands
3. **Single Responsibility** - Commands focus on CLI concerns (parsing, validation, output)
4. **Maintainability** - Business logic changes don't require touching command classes

### Example Refactoring

```csharp
// Create a service interface
public interface IJournalUpdateService
{
    UpdateResult UpdateJournal(string journalPath, UpdateOptions options);
}

public class UpdateOptions
{
    public bool UpdateConfig { get; set; } = true;
    public bool UpdateTracking { get; set; } = true;
    public bool UpdateToc { get; set; } = true;
}

public class UpdateResult
{
    public int FilesUpdated { get; set; }
    public bool ConfigChanged { get; set; }
    public bool TocUpdated { get; set; }
    public List<string> Errors { get; set; } = new();
}
```

```csharp
// Command becomes thin orchestrator
public sealed class UpdateCommand : Command<UpdateJournalSettings>
{
    private readonly IJournalUpdateService _updateService;
    private readonly IAnsiConsole _console;

    public override int Execute(CommandContext context, UpdateJournalSettings settings)
    {
        var options = new UpdateOptions
        {
            UpdateConfig = settings.UpdateConfig,
            UpdateTracking = settings.UpdateDates,
            UpdateToc = settings.UpdateToc
        };

        var result = _updateService.UpdateJournal(settings.FilePath, options);

        // Just handle presentation
        if (result.FilesUpdated > 0)
            _console.MarkupLine($"[green]Updated dates for {result.FilesUpdated} file(s).[/]");
        
        if (result.ConfigChanged)
            _console.MarkupLine("[yellow]Configuration updated.[/]");

        return result.Errors.Any() ? 1 : 0;
    }
}
```

### What Stays in Commands vs Services

**Commands handle:**
- CLI-specific logic: Argument parsing, validation, user prompts
- Presentation logic: Console output formatting, progress bars
- Error handling: Converting exceptions to user-friendly messages
- Flow control: Deciding which services to call based on flags

**Services handle:**
- Business logic: File processing, change detection, metadata updates
- Data manipulation: Config updates, TOC generation
- File I/O operations: Reading/writing files (via IFileSystem)
- Coordination: Orchestrating multiple operations

---

## Q2: What design patterns do people usually follow for CLIs like this?

### Common CLI Design Patterns

#### 1. **Command Pattern** ✅ (Currently using this)
Each command is a separate class that encapsulates a single action.

**Your implementation:**
- `AddEntryCommand`, `NewJournalCommand`, `UpdateCommand`

**Best practice:** Keep commands thin - they should orchestrate, not implement business logic.

---

#### 2. **Service Layer Pattern** ⚠️ (Partially implemented)
Business logic lives in reusable service classes.

**What you have:**
```csharp
ITemplateManager, IFileTracking, ITableOfContentsGenerator // ✅ Good
```

**What's missing:**
- Commands like `AddEntryCommand` have too much logic inline
- Should have: `IJournalEntryService`

---

#### 3. **Repository Pattern** ✅ (Currently using this)
Abstract data access behind interfaces.

**Your implementation:**
```csharp
IJournalConfiguration // Handles .journalrc CRUD
IFileSystem          // Abstracts file operations
IFileTracking        // Manages tracking index
```

---

#### 4. **Dependency Injection** ✅ (Currently using this)
Dependencies injected via constructor.

**Your implementation:**
```csharp
public sealed class AddEntry(
    IAnsiConsole console,
    IFileSystem fileSystem,
    // ... 8 dependencies
)
```

**⚠️ Code smell:** 8+ dependencies suggests the command is doing too much.

---

#### 5. **Factory Pattern** ⚠️ (Could use this)
For creating complex objects.

**Where you could use it:**
```csharp
// Instead of manual string manipulation in commands
IFileNameFactory.CreateEntryFileName(heading, subheading, name)
IEntryFactory.CreateEntry(settings)
```

---

#### 6. **Strategy Pattern** ❌ (Not using, but could help)
Different algorithms for the same operation.

**Where it could help:**
```csharp
// Different update strategies
IUpdateStrategy
  - FullUpdateStrategy
  - ConfigOnlyUpdateStrategy  
  - TocOnlyUpdateStrategy
```

---

#### 7. **Chain of Responsibility** ❌ (Not using, but could help)
Pipeline of validation/processing steps.

**Where it could help:**
```csharp
// Validation pipeline
IJournalValidator
  - JournalExistsValidator
  - TrackingFileExistsValidator
  - FileNotExistsValidator
```

---

### Recommended Architecture

```
┌─────────────────────────────────────────────────────┐
│                  CLI Layer                          │
│  (Commands - thin orchestration only)               │
└─────────────────┬───────────────────────────────────┘
                  │
┌─────────────────▼───────────────────────────────────┐
│              Service Layer                          │
│  (Business logic & orchestration)                   │
│                                                     │
│  IJournalEntryService                              │
│  IJournalUpdateService                             │
│  IJournalInitializationService                     │
└─────────────────┬───────────────────────────────────┘
                  │
┌─────────────────▼───────────────────────────────────┐
│           Infrastructure Layer                      │
│  (Data access & external concerns)                  │
│                                                     │
│  IJournalConfiguration                             │
│  IFileTracking                                     │
│  IFileSystem                                       │
│  ITableOfContentsGenerator                         │
└─────────────────────────────────────────────────────┘
```

---

### Refactored AddEntryCommand Example

#### Service Interface

```csharp
// filepath: markdown-journal-cli/Services/IJournalEntryService.cs
public interface IJournalEntryService
{
    Task<EntryCreationResult> CreateEntryAsync(CreateEntryRequest request);
}

public record CreateEntryRequest(
    string JournalPath,
    string EntryName,
    string? EntryTitle = null,
    string? Heading = null,
    string? Subheading = null,
    bool IgnoreFile = false
);

public record EntryCreationResult(
    bool Success,
    string? FilePath = null,
    string? ErrorMessage = null
);
```

#### Thin Command

```csharp
// filepath: markdown-journal-cli/Commands/Add/AddEntryCommand.cs
public sealed class AddEntry(
    IAnsiConsole console,
    IJournalEntryService entryService  // ✅ Single service dependency
) : Command<AddEntrySettings>
{
    public override int Execute(CommandContext context, AddEntrySettings settings)
    {
        try
        {
            var request = new CreateEntryRequest(
                settings.FilePath,
                settings.EntryName,
                settings.EntryTitle,
                settings.Heading,
                settings.Subheading,
                settings.IgnoreFile
            );

            var result = _entryService.CreateEntryAsync(request).Result;

            if (!result.Success)
            {
                _console.MarkupLine($"[red]Error:[/] {result.ErrorMessage}");
                return 1;
            }

            _console.MarkupLine($"[green]Created:[/] {result.FilePath}");
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

#### Service Implementation

```csharp
// filepath: markdown-journal-cli/Services/JournalEntryService.cs
public class JournalEntryService : IJournalEntryService
{
    private readonly IFileSystem _fileSystem;
    private readonly ITemplateManager _templateManager;
    private readonly IEntryFormatterService _entryFormatter;
    private readonly IJournalConfiguration _journalConfiguration;
    private readonly IFileTracking _fileTracking;
    private readonly ITableOfContentsGenerator _tableOfContentsGenerator;
    private readonly IJournalValidator _validator;

    public async Task<EntryCreationResult> CreateEntryAsync(CreateEntryRequest request)
    {
        // 1. Validate
        var validationResult = await _validator.ValidateJournalAsync(request.JournalPath);
        if (!validationResult.IsValid)
            return new EntryCreationResult(false, ErrorMessage: validationResult.Error);

        // 2. Format names
        var entryTitle = _entryFormatter.RemoveSpaceSeparators(
            request.EntryTitle ?? request.EntryName
        );
        
        var fileName = BuildFileName(request);
        var filePath = Path.Combine(request.JournalPath, fileName);

        // 3. Check existence
        if (_fileSystem.FileExists(filePath))
            return new EntryCreationResult(false, ErrorMessage: $"Entry already exists: {fileName}");

        // 4. Create file
        await CreateEntryFileAsync(filePath, entryTitle);

        // 5. Update metadata
        await UpdateJournalMetadataAsync(request, fileName, entryTitle);

        return new EntryCreationResult(true, FilePath: filePath);
    }

    private string BuildFileName(CreateEntryRequest request)
    {
        var entryNameFormatted = _entryFormatter.AddSpaceSeparators(request.EntryName);
        var headingFormatted = request.Heading != null
            ? _entryFormatter.AddSpaceSeparators(request.Heading)
            : null;

        var fileName = new[] { headingFormatted, request.Subheading, entryNameFormatted }
            .Where(x => x != null)
            .Cast<string>()
            .ToArray();

        return $"{_entryFormatter.AddHeadingSeparators(fileName)}.md";
    }

    private async Task CreateEntryFileAsync(string filePath, string title)
    {
        var entryParams = new Dictionary<string, object>
        {
            ["title"] = title,
            ["addSourceBlock"] = true
        };

        var content = _templateManager.GenerateFromTemplate("journal-entry", entryParams);
        await _fileSystem.CreateMarkdownFileAsync(filePath, content);
    }

    private async Task UpdateJournalMetadataAsync(
        CreateEntryRequest request,
        string fileName,
        string entryTitle)
    {
        // Build headings array
        string[] headings = (request.Heading != null
                ? [_entryFormatter.RemoveSpaceSeparators(request.Heading)]
                : Array.Empty<string>())
            .Concat(request.Subheading != null
                ? _entryFormatter.SeperateSubheadingString(request.Subheading)
                : [])
            .Where(h => !string.IsNullOrEmpty(h))
            .ToArray();

        // Update config
        _journalConfiguration.AddEntry(
            request.JournalPath,
            entryTitle,
            fileName,
            headings.Length > 0 ? headings : null,
            ignoreFile: request.IgnoreFile
        );

        // Update tracking
        await _fileTracking.UpdateFileInIndexAsync(request.JournalPath, fileName);

        // Update TOC
        if (!request.IgnoreFile)
        {
            await _tableOfContentsGenerator.UpdateTableOfContentsAsync(
                request.JournalPath,
                lastEditedDate: DateTime.Now
            );
        }
    }
}
```

---

### Key Improvements

1. **Separation of Concerns**
   - Commands: UI/CLI concerns only
   - Services: Business logic
   - Infrastructure: Data access

2. **Testability**
   - Easy to unit test `JournalEntryService` without CLI
   - Easy to test commands with mocked services

3. **Single Responsibility**
   - Each class has one reason to change
   - Command changes for CLI updates
   - Service changes for business logic updates

4. **Dependency Management**
   - Commands depend on 1-3 services
   - Services depend on infrastructure
   - Clear dependency hierarchy

5. **Reusability**
   - Services can be used by multiple commands
   - Services can be used in future GUI/API

---

### Industry Examples

This architecture matches patterns used by enterprise CLIs:
- `dotnet` CLI
- `git` CLI
- `kubectl` CLI
- Azure CLI
- AWS CLI

All follow similar patterns: thin commands + rich service layer + abstracted infrastructure.

---

## Q3: Where should services live and does "Infrastructure" make sense as a folder name?

### Analysis of Current Project Structure

**Infrastructure/** ✅ **Name is appropriate**

Current contents:
- `Configuration/` - Repository pattern for .journalrc persistence (IJournalConfiguration)
- `FileSystem/` - Abstraction over System.IO (IFileSystem)
- `Tracking/` - Repository pattern for tracking index (IFileTracking)
- `DependencyInjection/` - DI configuration

**Purpose**: Data access and external system abstractions (file system, configuration persistence, hash services)

**Verdict**: ✅ Correctly named. This IS infrastructure-layer code according to Clean Architecture and Domain-Driven Design principles.

---

**JournalTemplates/** ⚠️ **Misleading name, mixed concerns**

Current contents contain TWO distinct types:

**Type 1: Infrastructure (Template Management)**
- `ITemplateManager` - Template rendering infrastructure
- `ITemplateGenerator` - Template generation engine  
- `JournalEntryTemplate`, `TableOfContentsTemplate` - Template implementations

**Type 2: Services (Business Logic)**
- `IJournalInitializer` - Orchestrates journal creation workflow (business logic)
- `ITableOfContentsGenerator` - Business logic for TOC generation

**Problem**: "JournalTemplates" implies template infrastructure only, but half the folder contains domain services that orchestrate business workflows.

---

**Services/** ⚠️ **Severely underutilized**

Current contents:
- Only `IEntryFormatterService` - String formatting utility

**Problem**: Should contain ALL business logic orchestration services, but currently has only one utility service.

---

### Architectural Assessment by Layer

Following **Clean Architecture** and **Hexagonal Architecture** principles:

```
┌────────────────────────────────────────────────────────┐
│  Commands/ (Presentation Layer)                        │
│  - CLI argument parsing                                │
│  - User interaction (prompts, output formatting)       │
│  - Exception → User message translation                │
└──────────────────┬─────────────────────────────────────┘
                   │ depends on
┌──────────────────▼─────────────────────────────────────┐
│  Services/ (Application/Domain Services Layer)         │
│  - Business logic orchestration                        │
│  - Use case implementations                            │
│  - Cross-cutting concerns coordination                 │
└──────────────────┬─────────────────────────────────────┘
                   │ depends on
┌──────────────────▼─────────────────────────────────────┐
│  Infrastructure/ (Infrastructure Layer)                │
│  - Data persistence (repositories)                     │
│  - External system abstractions (file system)          │
│  - Technical utilities (hashing, templates)            │
└────────────────────────────────────────────────────────┘
```

**Key Principle**: Commands should NEVER directly depend on Infrastructure. They should go through Services.

---

### Current Violations & Code Smells

**Violation 1: Commands bypass service layer**

Current `AddEntryCommand` constructor:
```csharp
public sealed class AddEntry(
    IAnsiConsole console,           // ✅ Presentation
    IFileSystem fileSystem,          // ❌ Infrastructure - shouldn't be here
    ITemplateManager templateManager, // ❌ Infrastructure - shouldn't be here
    IEntryFormatterService entryFormatter,
    IJournalConfiguration journalConfiguration, // ❌ Infrastructure - shouldn't be here
    IFileTracking fileTracking,      // ❌ Infrastructure - shouldn't be here
    ITableOfContentsGenerator tableOfContentsGenerator,
    IOptions<JournalSettings> journalSettings
) : Command<AddEntrySettings>
```

**Problem**: 8 dependencies, 4 of which are infrastructure. Command is doing too much.

**Should be**:
```csharp
public sealed class AddEntry(
    IAnsiConsole console,
    IJournalEntryService entryService  // ✅ Single service dependency
) : Command<AddEntrySettings>
```

---

**Violation 2: Business logic lives in JournalTemplates/ instead of Services/**

`JournalInitializer` and `TableOfContentsGenerator` are NOT template infrastructure - they're business services that:
- Orchestrate multi-step workflows
- Coordinate multiple infrastructure components
- Implement business rules

These belong in `Services/`, not `JournalTemplates/`.

---

### Recommended Folder Structure

#### Option 1: Clean Separation (Recommended)

```
markdown-journal-cli/
├── Commands/              # CLI orchestration only
├── Services/              # ← All business logic lives here
│   ├── EntryFormatterService.cs
│   ├── IEntryFormatterService.cs
│   ├── JournalEntryService.cs           # ← New: orchestrates entry creation
│   ├── IJournalEntryService.cs
│   ├── JournalInitializerService.cs     # ← Moved & renamed from JournalTemplates/
│   ├── IJournalInitializerService.cs
│   ├── JournalUpdateService.cs          # ← New: orchestrates updates
│   ├── IJournalUpdateService.cs
│   ├── TableOfContentsService.cs        # ← Moved & renamed from JournalTemplates/
│   └── ITableOfContentsService.cs
├── Infrastructure/        # Data access & external system abstractions
│   ├── Configuration/     # .journalrc repository pattern
│   ├── FileSystem/        # File I/O abstraction
│   ├── Tracking/          # File tracking repository
│   └── Templates/         # ← Template infrastructure moved here
│       ├── ITemplateManager.cs
│       ├── ITemplateGenerator.cs
│       ├── TemplateManager.cs
│       ├── JournalEntryTemplate.cs
│       └── TableOfContentsTemplate.cs
└── Exceptions/
```

**Rationale:**
- **Infrastructure** = "How do we persist/retrieve data and interact with external systems?"
  - File system operations, persistence, templates, hashing
- **Services** = "What business operations can we perform?"
  - Initialize journal, create entry, update TOC, validate journal
- **Commands** = "How do users invoke operations via CLI?"
  - Argument parsing, validation, presentation, error formatting

---

#### Option 2: Domain-Driven Design Structure (Advanced)

If you want more explicit domain modeling in the future:

```
markdown-journal-cli/
├── Application/           # Application layer
│   ├── Commands/          # CLI commands
│   └── Services/          # Application services (use cases)
├── Domain/                # Domain layer (business logic)
│   ├── Services/
│   │   ├── JournalService.cs
│   │   ├── EntryService.cs
│   │   └── TableOfContentsService.cs
│   └── Models/            # Rich domain models (if needed)
├── Infrastructure/        # Infrastructure layer
│   ├── Configuration/
│   ├── FileSystem/
│   ├── Tracking/
│   └── Templates/
└── Exceptions/
```

**Note**: This is more complex and potentially over-engineered for a CLI tool. Stick with Option 1 unless you anticipate significant domain complexity.

---

### Specific Issues with Current Naming

**Issue 1: "TableOfContentsGenerator" is ambiguous**

What does it generate?
- The TOC markdown content? (Infrastructure concern - template generation)
- The TOC business logic? (Service concern - orchestration)

**Current reality**: It does BOTH, which violates Single Responsibility Principle.

**Solution**: Split into two classes:
```csharp
// Infrastructure/Templates/TableOfContentsTemplate.cs
public class TableOfContentsTemplate : ITemplateGenerator
{
    // Renders TOC markdown from data
}

// Services/TableOfContentsService.cs
public class TableOfContentsService : ITableOfContentsService
{
    // Orchestrates TOC generation workflow
    // - Reads config
    // - Builds structure
    // - Calls template
    // - Writes file
}
```

---

**Issue 2: "JournalInitializer" sounds like infrastructure**

The suffix "-Initializer" suggests low-level setup, but it's actually a high-level orchestration service.

**Better names**:
- `JournalInitializerService` (signals it's a service)
- `JournalCreationService` (describes what it does)
- `NewJournalService` (mirrors the command name)

---

### Migration Path

**Phase 1: Create new services (non-breaking)**
1. Create `IJournalEntryService` and implementation
2. Create `IJournalUpdateService` and implementation
3. Create `IJournalInitializerService` (wrapper around existing JournalInitializer)
4. Register in DI container

**Phase 2: Update commands to use new services**
1. Refactor `AddEntryCommand` to use `IJournalEntryService`
2. Refactor `UpdateCommand` to use `IJournalUpdateService`
3. Update tests for commands

**Phase 3: Move infrastructure (breaking changes)**
1. Move template infrastructure from `JournalTemplates/` to `Infrastructure/Templates/`
2. Move business services from `JournalTemplates/` to `Services/`
3. Delete empty `JournalTemplates/` folder
4. Update namespaces across codebase
5. Update all tests

**Phase 4: Clean up (optional)**
1. Consider splitting `TableOfContentsGenerator` into service + template
2. Rename services for consistency (`*Service` suffix)

---

### Dependency Direction Rules

**✅ Allowed**:
```
Commands → Services
Services → Infrastructure
Services → Services (with caution)
Infrastructure → Infrastructure (with caution)
```

**❌ Forbidden**:
```
Commands → Infrastructure (must go through Services)
Infrastructure → Services (dependency inversion violation)
Infrastructure → Commands (architectural violation)
Services → Commands (architectural violation)
```

**Example of current violation**:
```csharp
// ❌ Command directly uses infrastructure
AddEntryCommand → IFileSystem
AddEntryCommand → IJournalConfiguration

// ✅ Should be
AddEntryCommand → IJournalEntryService → IFileSystem
AddEntryCommand → IJournalEntryService → IJournalConfiguration
```

---

### Benefits of Proper Layering

1. **Testability**
   - Mock services, not 8 infrastructure dependencies
   - Test business logic without CLI framework
   - Easier test setup and maintenance

2. **Reusability**
   - Services can be used by future GUI
   - Services can be used by future API
   - Business logic independent of CLI framework

3. **Maintainability**
   - Clear responsibility boundaries
   - Easy to find where logic lives
   - Changes isolated to single layer

4. **Flexibility**
   - Swap infrastructure implementations (e.g., different file system)
   - Change CLI framework without touching business logic
   - Add new commands easily by composing existing services

---

### Conclusion

**"Infrastructure" is the correct name** for the folder. The problems in your architecture are:

1. ❌ **JournalTemplates/** mixes infrastructure (templates) with services (business logic)
2. ❌ **Services/** is severely underutilized
3. ❌ **Commands** bypass the service layer and directly depend on infrastructure
4. ❌ **Naming** doesn't clearly signal layer boundaries (e.g., "Generator" vs "Service")

**Immediate action items**:
1. Move `JournalInitializer` → `Services/JournalInitializerService.cs`
2. Move `TableOfContentsGenerator` → `Services/TableOfContentsService.cs`
3. Move template infrastructure → `Infrastructure/Templates/`
4. Create new service interfaces: `IJournalEntryService`, `IJournalUpdateService`
5. Refactor commands to use service layer exclusively
6. Delete empty `JournalTemplates/` folder

This will align your architecture with industry-standard CLI patterns used by dotnet, git, kubectl, and Azure CLI.
