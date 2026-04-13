# Markdown Journal CLI Tests

This project contains the unit, integration, and rollback tests for the Markdown Journal CLI tool.

## Project Structure

```
markdown-journal-cli.Tests/
├── Commands/
│   ├── Add/
│   │   ├── AddEntryCommandTests.cs           # Unit tests
│   │   ├── AddEntryIntegrationTests.cs        # Integration tests (real disk I/O)
│   │   ├── AddFileTrackingCommandTests.cs
│   │   ├── AddFileTrackingRollbackTests.cs    # Rollback / fault-injection tests
│   │   ├── AddJournalrcCommandTests.cs
│   │   ├── AddJournalrcRollbackTests.cs
│   │   ├── AddTableOfContentsCommandTests.cs
│   │   ├── AddTableOfContentsIntegrationTests.cs
│   │   └── AddTableOfContentsRollbackTests.cs
│   ├── Init/
│   │   ├── InitCommandTests.cs
│   │   └── InitCommandIntegrationTests.cs
│   ├── New/
│   │   ├── NewCommandTests.cs
│   │   └── NewCommandIntegrationTests.cs
│   ├── Remove/
│   │   ├── RemoveEntryCommandTests.cs
│   │   └── RemoveEntryCommandIntegrationTests.cs
│   └── Update/
│       ├── UpdateCommandTests.cs
│       ├── UpdateCommandIntegrationTests.cs
│       └── UpdateEntryCommandTests.cs
├── Infrastructure/
│   ├── CommandAppTester.cs           # Spectre.Console test harness helper
│   ├── CommandTestBase.cs            # Abstract base for command unit tests
│   ├── JournalIntegrationTestBase.cs # Abstract base for command integration tests
│   ├── MockFactory.cs                # Pre-configured Mock<T> factory methods
│   ├── QuickstartValidationTests.cs  # Tests validating the test infrastructure itself
│   ├── ServiceTestBase.cs            # Abstract base for service unit tests
│   ├── Configuration/
│   ├── DependencyInjection/
│   ├── FileSystem/
│   │   ├── FaultInjectingFileSystem.cs  # Fault-injection helper for rollback tests
│   │   └── TestFileSystem.cs            # In-memory IFileSystem for unit tests
│   ├── JournalTemplates/
│   ├── Tracking/
│   └── Transactions/
├── Services/
│   ├── EntryFormatter/
│   ├── InitJournal/
│   ├── JournalEntry/
│   ├── JournalFileUpdate/
│   ├── JournalUpdate/
│   ├── NewJournal/
│   ├── RemoveEntry/
│   ├── Rollback/                     # Rollback tests (preserved; not modified by cleanup)
│   │   ├── ServiceRollbackTestBase.cs
│   │   ├── InitJournalServiceRollbackTests.cs
│   │   ├── JournalEntryServiceRollbackTests.cs
│   │   ├── JournalFileUpdateServiceRollbackTests.cs
│   │   ├── JournalUpdateServiceRollbackTests.cs
│   │   ├── NewJournalServiceRollbackTests.cs
│   │   └── RemoveEntryServiceRollbackTests.cs
│   └── TableOfContents/
├── Exceptions/
├── JournalTemplates/
├── markdown-journal-cli.Tests.csproj
└── README.md
```

## Technologies Used

