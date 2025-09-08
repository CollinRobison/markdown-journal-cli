[Back to README](../README.md)

# Development Guide

This guide covers everything developers need to know to contribute to the Markdown Journal CLI project.

## 🚀 Getting Started

### Prerequisites
- .NET 9.0 SDK
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
│   ├── Commands/New/              # Command implementations
│   ├── Exceptions/                # Custom exceptions
│   ├── Infrastructure/            # Core services
│   └── Program.cs                # Entry point
├── markdown-journal-cli.Tests/    # Unit tests
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
using markdown_journal_cli.Infrastructure.DependencyInjection;

namespace markdown_journal_cli.Commands.YourCommand;

[Description("TODO: Add your command description")]
public sealed class YourCommand : Command<YourCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly IFileSystem _fileSystem;

    public YourCommand(IAnsiConsole console, IFileSystem fileSystem)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
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

    public override int Execute(CommandContext context, Settings settings)
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
    private readonly CommandAppTester _app;

    public YourCommandTests()
    {
        _console = new TestConsole();
        _fileSystem = new TestFileSystem();
        
        var registrar = new markdown_journal_cli.Tests.Infrastructure.TypeRegistrar()
            .RegisterInstance(_console)
            .RegisterInstance<IFileSystem>(_fileSystem);

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
- ✅ Exception handling architecture
- ✅ Testing framework setup
- ⏳ Additional commands (add, list, open, search)
- ⏳ Configuration system
- ⏳ Documentation completion
