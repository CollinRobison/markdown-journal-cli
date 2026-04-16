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
        var journalConfiguration = new JournalConfiguration(
            FileSystem,
            JournalSettings,
            NullLogger<JournalConfiguration>.Instance,
            fileTracking
        );
        var entryFormatter = new EntryFormatterService(JournalSettings);
        var tocService = new TableOfContentsService(
            FileSystem,
            journalConfiguration,
            JournalSettings,
            NullLogger<TableOfContentsService>.Instance
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
            NullLogger<JournalUpdateService>.Instance
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
        _console.Output.ShouldContain("--sync active");
    }
}
