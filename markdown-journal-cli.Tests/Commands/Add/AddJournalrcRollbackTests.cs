using markdown_journal_cli.Commands.Add;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.Configuration.Models;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.Tracking;
using markdown_journal_cli.Infrastructure.Transactions;
using markdown_journal_cli.Services;
using markdown_journal_cli.Tests.Infrastructure.FileSystem;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Spectre.Console.Testing;

namespace markdown_journal_cli.Tests.Commands.Add;

/// <summary>
/// Fault-injection rollback tests for the <see cref="AddJournalrc"/> command.
/// </summary>
public class AddJournalrcRollbackTests : IDisposable
{
    private const string JournalPath = "/test/add-journalrc";
    private const string JournalrcPath = "/test/add-journalrc/.journalrc";

    private readonly FaultInjectingFileSystem _fileSystem;
    private readonly FileTransactionCoordinator _coordinator;
    private readonly TestConsole _console;
    private readonly JournalSettings _journalSettings;
    private readonly RollbackReporter _rollbackReporter;

    public AddJournalrcRollbackTests()
    {
        _fileSystem = new FaultInjectingFileSystem();
        var buffer = new InMemoryFileBuffer(_fileSystem);
        var deletionStrategy = new InMemoryDeletionRollbackStrategy();
        _coordinator = new FileTransactionCoordinator(_fileSystem, buffer, deletionStrategy, NullLoggerFactory.Instance);

        _console = new TestConsole();
        var rollbackConsole = new TestConsole();
        _rollbackReporter = new RollbackReporter(rollbackConsole, NullLogger<RollbackReporter>.Instance);

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

        _fileSystem.CreateDirectory(JournalPath);
        _fileSystem.ResetCallCounts();
    }

    private AddJournalrc CreateCommand(IJournalConfigGenerator? generator = null)
    {
        if (generator == null)
        {
            var mockGenerator = new Mock<IJournalConfigGenerator>();
            // All sources return null so fallback GenerateFromDirectory is used
            mockGenerator
                .Setup(g => g.GenerateFromTableOfContents(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns((JournalConfigGenerationResult?)null);
            mockGenerator
                .Setup(g => g.GenerateFromTrackingIndex(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns((JournalConfigGenerationResult?)null);
            mockGenerator
                .Setup(g => g.GenerateFromDirectory(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback((string dir, string toc, string name) =>
                {
                    // Simulate a partial file write before throwing
                    _fileSystem.CreateFile(dir, ".journalrc", "{}");
                })
                .Throws(new IOException("Write failed mid-way"));
            generator = mockGenerator.Object;
        }

        return new AddJournalrc(
            _console,
            _fileSystem,
            generator,
            Options.Create(_journalSettings),
            _coordinator,
            _rollbackReporter
        );
    }

    [Fact]
    public void Should_Delete_Created_Journalrc_When_Write_Fails_Midway()
    {
        // The mock generator writes .journalrc then throws — rollback must delete the partial file
        var command = CreateCommand();
        var result = command.Execute(null!, new AddJournalrcSettings { FilePath = JournalPath });

        result.ShouldBeInRange(2, 3);

        // .journalrc should be cleaned up by rollback
        _fileSystem.FileExists(JournalrcPath).ShouldBeFalse();
    }

    [Fact]
    public void Should_Not_Start_Transaction_When_Journalrc_Already_Exists()
    {
        // Pre-create .journalrc so the command returns 1 without starting a tx
        _fileSystem.CreateFile(JournalPath, ".journalrc", "{}");
        _fileSystem.ResetCallCounts();

        var command = CreateCommand();
        var result = command.Execute(null!, new AddJournalrcSettings { FilePath = JournalPath });

        result.ShouldBe(1);
        _coordinator.Current.ShouldBeNull();
    }

    public void Dispose()
    {
        _coordinator.Current?.Rollback();
    }
}
