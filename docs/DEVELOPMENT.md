[Back to README](../README.md)

# Development Guide

This guide covers local setup, architecture-aware development workflow, coding
standards, debugging, and release steps for `markdown-journal-cli`.

For testing specifics, see `TESTING.md`.
For contribution process and expectations, see `../CONTRIBUTING.md`.

## Getting Started

### Prerequisites

- .NET 10 SDK
- Git
- C# IDE (VS Code, Visual Studio, Rider)

### First-Time Setup

```bash
git clone https://github.com/CollinRobison/markdown-journal-cli.git
cd markdown-journal-cli
dotnet restore
dotnet build
dotnet test
```

### Run the CLI Locally

```bash
dotnet run --project markdown-journal-cli -- --help
```

## Project Structure

```text
markdown-journal-cli/
├── markdown-journal-cli/
│   ├── Commands/                  # CLI layer (thin commands)
│   ├── Services/                  # Business logic
│   ├── Infrastructure/
│   │   ├── Configuration/
│   │   ├── DependencyInjection/
│   │   ├── FileSystem/
│   │   ├── JournalTemplates/
│   │   ├── Tracking/
│   │   ├── Transactions/
│   │   └── Validation/
│   ├── Exceptions/
│   ├── Program.cs
│   └── appsettings.json
├── markdown-journal-cli.Tests/
│   ├── Commands/
│   ├── Services/
│   └── Infrastructure/
└── docs/
```

### Architectural Rule of Thumb

- Commands are thin: parse args, validate, call services, print output, return exit code.
- Services own business logic.
- All file I/O goes through `IFileSystem`.
- Write flows are transactional (rollback-aware).

## Development Workflow

### 1) Adding a Command

### Command Pattern (Current)

All commands inherit from `JournalCommand<TSettings>` and override `ExecuteCore`.

- Use the parameterless base constructor only if no metadata validation is needed.
- Commands operating on an existing journal should inject `IJournalValidator` and
  `IAnsiConsole` into the base constructor: `: JournalCommand<T>(validator, console)`.
- Commands that create journals (`new`, `init`) should set:

```csharp
protected override bool SkipMetadataValidation => true;
```

### Command Template

```csharp
using System.ComponentModel;
using markdown_journal_cli.Commands;
using markdown_journal_cli.Exceptions;
using markdown_journal_cli.Infrastructure.Validation;
using Spectre.Console;
using Spectre.Console.Cli;

namespace markdown_journal_cli.Commands.Example;

[Description("Describe what the command does")]
public sealed class ExampleCommand(
    IAnsiConsole console,
    IJournalValidator validator,
    IExampleService service
) : JournalCommand<ExampleCommand.Settings>(validator, console)
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-p|--path")]
        public string FilePath { get; set; } = ".";

        public override ValidationResult Validate() => ValidationResult.Success();
    }

    protected override string? GetJournalDirectory(Settings settings) => settings.FilePath;

    protected override int ExecuteCore(CommandContext context, Settings settings)
    {
        try
        {
            service.Execute(settings.FilePath);
            console.MarkupLine("[green]Success:[/] Done.");
            return 0;
        }
        catch (JournalException ex)
        {
            console.MarkupLine($"[red]Error:[/] {ex.Message.EscapeMarkup()}");
            return 1;
        }
        catch (Exception ex)
        {
            console.MarkupLine($"[red]Error:[/] Unexpected: {ex.Message.EscapeMarkup()}");
            return 1;
        }
    }
}
```

### Register the Command in `Program.cs`

```csharp
config.AddCommand<ExampleCommand>("example");
```

If it belongs under a branch (`add`, `update`, `remove`), register in that branch.

### Add Tests

- Add command unit tests under mirrored path in `markdown-journal-cli.Tests/Commands/...`
- Add integration tests when command behavior spans multiple services/files

See `docs/TESTING.md` for full patterns.

### 2) Adding a Service

### Service Pattern

- Every service has an interface.
- Use primary constructors and null-guard dependencies.
- Keep business logic in services, not commands.

Template:

```csharp
namespace markdown_journal_cli.Services.Example;

public interface IExampleService
{
    void Execute(string journalPath);
}

public sealed class ExampleService(
    IFileSystem fileSystem,
    ILogger<ExampleService> logger
) : IExampleService
{
    private readonly IFileSystem _fileSystem =
        fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

    public void Execute(string journalPath)
    {
        logger.LogDebug("Executing example service for {JournalPath}", journalPath);
        // business logic
    }
}
```

Register in `Program.cs`:

```csharp
host.Services.AddSingleton<IExampleService, ExampleService>();
```

### 3) Metadata Directory Pattern

Internal metadata is rooted in `.mdjournal/`:

- `.mdjournal/.journalindex`
- `.mdjournal/.journaltoc`

Root-level `.journalrc` remains the user-facing configuration file. It includes both TOC exclusions (`tableOfContents.ignoreFiles`) and tracking exclusions (`trackingIndex.noTrack`). When touching file tracking, make sure no-track filtering happens before hashing or saving `.journalindex`; when touching TOC generation, use `ignoreFiles` instead.

