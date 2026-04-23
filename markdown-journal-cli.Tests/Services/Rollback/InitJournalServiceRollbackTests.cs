using markdown_journal_cli.Exceptions;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.Transactions;
using markdown_journal_cli.Services;
using markdown_journal_cli.Tests.Infrastructure.FileSystem;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace markdown_journal_cli.Tests.Services.Rollback;

/// <summary>
/// Fault-injection rollback tests for <see cref="InitJournalService"/>.
/// </summary>
public class InitJournalServiceRollbackTests : ServiceRollbackTestBase
{
    private const string InitPath = "/test/init-target";
    private readonly string _trackingPath;
    private readonly string _journalrcPath;
    private readonly string _tocPath;

    public InitJournalServiceRollbackTests()
    {
        _trackingPath = $"{InitPath}/.md-journal";
        _journalrcPath = $"{InitPath}/.journalrc";
        _tocPath = $"{InitPath}/1a-TableOfContents.md";

        // Create an empty target directory for initialization tests
        FileSystem.CreateDirectory(InitPath);
        FileSystem.ResetCallCounts();
    }

    private InitJournalService CreateService()
    {
        var tocParser = new TableOfContentsMarkdownParser();
        var configGenerator = new JournalConfigGenerator(
            FileSystem,
            tocParser,
            FileTracking,
            EntryFormatter,
            JournalConfiguration,
            JournalSettings,
            TocStructureRepository
        );

        return new InitJournalService(
            FileSystem,
            configGenerator,
            FileTracking,
            TableOfContentsService,
            JournalSettings,
            Coordinator,
            RollbackReporter
        );
    }

    [Fact]
    public void Should_Delete_Created_Mdjournal_And_Journalrc_When_Toc_Creation_Throws()
    {
        // UpdateFile #1 = FileTracking.UpdateIndex → .md-journal
        // CreateFile #1 = JournalConfiguration.Create → .journalrc
        // UpdateFile #2 = TableOfContentsService → TOC (inject fault here)
        FileSystem.InjectFaultOn(
            FaultInjectPoint.UpdateFile,
            2,
            new IOException("TOC write failed")
        );

        var service = CreateService();

        Should.Throw<RollbackCompletedException>(() =>
            service.Initialize(InitPath, "My Journal", null)
        );

        // Rollback should delete .md-journal and .journalrc (TOC was never written)
        FileSystem.FileExists(_trackingPath).ShouldBeFalse();
        FileSystem.FileExists(_journalrcPath).ShouldBeFalse();
    }

    [Fact]
    public void Should_Delete_All_Created_Files_When_Second_TrackingUpdate_Throws()
    {
        // UpdateFile #1 = FileTracking.UpdateIndex (1st call)
        // CreateFile #1 = JournalConfiguration.Create → .journalrc
        // UpdateFile #2 = TableOfContentsService → TOC
        // UpdateFile #3 = FileTracking.UpdateIndex (2nd call) — inject fault here
        FileSystem.InjectFaultOn(
            FaultInjectPoint.UpdateFile,
            3,
            new IOException("2nd tracking update failed")
        );

        var service = CreateService();

        Should.Throw<RollbackCompletedException>(() =>
            service.Initialize(InitPath, "My Journal", null)
        );

        // All three files should be deleted by rollback
        FileSystem.FileExists(_trackingPath).ShouldBeFalse();
        FileSystem.FileExists(_journalrcPath).ShouldBeFalse();
        FileSystem.FileExists(_tocPath).ShouldBeFalse();
    }

    [Fact]
    public void Should_Not_Start_Transaction_For_TocAlreadyExistsException()
    {
        // Pre-create the TOC file so Initialize throws TocFileAlreadyExistsException (pre-flight)
        FileSystem.CreateFile(InitPath, "1a-TableOfContents.md", "# TOC");
        FileSystem.ResetCallCounts();

        var service = CreateService();

        Should.Throw<TocFileAlreadyExistsException>(() =>
            service.Initialize(InitPath, "My Journal", null)
        );

        // No transaction should have been started
        Coordinator.Current.ShouldBeNull();
    }
}
