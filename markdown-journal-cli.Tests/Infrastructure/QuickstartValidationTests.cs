using markdown_journal_cli.Infrastructure.Transactions;
using markdown_journal_cli.Services;
using markdown_journal_cli.Tests.Services.Rollback;
using Moq;
using Shouldly;
using Xunit;

namespace markdown_journal_cli.Tests.Infrastructure;

/// <summary>
/// Smoke tests validating the four quickstart test patterns for this project.
/// Each test instantiates the relevant base class and asserts the expected
/// infrastructure is wired up correctly.
/// </summary>
/// <remarks>
/// <para><b>Pattern 1 — Command unit test</b> (<see cref="CommandTestBase"/>)</para>
/// <para>
/// Extend <c>CommandTestBase</c>. Six mocks (<c>MockFileSystem</c>,
/// <c>MockJournalConfiguration</c>, <c>MockFileTracking</c>, <c>MockTemplateManager</c>,
/// <c>MockTableOfContentsService</c>, <c>MockEntryFormatterService</c>) and
/// <c>JournalSettings</c> are ready to use. Call <c>BuildApp(config =&gt; ...)</c>
/// for a fresh <c>CommandAppTester</c> per test — no manual <c>ServiceCollection</c>
/// or <c>TestConsole</c> required.
/// </para>
/// <para><b>Pattern 2 — Service unit test</b> (<see cref="ServiceTestBase"/>)</para>
/// <para>
/// Extend <c>ServiceTestBase</c>. Same six mocks plus <c>NoOpCoordinator</c> and
/// <c>NoOpReporter</c> are provided. Create the SUT in a private <c>CreateSut()</c>
/// factory method, passing <c>MockXxx.Object</c> and <c>NoOpCoordinator</c>/<c>NoOpReporter</c>
/// for dependencies not under test. Use <c>NullLogger&lt;T&gt;()</c> for loggers.
/// </para>
/// <para><b>Pattern 3 — Integration test</b> (<see cref="JournalIntegrationTestBase"/>)</para>
/// <para>
/// Extend <c>JournalIntegrationTestBase</c>. A unique temp directory
/// (<c>journal-{Guid}</c> under <c>Path.GetTempPath()</c>) is created on construction
/// and deleted automatically by <c>Dispose()</c>. Wire real service implementations
/// using <c>FileSystem</c>, <c>JournalPath</c>, and <c>JournalSettings</c> from the base.
/// Do NOT call <c>Directory.Delete</c> yourself. No mocks — use real implementations only.
/// </para>
/// <para><b>Pattern 4 — Rollback / fault-injection test</b> (<see cref="Services.Rollback.ServiceRollbackTestBase"/>)</para>
/// <para>
/// Extend <c>ServiceRollbackTestBase</c> (NOT <c>ServiceTestBase</c>). Call
/// <c>FileSystem.ResetCallCounts()</c> before injecting faults via
/// <c>FileSystem.InjectFaultOn(...)</c>. Assert with
/// <c>Should.Throw&lt;RollbackCompletedException&gt;()</c> for fully-rolled-back
/// scenarios or <c>Should.Throw&lt;RollbackFailedException&gt;()</c> when rollback itself fails.
/// </para>
/// <para><b>Naming convention</b></para>
/// <para>
/// All test methods must follow: <c>Method_Should_ExpectedBehavior_When_Condition</c><br/>
/// ✅ <c>Execute_Should_ReturnExitCode0_When_ValidPath</c><br/>
/// ✅ <c>AddEntry_Should_ThrowArgumentNull_When_PathIsNull</c><br/>
/// ❌ <c>TestExecute</c>, <c>AddEntry_Works</c>, <c>Should_Rollback</c>
/// </para>
/// <para><b>Which base class?</b></para>
/// <list type="table">
/// <listheader><term>Test type</term><description>Base class</description></listheader>
/// <item><term>Command unit test</term><description><see cref="CommandTestBase"/></description></item>
/// <item><term>Service unit test</term><description><see cref="ServiceTestBase"/></description></item>
/// <item><term>Integration test (real disk)</term><description><see cref="JournalIntegrationTestBase"/></description></item>
/// <item><term>Rollback / fault-injection</term><description><see cref="Services.Rollback.ServiceRollbackTestBase"/></description></item>
/// <item><term>Infrastructure unit test</term><description>Plain xUnit class (no base)</description></item>
/// </list>
/// </remarks>
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