Services should resolve these via configured names in `JournalSettings`, not hardcoded strings.

Commands that target existing journals should rely on `JournalCommand<T>` metadata validation
instead of duplicating validation logic.

## Code Standards

### Language/Style

- Primary constructors
- File-scoped namespaces
- `sealed` where appropriate
- Nullable reference types enabled (`string?` vs `string`)
- Constructor null-guards for injected dependencies

### CLI and Spectre Console

- Use injected `IAnsiConsole` for all user output.
- Do not use `Console.WriteLine()`.
- Use `.EscapeMarkup()` for user-provided strings interpolated into markup.
- Preferred message style:
  - success: `[green]Success:[/] ...`
  - failure: `[red]Error:[/] ...`

### Command Boundaries

- Keep command files small and orchestration-only.
- If logic grows, extract/extend a service.
- Keep exit code contract stable: `0`, `1`, `2`, `3`.

### Logging

- Use `ILogger<T>` with structured messages.
- `LogDebug` for normal trace flow.
- `LogWarning` for recoverable issues.

### Dependency Injection Snapshot

`Program.cs` is the source of truth for all DI registrations. Refer to it directly for the current list of services and commands.

## Debugging Tips

### Common Failures

### Service resolution errors

Symptom:

```text
Unable to resolve service for type 'IYourService'
```

Check:

- interface/implementation registration in `Program.cs`
- command constructor matches registered service types

### Command not found

Symptom:

```text
Unknown command '...'
```

Check:

- command added to root or correct branch in `Program.cs`
- branch/subcommand ordering in CLI input

### Metadata validation errors

Symptom: command exits `1` with missing metadata or no-journal message.

Check:

- target path points at an initialized journal
- `.mdjournal/.journalindex` and `.mdjournal/.journaltoc` exist

### Helpful Commands

```bash
dotnet build
dotnet test
dotnet test --filter "FullyQualifiedName~UpdateCommandTests"
dotnet run --project markdown-journal-cli -- update --help
```

## Release Process

Releases are fully automated via [release-please](https://github.com/googleapis/release-please).

**How it works:**

1. Merge PRs to `main` using conventional commit titles (`feat:`, `fix:`, etc.).
2. `release-please` reads those commits and opens a "Release PR" that bumps
   `<Version>` in `.csproj` and updates `CHANGELOG.md`.
3. When you merge the Release PR, a GitHub Release is created automatically.
4. The release triggers the build pipeline: binaries for 6 platforms are built
   and attached to the Release, and the NuGet package is published.

**Version bump rules:**
- `docs:` is an allowed PR title prefix and is treated as a patch release.

### PR Title Prefix Reference

Use this format for PR titles:

`<type>(optional-scope): <description>`

| Prefix                    | Effect on release version | Notes                                        |
| ------------------------- | ------------------------- | -------------------------------------------- |
| `feat:`                   | Minor bump                | Standard feature work.                       |
| `feat!:`                  | Major bump                | Breaking change via `!`.                     |
| `fix:`                    | Patch bump                | Bug fix.                                     |
| `perf:`                   | Patch bump                | Performance-only improvement.                |
| `refactor:`               | Patch bump                | Internal refactor without feature semantics. |
| `docs:`                   | Patch bump                | Documentation-only updates.                  |
| `test:`                   | No bump by itself         | Test-only work.                              |
| `chore:`                  | No bump by itself         | Maintenance/housekeeping.                    |
| `ci:`                     | No bump by itself         | CI workflow changes.                         |
| `BREAKING CHANGE:` footer | Major bump                | Use in commit/PR body when applicable.       |

### Testing packaging locally

**NuGet tool** (validates the tool installs and `--version` reports correctly):

```bash
dotnet pack markdown-journal-cli --configuration Release
dotnet tool install -g --add-source ./markdown-journal-cli/nupkg mdjournal
mdjournal --version
```

To reinstall a rebuilt version:

```bash
dotnet tool uninstall -g mdjournal
dotnet tool install -g --add-source ./markdown-journal-cli/nupkg mdjournal
```

**Self-contained binary** (validates single-file publish and trim behaviour before a real release):

```bash
# macOS (Apple Silicon)
dotnet publish markdown-journal-cli -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true

# macOS (Intel)
dotnet publish markdown-journal-cli -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true

# Linux (x64)
dotnet publish markdown-journal-cli -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true

# Windows (x64)
dotnet publish markdown-journal-cli -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Output lands in `markdown-journal-cli/bin/Release/net10.0/<rid>/publish/`. Run it directly:

```bash
./markdown-journal-cli/bin/Release/net10.0/osx-arm64/publish/markdown-journal-cli --version
```

Optional — install to PATH as `mdjournal` for a fuller local smoke test:

```bash
sudo cp ./markdown-journal-cli/bin/Release/net10.0/osx-arm64/publish/markdown-journal-cli /usr/local/bin/mdjournal
mdjournal --version
sudo rm /usr/local/bin/mdjournal  # clean up when done
```
