# Markdown Journal CLI - Agent Instructions

> .NET 10 CLI app for markdown journals. Uses Spectre.Console.Cli, Microsoft DI, xUnit/Moq/Shouldly.

## Core Rules

**Commands must be THIN** — validate input, call services, output results. No business logic.
- Max ~100 lines, no static helpers, no file/config manipulation
- Return 0 = success, 1 = error

**Services own all business logic** — every service needs an interface, constructor null-guards, singleton registration.

**Every new file needs a test file** mirroring the source path in `markdown-journal-cli.Tests/`.

**Never use `Console.WriteLine()`** — always `IAnsiConsole.MarkupLine()`. Escape user input with `.EscapeMarkup()`.

## Spectre.Console

For all Spectre.Console UI work (markup, tables, prompts, progress bars, command framework), read `.github/skills/spectre-console/AGENTS.md` — it contains full API examples and best practices.

Key rules:
- Inject `IAnsiConsole` into commands (not static `AnsiConsole`) for testability
- `[green]Success:[/]`, `[red]Error:[/]` markup conventions
- `.EscapeMarkup()` on all user-provided strings before markup interpolation

## Directory Structure

```
Commands/        # CLI layer — grouped by verb (Add/, Init/, New/, Remove/, Update/)
Services/        # High-level business logic
Infrastructure/
  Configuration/        # Journal config management
  DependencyInjection/  # TypeRegistrar: Spectre ↔ MS DI adapter
  FileSystem/           # IFileSystem abstraction over System.IO
  JournalTemplates/     # ITemplateGenerator / ITemplateManager
  Tracking/             # File change detection
  Transactions/         # Rollback / transactional file operations
  Validation/           # Input validation helpers
```

> The directory map above is illustrative, not exhaustive — new infrastructure concerns may introduce additional subfolders.

## Command Template

All commands inherit from `JournalCommand<TSettings>` (not `Command<TSettings>`) and override `ExecuteCore` (not `Execute`). The base class handles `RollbackCompletedException` (exit 2 = full rollback, 3 = partial) and metadata directory validation before delegating to `ExecuteCore`.

**Standard command** (no metadata validation needed):
```csharp
public sealed class YourCommand(IYourService service) : JournalCommand<YourCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[name]")] public string? Name { get; init; }
        [CommandOption("-p|--path")] public string? Path { get; init; }
        public override ValidationResult Validate() => ValidationResult.Success();
    }

    protected override int ExecuteCore(CommandContext context, Settings settings)
    {
        try
        {
            service.DoWork(settings.Name);
            return 0;
        }
        catch (JournalException ex) { /* log via IAnsiConsole if injected */ return 1; }
        catch (Exception ex) { return 1; }
    }
}
```

**Command with metadata validation** (most commands — validates the journal directory exists before running):
```csharp
public sealed class YourCommand(
    IAnsiConsole console,
    IJournalValidator validator,
    IYourService service
) : JournalCommand<YourCommand.Settings>(validator, console)
{
    protected override string? GetJournalDirectory(Settings settings) => settings.Path;

    protected override int ExecuteCore(CommandContext context, Settings settings)
    {
        try
        {
            service.DoWork(settings.Name);
            console.MarkupLine("[green]Success:[/] Done.");
            return 0;
        }
        catch (JournalException ex) { console.MarkupLine($"[red]Error:[/] {ex.Message}"); return 1; }
        catch (Exception ex) { console.MarkupLine($"[red]Error:[/] Unexpected: {ex.Message}"); return 1; }
    }
}
```

**Commands that create a journal** (e.g. `new`, `init`) skip validation:
```csharp
protected override bool SkipMetadataValidation => true;
```

**Registration in `Program.cs`**:
```csharp
config.AddCommand<NewCommand>("new");

config.AddBranch<AddSettings>("add", add =>
{
    add.AddCommand<AddEntry>("entry");
    add.AddCommand<AddJournalrc>("config");
});
```

