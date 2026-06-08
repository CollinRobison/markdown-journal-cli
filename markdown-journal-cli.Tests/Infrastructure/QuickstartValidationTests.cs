using markdown_journal_cli.Infrastructure.Transactions;
using markdown_journal_cli.Services;
using markdown_journal_cli.Tests.Services.Rollback;
using Moq;
using Shouldly;
using Xunit;

namespace markdown_journal_cli.Tests.Infrastructure;

/*
 * Smoke tests validating the four quickstart test patterns for this project.
 * Each test instantiates the relevant base class and asserts the expected
 * infrastructure is wired up correctly.
 *
 * ── Pattern 1: Command unit test (CommandTestBase) ───────────────────────────
 * Extend CommandTestBase. Six mocks (MockFileSystem, MockJournalConfiguration,
 * MockFileTracking, MockTemplateManager, MockTableOfContentsService,
 * MockEntryFormatterService) and JournalSettings are ready to use.
 * Call BuildApp(config => ...) for a fresh CommandAppTester per test —
 * no manual ServiceCollection or TestConsole required.
 *
 * ── Pattern 2: Service unit test (ServiceTestBase) ───────────────────────────
 * Extend ServiceTestBase. Same six mocks plus NoOpCoordinator and NoOpReporter
 * are provided. Create the SUT in a private CreateSut() factory method, passing
 * MockXxx.Object and NoOpCoordinator/NoOpReporter for dependencies not under
 * test. Use NullLogger<T>() for loggers.
 *
 * ── Pattern 3: Integration test (JournalIntegrationTestBase) ─────────────────
 * Extend JournalIntegrationTestBase. A unique temp directory (journal-{Guid}
 * under Path.GetTempPath()) is created on construction and deleted automatically
 * by Dispose(). Wire real service implementations using FileSystem, JournalPath,
 * and JournalSettings from the base. Do NOT call Directory.Delete yourself.
 * No mocks — use real implementations only.
 *
 * ── Pattern 4: Rollback / fault-injection test (ServiceRollbackTestBase) ─────
 * Extend ServiceRollbackTestBase (NOT ServiceTestBase). Call
 * FileSystem.ResetCallCounts() before injecting faults via
 * FileSystem.InjectFaultOn(...). Assert with
 * Should.Throw<RollbackCompletedException>() for fully-rolled-back scenarios
 * or Should.Throw<RollbackFailedException>() when rollback itself fails.
 *
 * ── Naming convention ────────────────────────────────────────────────────────
 * All test methods must follow: Method_Should_ExpectedBehavior_When_Condition
 *   ✅ Execute_Should_ReturnExitCode0_When_ValidPath
 *   ✅ AddEntry_Should_ThrowArgumentNull_When_PathIsNull
 *   ❌ TestExecute, AddEntry_Works, Should_Rollback
 *
 * ── Which base class? ────────────────────────────────────────────────────────
 *   Command unit test            → CommandTestBase
 *   Service unit test            → ServiceTestBase
 *   Integration test (real disk) → JournalIntegrationTestBase
 *   Rollback / fault-injection   → ServiceRollbackTestBase
 *   Infrastructure unit test     → plain xUnit class (no base)
 */
public class QuickstartValidationTests
{
    [Fact]
    public void CommandTestBase_Should_ProvideAllRequiredMocks_When_Instantiated()
    {
        var sut = new ConcreteCommandTestBase();

        sut.MockFileSystem.ShouldNotBeNull();
        sut.MockJournalConfiguration.ShouldNotBeNull();
        sut.MockFileTracking.ShouldNotBeNull();
        sut.MockTemplateManager.ShouldNotBeNull();
        sut.MockTableOfContentsService.ShouldNotBeNull();
        sut.MockEntryFormatterService.ShouldNotBeNull();
        sut.JournalSettings.ShouldNotBeNull();
        sut.JournalSettings.Value.ShouldNotBeNull();
    }

