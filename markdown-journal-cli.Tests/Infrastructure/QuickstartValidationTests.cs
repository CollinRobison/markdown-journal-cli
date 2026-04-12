using markdown_journal_cli.Infrastructure.Transactions;
using markdown_journal_cli.Services;
using markdown_journal_cli.Tests.Services.Rollback;
using Moq;
using Shouldly;
using Xunit;

namespace markdown_journal_cli.Tests.Infrastructure;

/// <summary>
/// Smoke tests validating the four quickstart test patterns described in
/// <c>specs/002-test-suite-cleanup/quickstart.md</c>.
/// Each test simply instantiates the relevant base class and asserts that the
/// expected infrastructure is wired up correctly.
/// </summary>
public class QuickstartValidationTests
{
    /// <summary>
    /// Pattern 1 (quickstart section 1): Command unit test using CommandTestBase.
    /// Verifies the base class provides 6 mocks and JournalSettings.
    /// </summary>
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

    /// <summary>
    /// Pattern 2 (quickstart section 2): Service unit test using ServiceTestBase.
    /// Verifies the base class provides mocks, NoOpCoordinator, and NoOpReporter.
    /// </summary>
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
        sut.JournalSettings.ShouldNotBeNull();
        sut.NoOpCoordinator.ShouldNotBeNull();
        sut.NoOpReporter.ShouldNotBeNull();
    }

    /// <summary>
    /// Pattern 3 (quickstart section 3): Integration test using JournalIntegrationTestBase.
    /// Verifies the base class creates a temp directory that exists on disk.
    /// </summary>
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

    /// <summary>
    /// Guard (FR-003): Dispose() deletes the temp root created during construction.
    /// </summary>
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

    /// <summary>
    /// Guard (FR-013): Dispose() is a safe no-op when JournalRoot was already removed.
    /// </summary>
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

    /// <summary>
    /// Guard (NFR-002): Each test class instance receives a unique temp directory.
    /// </summary>
    [Fact]
    public void JournalIntegrationTestBase_Should_CreateUniqueDirectories_When_MultipleInstancesCreated()
    {
        using var sut1 = new ConcreteIntegrationTestBase();
        using var sut2 = new ConcreteIntegrationTestBase();

        sut1.JournalRoot.ShouldNotBe(sut2.JournalRoot);
        System.IO.Directory.Exists(sut1.JournalRoot).ShouldBeTrue();
        System.IO.Directory.Exists(sut2.JournalRoot).ShouldBeTrue();
    }

    /// <summary>
    /// Pattern 4 (quickstart section 4): Rollback test using ServiceRollbackTestBase.
    /// Verifies the rollback base provides a TestFileSystem and wired Coordinator.
    /// </summary>
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
        public new Mock<markdown_journal_cli.Infrastructure.FileSystem.IFileSystem> MockFileSystem => base.MockFileSystem;
        public new Mock<markdown_journal_cli.Infrastructure.Configuration.IJournalConfiguration> MockJournalConfiguration => base.MockJournalConfiguration;
        public new Mock<markdown_journal_cli.Infrastructure.Tracking.IFileTracking> MockFileTracking => base.MockFileTracking;
        public new Mock<markdown_journal_cli.Infrastructure.JournalTemplates.ITemplateManager> MockTemplateManager => base.MockTemplateManager;
        public new Mock<ITableOfContentsService> MockTableOfContentsService => base.MockTableOfContentsService;
        public new Mock<IEntryFormatterService> MockEntryFormatterService => base.MockEntryFormatterService;
        public new Microsoft.Extensions.Options.IOptions<markdown_journal_cli.JournalSettings> JournalSettings => base.JournalSettings;
    }

    private sealed class ConcreteServiceTestBase : ServiceTestBase
    {
        // Expose protected members for testing
        public new Mock<markdown_journal_cli.Infrastructure.FileSystem.IFileSystem> MockFileSystem => base.MockFileSystem;
        public new Mock<markdown_journal_cli.Infrastructure.Configuration.IJournalConfiguration> MockJournalConfiguration => base.MockJournalConfiguration;
        public new Mock<markdown_journal_cli.Infrastructure.Tracking.IFileTracking> MockFileTracking => base.MockFileTracking;
        public new Mock<markdown_journal_cli.Infrastructure.JournalTemplates.ITemplateManager> MockTemplateManager => base.MockTemplateManager;
        public new Mock<ITableOfContentsService> MockTableOfContentsService => base.MockTableOfContentsService;
        public new Mock<IEntryFormatterService> MockEntryFormatterService => base.MockEntryFormatterService;
        public new Microsoft.Extensions.Options.IOptions<markdown_journal_cli.JournalSettings> JournalSettings => base.JournalSettings;
        public new IFileTransactionCoordinator NoOpCoordinator => base.NoOpCoordinator;
        public new IRollbackReporter NoOpReporter => base.NoOpReporter;
    }

    private sealed class ConcreteIntegrationTestBase : JournalIntegrationTestBase
    {
        public ConcreteIntegrationTestBase() : base("QuickstartTest") { }

        // Expose protected members for testing
        public new string JournalRoot => base.JournalRoot;
        public new string JournalPath => base.JournalPath;
        public new markdown_journal_cli.Infrastructure.FileSystem.IFileSystem FileSystem => base.FileSystem;
        public new Microsoft.Extensions.Options.IOptions<markdown_journal_cli.JournalSettings> JournalSettings => base.JournalSettings;
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
