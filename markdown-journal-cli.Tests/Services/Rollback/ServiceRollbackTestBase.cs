using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.Configuration.Models;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.JournalTemplates;
using markdown_journal_cli.Infrastructure.Tracking;
using markdown_journal_cli.Infrastructure.Transactions;
using markdown_journal_cli.Services;
using markdown_journal_cli.Tests.Infrastructure.FileSystem;
using markdown_journal_cli.Tests.Infrastructure.Tracking;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Spectre.Console.Testing;

namespace markdown_journal_cli.Tests.Services.Rollback;

/// <summary>
/// Base class for service rollback fault-injection tests.
/// Provides a fully wired journal environment with a real <see cref="FileTransactionCoordinator"/>.
/// </summary>
public abstract class ServiceRollbackTestBase : IDisposable
{
    protected const string JournalPath = "/test/journal";
    protected const string JournalrcPath = "/test/journal/.journalrc";
    protected const string TrackingPath = "/test/journal/.md-journal";
    protected const string TocPath = "/test/journal/1a-TableOfContents.md";

    protected readonly FaultInjectingFileSystem FileSystem;
    protected readonly InMemoryFileBuffer Buffer;
    protected readonly InMemoryDeletionRollbackStrategy DeletionStrategy;
    protected readonly FileTransactionCoordinator Coordinator;
    protected readonly TestConsole Console;
    protected readonly IOptions<JournalSettings> JournalSettings;
    protected readonly JournalConfiguration JournalConfiguration;
    protected readonly FileTracking FileTracking;
    protected readonly TableOfContentsService TableOfContentsService;
    protected readonly EntryFormatterService EntryFormatter;
    protected readonly TestConsole RollbackConsole;
    protected readonly RollbackReporter RollbackReporter;

    protected ServiceRollbackTestBase()
    {
        FileSystem = new FaultInjectingFileSystem();
        Buffer = new InMemoryFileBuffer(FileSystem);
        DeletionStrategy = new InMemoryDeletionRollbackStrategy();
        Coordinator = new FileTransactionCoordinator(
            FileSystem,
            Buffer,
            DeletionStrategy,
            NullLoggerFactory.Instance
        );

        Console = new TestConsole();
        RollbackConsole = new TestConsole();
        RollbackReporter = new RollbackReporter(
            RollbackConsole,
            NullLogger<RollbackReporter>.Instance
        );

        JournalSettings = Options.Create(
            new JournalSettings
            {
                AppName = "md-journal",
                JournalConfigFileName = ".journalrc",
                TableOfContentsFileName = "1a-TableOfContents",
                TableOfContentsTitle = "Table of Contents",
                DateFormat = "MM/dd/yyyy",
                TitleSpaceSeparator = "_",
                HeadingSeparator = "-",
            }
        );

        var hashService = new TestHashService();
        FileTracking = new FileTracking(FileSystem, JournalSettings, hashService);
        JournalConfiguration = new JournalConfiguration(
            FileSystem,
            JournalSettings,
            NullLogger<JournalConfiguration>.Instance,
            FileTracking
        );
        TableOfContentsService = new TableOfContentsService(
            FileSystem,
            JournalConfiguration,
            JournalSettings,
            NullLogger<TableOfContentsService>.Instance
        );
        EntryFormatter = new EntryFormatterService(JournalSettings);

        SetupJournal();
    }

    protected void SetupJournal()
    {
        FileSystem.CreateDirectory(JournalPath);
        var config = new JournalConfig
        {
            JournalName = "Test Journal",
            TableOfContents = new TableOfContents
            {
                File = "1a-TableOfContents.md",
                Extensions = [".md"],
                Structure = new Structure { Topics = [] },
                RootEntries = [],
            },
        };
        JournalConfiguration.Create(JournalPath, config);
        FileTracking.LoadIndex(JournalPath);
        FileTracking.UpdateIndex(JournalPath);
        TableOfContentsService.UpdateTableOfContents(JournalPath, DateTime.Now, DateTime.Now);
        // Reset fault counters so tests inject faults cleanly from call 1
        FileSystem.ResetCallCounts();
    }

    public void Dispose()
    {
        Coordinator.Current?.Rollback();
    }
}