    [Fact]
    public void ServiceTestBase_Should_ProvideAllRequiredInfrastructure_When_Instantiated()
    {
        var sut = new ConcreteServiceTestBase();

        sut.MockFileSystem.ShouldNotBeNull();
        sut.MockJournalConfiguration.ShouldNotBeNull();
        sut.MockFileTracking.ShouldNotBeNull();
        sut.MockTemplateManager.ShouldNotBeNull();
        sut.MockTableOfContentsService.ShouldNotBeNull();
        sut.MockEntryFormatterService.ShouldNotBeNull();
        sut.MockTocStructureRepository.ShouldNotBeNull();
        sut.JournalSettings.ShouldNotBeNull();
        sut.NoOpCoordinator.ShouldNotBeNull();
        sut.NoOpReporter.ShouldNotBeNull();
    }

    [Fact]
    public void JournalIntegrationTestBase_Should_CreateTempDirectory_When_Instantiated()
    {
        using var sut = new ConcreteIntegrationTestBase();

        sut.JournalRoot.ShouldNotBeNullOrEmpty();
        sut.JournalPath.ShouldNotBeNullOrEmpty();
        System.IO.Directory.Exists(sut.JournalRoot).ShouldBeTrue();
        System.IO.Directory.Exists(sut.JournalPath).ShouldBeTrue();
        sut.FileSystem.ShouldNotBeNull();
        sut.JournalSettings.ShouldNotBeNull();
    }

    [Fact]
    public void JournalIntegrationTestBase_Should_DeleteTempRoot_When_Disposed()
    {
        string journalRoot;
        using (var sut = new ConcreteIntegrationTestBase())
        {
            journalRoot = sut.JournalRoot;
            System.IO.Directory.Exists(journalRoot).ShouldBeTrue();
        }
        System.IO.Directory.Exists(journalRoot).ShouldBeFalse();
    }

    [Fact]
    public void JournalIntegrationTestBase_Should_NotThrow_When_DisposeCalledAndRootDoesNotExist()
    {
        var sut = new ConcreteIntegrationTestBase();
        var journalRoot = sut.JournalRoot;

        if (System.IO.Directory.Exists(journalRoot))
            System.IO.Directory.Delete(journalRoot, recursive: true);

        Should.NotThrow(() => sut.Dispose());
        System.IO.Directory.Exists(journalRoot).ShouldBeFalse();
    }

    [Fact]
    public void JournalIntegrationTestBase_Should_CreateUniqueDirectories_When_MultipleInstancesCreated()
    {
        using var sut1 = new ConcreteIntegrationTestBase();
        using var sut2 = new ConcreteIntegrationTestBase();

        sut1.JournalRoot.ShouldNotBe(sut2.JournalRoot);
        System.IO.Directory.Exists(sut1.JournalRoot).ShouldBeTrue();
        System.IO.Directory.Exists(sut2.JournalRoot).ShouldBeTrue();
    }

    [Fact]
    public void JournalIntegrationTestBase_Should_CreateMetadataDirWithFiles_When_InitializeJournalCalled()
    {
        using var sut = new ConcreteIntegrationTestBase();

        sut.InitializeJournal();

        var metadataDir = System.IO.Path.Combine(sut.JournalPath, ".mdjournal");
        System.IO.Directory.Exists(metadataDir).ShouldBeTrue();
        System.IO.File.Exists(System.IO.Path.Combine(metadataDir, ".journalindex")).ShouldBeTrue();
        System.IO.File.Exists(System.IO.Path.Combine(metadataDir, ".journaltoc")).ShouldBeTrue();
    }

    [Fact]
    public void JournalIntegrationTestBase_Should_WriteNoStructureOrRootEntriesToJournalrc_When_InitializeJournalCalled()
    {
        using var sut = new ConcreteIntegrationTestBase();

        sut.InitializeJournal();

        var journalrcPath = System.IO.Path.Combine(sut.JournalPath, ".journalrc");
        var json = System.IO.File.ReadAllText(journalrcPath);
        json.ShouldNotContain("\"structure\"");
        json.ShouldNotContain("\"rootEntries\"");
    }

