using markdown_journal_cli.Commands.Update;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.DependencyInjection;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.JournalTemplates;
using markdown_journal_cli.Infrastructure.Tracking;
using markdown_journal_cli.Infrastructure.Transactions;
using markdown_journal_cli.Services;
using markdown_journal_cli.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using Xunit;

namespace markdown_journal_cli.Tests.Commands.Update;

/// <summary>
/// Integration tests for the update command using real services and file operations.
/// The full CLI pipeline is exercised against a real temp directory with no mocked dependencies.
/// </summary>
[Trait("Category", "Integration")]
public class UpdateCommandIntegrationTests : JournalIntegrationTestBase
{
    private readonly CommandAppTester _app;
    private readonly TestConsole _console;

    public UpdateCommandIntegrationTests() : base("UpdateTest")
    {
        InitializeJournal();

        var hashService = new HashService();
        var fileTracking = new FileTracking(FileSystem, JournalSettings, hashService);
        var tocStructureRepository = new JournalTocStructureRepository(FileSystem, JournalSettings);
        var journalConfiguration = new JournalConfiguration(
            FileSystem,
            JournalSettings,
            NullLogger<JournalConfiguration>.Instance,
            fileTracking,
            tocStructureRepository
        );
        var entryFormatter = new EntryFormatterService(JournalSettings);
        var tocService = new TableOfContentsService(
            FileSystem,
            journalConfiguration,
            JournalSettings,
            NullLogger<TableOfContentsService>.Instance,
            tocStructureRepository
        );
        var linkRewriter = new MarkdownLinkRewriter(FileSystem, NullLogger<MarkdownLinkRewriter>.Instance);
        var buffer = new InMemoryFileBuffer(FileSystem);
        var deletionStrategy = new InMemoryDeletionRollbackStrategy();
        var coordinator = new FileTransactionCoordinator(
            FileSystem, buffer, deletionStrategy, NullLoggerFactory.Instance
        );
        _console = new TestConsole();
        var rollbackReporter = new RollbackReporter(_console, NullLogger<RollbackReporter>.Instance);

        var journalUpdateService = new JournalUpdateService(
            _console,
            FileSystem,
            journalConfiguration,
            fileTracking,
            tocService,
            JournalSettings,
            linkRewriter,
            coordinator,
            rollbackReporter,
            NullLogger<JournalUpdateService>.Instance,
            tocStructureRepository
        );
        var dryRunRenderer = new DryRunRenderer(_console, journalConfiguration, JournalSettings);

        var templateManager = new TemplateManager(JournalSettings);
        var journalEntryService = new JournalEntryService(
            FileSystem,
            journalConfiguration,
            JournalSettings,
            entryFormatter,
            templateManager,
            fileTracking,
            tocService,
            NoOpFileTransactionCoordinator.Instance,
            NoOpRollbackReporter.Instance,
            NullLogger<JournalEntryService>.Instance
        );

        // Seed an entry so the journal has something to update
        journalEntryService.AddEntry(JournalPath, false, "Alpha", null, null, null);

        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(_console);
        services.AddSingleton<IFileSystem>(FileSystem);
        services.AddSingleton<IJournalUpdateService>(journalUpdateService);
        services.AddSingleton<IFileTracking>(fileTracking);
        services.AddSingleton<IJournalConfiguration>(journalConfiguration);
        services.AddSingleton<IDryRunRenderer>(dryRunRenderer);
        services.AddSingleton<IFileTransactionCoordinator>(coordinator);
        services.AddSingleton(JournalSettings);
        services.AddSingleton<ILogger<UpdateCommand>>(NullLogger<UpdateCommand>.Instance);

        var registrar = new TypeRegistrar();
        foreach (var sd in services)
        {
            if (sd.ImplementationInstance != null)
                registrar.RegisterInstance(sd.ServiceType, sd.ImplementationInstance);
            else if (sd.ImplementationType != null)
                registrar.Register(sd.ServiceType, sd.ImplementationType);
        }

        _app = new CommandAppTester(registrar);
        _app.Configure(config =>
        {
            config.SetApplicationName("mdjournal");
            config.AddBranch<UpdateSettings>("update", update =>
            {
                update.AddCommand<UpdateCommand>("journal");
            });
        });
    }

    [Fact]
    public void Execute_Should_ReturnExitCode0_When_JournalIsInitialized()
    {
        // Act
        var result = _app.Run(["update", "--path", JournalPath, "journal", "--toc"]);

        // Assert
        result.ExitCode.ShouldBe(0);
    }

