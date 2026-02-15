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
        var entryTitle = _entryFormatter.RemoveSpaceSeperators(
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
        var entryNameFormatted = _entryFormatter.AddSpaceSeperators(request.EntryName);
        var headingFormatted = request.Heading != null
            ? _entryFormatter.AddSpaceSeperators(request.Heading)
            : null;

        var fileName = new[] { headingFormatted, request.Subheading, entryNameFormatted }
            .Where(x => x != null)
            .Cast<string>()
            .ToArray();

        return $"{_entryFormatter.AddHeadingSeperators(fileName)}.md";
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
                ? [_entryFormatter.RemoveSpaceSeperators(request.Heading)]
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
