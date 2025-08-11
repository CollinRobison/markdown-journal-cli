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
                            ├── IFileSystem
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

// Our Implementation
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
2. **Registration** - Services registered via `registrar.Register()`
3. **Building** - `registrar.Build()` creates `IServiceProvider`
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

## 🧪 Testing Architecture

### Test Structure
```
markdown-journal-cli.Tests/
├── Commands/
│   └── NewCommandTests.cs      # Command behavior tests
└── Infrastructure/
    ├── TestFileSystem.cs       # Mock file system
    └── TypeRegistrar.cs        # Test DI container
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
