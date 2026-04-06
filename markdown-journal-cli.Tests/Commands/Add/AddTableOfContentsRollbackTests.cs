using markdown_journal_cli.Commands.Add;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.Tracking;
using markdown_journal_cli.Infrastructure.Transactions;
using markdown_journal_cli.Services;
using markdown_journal_cli.Tests.Infrastructure.FileSystem;
using markdown_journal_cli.Tests.Infrastructure.Tracking;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Spectre.Console.Testing;

namespace markdown_journal_cli.Tests.Commands.Add;

/// <summary>
/// Fault-injection rollback tests for the <see cref="AddTableOfContents"/> command.
/// </summary>
public class AddTableOfContentsRollbackTests : IDisposable
{
    private const string JournalPath = "/test/journal";
    private const string JournalrcPath = "/test/journal/.journalrc";
    private const string TocPath = "/test/journal/1a-TableOfContents.md";

    private readonly FaultInjectingFileSystem _fileSystem;
    private readonly FileTransactionCoordinator _coordinator;
    private readonly TestConsole _console;
    private readonly JournalSettings _journalSettings;
    private readonly JournalConfiguration _journalConfiguration;
    private readonly TableOfContentsService _tableOfContentsService;
    private readonly RollbackReporter _rollbackReporter;

    public AddTableOfContentsRollbackTests()
    {
        _fileSystem = new FaultInjectingFileSystem();
        var buffer = new InMemoryFileBuffer(_fileSystem);
        var deletionStrategy = new InMemoryDeletionRollbackStrategy();
        _coordinator = new FileTransactionCoordinator(
            _fileSystem,
            buffer,
            deletionStrategy,
            NullLoggerFactory.Instance
        );

        _console = new TestConsole();
        var rollbackConsole = new TestConsole();
        _rollbackReporter = new RollbackReporter(
            rollbackConsole,
            NullLogger<RollbackReporter>.Instance
        );

        _journalSettings = new JournalSettings
        {
            AppName = "md-journal",
            JournalConfigFileName = ".journalrc",
            TableOfContentsFileName = "1a-TableOfContents",
            TableOfContentsTitle = "Table of Contents",
            DateFormat = "MM/dd/yyyy",
            TitleSpaceSeparator = "_",
            HeadingSeparator = "-",
        };

        var settingsOptions = Options.Create(_journalSettings);
        var hashService = new TestHashService();
        var fileTracking = new FileTracking(_fileSystem, settingsOptions, hashService);
        _journalConfiguration = new JournalConfiguration(
            _fileSystem,
            settingsOptions,
            NullLogger<JournalConfiguration>.Instance,
            fileTracking
        );
        _tableOfContentsService = new TableOfContentsService(
            _fileSystem,
            _journalConfiguration,
            settingsOptions,
            NullLogger<TableOfContentsService>.Instance
        );

        // Set up a journal with .journalrc (no TOC file yet) — use camelCase JSON matching model's [JsonPropertyName]
        _fileSystem.CreateDirectory(JournalPath);
        _fileSystem.CreateFile(
            JournalPath,
            ".journalrc",
            """{"journalName":"Test","tableOfContents":{"file":"1a-TableOfContents.md","extensions":[".md"],"structure":{"topics":[]},"rootEntries":[]}}"""
        );
        _fileSystem.ResetCallCounts();
    }

    private AddTableOfContents CreateCommand() =>
        new AddTableOfContents(
            _console,
            _fileSystem,
            _journalConfiguration,
            _tableOfContentsService,
            Options.Create(_journalSettings),
            _coordinator,
            _rollbackReporter
        );

    [Fact]
    public void Should_Delete_Created_Toc_When_FileWrite_Fails()
    {
        // TOC does not yet exist; inject fault on UpdateFile #1 (TOC write)
        _fileSystem.InjectFaultOn(
            FaultInjectPoint.UpdateFile,
            1,
            new IOException("TOC write failed")
        );

        var command = CreateCommand();
        var result = command.Execute(
            null!,
            new AddTableOfContentsSettings { FilePath = JournalPath }
        );

        // Exit code 2 = rollback completed, fully restored
        result.ShouldBe(2);

        // TOC should not have been created (or was deleted by rollback)
        _fileSystem.FileExists(TocPath).ShouldBeFalse();
    }

    [Fact]
    public void Should_Restore_Journalrc_When_Toc_Creation_Fails_After_Config_Update()
    {
        // .journalrc points to a DIFFERENT TOC file, so command will update it first
        var altJournalrcContent =
            """{"journalName":"Test","tableOfContents":{"file":"old-toc.md","extensions":[".md"],"structure":{"topics":[]},"rootEntries":[]}}""";
        _fileSystem.UpdateFile(JournalPath, ".journalrc", altJournalrcContent);
        _fileSystem.ResetCallCounts();

        var journalrcBefore = _fileSystem.GetFileContent(JournalrcPath);

        // UpdateFile #1 = journalrc update; UpdateFile #2 = TOC write (inject fault here)
        _fileSystem.InjectFaultOn(
            FaultInjectPoint.UpdateFile,
            2,
            new IOException("TOC write failed")
        );

        var command = CreateCommand();
        var result = command.Execute(
            null!,
            new AddTableOfContentsSettings
            {
                FilePath = JournalPath,
                TableOfContentsName = "1a-TableOfContents",
            }
        );

        result.ShouldBeInRange(2, 3);

        // .journalrc should be restored to what it was before the command ran
        _fileSystem.GetFileContent(JournalrcPath).ShouldBe(journalrcBefore);
    }

    [Fact]
    public void Should_Not_Start_Transaction_When_Toc_Already_Exists()
    {
        // Pre-create the TOC so the command early-returns without starting a tx
        _fileSystem.CreateFile(JournalPath, "1a-TableOfContents.md", "# TOC");
        _fileSystem.ResetCallCounts();

        var command = CreateCommand();
        var result = command.Execute(
            null!,
            new AddTableOfContentsSettings { FilePath = JournalPath }
        );

        result.ShouldBe(1);
        _coordinator.Current.ShouldBeNull();
    }

    public void Dispose()
    {
        _coordinator.Current?.Rollback();
    }
}
