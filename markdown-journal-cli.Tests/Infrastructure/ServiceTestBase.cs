using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.JournalTemplates;
using markdown_journal_cli.Infrastructure.Tracking;
using markdown_journal_cli.Infrastructure.Transactions;
using markdown_journal_cli.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace markdown_journal_cli.Tests.Infrastructure;

/// <summary>
/// Abstract base class for service-layer unit tests.
/// Provides Moq mocks and NoOp transaction infrastructure for typical service tests.
/// For rollback / fault-injection tests, use <see cref="markdown_journal_cli.Tests.Services.Rollback.ServiceRollbackTestBase"/> instead.
/// </summary>
public abstract class ServiceTestBase
{
    // ── Mocks ──
    protected readonly Mock<IFileSystem> MockFileSystem;
    protected readonly Mock<IJournalConfiguration> MockJournalConfiguration;
    protected readonly Mock<IFileTracking> MockFileTracking;
    protected readonly Mock<ITemplateManager> MockTemplateManager;
    protected readonly Mock<ITableOfContentsService> MockTableOfContentsService;
    protected readonly Mock<IEntryFormatterService> MockEntryFormatterService;
    protected readonly IOptions<JournalSettings> JournalSettings;

    // ── NoOp transaction infrastructure (for tests that don't verify rollback) ──
    protected readonly IFileTransactionCoordinator NoOpCoordinator;
    protected readonly IRollbackReporter NoOpReporter;

    protected ServiceTestBase()
    {
        MockFileSystem = MockFactory.CreateFileSystem();
        MockJournalConfiguration = MockFactory.CreateJournalConfiguration();
        MockFileTracking = MockFactory.CreateFileTracking();
        MockTemplateManager = MockFactory.CreateTemplateManager();
        MockTableOfContentsService = MockFactory.CreateTableOfContentsService();
        MockEntryFormatterService = MockFactory.CreateEntryFormatterService();
        JournalSettings = MockFactory.CreateJournalSettings();

        NoOpCoordinator = NoOpFileTransactionCoordinator.Instance;
        NoOpReporter = NoOpRollbackReporter.Instance;
    }

    /// <summary>Returns a null logger. No assertions on logging output.</summary>
    protected ILogger<T> NullLogger<T>() => Microsoft.Extensions.Logging.Abstractions.NullLogger<T>.Instance;
}
