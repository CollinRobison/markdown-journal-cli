[Back to README](../README.md)

# Testing Guide

This project uses xUnit + Moq + Shouldly with dedicated base classes for
command, service, integration, and rollback testing.

## Run Tests

```bash
dotnet test
```

Useful filters:

```bash
dotnet test --filter "FullyQualifiedName~UpdateCommandTests"
dotnet test --filter "FullyQualifiedName~Rollback"
```

## Test Project Layout

`markdown-journal-cli.Tests/` mirrors production code structure:

- `Commands/` command-layer tests
- `Services/` business-logic tests
- `Infrastructure/` infrastructure and test-harness tests
- `Exceptions/` exception behavior tests

When adding a new production class, add a matching test file under the mirrored
folder path.

## Base Classes

Use the base class that matches the test intent.

| Base class | Purpose | Key behavior |
| --- | --- | --- |
| `CommandTestBase` | command unit tests | pre-wired command mocks + `BuildApp(...)` |
| `ServiceTestBase` | service unit tests | pre-wired service mocks + no-op transaction/reporter |
| `JournalIntegrationTestBase` | integration tests | real file system + isolated temp journal |
| `ServiceRollbackTestBase` | rollback tests | fault-injecting file system + real transaction coordinator |

### `CommandTestBase`

Location: `markdown-journal-cli.Tests/Infrastructure/CommandTestBase.cs`

Provides pre-wired mocks for all common dependencies and a `BuildApp(Action<IConfigurator>, Action<IServiceCollection>?)` helper. See the source file for the full list of available mocks.

Pattern:

```csharp
public sealed class ExampleCommandTests : CommandTestBase
{
    protected override void SetupDefaultBehaviors()
    {
        MockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
    }

    [Fact]
    public void Execute_Should_ReturnZero_When_InputIsValid()
    {
        var app = BuildApp(
            cfg => cfg.AddCommand<MyCommand>("my"),
            services => services.AddSingleton<MyCommand>()
        );

        var result = app.Run(["my"]);

        result.ExitCode.ShouldBe(0);
    }
}
```

### `ServiceTestBase`

Location: `markdown-journal-cli.Tests/Infrastructure/ServiceTestBase.cs`

Provides the same core mocks as `CommandTestBase` plus no-op wiring for transaction coordinator, rollback reporter, and a `NullLogger<T>()` factory. See the source file for the full list.

Pattern:

```csharp
public sealed class ExampleServiceTests : ServiceTestBase
{
    private ExampleService CreateSut() =>
        new(
            MockFileSystem.Object,
            MockJournalConfiguration.Object,
            NoOpCoordinator,
            NoOpReporter,
            NullLogger<ExampleService>()
        );

    [Fact]
    public void Execute_Should_UpdateState_When_ConditionIsMet()
    {
        var sut = CreateSut();
        sut.Execute("/test");
        MockJournalConfiguration.Verify(x => x.Read("/test"), Times.Once);
    }
}
```

### `JournalIntegrationTestBase`

Location: `markdown-journal-cli.Tests/Infrastructure/JournalIntegrationTestBase.cs`

Provides:

- real `IFileSystem` implementation
- unique temp directory per test class instance
- `JournalRoot` and `JournalPath`
- `JournalSettings`
- optional `InitializeJournal()` helper for seeded journal state

Rules:

- use real implementations, not mocks
- do not manually clean up temp directories; base class `Dispose()` does it

### `ServiceRollbackTestBase`

Location: `markdown-journal-cli.Tests/Services/Rollback/ServiceRollbackTestBase.cs`

Provides rollback-focused wiring:

- `FaultInjectingFileSystem`
- real `FileTransactionCoordinator`
- `InMemoryFileBuffer`
- `InMemoryDeletionRollbackStrategy`
- `RollbackReporter`
- pre-seeded journal + metadata state

Use this for asserting transaction rollback behavior under injected failures.

## MockFactory

Location: `markdown-journal-cli.Tests/Infrastructure/MockFactory.cs`

Use `MockFactory` for quick, consistent mock setup when a base class is not the best fit. It includes preconfigured factories for all common interfaces — see the source file for the complete list.

## Command Testing with `CommandAppTester`

Location: `markdown-journal-cli.Tests/Infrastructure/CommandAppTester.cs`

The project ships its own `CommandAppTester` wrapper to capture Spectre output
via `TestConsole`.

Use result assertions on both:

- `result.ExitCode`
- `result.Output`

This is the preferred way to verify command UX messages and validation behavior.

## Rollback Assertions

Write operations are transactional. Failures may bubble as:

- `RollbackCompletedException` (rollback attempted; base command maps to exit `2/3`)

In service-level rollback tests, validate both:

- expected exception behavior
- restored file/config/tracking state after rollback

## Naming Convention

Test names should follow:

`MethodName_Should_ExpectedBehavior_When_Condition`

Examples:

- `Execute_Should_ReturnZero_When_JournalIsUpToDate`
- `RemoveEntry_Should_ThrowProtectedJournalFileException_When_TargetIsJournalrc`

## Coverage Expectations

For new behavior, include:

- happy-path test(s)
- validation/guard failure tests
- exceptional/error handling tests
- rollback tests for write-path failures (when applicable)

## Testing Checklist for PRs

- `dotnet build` passes
- `dotnet test` passes
- new/changed command or service has corresponding tests
- assertions cover exit code and user-facing output where relevant
