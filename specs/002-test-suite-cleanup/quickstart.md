# Quickstart: Writing Tests in markdown-journal-cli

**Feature**: `002-test-suite-cleanup`  
**Audience**: Any developer adding or modifying tests  
**Date**: 2026-04-11

---

## 1. Writing a Command Unit Test

Command tests exercise a single command in isolation, with all service dependencies mocked.

**Step 1** — Extend `CommandTestBase`:

```csharp
public class InitCommandTests : CommandTestBase
{
    [Fact]
    public void Execute_Should_CreateJournalDirectory_When_PathIsValid()
    {
        // Arrange: override default mock behavior for this specific scenario
        MockFileSystem
            .Setup(fs => fs.FileExists(It.Is<string>(s => s.EndsWith(".journalrc"))))
            .Returns(false); // journal does not exist yet

        MockJournalConfiguration
            .Setup(jc => jc.CreateConfiguration(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(true);

        // Get a fresh CommandAppTester — new output buffer every call
        var app = BuildApp(config =>
        {
            config.SetApplicationName("mdjournal");
            config.AddCommand<InitJournalCommand>("init");
        });

        // Act
        var result = app.Run(["init", "--path", "/tmp/journals", "MyJournal"]);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.Output.ShouldContain("Created");
        MockFileSystem.Verify(fs => fs.CreateDirectory(It.IsAny<string>()), Times.Once);
    }
}
```

**What you do NOT need to do:**
- Create your own `Mock<IFileSystem>` — it's already in `CommandTestBase`
- Set up a `ServiceCollection` manually — `BuildApp()` does it
- Create a `TestConsole` — embedded in `CommandAppTester`

---

## 2. Writing a Service Unit Test

Service tests exercise a single service in isolation, with all its dependencies mocked.

**Step 1** — Extend `ServiceTestBase`:

```csharp
public class JournalEntryServiceTests : ServiceTestBase
{
    private JournalEntryService CreateSut()
    {
        return new JournalEntryService(
            MockFileSystem.Object,
            MockJournalConfiguration.Object,
            JournalSettings,
            MockEntryFormatterService.Object,
            MockTemplateManager.Object,
            MockFileTracking.Object,
            MockTableOfContentsService.Object,
            NoOpCoordinator,  // not testing transactions
            NoOpReporter,     // not testing rollback
            NullLogger<JournalEntryService>()
        );
    }

    [Fact]
    public void AddEntry_Should_CreateMarkdownFile_When_ValidInput()
    {
        // Arrange
        MockFileSystem
            .Setup(fs => fs.FileExists(It.Is<string>(s => s.EndsWith(".journalrc"))))
            .Returns(true);
        MockTemplateManager
            .Setup(tm => tm.GetEntryTemplate(It.IsAny<string>()))
            .Returns("# {title}");
        MockFileSystem
            .Setup(fs => fs.CreateMarkdownFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(true);
        // ... other required setups

        var sut = CreateSut();

        // Act
        sut.AddEntry("/test/journal", false, "My Entry", null, null, null);

        // Assert
        MockFileSystem.Verify(
            fs => fs.CreateMarkdownFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
    }
}
```

**Rules:**
- `NoOpCoordinator` and `NoOpReporter` are for tests that do **not** test transactions — use them here.
- For tests that verify rollback behavior, use `ServiceRollbackTestBase` (see section 4).
- Factory method `CreateSut()` should be a private method — keeps test bodies readable.

---

## 3. Writing a Command Integration Test

Integration tests run the real CLI pipeline against a real temporary directory on disk.

**Step 1** — Extend `JournalIntegrationTestBase`:

```csharp
public class InitCommandIntegrationTests : JournalIntegrationTestBase
{
    // Wire real services — no mocks
    private readonly IInitJournalService _initJournalService;
    private readonly CommandAppTester _app;

    public InitCommandIntegrationTests() : base(journalName: "InitTest")
    {
        // Use real implementations — same wiring as production
        _initJournalService = new InitJournalService(
            FileSystem,
            JournalSettings,
            new TemplateManager(JournalSettings),
            new FileTransactionCoordinator(/* ... */),
            new RollbackReporter(/* ... */),
            NullLoggerFactory.Instance.CreateLogger<InitJournalService>()
        );

        var services = new ServiceCollection();
        services.AddSingleton<IInitJournalService>(_initJournalService);
        services.AddSingleton<IAnsiConsole>(new TestConsole());

        var registrar = new TypeRegistrar(services);
        _app = new CommandAppTester(registrar);
        _app.Configure(config =>
        {
            config.SetApplicationName("mdjournal");
            config.AddCommand<InitJournalCommand>("init");
        });
    }

    [Fact]
    public void Execute_Should_CreateJournalStructure_When_PathIsValid()
    {
        // Act — use the real temp directory created by the base class
        var result = _app.Run(["init", "--path", JournalRoot, "InitTest"]);

        // Assert — check real files on disk
        result.ExitCode.ShouldBe(0);
        File.Exists(Path.Combine(JournalPath, ".journalrc")).ShouldBeTrue();
        File.Exists(Path.Combine(JournalPath, "1a-TableOfContents.md")).ShouldBeTrue();
        // Cleanup is automatic — the base Dispose() deletes JournalRoot
    }
}
```

**Rules:**
- MUST use NO mock objects unless there is no real implementation available.
- MUST NOT call `Directory.Delete` yourself — `Dispose()` does it.
- Each test class gets its own unique temp directory (new `Guid` per construction).

---

## 4. Writing a Rollback Test

Rollback tests verify that a service restores state correctly when a fault occurs mid-operation.

**Step 1** — Extend `ServiceRollbackTestBase` (existing, unchanged):

```csharp
public class InitJournalServiceRollbackTests : ServiceRollbackTestBase
{
    private InitJournalService CreateService() =>
        new(FileSystem, JournalSettings, CreateDefaultTemplateManager(),
            Coordinator, RollbackReporter, NullLogger<InitJournalService>.Instance);

    [Fact]
    public void Init_Should_RollbackCreatedFiles_When_ConfigWriteFails()
    {
        // Arrange: fail on the 2nd file write (e.g., .journalrc write after directory create)
        FileSystem.ResetCallCounts();
        FileSystem.InjectFaultOn(FaultInjectPoint.CreateFile, 2, new IOException("Disk full"));

        var service = CreateService();

        // Act
        Should.Throw<RollbackCompletedException>(() =>
            service.InitializeJournal(JournalPath, "TestJournal")
        );

        // Assert: all created files were deleted by rollback
        FileSystem.GetAllFiles().ShouldBeEmpty();
    }
}
```

**Rules:**
- MUST extend `ServiceRollbackTestBase` — do NOT extend `ServiceTestBase`.
- `FileSystem.ResetCallCounts()` MUST be called before injecting faults.
- Use `Should.Throw<RollbackCompletedException>()` for fully-rolled-back scenarios.
- Use `Should.Throw<RollbackFailedException>()` for scenarios where rollback itself fails.

---

## 5. Test Naming Convention

All test methods MUST follow:

```
{MethodOrScenario}_Should_{ExpectedBehavior}_When_{Condition}
```

| ✅ Correct | ❌ Incorrect |
|---|---|
| `Execute_Should_ReturnExitCode0_When_ValidPath` | `TestExecute` |
| `AddEntry_Should_ThrowArgumentNull_When_PathIsNull` | `AddEntry_Works` |
| `RemoveEntry_Should_UndoChanges_When_ConfigWriteFails` | `Should_Rollback` |

---

## 6. Quick Reference: Which Base Class Do I Use?

| Test type | Base class |
|---|---|
| Command unit test | `CommandTestBase` |
| Service unit test | `ServiceTestBase` |
| Command or service integration test (real disk) | `JournalIntegrationTestBase` |
| Service rollback / fault-injection test | `ServiceRollbackTestBase` |
| Infrastructure unit test (FileSystem, Transactions, etc.) | None — plain xUnit class |

---

## 7. Running the Tests

```bash
# Run all tests
dotnet test

# Run tests for a specific namespace
dotnet test --filter "FullyQualifiedName~Commands.Init"

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```
