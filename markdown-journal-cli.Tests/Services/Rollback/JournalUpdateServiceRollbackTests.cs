using markdown_journal_cli.Infrastructure.Configuration.Models;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.Tracking.Models;
using markdown_journal_cli.Infrastructure.Transactions;
using markdown_journal_cli.Services;
using markdown_journal_cli.Tests.Infrastructure.FileSystem;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace markdown_journal_cli.Tests.Services.Rollback;

/// <summary>
/// Fault-injection rollback tests for <see cref="JournalUpdateService"/>.
/// </summary>
public class JournalUpdateServiceRollbackTests : ServiceRollbackTestBase
{
    private JournalUpdateService CreateService(IMarkdownLinkRewriter? linkRewriter = null)
    {
        linkRewriter ??= Mock.Of<IMarkdownLinkRewriter>(r =>
            r.FindFilesWithLinkTo(It.IsAny<string>(), It.IsAny<string>()) == Array.Empty<string>()
            && r.ReplaceLinksInDirectory(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyCollection<string>?>()
            ) == Array.Empty<string>()
        );

        return new JournalUpdateService(
            Console,
            FileSystem,
            JournalConfiguration,
            FileTracking,
            TableOfContentsService,
            JournalSettings,
            linkRewriter,
            Coordinator,
            RollbackReporter,
            NullLogger<JournalUpdateService>.Instance,
            TocStructureRepository
        );
    }

    [Fact]
    public void Should_Rollback_All_Steps_When_UpdateJournalConfig_Fails_After_TrackingSync()
    {
        // Capture config state before operation
        var journalrcBefore = FileSystem.GetFileContent(JournalrcPath);
        FileSystem.ResetCallCounts();

        // UpdateFile #1 is the .journalrc write from JournalConfiguration.AddEntry
        FileSystem.InjectFaultOn(
            FaultInjectPoint.UpdateFile,
            1,
            new IOException("Config write failed")
        );

        var service = CreateService();
        var syncResult = new JournalRegistrationDriftResult
        {
            FilesToAdd = ["new_entry.md"],
            FilesToRemove = [],
        };

        Should.Throw<RollbackCompletedException>(() =>
            service.UpdateJournalConfig(JournalPath, syncResult)
        );

        // .journalrc should be restored to pre-operation content
        FileSystem.GetFileContent(JournalrcPath).ShouldBe(journalrcBefore);
    }

    [Fact]
    public void Should_Rollback_All_Steps_When_UpdateTableOfContents_Fails_After_ConfigSync()
    {
        // Capture TOC state before operation
        var tocBefore = FileSystem.GetFileContent(TocPath);

        // Force a real TOC diff so the conditional TOC update does not no-op.
        JournalConfiguration.AddEntry(JournalPath, "New Entry", "new_entry.md");
        FileSystem.ResetCallCounts();

        // UpdateFile #1 is the TOC write from TableOfContentsService
        FileSystem.InjectFaultOn(
            FaultInjectPoint.UpdateFile,
            1,
            new IOException("TOC write failed")
        );

        var service = CreateService();

        Should.Throw<RollbackCompletedException>(() => service.UpdateTableOfContents(JournalPath));

        // TOC should be restored to pre-operation content
        FileSystem.GetFileContent(TocPath).ShouldBe(tocBefore);
    }

    [Fact]
    public void Should_Rollback_Toc_Rename_And_Config_When_JournalRc_Update_Throws()
    {
        var originalTocPath = TocPath;
        var newTocName = "renamed-toc";
        var newTocPath = $"{JournalPath}/{newTocName}.md";

        // Capture state before operation
        var journalrcBefore = FileSystem.GetFileContent(JournalrcPath);
        FileSystem.ResetCallCounts();

        // UpdateFile #1 = journalrc update (from _journalConfiguration.Update in RenameToc)
        FileSystem.InjectFaultOn(
            FaultInjectPoint.UpdateFile,
            1,
            new IOException("Config update failed")
        );

        var service = CreateService();

        Should.Throw<RollbackCompletedException>(() => service.RenameToc(JournalPath, newTocName));

        // The physical rename should have been reversed — original TOC exists, new one does not
        FileSystem.FileExists(originalTocPath).ShouldBeTrue();
        FileSystem.FileExists(newTocPath).ShouldBeFalse();

        // .journalrc should remain at its original content (update was rolled back)
        FileSystem.GetFileContent(JournalrcPath).ShouldBe(journalrcBefore);
    }

    [Fact]
    public void Should_Rollback_All_Backlinked_Files_When_LinkRewriter_Throws()
    {
        var originalTocPath = TocPath;
        var newTocName = "new-toc";
        var newTocPath = $"{JournalPath}/{newTocName}.md";

        var journalrcBefore = FileSystem.GetFileContent(JournalrcPath);
        FileSystem.ResetCallCounts();

        // Mock link rewriter to throw when scanning for files to rewrite
        var throwingRewriter = new Mock<IMarkdownLinkRewriter>();
        throwingRewriter
            .Setup(r => r.FindFilesWithLinkTo(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Array.Empty<string>());
        throwingRewriter
            .Setup(r =>
                r.ReplaceLinksInDirectory(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<IReadOnlyCollection<string>?>()
                )
            )
            .Throws(new IOException("Link scan failed"));

        var service = CreateService(throwingRewriter.Object);

        Should.Throw<RollbackCompletedException>(() => service.RenameToc(JournalPath, newTocName));

        // Rename + config + tracking all happened before throw — rollback must undo all
        FileSystem.FileExists(originalTocPath).ShouldBeTrue();
        FileSystem.FileExists(newTocPath).ShouldBeFalse();
        FileSystem.GetFileContent(JournalrcPath).ShouldBe(journalrcBefore);
    }
}
