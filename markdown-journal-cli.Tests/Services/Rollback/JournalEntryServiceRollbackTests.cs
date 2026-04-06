using markdown_journal_cli.Exceptions;
using markdown_journal_cli.Infrastructure.JournalTemplates;
using markdown_journal_cli.Infrastructure.Transactions;
using markdown_journal_cli.Services;
using markdown_journal_cli.Tests.Infrastructure.FileSystem;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace markdown_journal_cli.Tests.Services.Rollback;

/// <summary>
/// Fault-injection rollback tests for <see cref="JournalEntryService"/>.
/// Each test verifies that a failure mid-operation leaves the journal in its original state.
/// </summary>
public class JournalEntryServiceRollbackTests : ServiceRollbackTestBase
{
    private JournalEntryService CreateService(ITemplateManager? templateManager = null)
    {
        templateManager ??= CreateDefaultTemplateManager();
        return new JournalEntryService(
            FileSystem,
            JournalConfiguration,
            JournalSettings,
            EntryFormatter,
            templateManager,
            FileTracking,
            TableOfContentsService,
            Coordinator,
            RollbackReporter,
            NullLogger<JournalEntryService>.Instance
        );
    }

    private static ITemplateManager CreateDefaultTemplateManager()
    {
        var mock = new Mock<ITemplateManager>();
        mock.Setup(t =>
                t.GenerateFromTemplate(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>())
            )
            .Returns("# Generated Entry");
        return mock.Object;
    }

    [Fact]
    public void Should_Delete_Created_Entry_When_Config_AddEntry_Throws()
    {
        // Arrange — reset write-call counters, then inject fault on first config/tracking write
        FileSystem.ResetCallCounts();
        FileSystem.InjectFaultOn(
            FaultInjectPoint.UpdateFile,
            1,
            new IOException("Config write failed")
        );

        var service = CreateService();
        var entryPath = $"{JournalPath}/my_entry.md";

        // Act
        Should.Throw<RollbackCompletedException>(() =>
            service.AddEntry(JournalPath, false, "my entry", null, null, null)
        );

        // Assert — the new entry file should have been deleted by rollback
        FileSystem.FileExists(entryPath).ShouldBeFalse();
    }

    [Fact]
    public void Should_Delete_Created_Entry_And_Restore_Config_When_Tracking_Update_Throws()
    {
        // Arrange — reset write-call counters, inject on 2nd UpdateFile (after entry markdown written)
        FileSystem.ResetCallCounts();
        FileSystem.InjectFaultOn(
            FaultInjectPoint.UpdateFile,
            2,
            new IOException("Tracking update failed")
        );

        var configBefore = System.IO.File.Exists(JournalrcPath)
            ? null
            : FileSystem.GetFileContent(JournalrcPath);
        var journalrcContent = FileSystem.GetFileContent(JournalrcPath);

        var service = CreateService();

        Should.Throw<RollbackCompletedException>(() =>
            service.AddEntry(JournalPath, false, "test_entry", null, null, null)
        );

        // Assert — entry file should be gone
        var entryPath = $"{JournalPath}/test_entry.md";
        FileSystem.FileExists(entryPath).ShouldBeFalse();

        // Assert — journalrc should be restored to original state
        FileSystem.GetFileContent(JournalrcPath).ShouldBe(journalrcContent);
    }

    [Fact]
    public void Should_Not_Start_Transaction_For_PreFlight_Exceptions()
    {
        // .journalrc doesn't exist — should throw JournalrcNotFoundException, not RollbackCompletedException
        FileSystem.DeleteFile(JournalrcPath);

        var service = CreateService();

        Should.Throw<JournalrcNotFoundException>(() =>
            service.AddEntry(JournalPath, false, "my entry", null, null, null)
        );

        Coordinator.Current.ShouldBeNull();
    }
}
