# Contracts: Shared Test Infrastructure API

**Feature**: `002-test-suite-cleanup`  
**Phase**: 1 — Design  
**Date**: 2026-04-11

---

## Overview

For a test project, "contracts" define the public API surface of the shared test infrastructure that all test authors will use. These contracts ensure that:
1. Tests written by different developers follow the same patterns
2. Changes to the shared infrastructure have a defined impact surface
3. New test authors have a clear, minimal API to learn

---

## Contract 1: `CommandTestBase`

**Namespace**: `MarkdownJournalCli.Tests.Infrastructure`

```csharp
/// <summary>
/// Abstract base class for command-layer unit tests.
/// Provides pre-constructed Moq mocks for all standard command dependencies.
/// Override SetupDefaultBehaviors() to change defaults for the entire test class.
/// Call BuildApp(configure) in each test or in a helper to get a fresh CommandAppTester.
/// </summary>
public abstract class CommandTestBase
{
    // ── Mocks (created once; Set up defaults in SetupDefaultBehaviors) ──
    protected readonly Mock<IFileSystem>                MockFileSystem;
    protected readonly Mock<IJournalConfiguration>      MockJournalConfiguration;
    protected readonly Mock<IFileTracking>              MockFileTracking;
    protected readonly Mock<ITemplateManager>           MockTemplateManager;
    protected readonly Mock<ITableOfContentsService>    MockTableOfContentsService;
    protected readonly Mock<IEntryFormatterService>     MockEntryFormatterService;
    protected readonly IOptions<JournalSettings>        JournalSettings;

    protected CommandTestBase();                       // calls SetupDefaultBehaviors()

    /// <summary>
    /// Override to configure mock defaults for the whole test class.
    /// Called by the base constructor; subclass can call base.SetupDefaultBehaviors().
    /// </summary>
    protected virtual void SetupDefaultBehaviors();

    /// <summary>
    /// Creates a fresh CommandAppTester with all current mock objects registered.
    /// Must be called per-test (NOT shared across tests) to get a clean output buffer.
    /// </summary>
    /// <param name="configure">Configure the Spectre.Console command tree (add commands/branches).</param>
    protected CommandAppTester BuildApp(Action<IConfigurator> configure);
}
```

**Usage contract:**
- Subclasses MUST call `BuildApp()` inside each test method (or a per-test factory helper), not in the constructor.
- Subclasses MAY override `SetupDefaultBehaviors()` to add scenario-specific defaults.
- Subclasses SHOULD use `Mock.Verify()` after act to assert service calls.

---

## Contract 2: `ServiceTestBase`

**Namespace**: `MarkdownJournalCli.Tests.Infrastructure`

```csharp
/// <summary>
/// Abstract base class for service-layer unit tests.
/// Provides Moq mocks and NoOp transaction infrastructure for typical service tests.
/// For rollback / fault-injection tests, use ServiceRollbackTestBase instead.
/// </summary>
public abstract class ServiceTestBase
{
    // ── Mocks ──
    protected readonly Mock<IFileSystem>                MockFileSystem;
    protected readonly Mock<IJournalConfiguration>      MockJournalConfiguration;
    protected readonly Mock<IFileTracking>              MockFileTracking;
    protected readonly Mock<ITemplateManager>           MockTemplateManager;
    protected readonly Mock<ITableOfContentsService>    MockTableOfContentsService;
    protected readonly Mock<IEntryFormatterService>     MockEntryFormatterService;
    protected readonly IOptions<JournalSettings>        JournalSettings;

    // ── NoOp transaction infrastructure (for tests that don't verify rollback) ──
    protected readonly IFileTransactionCoordinator      NoOpCoordinator;   // NoOpFileTransactionCoordinator.Instance
    protected readonly IRollbackReporter                NoOpReporter;       // NoOpRollbackReporter.Instance

    // ── Logger (null logger — no assertions on logging) ──
    protected ILogger<T> NullLogger<T>();                // NullLogger<T>.Instance

    protected ServiceTestBase();
}
```

**Usage contract:**
- Subclasses instantiate the concrete service under test in each test method, passing `MockXxx.Object` and `NoOpCoordinator` / `NoOpReporter`.
- This base class MUST NOT be used for rollback tests — use `ServiceRollbackTestBase`.
- `NoOpCoordinator` / `NoOpReporter` MUST NOT be replaced with Moq mocks unless the test explicitly verifies transaction behavior.

---

## Contract 3: `MockFactory`

**Namespace**: `MarkdownJournalCli.Tests.Infrastructure`

