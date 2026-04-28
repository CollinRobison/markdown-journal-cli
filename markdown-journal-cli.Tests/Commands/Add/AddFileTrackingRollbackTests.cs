using markdown_journal_cli.Commands.Add;
using markdown_journal_cli.Exceptions;
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
/// Fault-injection rollback tests for the <see cref="AddFileTracking"/> command.
/// </summary>
public class AddFileTrackingRollbackTests : IDisposable
{
    private const string JournalPath = "/test/add-tracking";
    private const string JournalrcPath = "/test/add-tracking/.journalrc";
    private readonly string _metadataDirPath = "/test/add-tracking/.mdjournal";
    private readonly string _trackingFilePath = "/test/add-tracking/.mdjournal/.journalindex";

    private readonly FaultInjectingFileSystem _fileSystem;
    private readonly FileTransactionCoordinator _coordinator;
    private readonly TestConsole _console;
    private readonly JournalSettings _journalSettings;
    private readonly RollbackReporter _rollbackReporter;
    private readonly FileTracking _fileTracking;

    public AddFileTrackingRollbackTests()
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
            DateFormat = "MM/dd/yyyy",
        };

        var settingsOptions = Options.Create(_journalSettings);
        var hashService = new TestHashService();
        _fileTracking = new FileTracking(_fileSystem, settingsOptions, hashService);

        _fileSystem.CreateDirectory(JournalPath);
        _fileSystem.CreateFile(JournalPath, ".journalrc", "{}");
        _fileSystem.ResetCallCounts();
    }

    private AddFileTracking CreateCommand() =>
        new AddFileTracking(
            _console,
            _fileSystem,
            _fileTracking,
            Options.Create(_journalSettings),
            _coordinator,
            _rollbackReporter
        );

    [Fact]
    public void Should_Delete_Created_Tracking_File_When_UpdateIndex_Throws()
    {
        // UpdateFile #1 is FileTracking.UpdateIndex → .mdjournal/.journalindex write; inject fault
        _fileSystem.InjectFaultOn(
            FaultInjectPoint.UpdateFile,
            1,
            new IOException("Tracking write failed")
        );

        var command = CreateCommand();
        var result = command.Execute(null!, new AddFileTrackingSettings { FilePath = JournalPath });

        // Exit code 2 = rollback completed
        result.ShouldBeInRange(2, 3);

        // Tracking file should be absent (rolled back)
        _fileSystem.FileExists(_trackingFilePath).ShouldBeFalse();
    }

    [Fact]
    public void Should_Not_Start_Transaction_When_Tracking_File_Already_Exists()
    {
        // Pre-create the tracking file so the command returns early without a tx
        _fileSystem.CreateDirectory(_metadataDirPath);
        _fileSystem.CreateFile(_metadataDirPath, ".journalindex", "{}");
        _fileSystem.ResetCallCounts();

        var command = CreateCommand();
        var result = command.Execute(null!, new AddFileTrackingSettings { FilePath = JournalPath });

        result.ShouldBe(0);
        _coordinator.Current.ShouldBeNull();
    }

    [Fact]
    public void Should_Not_Start_Transaction_When_JournalrcNotFound()
    {
        // Remove .journalrc so JournalrcNotFoundException is thrown before any tx
        _fileSystem.DeleteFile(JournalrcPath);
        _fileSystem.ResetCallCounts();

        var command = CreateCommand();
        var result = command.Execute(null!, new AddFileTrackingSettings { FilePath = JournalPath });

        result.ShouldBe(1);
        _coordinator.Current.ShouldBeNull();
    }

    public void Dispose()
    {
        _coordinator.Current?.Rollback();
    }
}