**Create a new service when**: command exceeds ~80 lines, you need static helpers, multiple commands share logic, or orchestration is complex.

## Service Template

```csharp
public class YourService(IFileSystem fs, ILogger<YourService> logger) : IYourService
{
    private readonly IFileSystem _fs = fs ?? throw new ArgumentNullException(nameof(fs));

    public void DoWork(string name)
    {
        logger.LogDebug("Doing work for '{Name}'", name);
        // business logic here
    }
}
```

**Registration**:
```csharp
host.Services.AddSingleton<IFileSystem, FileSystem>();
host.Services.AddSingleton<IYourService, YourService>();
host.Services.AddSingleton<YourCommand>(); // commands too
```

## Key Interfaces

**`IFileSystem`** — all file and directory operations MUST go through this interface, never call `System.IO` directly. Before writing any file-related code, read `Infrastructure/FileSystem/IFileSystem.cs` to find the right method. If no existing method fits your need, add one to the interface and its implementation — do not add a method that duplicates an existing one under a different name. Always mock `IFileSystem` in unit tests; never touch real files.

**`ITemplateGenerator`** — implement for new templates, register in `TemplateManager.RegisterDefaultTemplates()`:
```csharp
public interface ITemplateGenerator
{
    string TemplateName { get; }
    string GenerateTemplate(Dictionary<string, object>? parameters);
}
```

## Exception Hierarchy

All exceptions inherit from `JournalException` and live in `Exceptions/JournalExceptions.cs`. When adding a new exception, check that file first to avoid duplicates. Provide `(string message)` and `(string message, Exception inner)` constructors.

Catch order in commands: most specific → `JournalException` → `Exception`.

## Test Template

```csharp
public class YourServiceTests
{
    private readonly Mock<IDependency> _mock = new();
    private readonly YourService _sut;

    public YourServiceTests() => _sut = new YourService(_mock.Object);

    [Fact]
    public void Method_Should_Behavior_When_Condition()
    {
        // Given
        _mock.Setup(x => x.Op()).Returns("value");
        // When
        var result = _sut.Method();
        // Then
        result.ShouldBe("value");
        _mock.Verify(x => x.Op(), Times.Once);
    }
}
```

Test naming: `MethodName_Should_ExpectedBehavior_When_Condition`
Test all: happy path, edge cases (null/empty), exception scenarios.

## Naming Conventions

| Kind | Convention | Example |
|---|---|---|
| Interface | `I` prefix | `IFileSystem` |
| Implementation | Drop `I` | `FileSystem` |
| Command | `Command` suffix | `AddEntryCommand` |
| Settings | `Settings` suffix | `AddEntrySettings` |
| Service | `Service` suffix | `JournalEntryService` |
| Test class | `Tests` suffix | `JournalEntryServiceTests` |

One class per file; file name = class name. Nested settings classes are fine in the same file.

## Workflow Checklists

**New command**: class in `Commands/{Group}/` → settings → constructor inject → `Execute()` → register in `Program.cs` → tests

**New service**: interface → implementation → register singleton → tests

**New template**: implement `ITemplateGenerator` in `Infrastructure/JournalTemplates/Templates/` → register in `TemplateManager.RegisterDefaultTemplates()` → tests

**Modify existing**: read tests first → write failing test → implement → `dotnet test`

## Code Style

- Primary constructors; file-scoped namespaces; `sealed` where applicable
- `string?` nullable, `string` non-null; `readonly` fields
- XML docs (`///`) on all public interfaces and classes
- `ILogger<T>` for structured logging — `LogDebug` for trace, `LogWarning` for recoverable issues
- Config via `IOptions<JournalSettings>` / `appsettings.json`

## Anti-Patterns

- Business logic in commands
- `new ServiceImpl()` instead of DI
- `Console.WriteLine()` instead of `IAnsiConsole`
- Missing `.EscapeMarkup()` on user input
- Services without interfaces
- Missing constructor null checks
- Production code without tests
- Catching `Exception` before specific exceptions
