using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.Transactions;
using markdown_journal_cli.Services;
using markdown_journal_cli.Services.RemoveEntry;
using markdown_journal_cli.Tests.Infrastructure.FileSystem;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace markdown_journal_cli.Tests.Services.Rollback;

/// <summary>
/// Fault-injection rollback tests for <see cref="RemoveEntryService"/>.
/// </summary>
public class RemoveEntryServiceRollbackTests : ServiceRollbackTestBase
{
    private const string EntryFileName = "my_entry.md";
    private readonly string _entryPath = $"{JournalPath}/{EntryFileName}";
    private readonly string _entryContent = "# My Entry\n\nContent here.";

    private RemoveEntryService CreateService(IMarkdownLinkRewriter? linkRewriter = null)
    {
        linkRewriter ??= Mock.Of<IMarkdownLinkRewriter>(r =>
            r.StripLinksInDirectory(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>?>()) == Array.Empty<string>() &&
            r.FindFilesWithLinkTo(It.IsAny<string>(), It.IsAny<string>()) == Array.Empty<string>()
        );

        return new RemoveEntryService(
            FileSystem,
            JournalConfiguration,
            FileTracking,
            TableOfContentsService,
            linkRewriter,
            JournalSettings,
            Coordinator,
            RollbackReporter,
            NullLogger<RemoveEntryService>.Instance
        );
    }

    private void AddEntryToJournal()
    {
        FileSystem.CreateFile(JournalPath, EntryFileName, _entryContent);
        JournalConfiguration.AddEntry(JournalPath, "My Entry", EntryFileName);
        FileTracking.UpdateFileInIndex(JournalPath, EntryFileName);
    }

    [Fact]
    public void Should_Restore_Deleted_Entry_When_Config_RemoveEntry_Throws()
    {
        AddEntryToJournal();
        FileSystem.ResetCallCounts();
        var journalrcContent = FileSystem.GetFileContent(JournalrcPath);

        // Force the config update (UpdateFile) to fail 
        FileSystem.InjectFaultOn(FaultInjectPoint.UpdateFile, 1, new IOException("Config write failed"));

        var service = CreateService();

        Should.Throw<RollbackCompletedException>(() =>
            service.RemoveEntry(JournalPath, EntryFileName, cleanRefs: false)
        );

        // Entry file should be restored
        FileSystem.FileExists(_entryPath).ShouldBeTrue();
        FileSystem.GetFileContent(_entryPath).ShouldBe(_entryContent);
    }

    [Fact]
    public void Should_Restore_Deleted_Entry_And_Config_When_Tracking_Update_Throws()
    {
        AddEntryToJournal();
        FileSystem.ResetCallCounts();
        var journalrcContent = FileSystem.GetFileContent(JournalrcPath);

        // Force the 2nd UpdateFile (tracking) to fail
        FileSystem.InjectFaultOn(FaultInjectPoint.UpdateFile, 2, new IOException("Tracking failed"));

        var service = CreateService();

        Should.Throw<RollbackCompletedException>(() =>
            service.RemoveEntry(JournalPath, EntryFileName, cleanRefs: false)
        );

        // Both the entry and config should be restored
        FileSystem.FileExists(_entryPath).ShouldBeTrue();
        FileSystem.GetFileContent(_entryPath).ShouldBe(_entryContent);
        FileSystem.GetFileContent(JournalrcPath).ShouldBe(journalrcContent);
    }

    [Fact]
    public void Should_Restore_All_Files_When_Toc_Regeneration_Throws()
    {
        AddEntryToJournal();
        FileSystem.ResetCallCounts();

        // TOC update is the 3rd write (config, tracking, toc)
        FileSystem.InjectFaultOn(FaultInjectPoint.UpdateFile, 3, new IOException("TOC write failed"));

        var service = CreateService();

        Should.Throw<RollbackCompletedException>(() =>
            service.RemoveEntry(JournalPath, EntryFileName, cleanRefs: false)
        );

        FileSystem.FileExists(_entryPath).ShouldBeTrue();
    }
}
