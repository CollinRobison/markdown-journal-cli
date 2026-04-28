using markdown_journal_cli.Commands.Remove;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.DependencyInjection;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.JournalTemplates;
using markdown_journal_cli.Infrastructure.Tracking;
using markdown_journal_cli.Infrastructure.Transactions;
using markdown_journal_cli.Services;
using markdown_journal_cli.Services.RemoveEntry;
using markdown_journal_cli.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using Xunit;

namespace markdown_journal_cli.Tests.Commands.Remove;

/// <summary>
/// Integration tests for the remove command using real services and file operations.
/// The full CLI pipeline is exercised against a real temp directory with no mocked dependencies.
/// </summary>
[Trait("Category", "Integration")]
public class RemoveEntryCommandIntegrationTests : JournalIntegrationTestBase
{
    private readonly CommandAppTester _app;
    private readonly TestConsole _console;

    public RemoveEntryCommandIntegrationTests() : base("RemoveTest")
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
        var templateManager = new TemplateManager(JournalSettings);
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

        // Use JournalEntryService to seed entries for removal tests
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
        journalEntryService.AddEntry(JournalPath, false, "Alpha", null, null, null);

        var removeEntryService = new RemoveEntryService(
            FileSystem,
            journalConfiguration,
            fileTracking,
            tocService,
            linkRewriter,
            JournalSettings,
            coordinator,
            rollbackReporter,
            NullLogger<RemoveEntryService>.Instance
        );

        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(_console);
        services.AddSingleton<IRemoveEntryService>(removeEntryService);
        services.AddSingleton<ILogger<RemoveEntryCommand>>(NullLogger<RemoveEntryCommand>.Instance);

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
            config.AddBranch<RemoveSettings>("remove", remove =>
            {
                remove.AddCommand<RemoveEntryCommand>("entry");
            });
        });
    }

    [Fact]
    public void Execute_Should_DeleteEntryFile_When_EntryExists()
    {
        // Arrange
        var entryPath = Path.Combine(JournalPath, "Alpha.md");
        File.Exists(entryPath).ShouldBeTrue("Pre-condition: Alpha.md must exist before removal");

        // Act
        var result = _app.Run(["remove", "--path", JournalPath, "entry", "Alpha.md", "--force"]);

        // Assert
        result.ExitCode.ShouldBe(0);
        File.Exists(entryPath).ShouldBeFalse("Alpha.md should have been deleted by the remove command");
    }

    [Fact]
    public void Execute_Should_UpdateTableOfContents_When_EntryRemoved()
    {
        // Arrange — verify TOC contains Alpha before removal
        var tocPath = Path.Combine(JournalPath, "1a-TableOfContents.md");

        // Act
        _app.Run(["remove", "--path", JournalPath, "entry", "Alpha.md", "--force"]);

        // Assert — TOC should no longer reference Alpha
        var tocContent = File.ReadAllText(tocPath);
        tocContent.ShouldNotContain("Alpha.md");
    }

    [Fact]
    public void Execute_Should_UpdateMetadataFiles_When_EntryRemoved()
    {
        // Arrange
        var trackingPath = Path.Combine(JournalPath, ".mdjournal", ".journalindex");
        var tocStructurePath = Path.Combine(JournalPath, ".mdjournal", ".journaltoc");

        // Act
        var result = _app.Run(["remove", "--path", JournalPath, "entry", "Alpha.md", "--force"]);

        // Assert
        result.ExitCode.ShouldBe(0);

        File.Exists(trackingPath).ShouldBeTrue();
        File.Exists(tocStructurePath).ShouldBeTrue();

        var trackingContent = File.ReadAllText(trackingPath);
        trackingContent.ShouldNotContain("Alpha.md");

        var tocStructureContent = File.ReadAllText(tocStructurePath);
        tocStructureContent.ShouldNotContain("Alpha.md");
    }

    [Fact]
    public void Execute_Should_ReturnExitCode1_When_EntryDoesNotExist()
    {
        // Act
        var result = _app.Run(["remove", "--path", JournalPath, "entry", "NonExistent.md", "--force"]);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Output.ShouldContain("Error");
    }

    [Fact]
    public void Execute_Should_Succeed_When_EntryAlreadyDeletedAndCleanRefsSet()
    {
        // Arrange — seed a second entry (Beta) that links to Alpha, then manually
        // delete Alpha so it is absent from disk but still referenced by Beta.
        var betaPath = Path.Combine(JournalPath, "Beta.md");
        File.WriteAllText(betaPath, "# Beta\n\nSee [Alpha](Alpha.md) for details.");

        var alphaPath = Path.Combine(JournalPath, "Alpha.md");
        File.Delete(alphaPath);
        File.Exists(alphaPath).ShouldBeFalse("Pre-condition: Alpha.md must be absent before the test");

        // Act
        var result = _app.Run(
            ["remove", "--path", JournalPath, "entry", "Alpha.md", "--clean-refs", "--force"]
        );

        // Assert
        result.ExitCode.ShouldBe(0);
        result.Output.ShouldContain("Success:");

        // The dead link in Beta.md should have been stripped
        var betaContent = File.ReadAllText(betaPath);
        betaContent.ShouldNotContain("Alpha.md");
    }

    [Fact]
    public void Execute_Should_ShowPromptNotError_When_FileAlreadyDeletedAndCleanRefsSetWithoutForce()
    {
        // Arrange — manually delete Alpha so it is absent from disk
        var alphaPath = Path.Combine(JournalPath, "Alpha.md");
        File.Delete(alphaPath);
        File.Exists(alphaPath).ShouldBeFalse("Pre-condition: Alpha.md must be absent");

        // Simulate the user confirming the prompt
        _app.Console.Input.PushTextWithEnter("y");

        // Act — --clean-refs set, but NOT --force: should show the confirmation prompt
        var result = _app.Run(
            ["remove", "--path", JournalPath, "entry", "Alpha.md", "--clean-refs"]
        );

        // Assert
        result.ExitCode.ShouldBe(0);
        result.Output.ShouldContain("Success:");
        result.Output.ShouldNotContain("Error:");
    }

    [Fact]
    public void Execute_Should_CompletePartialCleanup_When_FileDeletedButMetadataIntact()
    {
        // Arrange — manually delete Alpha.md, leaving config and tracking intact
        var alphaPath = Path.Combine(JournalPath, "Alpha.md");
        File.Delete(alphaPath);
        File.Exists(alphaPath).ShouldBeFalse("Pre-condition: Alpha.md must be absent");

        // Verify Alpha is still represented in split metadata before cleanup
        var trackingBefore = File.ReadAllText(Path.Combine(JournalPath, ".mdjournal", ".journalindex"));
        trackingBefore.ShouldContain("Alpha.md");
        var tocStructureBefore = File.ReadAllText(Path.Combine(JournalPath, ".mdjournal", ".journaltoc"));
        tocStructureBefore.ShouldContain("Alpha.md");

        // Act
        var result = _app.Run(
            ["remove", "--path", JournalPath, "entry", "Alpha.md", "--clean-refs", "--force"]
        );

        // Assert
        result.ExitCode.ShouldBe(0);
        result.Output.ShouldContain("Success:");

        // Alpha should no longer appear in metadata after cleanup
        var trackingAfter = File.ReadAllText(Path.Combine(JournalPath, ".mdjournal", ".journalindex"));
        trackingAfter.ShouldNotContain("Alpha.md");
        var tocStructureAfter = File.ReadAllText(Path.Combine(JournalPath, ".mdjournal", ".journaltoc"));
        tocStructureAfter.ShouldNotContain("Alpha.md");
    }

    [Fact]
    public void Execute_Should_NotReportFalseRemovals_When_SecondRunOnFullyCleanedJournal()
    {
        // Arrange — first run cleans up Alpha
        var firstResult = _app.Run(["remove", "--path", JournalPath, "entry", "Alpha.md", "--clean-refs", "--force"]);
        var offsetAfterFirstRun = firstResult.Output.Length;

        // Act — second run on an already fully-cleaned journal
        var secondResult = _app.Run(
            ["remove", "--path", JournalPath, "entry", "Alpha.md", "--clean-refs", "--force"]
        );

        // Only examine the output produced by the second run
        var secondRunOutput = secondResult.Output[offsetAfterFirstRun..];

        // Assert
        secondResult.ExitCode.ShouldBe(0);
        // Must NOT claim it removed things that were already gone
        secondRunOutput.ShouldNotContain("removed from config");
        secondRunOutput.ShouldNotContain("removed from tracking");
        // Must always show a message about dead refs
        secondRunOutput.ShouldContain("No dead references found.");
    }
}
