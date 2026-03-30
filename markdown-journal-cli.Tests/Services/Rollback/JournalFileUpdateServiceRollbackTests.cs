using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.Transactions;
using markdown_journal_cli.Services;
using markdown_journal_cli.Tests.Infrastructure.FileSystem;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace markdown_journal_cli.Tests.Services.Rollback;

/// <summary>
/// Fault-injection rollback tests for <see cref="JournalFileUpdateService"/>.
/// </summary>
public class JournalFileUpdateServiceRollbackTests : ServiceRollbackTestBase
{
    private const string EntryFileName = "entry1.md";
    private const string EntryDisplayName = "Entry One";
    private readonly string _entryPath = $"{JournalPath}/{EntryFileName}";

    private JournalFileUpdateService CreateService(IMarkdownLinkRewriter? linkRewriter = null)
    {
        linkRewriter ??= Mock.Of<IMarkdownLinkRewriter>(r =>
            r.FindFilesWithLinkTo(It.IsAny<string>(), It.IsAny<string>()) == Array.Empty<string>() &&
            r.ReplaceLinksInDirectory(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyCollection<string>?>()) == Array.Empty<string>()
        );

        return new JournalFileUpdateService(
            FileSystem,
            JournalConfiguration,
            EntryFormatter,
            TableOfContentsService,
            JournalSettings,
            NullLogger<JournalFileUpdateService>.Instance,
            FileTracking,
            linkRewriter,
            Coordinator,
            RollbackReporter
        );
    }

    private void AddEntry()
    {
        FileSystem.CreateFile(JournalPath, EntryFileName, "# Entry One\n\nContent.");
        JournalConfiguration.AddEntry(JournalPath, EntryDisplayName, EntryFileName);
        FileTracking.UpdateFileInIndex(JournalPath, EntryFileName);
        FileSystem.ResetCallCounts();
    }

    [Fact]
    public void Should_Rollback_Rename_When_Config_Update_Throws()
    {
        AddEntry();
        var journalrcBefore = FileSystem.GetFileContent(JournalrcPath);

        // RenameFile #1 succeeds; UpdateFile #1 (journalrc via UpdateFileReferences) fails
        FileSystem.InjectFaultOn(FaultInjectPoint.UpdateFile, 1, new IOException("Config update failed"));

        var service = CreateService();

        Should.Throw<RollbackCompletedException>(() =>
            service.UpdateEntry(JournalPath, EntryFileName, newEntryName: "entry2")
        );

        // Physical file should be renamed back
        FileSystem.FileExists(_entryPath).ShouldBeTrue();
        FileSystem.FileExists($"{JournalPath}/entry2.md").ShouldBeFalse();

        // .journalrc should be restored
        FileSystem.GetFileContent(JournalrcPath).ShouldBe(journalrcBefore);
    }

    [Fact]
    public void Should_Rollback_Rename_And_Backlinks_When_Toc_Regeneration_Throws()
    {
        AddEntry();
        var journalrcBefore = FileSystem.GetFileContent(JournalrcPath);

        // UpdateFile #1 (journalrc) and #2 (tracking) succeed; UpdateFile #3 (TOC) fails
        FileSystem.InjectFaultOn(FaultInjectPoint.UpdateFile, 3, new IOException("TOC regeneration failed"));

        var service = CreateService();

        Should.Throw<RollbackCompletedException>(() =>
            service.UpdateEntry(JournalPath, EntryFileName, newEntryName: "entry2")
        );

        // Physical file should be renamed back
        FileSystem.FileExists(_entryPath).ShouldBeTrue();
        FileSystem.FileExists($"{JournalPath}/entry2.md").ShouldBeFalse();

        // .journalrc should be restored
        FileSystem.GetFileContent(JournalrcPath).ShouldBe(journalrcBefore);
    }

    [Fact]
    public void Should_Rollback_Config_When_DisplayName_Change_Followed_By_Toc_Failure()
    {
        AddEntry();
        var journalrcBefore = FileSystem.GetFileContent(JournalrcPath);

        // UpdateFile #1 (journalrc display name change) succeeds; UpdateFile #2 (TOC) fails
        FileSystem.InjectFaultOn(FaultInjectPoint.UpdateFile, 2, new IOException("TOC write failed"));

        var service = CreateService();

        Should.Throw<RollbackCompletedException>(() =>
            service.UpdateEntry(JournalPath, EntryFileName, newEntryTitle: "New Display Name")
        );

        // .journalrc should be restored to its original content (display name unchanged)
        FileSystem.GetFileContent(JournalrcPath).ShouldBe(journalrcBefore);
    }
}
