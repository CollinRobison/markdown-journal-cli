using markdown_journal_cli.Commands.Add;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.Tracking;
using markdown_journal_cli.Infrastructure.Transactions;
using markdown_journal_cli.Services;
using markdown_journal_cli.Services.AddToc;
using markdown_journal_cli.Tests.Infrastructure.FileSystem;
using markdown_journal_cli.Tests.Infrastructure.Tracking;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Spectre.Console.Testing;

namespace markdown_journal_cli.Tests.Commands.Add;

/// <summary>
/// Fault-injection rollback tests for the <see cref="AddTableOfContents"/> command via
/// <see cref="AddTocService"/>. Verifies that partial writes are rolled back on failure.
/// </summary>
public class AddTableOfContentsRollbackTests : IDisposable
{
    private const string JournalPath = "/test/journal";
    private const string TocMdPath = "/test/journal/1a-TableOfContents.md";

    private readonly FaultInjectingFileSystem _fileSystem;
    private readonly FileTransactionCoordinator _coordinator;
    private readonly TestConsole _console;
    private readonly JournalSettings _journalSettings;
    private readonly JournalConfiguration _journalConfiguration;
    private readonly TableOfContentsService _tableOfContentsService;
    private readonly RollbackReporter _rollbackReporter;
    private readonly JournalTocStructureRepository _tocStructureRepository;
    private readonly FileTracking _fileTracking;

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
            MetadataDirName = ".mdjournal",
            TrackingFileName = ".journalindex",
            TocStructureFileName = ".journaltoc",
        };

        var settingsOptions = Options.Create(_journalSettings);
        var hashService = new TestHashService();
        _fileTracking = new FileTracking(_fileSystem, settingsOptions, hashService);
        _tocStructureRepository = new JournalTocStructureRepository(_fileSystem, settingsOptions);
        _journalConfiguration = new JournalConfiguration(
            _fileSystem,
            settingsOptions,
            NullLogger<JournalConfiguration>.Instance,
            _tocStructureRepository
        );
        _tableOfContentsService = new TableOfContentsService(
            _fileSystem,
            _journalConfiguration,
            settingsOptions,
            NullLogger<TableOfContentsService>.Instance,
            _tocStructureRepository
        );

        // Set up journal: .journalrc + .mdjournal/.journaltoc (no markdown TOC yet)
        _fileSystem.CreateDirectory(JournalPath);
        _fileSystem.CreateFile(
            JournalPath,
            ".journalrc",
            """{"journalName":"Test","tableOfContents":{"file":"1a-TableOfContents.md","extensions":[".md"]}}"""
        );
        _fileSystem.CreateDirectory($"{JournalPath}/.mdjournal");
        _fileSystem.CreateFile(
            $"{JournalPath}/.mdjournal",
            ".journaltoc",
            """{"Structure":{"Topics":[]},"RootEntries":[]}"""
        );
        _fileSystem.ResetCallCounts();
    }

    private AddTableOfContents CreateCommand()
    {
        var settingsOptions = Options.Create(_journalSettings);
        var addTocService = new AddTocService(
            _fileSystem,
            _journalConfiguration,
            _tocStructureRepository,
            _tableOfContentsService,
            _fileTracking,
            _coordinator,
            _rollbackReporter,
            settingsOptions
        );
        return new AddTableOfContents(_console, addTocService, _rollbackReporter);
    }

    [Fact]
    public void Should_RollbackTocMd_When_TocMdWriteFails()
    {
        // Markdown TOC does not exist yet; inject fault on the first UpdateFile (TOC md write)
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

        // Exit code 2 = rollback completed and fully restored
        result.ShouldBe(2);

        // Markdown TOC should not have been created (or was deleted by rollback)
        _fileSystem.FileExists(TocMdPath).ShouldBeFalse();
    }

    [Fact]
    public void Should_NotStartTransaction_When_BothArtifactsAlreadyExist()
    {
        // Pre-create the markdown TOC so both artifacts now exist
        _fileSystem.CreateFile(JournalPath, "1a-TableOfContents.md", "# TOC");
        _fileSystem.ResetCallCounts();

        var command = CreateCommand();
        var result = command.Execute(
            null!,
            new AddTableOfContentsSettings { FilePath = JournalPath }
        );

        // Should warn "already exists" → exit code 1
        result.ShouldBe(1);
        // No transaction should have been started
        _coordinator.Current.ShouldBeNull();
    }

    public void Dispose()
    {
        _coordinator.Current?.Rollback();
    }
}