```csharp
/// <summary>
/// Static factory for creating pre-configured Mock&lt;T&gt; instances.
/// Returns Mock&lt;T&gt; (not .Object) so tests can add Setup() and Verify() calls.
/// All mocks use MockBehavior.Strict unless documented otherwise.
/// </summary>
public static class MockFactory
{
    public static Mock<IFileSystem>                 CreateFileSystem();
    public static Mock<IJournalConfiguration>       CreateJournalConfiguration();
    public static Mock<IFileTracking>               CreateFileTracking();
    public static Mock<ITemplateManager>            CreateTemplateManager();
    public static Mock<ITableOfContentsService>     CreateTableOfContentsService();
    public static Mock<IEntryFormatterService>      CreateEntryFormatterService();
    public static IOptions<JournalSettings>         CreateJournalSettings(
                                                        string journalPath = "/test/journal",
                                                        string journalName = "TestJournal");
}
```

**Usage contract:**
- Base classes call `MockFactory.CreateXxx()` in their constructors.
- Tests MAY call factories directly for one-off mock creation outside a base class.
- Pre-configured defaults are documented per factory method; tests override only what they test.
- Factories MUST be updated when production interface signatures change; this is the single-edit point.

---

## Contract 4: `JournalIntegrationTestBase`

**Namespace**: `MarkdownJournalCli.Tests.Infrastructure`

```csharp
/// <summary>
/// Abstract base class for command integration tests.
/// Creates a unique temp directory, seeds a journal, and provides real service instances.
/// Cleans up the temp directory on Dispose() regardless of test outcome.
/// </summary>
public abstract class JournalIntegrationTestBase : IDisposable
{
    /// <summary>Root temp directory for this test class instance.</summary>
    protected readonly string JournalRoot;

    /// <summary>Journal subdirectory path (JournalRoot/JournalName).</summary>
    protected readonly string JournalPath;

    /// <summary>Real FileSystem instance backed by real disk I/O.</summary>
    protected readonly IFileSystem FileSystem;

    /// <summary>JournalSettings wired to JournalPath.</summary>
    protected readonly IOptions<JournalSettings> JournalSettings;

    /// <param name="journalName">Name of the journal subfolder (default: "TestJournal").</param>
    protected JournalIntegrationTestBase(string journalName = "TestJournal");

    /// <summary>
    /// Seeds the journal directory with .journalrc, .mdjournal, and 1a-TableOfContents.md.
    /// Call from subclass constructor after registering services if you need a pre-initialized journal.
    /// </summary>
    protected void InitializeJournal();

    /// <summary>Deletes JournalRoot and all contents. Always runs, even if a test fails.</summary>
    public void Dispose();
}
```

**Usage contract:**
- Subclasses MUST call `base(journalName)` or accept the default.
- `InitializeJournal()` is optional — only call it if the test requires a pre-seeded journal.
- Integration tests MUST use NO mocks unless there is no real implementation available.
- All temp file teardown happens automatically via `Dispose()` — subclasses MUST NOT delete `JournalRoot` themselves.

---

## Contract 5: Existing `ServiceRollbackTestBase` (preservation contract)

**Namespace**: `MarkdownJournalCli.Tests.Services.Rollback`

This base class is FROZEN for this feature — no changes permitted. New rollback tests for any service MUST extend `ServiceRollbackTestBase` without modification to the base.

```csharp
// (existing signature — do not change)
public abstract class ServiceRollbackTestBase : IDisposable
{
    protected const string JournalPath = "/test/journal";
    protected readonly FaultInjectingFileSystem FileSystem;
    protected readonly FileTransactionCoordinator Coordinator;
    protected readonly RollbackReporter RollbackReporter;
    // ... other stable fields

    protected void SetupJournal();
    public void Dispose();
}
```

---

## Contract 6: Test Naming Convention (enforced)

All test method names in the migrated and new test files MUST follow:

```
{MethodOrScenario}_Should_{ExpectedBehavior}_When_{Condition}
```

**Examples:**
```
Execute_Should_CreateJournalDirectory_When_PathIsValid
Execute_Should_ReturnExitCode1_When_JournalAlreadyExists
AddEntry_Should_CreateMarkdownFile_When_ValidEntryName
RemoveEntry_Should_ThrowFileNotFoundException_When_EntryDoesNotExist
```

**Naming contract violations (will be fixed in quality pass):**
- Names with no `_Should_` or `_When_` segment
- Names that are a single word (e.g., `Test`, `Works`)
- Names describing implementation rather than behavior

---

## Change Impact Surface

When the **production code changes**, only these files need updating:

| Production change | Test file(s) to update |
|---|---|
| Service adds a new constructor parameter | `MockFactory.cs` + the relevant `ServiceTestBase` or `CommandTestBase` field |
| Service is renamed or split | `MockFactory.cs` + affected test classes |
| New command added | Create new `*CommandTests.cs` + `*CommandIntegrationTests.cs` (subclass existing bases) |
| Integration test adds new service dependency | `JournalIntegrationTestBase.cs` |
| Rollback scenario added | Add new test to the relevant `*ServiceRollbackTests.cs` (extend existing `ServiceRollbackTestBase`) |
