# Markdown Journal CLI Tests

This project contains the unit tests for the Markdown Journal CLI tool. The tests are written using xUnit and Spectre.Console.Testing.

## Project Structure

```
markdown-journal-cli.Tests/
├── Commands/
│   └── NewCommandTests.cs      # Tests for the New command
├── Infrastructure/
│   └── TestFileSystem.cs       # Mock file system for testing
├── bin/                        # Build output
├── obj/                        # Build artifacts
├── markdown-journal-cli.Tests.csproj
└── README.md
```

## Technologies Used

- [xUnit](https://xunit.net/) - Testing framework
- [Spectre.Console.Testing](https://spectreconsole.net/cli/unit-testing) - Testing utilities for Spectre.Console
- [Shouldly](https://github.com/shouldly/shouldly) - Assertion framework for better test readability
- [Microsoft.Extensions.DependencyInjection](https://docs.microsoft.com/en-us/dotnet/core/extensions/dependency-injection) - For dependency injection in tests
- [coverlet.collector](https://github.com/coverlet-coverage/coverlet) - Code coverage collection

## Running Tests

To run all tests:

```bash
dotnet test
```

To run tests with coverage:

```bash
dotnet test --collect:"XPlat Code Coverage"
```

To run specific tests:

```bash
# Run specific test class
dotnet test --filter "FullyQualifiedName~NewCommandTests"

# Run specific test method
dotnet test --filter "FullyQualifiedName~Should_Create_New_Journal_With_Default_Name"
```

## Test Coverage

### NewCommand Tests

The `NewCommandTests` class provides comprehensive testing for the journal creation functionality:

- **Should_Create_New_Journal_With_Default_Name** - Tests basic journal creation with a simple name
- **Should_Create_New_Journal_With_Custom_Name** - Tests journal creation with custom naming
- **Should_Return_Error_When_Journal_Already_Exists** - Tests duplicate journal handling (returns exit code 1)
- **Should_Create_Journal_In_Custom_Path** - Tests journal creation with custom path using `--path` option
- **Should_Validate_Journal_Name_For_Invalid_Characters** - Tests input validation for invalid file name characters
- **Should_Validate_Empty_Journal_Name** - Tests input validation for empty journal names

### Infrastructure Tests

The test infrastructure includes:

- **TestFileSystem** - Mock implementation of `IFileSystem` for isolated file system testing
- **TypeRegistrar** - Uses the main project's dependency injection container for test setup
- **TestConsole** - Spectre.Console test console for verifying output

## Architecture

The test project follows these architectural patterns:

1. **Dependency Injection**: Uses the main project's `TypeRegistrar` to inject mock dependencies
2. **Mock File System**: `TestFileSystem` provides in-memory file system simulation
3. **Command Testing**: Uses `CommandAppTester` to test the full command pipeline
4. **Arrange-Act-Assert**: All tests follow the AAA pattern for clarity

## Current Implementation Status

✅ **Implemented Features:**
- File system abstraction (`IFileSystem`) with test mock
- Dependency injection for testability
- Input validation with proper error handling
- Console output testing
- Custom path support
- Comprehensive error scenarios

✅ **Best Practices Applied:**
- Dependency injection for console and file system operations
- Custom exceptions for domain-specific errors
- Settings validation
- Isolated, independent tests
- Meaningful test names following convention

## Best Practices and Recommendations

### Current Implementation Status

The main project has already implemented most recommended best practices:

✅ **Already Implemented:**
- **File System Abstraction**: `IFileSystem` interface with real implementation
- **Dependency Injection**: Console operations use injected `IAnsiConsole`
- **Custom Exceptions**: Domain-specific exception handling
- **Input Validation**: Settings validation with proper error messages
- **Testable Architecture**: All dependencies are mockable

### Future Enhancements

While the current implementation is well-architected, consider these enhancements:

1. **Additional Command Testing**
   - Add tests for any new commands as they're implemented
   - Consider integration tests for end-to-end scenarios

2. **Performance Testing**
   - Add benchmarks for large journal operations
   - Test memory usage with many files

3. **Error Scenario Coverage**
   - Test file system permission errors
   - Test disk space limitations
   - Test network path scenarios

### Testing Best Practices Applied

1. **Arrange-Act-Assert**: All tests follow the AAA pattern for clarity
2. **Meaningful Names**: Test names describe the scenario using Should_ExpectedBehavior_When_Condition pattern
3. **Independent Tests**: Each test is self-contained with proper setup and teardown
4. **Mock Dependencies**: File system and console operations are mocked for isolation
5. **Comprehensive Coverage**: Tests cover happy path, error conditions, and edge cases
6. **Domain-Specific Testing**: Tests verify business logic, not just technical implementation

### Test Structure Example

```csharp
[Fact]
public void Should_Create_New_Journal_With_Default_Name()
{
    // Arrange - Set up test data and expectations
    
    // Act - Execute the operation being tested
    var result = _app.Run(new[] { "new", "MyJournal" });

    // Assert - Verify the expected outcomes
    result.ExitCode.ShouldBe(0);
    result.Output.ShouldContain("MyJournal");
    _fileSystem.DirectoryExists("./MyJournal").ShouldBeTrue();
}
```

## Useful Links

- [Spectre.Console Unit Testing Documentation](https://spectreconsole.net/cli/unit-testing)
- [Spectre.Console Best Practices](https://spectreconsole.net/best-practices)
- [xUnit Documentation](https://xunit.net/#documentation)
- [Shouldly Documentation](https://shouldly.readthedocs.io/en/latest/)

## Notes

- The test project targets .NET 9.0, matching the main project
- Tests are organized by command in the `Commands/` directory
- Infrastructure helpers are in the `Infrastructure/` directory
- Each test class focuses on a single command with comprehensive scenario coverage
- Test names follow the pattern `Should_ExpectedBehavior_When_Condition`
- All file system operations are mocked using `TestFileSystem` for fast, isolated tests
- Console output is captured and verified using Spectre.Console.Testing
- Dependency injection is used throughout for better testability and maintainability
