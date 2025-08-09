# Markdown Journal CLI Tests

This project contains the unit tests for the Markdown Journal CLI tool. The tests are written using xUnit and Spectre.Console.Testing.

## Project Structure

```
markdown-journal-cli.Tests/
├── Commands/
│   └── NewCommandTests.cs      # Tests for the New command
└── markdown-journal-cli.Tests.csproj
```

## Technologies Used

- [xUnit](https://xunit.net/) - Testing framework
- [Spectre.Console.Testing](https://spectreconsole.net/cli/unit-testing) - Testing utilities for Spectre.Console
- [Shouldly](https://github.com/shouldly/shouldly) - Assertion framework for better test readability

## Running Tests

To run the tests, use:

```bash
dotnet test
```

## Test Coverage

### NewCommand Tests

- Creating a journal with default name
- Creating a journal with custom name
- Handling duplicate journal names
- Creating a journal in a custom path

## Best Practices and Recommendations

### For Main Project

1. **Dependency Injection for Console Operations**
   
   Currently, the NewCommand uses `AnsiConsole` directly, which makes testing harder because we can't mock or verify the console output easily.

   ```csharp
   // Current approach - hard to test
   public sealed class NewCommand
   {
       public override int Execute(CommandContext context, Settings settings)
       {
           AnsiConsole.MarkupLine("Some output"); // Direct dependency
       }
   }

   // Recommended approach - testable
   public sealed class NewCommand
   {
       private readonly IAnsiConsole _console;
       
       public NewCommand(IAnsiConsole console)
       {
           _console = console;
       }

       public override int Execute(CommandContext context, Settings settings)
       {
           _console.MarkupLine("Some output"); // Can be mocked and verified
       }
   }
   ```

   **Why?**
   - Makes unit testing easier by allowing us to inject TestConsole
   - Follows SOLID principles (Dependency Inversion)
   - Enables better test verification of console output
   - Makes it easier to change console behavior in the future

2. **File System Abstraction**

   Currently, file system operations are done directly using System.IO. This couples your code to the file system and makes testing harder.

   ```csharp
   // Current approach - tightly coupled to file system
   Directory.CreateDirectory(path);

   // Recommended approach - abstracted file system
   public interface IFileSystem
   {
       bool DirectoryExists(string path);
       void CreateDirectory(string path);
       string CombinePaths(params string[] paths);
   }

   public class RealFileSystem : IFileSystem
   {
       public bool DirectoryExists(string path) => Directory.Exists(path);
       public void CreateDirectory(string path) => Directory.CreateDirectory(path);
       public string CombinePaths(params string[] paths) => Path.Combine(paths);
   }

   public class NewCommand
   {
       private readonly IFileSystem _fileSystem;
       
       public NewCommand(IFileSystem fileSystem)
       {
           _fileSystem = fileSystem;
       }

       public override int Execute(CommandContext context, Settings settings)
       {
           var path = _fileSystem.CombinePaths(settings.FilePath ?? ".", settings.JournalName);
           if (!_fileSystem.DirectoryExists(path))
           {
               _fileSystem.CreateDirectory(path);
           }
       }
   }
   ```

   **Why?**
   - Makes unit testing possible without touching the real file system
   - Allows mocking file system operations in tests
   - Makes it easier to handle different file system implementations (e.g., for different platforms)
   - Enables better error simulation in tests

3. **Error Handling and Custom Exceptions**

   Instead of using generic exceptions, create specific ones for your domain:

   ```csharp
   public class JournalException : Exception
   {
       public JournalException(string message) : base(message) { }
       public JournalException(string message, Exception inner) : base(message, inner) { }
   }

   public class JournalAlreadyExistsException : JournalException
   {
       public string JournalName { get; }
       public string Path { get; }

       public JournalAlreadyExistsException(string journalName, string path)
           : base($"Journal '{journalName}' already exists at '{path}'")
       {
           JournalName = journalName;
           Path = path;
       }
   }
   ```

   **Why?**
   - Makes error handling more specific and meaningful
   - Allows catching specific types of errors
   - Provides more context in error messages
   - Makes testing error conditions more explicit

4. **Settings Validation**

   Consider adding validation to your Settings class:

   ```csharp
   public sealed class Settings : CommandSettings
   {
       [CommandArgument(0, "[name]")]
       [Description("The name of the journal to create")]
       public required string JournalName { get; set; }

       public override ValidationResult Validate()
       {
           if (string.IsNullOrWhiteSpace(JournalName))
           {
               return ValidationResult.Error("Journal name cannot be empty");
           }

           if (JournalName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
           {
               return ValidationResult.Error("Journal name contains invalid characters");
           }

           return ValidationResult.Success();
       }
   }
   ```

   **Why?**
   - Catches invalid input before execution
   - Makes validation logic centralized and reusable
   - Easier to test input validation separately from command execution
   - Provides clear feedback to users

### Testing Best Practices

1. **Arrange-Act-Assert**: Tests follow the AAA pattern for clarity
2. **Meaningful Names**: Test names describe the scenario being tested
3. **Independent Tests**: Each test is self-contained and doesn't rely on other tests

## Useful Links

- [Spectre.Console Unit Testing Documentation](https://spectreconsole.net/cli/unit-testing)
- [Spectre.Console Best Practices](https://spectreconsole.net/best-practices)
- [xUnit Documentation](https://xunit.net/#documentation)
- [Shouldly Documentation](https://shouldly.readthedocs.io/en/latest/)

## Notes

- The test project uses the same target framework as the main project (net9.0)
- Tests are organized by command in the Commands directory
- Each test class focuses on a single command
- Test names are descriptive and follow the pattern Should_ExpectedBehavior_When_Condition