    [Fact]
    public void ServiceRollbackTestBase_Should_ProvideTestFileSystem_When_Instantiated()
    {
        using var sut = new ConcreteRollbackTestBase();

        sut.AssertInfrastructureWired();
    }

    // ── Concrete subclasses for instantiation ────────────────────────────────

    private sealed class ConcreteCommandTestBase : CommandTestBase
    {
        // Expose protected members for testing
        public new Mock<markdown_journal_cli.Infrastructure.FileSystem.IFileSystem> MockFileSystem =>
            base.MockFileSystem;
        public new Mock<markdown_journal_cli.Infrastructure.Configuration.IJournalConfiguration> MockJournalConfiguration =>
            base.MockJournalConfiguration;
        public new Mock<markdown_journal_cli.Infrastructure.Tracking.IFileTracking> MockFileTracking =>
            base.MockFileTracking;
        public new Mock<markdown_journal_cli.Infrastructure.JournalTemplates.ITemplateManager> MockTemplateManager =>
            base.MockTemplateManager;
        public new Mock<ITableOfContentsService> MockTableOfContentsService =>
            base.MockTableOfContentsService;
        public new Mock<IEntryFormatterService> MockEntryFormatterService =>
            base.MockEntryFormatterService;
        public new Microsoft.Extensions.Options.IOptions<markdown_journal_cli.JournalSettings> JournalSettings =>
            base.JournalSettings;
    }

    private sealed class ConcreteServiceTestBase : ServiceTestBase
    {
        // Expose protected members for testing
        public new Mock<markdown_journal_cli.Infrastructure.FileSystem.IFileSystem> MockFileSystem =>
            base.MockFileSystem;
        public new Mock<markdown_journal_cli.Infrastructure.Configuration.IJournalConfiguration> MockJournalConfiguration =>
            base.MockJournalConfiguration;
        public new Mock<markdown_journal_cli.Infrastructure.Tracking.IFileTracking> MockFileTracking =>
            base.MockFileTracking;
        public new Mock<markdown_journal_cli.Infrastructure.JournalTemplates.ITemplateManager> MockTemplateManager =>
            base.MockTemplateManager;
        public new Mock<ITableOfContentsService> MockTableOfContentsService =>
            base.MockTableOfContentsService;
        public new Mock<IEntryFormatterService> MockEntryFormatterService =>
            base.MockEntryFormatterService;
        public new Mock<markdown_journal_cli.Infrastructure.Configuration.IJournalTocStructureRepository> MockTocStructureRepository =>
            base.MockTocStructureRepository;
        public new Microsoft.Extensions.Options.IOptions<markdown_journal_cli.JournalSettings> JournalSettings =>
            base.JournalSettings;
        public new IFileTransactionCoordinator NoOpCoordinator => base.NoOpCoordinator;
        public new IRollbackReporter NoOpReporter => base.NoOpReporter;
    }

    private sealed class ConcreteIntegrationTestBase : JournalIntegrationTestBase
    {
        public ConcreteIntegrationTestBase()
            : base("QuickstartTest") { }

        // Expose protected members for testing
        public new string JournalRoot => base.JournalRoot;
        public new string JournalPath => base.JournalPath;
        public new markdown_journal_cli.Infrastructure.FileSystem.IFileSystem FileSystem =>
            base.FileSystem;
        public new Microsoft.Extensions.Options.IOptions<markdown_journal_cli.JournalSettings> JournalSettings =>
            base.JournalSettings;

        public new void InitializeJournal() => base.InitializeJournal();
    }

    private sealed class ConcreteRollbackTestBase : ServiceRollbackTestBase
    {
        /// <summary>Asserts required infrastructure members are not null (accessed from within derived class).</summary>
        public void AssertInfrastructureWired()
        {
            FileSystem.ShouldNotBeNull();
            Buffer.ShouldNotBeNull();
            Coordinator.ShouldNotBeNull();
            JournalSettings.ShouldNotBeNull();
            FileTracking.ShouldNotBeNull();
            JournalConfiguration.ShouldNotBeNull();
            RollbackReporter.ShouldNotBeNull();
        }
    }
}