- [xUnit](https://xunit.net/) - Testing framework
- [Moq](https://github.com/moq/moq4) - Mocking framework (project-standard; all unit test mocks use Moq)
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

To run specific test groups:

```bash
# All integration tests (real disk I/O)
dotnet test --filter "FullyQualifiedName~IntegrationTests"

# All rollback / fault-injection tests
dotnet test --filter "FullyQualifiedName~Rollback"

# A specific command's tests
dotnet test --filter "FullyQualifiedName~NewCommandTests"
```

## Test Infrastructure Layers

There are four shared base classes that drive the test suite:

| Base class | Use for | Key properties |
|---|---|---|
| `CommandTestBase` | Command-layer unit tests | `Mock<IFileSystem>`, `Mock<IJournalConfiguration>`, etc.; `BuildApp(configure)` helper |
| `ServiceTestBase` | Service-layer unit tests | Same mocks plus `NoOpCoordinator`, `NoOpReporter`, `NullLogger<T>()` |
| `JournalIntegrationTestBase` | Command integration tests | Real `FileSystem`, `JournalRoot`/`JournalPath` temp dirs, `InitializeJournal()`, auto-cleanup |
| `ServiceRollbackTestBase` | Service rollback / fault-injection | `FaultInjectingFileSystem`, real `FileTransactionCoordinator` |

`MockFactory` provides pre-configured `Mock<T>` instances used internally by `CommandTestBase` and `ServiceTestBase`. Use it directly if you need a one-off mock outside a base class.

`TestFileSystem` is an in-memory `IFileSystem` substitute retained for infrastructure-layer tests that pre-date the Moq migration or that specifically need to verify in-memory state.

## Architecture

The test project follows four distinct patterns depending on the test type:

### 1. Command Unit Tests (`CommandTestBase`)

Extend `CommandTestBase` and call `BuildApp(configure)` per test to get a fresh `CommandAppTester`. Override `SetupDefaultBehaviors()` to configure Moq defaults for the whole class; add per-test `Setup()` calls for scenario-specific responses.

```csharp
public sealed class NewCommandTests : CommandTestBase
{
    protected override void SetupDefaultBehaviors()
    {
        MockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(false);
    }

    [Fact]
    public void Execute_Should_CreateJournal_When_NameIsValid()
    {
        var app = BuildApp(cfg =>
        {
            cfg.AddCommand<NewCommand>("new");
            cfg.PropagateExceptions();
        });

        var result = app.Run(["new", "MyJournal"]);

        result.ExitCode.ShouldBe(0);
        MockFileSystem.Verify(fs => fs.CreateDirectory(It.IsAny<string>()), Times.Once);
    }
}
```

### 2. Service Unit Tests (`ServiceTestBase`)

Extend `ServiceTestBase`, create the SUT in a `CreateSut()` factory method using base-class mocks, and inject `NoOpCoordinator` / `NoOpReporter` when transaction behavior is intentionally out of scope.

```csharp
public sealed class NewJournalServiceTests : ServiceTestBase
{
    private NewJournalService CreateSut() =>
        new(MockFileSystem.Object, MockTemplateManager.Object,
            MockJournalConfiguration.Object, NoOpCoordinator, NoOpReporter);

    [Fact]
    public void Initialize_Should_CallCreateDirectory_When_JournalPathIsNew()
    {
        MockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(false);
        var sut = CreateSut();

        sut.Initialize("/journals/MyJournal", "MyJournal");

        MockFileSystem.Verify(fs => fs.CreateDirectory(It.IsAny<string>()), Times.AtLeastOnce);
    }
}
```

### 3. Command Integration Tests (`JournalIntegrationTestBase`)

Extend `JournalIntegrationTestBase`. The base class sets up a unique temp directory under `Path.GetTempPath()` and deletes it automatically on `Dispose()`. Use no mocks unless a real implementation is unavailable.

```csharp
public sealed class NewCommandIntegrationTests : JournalIntegrationTestBase
{
    [Fact]
    public void Execute_Should_CreateJournalFiles_When_NameIsValid()
    {
        // Use real services wired against JournalRoot / JournalPath
        var result = BuildRealApp().Run(["new", "TestJournal", "--path", JournalRoot]);

        result.ExitCode.ShouldBe(0);
        File.Exists(Path.Combine(JournalPath, ".journalrc")).ShouldBeTrue();
        File.Exists(Path.Combine(JournalPath, ".mdjournal")).ShouldBeTrue();
    }
}
```

### 4. Rollback / Fault-Injection Tests (`ServiceRollbackTestBase`)

Use `FaultInjectingFileSystem` to simulate write failures at specific steps, then assert that the real `FileTransactionCoordinator` successfully rolls back all applied changes.

## Test Naming Convention

All test methods follow the pattern:

```
{MethodOrScenario}_Should_{ExpectedBehavior}_When_{Condition}
```

Examples:
- `Execute_Should_ReturnExitCode0_When_JournalCreatedSuccessfully`
- `Initialize_Should_ThrowJournalAlreadyExistsException_When_DirectoryAlreadyManaged`
- `RemoveEntry_Should_DeleteFileAndUpdateToc_When_EntryExists`

## Useful Links

- [Spectre.Console Unit Testing Documentation](https://spectreconsole.net/cli/unit-testing)
- [xUnit Documentation](https://xunit.net/#documentation)
- [Shouldly Documentation](https://shouldly.readthedocs.io/en/latest/)
- [Moq Documentation](https://github.com/moq/moq4/wiki/Quickstart)