    [Fact]
    public void Execute_Should_UpdateTableOfContents_When_TocFlagPassed()
    {
        // Arrange — record initial TOC content
        var tocPath = Path.Combine(JournalPath, "1a-TableOfContents.md");
        var initialContent = File.ReadAllText(tocPath);

        // Act
        var result = _app.Run(["update", "--path", JournalPath, "journal", "--toc"]);

        // Assert
        result.ExitCode.ShouldBe(0);
        var updatedContent = File.ReadAllText(tocPath);
        updatedContent.ShouldContain("Alpha");
    }

    [Fact]
    public void UpdateJournal_Should_NotModifyEntryLastEditedDates_When_SyncFlag()
    {
        // Arrange — find the Alpha entry file and read its Last Edited date
        var entryFile = Directory
            .GetFiles(JournalPath, "*.md", SearchOption.AllDirectories)
            .First(f => !Path.GetFileName(f).StartsWith("1a-") && !Path.GetFileName(f).StartsWith("1b-") && !Path.GetFileName(f).StartsWith("1c-"));
        var originalContent = File.ReadAllText(entryFile);

        // Corrupt the tracking hash so there are changes to sync
        var trackingPath = Path.Combine(JournalPath, ".md-journal");
        File.WriteAllText(trackingPath, "{}");

        // Act
        var result = _app.Run(["update", "--path", JournalPath, "journal", "--sync"]);

        // Assert — exit code 0 and entry file content unchanged
        result.ExitCode.ShouldBe(0);
        var contentAfter = File.ReadAllText(entryFile);
        contentAfter.ShouldBe(originalContent);
    }

    [Fact]
    public void UpdateJournal_Should_UpdateTrackingAndConfig_When_SyncFlag()
    {
        // Arrange — corrupt the tracking hash so --sync has work to do
        var trackingPath = Path.Combine(JournalPath, ".md-journal");
        File.WriteAllText(trackingPath, "{}");

        // Act
        var result = _app.Run(["update", "--path", JournalPath, "journal", "--sync"]);

        // Assert — exit code 0, tracking index no longer empty, config still valid
        result.ExitCode.ShouldBe(0);
        var trackingContent = File.ReadAllText(trackingPath);
        trackingContent.ShouldNotBe("{}");
        result.Output.ShouldContain("--sync active");
    }

    [Fact]
    public void UpdateJournal_Should_ReturnZeroAndPrintUpToDate_When_SyncFlagAndJournalCurrent()
    {
        // Arrange — first run a full sync so the journal is up to date
        _app.Run(["update", "--path", JournalPath, "journal", "--sync"]);

        // Act — second run should be a no-op
        var result = _app.Run(["update", "--path", JournalPath, "journal", "--sync"]);

        // Assert — exit 0 and the "up to date" no-op message appears (confirming the no-op branch ran)
        result.ExitCode.ShouldBe(0);
        result.Output.ShouldContain("Everything is up to date.");
    }

    [Fact]
    public void UpdateJournal_Should_AddNewEntryToTracking_When_SyncFlagAndNewFile()
    {
        // Arrange — first sync to stabilise, then drop a new raw .md file
        _app.Run(["update", "--path", JournalPath, "journal", "--sync"]);
        var newFilePath = Path.Combine(JournalPath, "New_Entry.md");
        File.WriteAllText(newFilePath,
            "Created: 01/01/2024\nLast Edited: 01/01/2024\n\n# New Entry\n\nContent.\n");
        var contentBefore = File.ReadAllText(newFilePath);

        // Act
        var result = _app.Run(["update", "--path", JournalPath, "journal", "--sync"]);

        // Assert — exit 0, file content unchanged (no "Last Edited:" stamp written)
        result.ExitCode.ShouldBe(0);
        File.ReadAllText(newFilePath).ShouldBe(contentBefore);
        // Tracking index must now reference the new file
        var trackingContent = File.ReadAllText(Path.Combine(JournalPath, ".md-journal"));
        trackingContent.ShouldContain("New_Entry");
    }

    [Fact]
    public void UpdateJournal_Should_RemoveDeletedEntryFromTracking_When_SyncFlagAndDeletedFile()
    {
        // Arrange — sync once to register Alpha, then delete it
        _app.Run(["update", "--path", JournalPath, "journal", "--sync"]);
        var entryFile = Directory
            .GetFiles(JournalPath, "*.md", SearchOption.AllDirectories)
            .First(f => !Path.GetFileName(f).StartsWith("1a-") && !Path.GetFileName(f).StartsWith("1b-") && !Path.GetFileName(f).StartsWith("1c-"));
        File.Delete(entryFile);

        // Act
        var result = _app.Run(["update", "--path", JournalPath, "journal", "--sync"]);

        // Assert — exit 0, deleted file no longer in tracking index
        result.ExitCode.ShouldBe(0);
        var trackingContent = File.ReadAllText(Path.Combine(JournalPath, ".md-journal"));
        trackingContent.ShouldNotContain(Path.GetFileNameWithoutExtension(entryFile));
    }
}
