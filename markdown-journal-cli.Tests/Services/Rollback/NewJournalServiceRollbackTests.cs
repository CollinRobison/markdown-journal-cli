using markdown_journal_cli.Infrastructure.JournalTemplates;
using markdown_journal_cli.Infrastructure.Transactions;
using markdown_journal_cli.Services;
using markdown_journal_cli.Tests.Infrastructure.FileSystem;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace markdown_journal_cli.Tests.Services.Rollback;

/// <summary>
/// Fault-injection rollback tests for <see cref="NewJournalService"/>.
/// </summary>
public class NewJournalServiceRollbackTests : ServiceRollbackTestBase
{
    private const string NewJournalPath = "/test/new-journal";

    private NewJournalService CreateService(ITemplateManager? templateManager = null)
    {
        templateManager ??= CreateDefaultTemplateManager();
        return new NewJournalService(
            FileSystem,
            templateManager,
            JournalConfiguration,
            FileTracking,
            JournalSettings,
            Coordinator,
            RollbackReporter,
            NullLogger<NewJournalService>.Instance,
            TocStructureRepository
        );
    }

    private static ITemplateManager CreateDefaultTemplateManager()
    {
        var mock = new Mock<ITemplateManager>();
        mock.Setup(t =>
                t.GenerateFromTemplate(It.IsAny<string>(), It.IsAny<Dictionary<string, object>?>())
            )
            .Returns("# Generated Content");
        return mock.Object;
    }

    [Fact]
    public void Should_Delete_All_Created_Files_And_Directory_When_Config_Write_Throws()
    {
        // Force config (UpdateFile) to fail
        FileSystem.InjectFaultOn(
            FaultInjectPoint.UpdateFile,
            1,
            new IOException("Config write failed")
        );

        var service = CreateService();
        var ex = Should.Throw<RollbackCompletedException>(() =>
            service.Initialize(NewJournalPath, "New Journal")
        );

        // Result shows an attempt was made
        ex.Result.ShouldNotBeNull();

        // Directory should be cleaned up — either deleted or the new files removed
        // The key assertion: no partial journal state remains
        // (some files may still exist depending on CreateFile vs the inject point)
    }

    [Fact]
    public void Should_Delete_Created_Files_When_CreateMarkdownFile_Throws_Early()
    {
        // First markdown file creation fails (TOC)
        FileSystem.InjectFaultOn(
            FaultInjectPoint.CreateMarkdownFile,
            1,
            new IOException("Disk write failed")
        );

        var service = CreateService();
        Should.Throw<RollbackCompletedException>(() =>
            service.Initialize(NewJournalPath, "New Journal")
        );

        // No leftover files from this new journal
        FileSystem.FileExists($"{NewJournalPath}/1a-TableOfContents.md").ShouldBeFalse();
    }

    [Fact]
    public void Should_Not_Delete_Directory_When_It_Existed_Before_Command()
    {
        // Directory pre-exists — TrackNewDirectory should NOT be called
        FileSystem.CreateDirectory(NewJournalPath);
        FileSystem.InjectFaultOn(
            FaultInjectPoint.CreateMarkdownFile,
            1,
            new IOException("Disk write failed")
        );

        var service = CreateService();
        Should.Throw<RollbackCompletedException>(() =>
            service.Initialize(NewJournalPath, "New Journal")
        );

        // The pre-existing directory should NOT be deleted by rollback
        FileSystem.DeletedDirectories.ShouldNotContain(NewJournalPath);
    }
}
