using System.Text.RegularExpressions;
using markdown_journal_cli.Exceptions;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.Configuration.Models;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.Tracking;
using markdown_journal_cli.Infrastructure.Tracking.Models;
using markdown_journal_cli.Infrastructure.Transactions;
using markdown_journal_cli.Services;
using markdown_journal_cli.Tests.Infrastructure;
using Moq;
using Shouldly;
using Spectre.Console.Testing;

namespace markdown_journal_cli.Tests.Services;

/// <summary>
/// Unit tests for <see cref="JournalUpdateService"/> covering journal configuration updates,
/// last-edited date tracking, and table of contents management.
/// Uses Moq mocks via <see cref="ServiceTestBase"/> for all infrastructure dependencies.
/// </summary>
public class JournalUpdateServiceTests : ServiceTestBase
{
    private readonly TestConsole _console;
    private readonly Mock<IMarkdownLinkRewriter> _mockMarkdownLinkRewriter;
    private const string _testPath = "/test/journal";

    public JournalUpdateServiceTests()
    {
        _console = new TestConsole();
        _mockMarkdownLinkRewriter = new Mock<IMarkdownLinkRewriter>();
    }

    private JournalUpdateService CreateSut() =>
        new JournalUpdateService(
            _console,
            MockFileSystem.Object,
            MockJournalConfiguration.Object,
            MockFileTracking.Object,
            MockTableOfContentsService.Object,
            JournalSettings,
            _mockMarkdownLinkRewriter.Object,
            NoOpCoordinator,
            NoOpReporter,
            NullLogger<JournalUpdateService>(),
            MockTocStructureRepository.Object
        );

    /// <summary>
    /// Creates a minimal <see cref="JournalConfig"/> with the given TOC file name and
    /// optional root entries for use in mock setups.
    /// </summary>
    private static JournalConfig CreateTestConfig(string tocFile, string[]? rootEntries = null) =>
        new JournalConfig
        {
            JournalName = "Test Journal",
            TableOfContents = new TableOfContents { File = tocFile, Extensions = [".md"] },
        };

    #region Constructor Guards

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_ConsoleIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new JournalUpdateService(
                null!,
                Mock.Of<IFileSystem>(),
                Mock.Of<IJournalConfiguration>(),
                Mock.Of<IFileTracking>(),
                Mock.Of<ITableOfContentsService>(),
                JournalSettings,
                Mock.Of<IMarkdownLinkRewriter>(),
                NoOpCoordinator,
                NoOpReporter,
                NullLogger<JournalUpdateService>(),
                Mock.Of<IJournalTocStructureRepository>()
            )
        );
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_FileSystemIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new JournalUpdateService(
                _console,
                null!,
                Mock.Of<IJournalConfiguration>(),
                Mock.Of<IFileTracking>(),
                Mock.Of<ITableOfContentsService>(),
                JournalSettings,
                Mock.Of<IMarkdownLinkRewriter>(),
                NoOpCoordinator,
                NoOpReporter,
                NullLogger<JournalUpdateService>(),
                Mock.Of<IJournalTocStructureRepository>()
            )
        );
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_JournalConfigurationIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new JournalUpdateService(
                _console,
                Mock.Of<IFileSystem>(),
                null!,
                Mock.Of<IFileTracking>(),
                Mock.Of<ITableOfContentsService>(),
                JournalSettings,
                Mock.Of<IMarkdownLinkRewriter>(),
                NoOpCoordinator,
                NoOpReporter,
                NullLogger<JournalUpdateService>(),
                Mock.Of<IJournalTocStructureRepository>()
            )
        );
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_FileTrackingIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new JournalUpdateService(
                _console,
                Mock.Of<IFileSystem>(),
                Mock.Of<IJournalConfiguration>(),
                null!,
                Mock.Of<ITableOfContentsService>(),
                JournalSettings,
                Mock.Of<IMarkdownLinkRewriter>(),
                NoOpCoordinator,
                NoOpReporter,
                NullLogger<JournalUpdateService>(),
                Mock.Of<IJournalTocStructureRepository>()
            )
        );
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_TableOfContentsServiceIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new JournalUpdateService(
                _console,
                Mock.Of<IFileSystem>(),
                Mock.Of<IJournalConfiguration>(),
                Mock.Of<IFileTracking>(),
                null!,
                JournalSettings,
                Mock.Of<IMarkdownLinkRewriter>(),
                NoOpCoordinator,
                NoOpReporter,
                NullLogger<JournalUpdateService>(),
                Mock.Of<IJournalTocStructureRepository>()
            )
        );
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_TxCoordinatorIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new JournalUpdateService(
                _console,
                Mock.Of<IFileSystem>(),
                Mock.Of<IJournalConfiguration>(),
                Mock.Of<IFileTracking>(),
                Mock.Of<ITableOfContentsService>(),
                JournalSettings,
                Mock.Of<IMarkdownLinkRewriter>(),
                null!,
                NoOpReporter,
                NullLogger<JournalUpdateService>(),
                Mock.Of<IJournalTocStructureRepository>()
            )
        );
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_RollbackReporterIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new JournalUpdateService(
                _console,
                Mock.Of<IFileSystem>(),
                Mock.Of<IJournalConfiguration>(),
                Mock.Of<IFileTracking>(),
                Mock.Of<ITableOfContentsService>(),
                JournalSettings,
                Mock.Of<IMarkdownLinkRewriter>(),
                NoOpCoordinator,
                null!,
                NullLogger<JournalUpdateService>(),
                Mock.Of<IJournalTocStructureRepository>()
            )
        );
    }

    #endregion

    #region UpdateJournalConfig

    [Fact]
    public void UpdateJournalConfig_Should_AddNewFileToConfig_When_FileIsAdded()
    {
        // Arrange
        var syncResult = new JournalRegistrationDriftResult { FilesToAdd = ["2a-SomeNote.md"] };
        var sut = CreateSut();

        // Act
        sut.UpdateJournalConfig(_testPath, syncResult);

        // Assert
        MockJournalConfiguration.Verify(
            jc => jc.AddEntry(_testPath, string.Empty, "2a-SomeNote.md"),
            Times.Once
        );
    }

    [Fact]
    public void UpdateJournalConfig_Should_AddMultipleFilesToConfig_When_MultipleFilesAdded()
    {
        // Arrange
        var syncResult = new JournalRegistrationDriftResult
        {
            FilesToAdd = ["2a-NoteOne.md", "3b-NoteTwo.md"],
        };
        var sut = CreateSut();

        // Act
        sut.UpdateJournalConfig(_testPath, syncResult);

        // Assert
        MockJournalConfiguration.Verify(
            jc => jc.AddEntry(_testPath, string.Empty, "2a-NoteOne.md"),
            Times.Once
        );
        MockJournalConfiguration.Verify(
            jc => jc.AddEntry(_testPath, string.Empty, "3b-NoteTwo.md"),
            Times.Once
        );
    }

    [Fact]
    public void UpdateJournalConfig_Should_RemoveDeletedFileFromConfig_When_FileIsDeleted()
    {
        // Arrange
        MockJournalConfiguration
            .Setup(jc => jc.RemoveEntry(_testPath, "2a-SomeNote.md"))
            .Returns(true);
        var syncResult = new JournalRegistrationDriftResult { FilesToRemove = ["2a-SomeNote.md"] };
        var sut = CreateSut();

        // Act
        sut.UpdateJournalConfig(_testPath, syncResult);

        // Assert
        MockJournalConfiguration.Verify(
            jc => jc.RemoveEntry(_testPath, "2a-SomeNote.md"),
            Times.Once
        );
    }

    [Fact]
    public void UpdateJournalConfig_Should_PrintConfigEntryNotFound_When_DeletedFileNotInConfig()
    {
        // Arrange — RemoveEntry returns false to simulate the entry not being found
        MockJournalConfiguration
            .Setup(jc => jc.RemoveEntry(_testPath, "nonexistent-file.md"))
            .Returns(false);
        var syncResult = new JournalRegistrationDriftResult { FilesToRemove = ["nonexistent-file.md"] };
        var sut = CreateSut();

        // Act
        sut.UpdateJournalConfig(_testPath, syncResult);

        // Assert
        _console.Output.ShouldContain("config entry not found for deleted file");
    }

    [Fact]
    public void UpdateJournalConfig_Should_PrintNoChangesNeeded_When_NeitherAddedNorRemoved()
    {
        // Arrange
        var syncResult = new JournalRegistrationDriftResult();
        var sut = CreateSut();

        // Act
        sut.UpdateJournalConfig(_testPath, syncResult);

        // Assert
        _console.Output.ShouldContain("No configuration changes needed");
    }

    [Fact]
    public void UpdateJournalConfig_Should_PrintConfigUpdated_When_ChangesExist()
    {
        // Arrange
        var syncResult = new JournalRegistrationDriftResult { FilesToAdd = ["2a-SomeNote.md"] };
        var sut = CreateSut();

        // Act
        sut.UpdateJournalConfig(_testPath, syncResult);

        // Assert
        _console.Output.ShouldContain("Journal configuration updated");
    }

    [Fact]
    public void UpdateJournalConfig_Should_HandleAddedAndDeletedTogether()
    {
        // Arrange
        MockJournalConfiguration
            .Setup(jc => jc.RemoveEntry(_testPath, "2a-OldNote.md"))
            .Returns(true);
        var syncResult = new JournalRegistrationDriftResult
        {
            FilesToAdd = ["3b-NewNote.md"],
            FilesToRemove = ["2a-OldNote.md"],
        };
        var sut = CreateSut();

        // Act
        sut.UpdateJournalConfig(_testPath, syncResult);

        // Assert
        MockJournalConfiguration.Verify(
            jc => jc.AddEntry(_testPath, string.Empty, "3b-NewNote.md"),
            Times.Once
        );
        MockJournalConfiguration.Verify(
            jc => jc.RemoveEntry(_testPath, "2a-OldNote.md"),
            Times.Once
        );
    }

    #endregion

    #region UpdateLastEditedDatesAndTracking

    [Fact]
    public void UpdateLastEditedDatesAndTracking_Should_UpdateLastEditedDate_When_FileIsModified()
    {
        // Arrange
        const string relativePath = "note.md";
        var absolutePath = Path.Combine(_testPath, relativePath);
        MockFileSystem
            .Setup(fs => fs.GetFileContent(absolutePath))
            .Returns("Created: 01/01/2025\n# My Note\n\nContent here.");
        MockFileSystem.Setup(fs => fs.GetDirectoryName(absolutePath)).Returns(_testPath);
        MockFileSystem.Setup(fs => fs.GetFileName(absolutePath)).Returns(relativePath);

        var fileResults = new ChangeDetectionResult { ModifiedFiles = [relativePath] };
        var sut = CreateSut();

        // Act
        sut.UpdateLastEditedDatesAndTracking(_testPath, fileResults, trackingOnly: false);

        // Assert
        MockFileSystem.Verify(
            fs =>
                fs.UpdateFile(
                    _testPath,
                    relativePath,
                    It.Is<string>(s => s.Contains("Last Edited:"))
                ),
            Times.Once
        );
    }

    [Fact]
    public void UpdateLastEditedDatesAndTracking_Should_NotUpdateLastEditedDate_When_TrackingOnly()
    {
        // Arrange
        const string relativePath = "note.md";
        var fileResults = new ChangeDetectionResult { ModifiedFiles = [relativePath] };
        var sut = CreateSut();

        // Act
        sut.UpdateLastEditedDatesAndTracking(_testPath, fileResults, trackingOnly: true);

        // Assert — content must not be written when trackingOnly is true
        MockFileSystem.Verify(
            fs => fs.UpdateFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never
        );
    }

    [Fact]
    public void UpdateLastEditedDatesAndTracking_Should_PrintReTrackedLabel_When_TrackingOnly()
    {
        // Arrange — simulates --sync: file is modified but dates must NOT be stamped
        const string relativePath = "note.md";
        var fileResults = new ChangeDetectionResult { ModifiedFiles = [relativePath] };
        var sut = CreateSut();

        // Act
        sut.UpdateLastEditedDatesAndTracking(_testPath, fileResults, trackingOnly: true);

        // Assert — output uses "Re-tracked:" (not "Updated:") and suppresses "Updated dates for" summary
        _console.Output.ShouldContain("Re-tracked:");
        _console.Output.ShouldNotContain("Updated:");
        _console.Output.ShouldNotContain("Updated dates for");
    }

    [Fact]
    public void UpdateLastEditedDatesAndTracking_Should_NotPrintUpdatedDatesSummary_When_TrackingOnly()
    {
        // Arrange
        var fileResults = new ChangeDetectionResult { ModifiedFiles = ["a.md", "b.md"] };
        var sut = CreateSut();

        // Act
        sut.UpdateLastEditedDatesAndTracking(_testPath, fileResults, trackingOnly: true);

        // Assert — "Updated dates for X file(s)." must never appear when --sync is active
        _console.Output.ShouldNotContain("Updated dates for");
    }

    [Fact]
    public void UpdateLastEditedDatesAndTracking_Should_UpdateTrackingIndex_When_FileIsModified()
    {
        // Arrange
        const string relativePath = "note.md";
        var absolutePath = Path.Combine(_testPath, relativePath);
        MockFileSystem
            .Setup(fs => fs.GetFileContent(absolutePath))
            .Returns("# My Note\n\nContent here.");
        MockFileSystem.Setup(fs => fs.GetDirectoryName(absolutePath)).Returns(_testPath);
        MockFileSystem.Setup(fs => fs.GetFileName(absolutePath)).Returns(relativePath);

        var fileResults = new ChangeDetectionResult { ModifiedFiles = [relativePath] };
        var sut = CreateSut();

        // Act
        sut.UpdateLastEditedDatesAndTracking(_testPath, fileResults, trackingOnly: false);

        // Assert — index must be updated for the modified file
        MockFileTracking.Verify(ft => ft.UpdateFileInIndex(_testPath, relativePath), Times.Once);
    }

    [Fact]
    public void UpdateLastEditedDatesAndTracking_Should_UpdateTrackingIndex_When_FileIsModifiedAndTrackingOnly()
    {
        // Arrange
        const string relativePath = "note.md";
        var fileResults = new ChangeDetectionResult { ModifiedFiles = [relativePath] };
        var sut = CreateSut();

        // Act
        sut.UpdateLastEditedDatesAndTracking(_testPath, fileResults, trackingOnly: true);

        // Assert — the index must be updated even when trackingOnly skips content updates
        MockFileTracking.Verify(ft => ft.UpdateFileInIndex(_testPath, relativePath), Times.Once);
    }

    [Fact]
    public void UpdateLastEditedDatesAndTracking_Should_AddNewFileToTrackingIndex_When_FileIsAdded()
    {
        // Arrange
        const string relativePath = "new-note.md";
        var fileResults = new ChangeDetectionResult { AddedFiles = [relativePath] };
        var sut = CreateSut();

        // Act
        sut.UpdateLastEditedDatesAndTracking(_testPath, fileResults, trackingOnly: false);

        // Assert
        MockFileTracking.Verify(ft => ft.UpdateFileInIndex(_testPath, relativePath), Times.Once);
    }

    [Fact]
    public void UpdateLastEditedDatesAndTracking_Should_NotUpdateContent_When_FileIsAdded()
    {
        // Arrange
        const string relativePath = "new-note.md";
        var fileResults = new ChangeDetectionResult { AddedFiles = [relativePath] };
        var sut = CreateSut();

        // Act
        sut.UpdateLastEditedDatesAndTracking(_testPath, fileResults, trackingOnly: false);

        // Assert — added files must not have their content modified, only tracked
        MockFileSystem.Verify(
            fs => fs.UpdateFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never
        );
    }

    [Fact]
    public void UpdateLastEditedDatesAndTracking_Should_RemoveDeletedFileFromTrackingIndex()
    {
        // Arrange
        const string relativePath = "deleted-note.md";
        var fileResults = new ChangeDetectionResult { DeletedFiles = [relativePath] };
        var sut = CreateSut();

        // Act
        sut.UpdateLastEditedDatesAndTracking(_testPath, fileResults, trackingOnly: false);

        // Assert
        MockFileTracking.Verify(ft => ft.RemoveFileFromIndex(_testPath, relativePath), Times.Once);
    }

    [Fact]
    public void UpdateLastEditedDatesAndTracking_Should_UpdateMultipleModifiedFiles()
    {
        // Arrange
        const string relativePath1 = "note-one.md";
        const string relativePath2 = "note-two.md";
        var absolutePath1 = Path.Combine(_testPath, relativePath1);
        var absolutePath2 = Path.Combine(_testPath, relativePath2);

        MockFileSystem
            .Setup(fs => fs.GetFileContent(absolutePath1))
            .Returns("Created: 01/01/2025\n# Note One");
        MockFileSystem
            .Setup(fs => fs.GetFileContent(absolutePath2))
            .Returns("Created: 01/01/2025\n# Note Two");
        MockFileSystem.Setup(fs => fs.GetDirectoryName(absolutePath1)).Returns(_testPath);
        MockFileSystem.Setup(fs => fs.GetDirectoryName(absolutePath2)).Returns(_testPath);
        MockFileSystem.Setup(fs => fs.GetFileName(absolutePath1)).Returns(relativePath1);
        MockFileSystem.Setup(fs => fs.GetFileName(absolutePath2)).Returns(relativePath2);

        var fileResults = new ChangeDetectionResult
        {
            ModifiedFiles = [relativePath1, relativePath2],
        };
        var sut = CreateSut();

        // Act
        sut.UpdateLastEditedDatesAndTracking(_testPath, fileResults, trackingOnly: false);

        // Assert
        MockFileSystem.Verify(
            fs =>
                fs.UpdateFile(
                    _testPath,
                    relativePath1,
                    It.Is<string>(s => s.Contains("Last Edited:"))
                ),
            Times.Once
        );
        MockFileSystem.Verify(
            fs =>
                fs.UpdateFile(
                    _testPath,
                    relativePath2,
                    It.Is<string>(s => s.Contains("Last Edited:"))
                ),
            Times.Once
        );
    }

    [Fact]
    public void UpdateLastEditedDatesAndTracking_Should_InsertLastEditedDate_When_FileHasNoMetadata()
    {
        // Arrange
        const string relativePath = "bare-note.md";
        var absolutePath = Path.Combine(_testPath, relativePath);
        MockFileSystem
            .Setup(fs => fs.GetFileContent(absolutePath))
            .Returns("# Bare Note\n\nJust content, no metadata.");
        MockFileSystem.Setup(fs => fs.GetDirectoryName(absolutePath)).Returns(_testPath);
        MockFileSystem.Setup(fs => fs.GetFileName(absolutePath)).Returns(relativePath);

        var fileResults = new ChangeDetectionResult { ModifiedFiles = [relativePath] };
        var sut = CreateSut();

        // Act
        sut.UpdateLastEditedDatesAndTracking(_testPath, fileResults, trackingOnly: false);

        // Assert — MarkdownMetadataParser must insert the date even when no existing metadata exists
        MockFileSystem.Verify(
            fs =>
                fs.UpdateFile(
                    _testPath,
                    relativePath,
                    It.Is<string>(s => s.Contains("Last Edited:"))
                ),
            Times.Once
        );
    }

    [Fact]
    public void UpdateLastEditedDatesAndTracking_Should_PrintUpdatedSummary_When_FilesAreModified()
    {
        // Arrange
        const string relativePath = "note.md";
        var absolutePath = Path.Combine(_testPath, relativePath);
        MockFileSystem.Setup(fs => fs.GetFileContent(absolutePath)).Returns("# My Note");
        MockFileSystem.Setup(fs => fs.GetDirectoryName(absolutePath)).Returns(_testPath);
        MockFileSystem.Setup(fs => fs.GetFileName(absolutePath)).Returns(relativePath);

        var fileResults = new ChangeDetectionResult { ModifiedFiles = [relativePath] };
        var sut = CreateSut();

        // Act
        sut.UpdateLastEditedDatesAndTracking(_testPath, fileResults, trackingOnly: false);

        // Assert
        _console.Output.ShouldContain("Updated dates for 1 file(s)");
    }

    [Fact]
    public void UpdateLastEditedDatesAndTracking_Should_PrintTrackedSummary_When_FilesAreAdded()
    {
        // Arrange
        var fileResults = new ChangeDetectionResult { AddedFiles = ["new-note.md"] };
        var sut = CreateSut();

        // Act
        sut.UpdateLastEditedDatesAndTracking(_testPath, fileResults, trackingOnly: false);

        // Assert
        _console.Output.ShouldContain("Tracked 1 new file(s)");
    }

    [Fact]
    public void UpdateLastEditedDatesAndTracking_Should_PrintRemovedSummary_When_FilesAreDeleted()
    {
        // Arrange
        var fileResults = new ChangeDetectionResult { DeletedFiles = ["gone-note.md"] };
        var sut = CreateSut();

        // Act
        sut.UpdateLastEditedDatesAndTracking(_testPath, fileResults, trackingOnly: false);

        // Assert
        _console.Output.ShouldContain("Removed 1 deleted file(s) from tracking");
    }

    [Fact]
    public void UpdateLastEditedDatesAndTracking_Should_UseConfiguredDateFormat()
    {
        // Arrange
        const string relativePath = "note.md";
        var absolutePath = Path.Combine(_testPath, relativePath);
        MockFileSystem
            .Setup(fs => fs.GetFileContent(absolutePath))
            .Returns("Created: 01/01/2025\n# My Note");
        MockFileSystem.Setup(fs => fs.GetDirectoryName(absolutePath)).Returns(_testPath);
        MockFileSystem.Setup(fs => fs.GetFileName(absolutePath)).Returns(relativePath);

        var fileResults = new ChangeDetectionResult { ModifiedFiles = [relativePath] };
        var sut = CreateSut();

        // Act
        sut.UpdateLastEditedDatesAndTracking(_testPath, fileResults, trackingOnly: false);

        // Assert — date must match the MM/dd/yyyy format configured in JournalSettings
        MockFileSystem.Verify(
            fs =>
                fs.UpdateFile(
                    _testPath,
                    relativePath,
                    It.Is<string>(s => Regex.IsMatch(s, @"Last Edited: \d{2}/\d{2}/\d{4}"))
                ),
            Times.Once
        );
    }

    #endregion

    #region UpdateTableOfContents

    [Fact]
    public void UpdateTableOfContents_Should_WriteTocFile_When_ConfigHasEntries()
    {
        // Arrange
        var config = CreateTestConfig("1a-TableOfContents.md", ["2a-SomeNote.md"]);
        MockJournalConfiguration.Setup(jc => jc.Read(_testPath)).Returns(config);
        var sut = CreateSut();

        // Act
        sut.UpdateTableOfContents(_testPath);

        // Assert — the TOC service must be invoked to write the file
        MockTableOfContentsService.Verify(
            toc => toc.UpdateTableOfContents(_testPath, null, It.IsAny<DateTime?>()),
            Times.Once
        );
    }

    [Fact]
    public void UpdateTableOfContents_Should_TrackTocFileSoItDoesNotAppearAsAdded()
    {
        // Arrange
        var config = CreateTestConfig("1a-TableOfContents.md");
        MockJournalConfiguration.Setup(jc => jc.Read(_testPath)).Returns(config);
        var sut = CreateSut();

        // Act
        sut.UpdateTableOfContents(_testPath);

        // Assert — TOC file must be recorded in the tracking index
        MockFileTracking.Verify(
            ft => ft.UpdateFileInIndex(_testPath, "1a-TableOfContents.md"),
            Times.Once
        );
    }

    [Fact]
    public void UpdateTableOfContents_Should_PrintSuccessMessage()
    {
        // Arrange
        var config = CreateTestConfig("1a-TableOfContents.md");
        MockJournalConfiguration.Setup(jc => jc.Read(_testPath)).Returns(config);
        var sut = CreateSut();

        // Act
        sut.UpdateTableOfContents(_testPath);

        // Assert
        _console.Output.ShouldContain("Table of contents updated");
    }

    [Fact]
    public void UpdateTableOfContents_Should_UseFallbackTocFileName_When_ConfigFileIsNull()
    {
        // Arrange — config with a null File value forces the fallback name to be used
        MockJournalConfiguration
            .Setup(jc => jc.Read(_testPath))
            .Returns(
                new JournalConfig
                {
                    JournalName = "Test Journal",
                    TableOfContents = new TableOfContents
                    {
                        File = null!, // force the null branch so the fallback name is used
                        Extensions = [".md"],
                    },
                }
            );
        var sut = CreateSut();

        // Act
        sut.UpdateTableOfContents(_testPath);

        // Assert — the fallback filename (TableOfContentsFileName + ".md") must be tracked
        MockFileTracking.Verify(
            ft => ft.UpdateFileInIndex(_testPath, "1a-TableOfContents.md"),
            Times.Once
        );
    }

    [Fact]
    public void UpdateTableOfContents_Should_SkipWrite_When_TocOnlyDiffersByLastEditedDate()
    {
        // Arrange
        const string tocFile = "1a-TableOfContents.md";
        var config = CreateTestConfig(tocFile);
        var tocPath = Path.Combine(_testPath, tocFile);
        var currentContent =
            "Created: 01/01/2024\nLast Edited: 01/01/2024\n\n# Table of Contents\n";
        var previewContent =
            "Created: 01/01/2024\nLast Edited: 06/20/2026\n\n# Table of Contents\n";

        MockJournalConfiguration.Setup(jc => jc.Read(_testPath)).Returns(config);
        MockFileSystem.Setup(fs => fs.FileExists(tocPath)).Returns(true);
        MockFileSystem.Setup(fs => fs.GetFileContent(tocPath)).Returns(currentContent);
        MockTableOfContentsService
            .Setup(toc => toc.PreviewTableOfContents(_testPath))
            .Returns(previewContent);
        var sut = CreateSut();

        // Act
        sut.UpdateTableOfContents(_testPath);

        // Assert
        MockTableOfContentsService.Verify(
            toc =>
                toc.UpdateTableOfContents(
                    It.IsAny<string>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<DateTime?>()
                ),
            Times.Never
        );
        MockFileTracking.Verify(
            ft => ft.UpdateFileInIndex(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never
        );
        _console.Output.ShouldContain("No table of contents changes needed");
    }

    [Fact]
    public void UpdateTableOfContents_Should_WriteTocFile_When_TocContentDiffersBeyondLastEditedDate()
    {
        // Arrange
        const string tocFile = "1a-TableOfContents.md";
        var config = CreateTestConfig(tocFile);
        var tocPath = Path.Combine(_testPath, tocFile);
        var currentContent =
            "Created: 01/01/2024\nLast Edited: 01/01/2024\n\n# Table of Contents\n";
        var previewContent =
            "Created: 01/01/2024\nLast Edited: 01/01/2024\n\n# Table of Contents\n- [Note](Note.md)\n";

        MockJournalConfiguration.Setup(jc => jc.Read(_testPath)).Returns(config);
        MockFileSystem.Setup(fs => fs.FileExists(tocPath)).Returns(true);
        MockFileSystem.Setup(fs => fs.GetFileContent(tocPath)).Returns(currentContent);
        MockTableOfContentsService
            .Setup(toc => toc.PreviewTableOfContents(_testPath))
            .Returns(previewContent);
        var sut = CreateSut();

        // Act
        sut.UpdateTableOfContents(_testPath);

        // Assert
        MockTableOfContentsService.Verify(
            toc => toc.UpdateTableOfContents(_testPath, null, It.IsAny<DateTime?>()),
            Times.Once
        );
        MockFileTracking.Verify(ft => ft.UpdateFileInIndex(_testPath, tocFile), Times.Once);
    }

    #endregion

    #region RenameToc

    [Fact]
    public void RenameToc_Should_RenameFileAndUpdateConfigAndRewriteLinksAndUpdateTracking()
    {
        // Arrange
        const string oldTocFile = "1a-TableOfContents.md";
        const string newTocName = "MyContents";
        const string newTocFile = "MyContents.md";
        const string noteRelPath = "notes/intro.md";

        var config = CreateTestConfig(oldTocFile);
        MockJournalConfiguration.Setup(jc => jc.Read(_testPath)).Returns(config);

        // No conflict — new TOC path does not exist
        var newTocAbsPath = Path.Combine(_testPath, newTocFile);
        MockFileSystem.Setup(fs => fs.FileExists(newTocAbsPath)).Returns(false);

        // Link rewriter finds one file and rewrites it
        _mockMarkdownLinkRewriter
            .Setup(r => r.FindFilesWithLinkTo(_testPath, oldTocFile))
            .Returns([noteRelPath]);
        _mockMarkdownLinkRewriter
            .Setup(r =>
                r.ReplaceLinksInDirectory(
                    _testPath,
                    oldTocFile,
                    newTocFile,
                    It.IsAny<IReadOnlyList<string>>()
                )
            )
            .Returns([noteRelPath]);

        var noteAbsPath = Path.Combine(_testPath, noteRelPath);
        var noteDir = Path.GetDirectoryName(noteAbsPath)!;
        MockFileSystem
            .Setup(fs => fs.GetFileContent(noteAbsPath))
            .Returns($"Created: 01/01/2025\n# Intro\nSee [TOC]({newTocFile}).");
        MockFileSystem.Setup(fs => fs.GetDirectoryName(noteAbsPath)).Returns(noteDir);
        MockFileSystem.Setup(fs => fs.GetFileName(noteAbsPath)).Returns("intro.md");

        var sut = CreateSut();

        // Act
        sut.RenameToc(_testPath, newTocName);

        // Assert — file renamed
        var oldTocAbsPath = Path.Combine(_testPath, oldTocFile);
        MockFileSystem.Verify(fs => fs.RenameFile(oldTocAbsPath, newTocAbsPath), Times.Once);

        // Config updated
        MockJournalConfiguration.Verify(
            jc => jc.Update(_testPath, It.IsAny<Action<JournalConfig>>()),
            Times.Once
        );

        // Tracking renamed
        MockFileTracking.Verify(
            ft => ft.RenameFileInIndex(_testPath, oldTocFile, newTocFile),
            Times.Once
        );

        // Backlink file stamped with Last Edited and re-tracked
        MockFileSystem.Verify(
            fs =>
                fs.UpdateFile(noteDir, "intro.md", It.Is<string>(s => s.Contains("Last Edited:"))),
            Times.Once
        );
        MockFileTracking.Verify(ft => ft.UpdateFileInIndex(_testPath, noteRelPath), Times.Once);

        // Console output contains expected messages
        _console.Output.ShouldContain($"Renamed TOC: {oldTocFile} → {newTocFile}");
        _console.Output.ShouldContain("Last Edited updated for 1 file(s).");
    }

    [Fact]
    public void RenameToc_Should_SkipRenameButRewriteStaleLinks_When_AlreadyNamedCorrectly()
    {
        // Arrange — config already uses the requested name
        const string tocFile = "1a-TableOfContents.md";
        var config = CreateTestConfig(tocFile);
        MockJournalConfiguration.Setup(jc => jc.Read(_testPath)).Returns(config);
        var sut = CreateSut();

        // Act — pass same stem (no change in name)
        sut.RenameToc(_testPath, "1a-TableOfContents");

        // Assert — no rename or config update should occur
        MockFileSystem.Verify(
            fs => fs.RenameFile(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never
        );
        MockJournalConfiguration.Verify(
            jc => jc.Update(It.IsAny<string>(), It.IsAny<Action<JournalConfig>>()),
            Times.Never
        );
        _console.Output.ShouldNotContain("Renamed TOC:");
    }

    [Fact]
    public void RenameToc_Should_ThrowTocRenameConflictException_When_TargetFileAlreadyExists()
    {
        // Arrange
        var config = CreateTestConfig("1a-TableOfContents.md");
        MockJournalConfiguration.Setup(jc => jc.Read(_testPath)).Returns(config);
        MockFileSystem
            .Setup(fs => fs.FileExists(Path.Combine(_testPath, "MyContents.md")))
            .Returns(true);
        var sut = CreateSut();

        // Act & Assert
        Should.Throw<TocRenameConflictException>(() => sut.RenameToc(_testPath, "MyContents"));
    }

    [Fact]
    public void RenameToc_Should_PrintNoLinkMessage_When_NoFilesReferenceTheToc()
    {
        // Arrange
        const string oldTocFile = "1a-TableOfContents.md";
        const string newTocName = "MyContents";
        const string newTocFile = "MyContents.md";

        var config = CreateTestConfig(oldTocFile);
        MockJournalConfiguration.Setup(jc => jc.Read(_testPath)).Returns(config);
        MockFileSystem
            .Setup(fs => fs.FileExists(Path.Combine(_testPath, newTocFile)))
            .Returns(false);

        _mockMarkdownLinkRewriter
            .Setup(r => r.FindFilesWithLinkTo(_testPath, oldTocFile))
            .Returns([]);
        _mockMarkdownLinkRewriter
            .Setup(r =>
                r.ReplaceLinksInDirectory(
                    _testPath,
                    oldTocFile,
                    newTocFile,
                    It.IsAny<IReadOnlyList<string>>()
                )
            )
            .Returns([]);

        var sut = CreateSut();

        // Act
        sut.RenameToc(_testPath, newTocName);

        // Assert
        _console.Output.ShouldContain("No link references needed updating.");
    }

    [Fact]
    public void RenameToc_Should_RewriteAllLinksAndUpdateTracking_When_MultipleFilesReferToc()
    {
        // Arrange
        const string oldTocFile = "1a-TableOfContents.md";
        const string newTocName = "MyContents";
        const string newTocFile = "MyContents.md";

        var config = CreateTestConfig(oldTocFile);
        MockJournalConfiguration.Setup(jc => jc.Read(_testPath)).Returns(config);
        MockFileSystem
            .Setup(fs => fs.FileExists(Path.Combine(_testPath, newTocFile)))
            .Returns(false);

        _mockMarkdownLinkRewriter
            .Setup(r => r.FindFilesWithLinkTo(_testPath, oldTocFile))
            .Returns([]);
        _mockMarkdownLinkRewriter
            .Setup(r =>
                r.ReplaceLinksInDirectory(
                    _testPath,
                    oldTocFile,
                    newTocFile,
                    It.IsAny<IReadOnlyList<string>>()
                )
            )
            .Returns(["intro.md", "chapter-1.md"]);

        var introAbsPath = Path.Combine(_testPath, "intro.md");
        var ch1AbsPath = Path.Combine(_testPath, "chapter-1.md");
        MockFileSystem
            .Setup(fs => fs.GetFileContent(introAbsPath))
            .Returns($"Created: 01/01/2025\n# Intro\n[TOC]({newTocFile})");
        MockFileSystem
            .Setup(fs => fs.GetFileContent(ch1AbsPath))
            .Returns($"Created: 01/01/2025\n# Chapter 1\n[TOC]({newTocFile})");
        MockFileSystem.Setup(fs => fs.GetDirectoryName(introAbsPath)).Returns(_testPath);
        MockFileSystem.Setup(fs => fs.GetDirectoryName(ch1AbsPath)).Returns(_testPath);
        MockFileSystem.Setup(fs => fs.GetFileName(introAbsPath)).Returns("intro.md");
        MockFileSystem.Setup(fs => fs.GetFileName(ch1AbsPath)).Returns("chapter-1.md");

        var sut = CreateSut();

        // Act
        sut.RenameToc(_testPath, newTocName);

        // Assert — both files stamped with Last Edited
        MockFileSystem.Verify(
            fs =>
                fs.UpdateFile(
                    _testPath,
                    "intro.md",
                    It.Is<string>(s => s.Contains("Last Edited:"))
                ),
            Times.Once
        );
        MockFileSystem.Verify(
            fs =>
                fs.UpdateFile(
                    _testPath,
                    "chapter-1.md",
                    It.Is<string>(s => s.Contains("Last Edited:"))
                ),
            Times.Once
        );
        _console.Output.ShouldContain("Last Edited updated for 2 file(s).");
    }

    [Fact]
    public void RenameToc_Should_NotModifyFiles_When_ConflictIsPreflightGuard()
    {
        // When the rename target already exists, the conflict must be detected
        // BEFORE any file writes so the journal is left completely untouched (exit 1 / guard).
        // Before this fix, a full rollback (exit 2) was triggered after rewriting backlinks.
        var config = CreateTestConfig("1a-TableOfContents.md");
        MockJournalConfiguration.Setup(jc => jc.Read(_testPath)).Returns(config);
        MockFileSystem
            .Setup(fs => fs.FileExists(Path.Combine(_testPath, "NewName.md")))
            .Returns(true);
        var sut = CreateSut();

        // Act & Assert — throws TocRenameConflictException (not a rollback exception)
        Should.Throw<TocRenameConflictException>(() => sut.RenameToc(_testPath, "NewName"));

        // Verify: no file writes occurred — no link rewrites happened before the guard
        MockFileSystem.Verify(
            fs => fs.UpdateFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never
        );
        MockJournalConfiguration.Verify(
            jc => jc.Update(It.IsAny<string>(), It.IsAny<Action<JournalConfig>>()),
            Times.Never
        );
    }

    [Fact]
    public void RenameToc_Should_NotWrapConflictExceptionInRollbackCompletedException()
    {
        // TocRenameConflictException must propagate as-is (not wrapped in
        // RollbackCompletedException) so UpdateCommand can catch it and return exit 1.
        var config = CreateTestConfig("1a-TableOfContents.md");
        MockJournalConfiguration.Setup(jc => jc.Read(_testPath)).Returns(config);
        MockFileSystem
            .Setup(fs => fs.FileExists(Path.Combine(_testPath, "AnotherFile.md")))
            .Returns(true);
        var sut = CreateSut();

        var exception = Should.Throw<TocRenameConflictException>(() =>
            sut.RenameToc(_testPath, "AnotherFile")
        );
        exception.ShouldNotBeNull();
    }

    #endregion

    #region BuildDryRunReport

    // ── todo 22: tracking + config + toc (implicit --dry-run all) ─────────────

    [Fact]
    public void BuildDryRunReport_Should_ReflectPendingTrackingAdditionInConfig_When_AllSectionsPresent()
    {
        // Arrange — journal has "existing.md" tracked and in config;
        // a new "new-entry.md" is pending in tracking but not yet in config.
        var config = CreateTestConfig("1a-TableOfContents.md", ["existing.md"]);
        MockJournalConfiguration.Setup(jc => jc.Read(_testPath)).Returns(config);

        MockFileTracking
            .Setup(ft => ft.LoadIndex(_testPath))
            .Returns(
                new JournalIndex
                {
                    Files = new Dictionary<string, FileState>
                    {
                        {
                            "1a-TableOfContents.md",
                            new FileState { FilePath = "1a-TableOfContents.md", Hash = "h1" }
                        },
                        {
                            "existing.md",
                            new FileState { FilePath = "existing.md", Hash = "h2" }
                        },
                    },
                }
            );

        // TOC file does not exist yet on disk
        MockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(false);

        // Preview returns the root entry file names of the projected toc structure
        MockTableOfContentsService
            .Setup(toc =>
                toc.PreviewTableOfContents(
                    _testPath,
                    It.IsAny<JournalConfig>(),
                    It.IsAny<JournalTocStructure>()
                )
            )
            .Returns(
                (string _, JournalConfig _, JournalTocStructure s) =>
                    string.Join("\n", s.RootEntries.Select(e => e.File))
            );

        var trackingChanges = new ChangeDetectionResult { AddedFiles = ["new-entry.md"] };
        // configChanges is non-null so projection is triggered
        var naiveConfigChanges = new JournalRegistrationDriftResult();
        var sut = CreateSut();

        // Act
        var report = sut.BuildDryRunReport(
            _testPath,
            trackingChanges,
            naiveConfigChanges,
            includeToc: true,
            renameTocTarget: null
        );

        // Assert — projected config drift includes the pending addition
        report.ConfigChanges.ShouldNotBeNull();
        report.ConfigChanges!.FilesToAdd.ShouldContain("new-entry.md");

        // TOC preview should include the new entry
        report.TocPreview.ShouldNotBeNull();
        report.TocPreview!.PreviewContent.ShouldContain("new-entry.md");
    }

    [Fact]
    public void BuildDryRunReport_Should_ReflectPendingTrackingDeletionInConfig_When_AllSectionsPresent()
    {
        // Arrange — journal has "going-away.md" tracked and in config; it is being deleted.
        var config = CreateTestConfig("1a-TableOfContents.md", ["going-away.md"]);
        MockJournalConfiguration.Setup(jc => jc.Read(_testPath)).Returns(config);

        MockFileTracking
            .Setup(ft => ft.LoadIndex(_testPath))
            .Returns(
                new JournalIndex
                {
                    Files = new Dictionary<string, FileState>
                    {
                        {
                            "1a-TableOfContents.md",
                            new FileState { FilePath = "1a-TableOfContents.md", Hash = "h1" }
                        },
                        {
                            "going-away.md",
                            new FileState { FilePath = "going-away.md", Hash = "h2" }
                        },
                    },
                }
            );

        MockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(false);

        // Set up toc structure so "going-away.md" is recognized as a registered entry
        MockTocStructureRepository
            .Setup(r => r.Load(It.IsAny<string>()))
            .Returns(
                new JournalTocStructure
                {
                    Structure = new Structure { Topics = [] },
                    RootEntries = [new Entries { Name = "going-away", File = "going-away.md" }],
                }
            );

        // Preview returns root entry file names of the projected toc structure
        MockTableOfContentsService
            .Setup(toc =>
                toc.PreviewTableOfContents(
                    _testPath,
                    It.IsAny<JournalConfig>(),
                    It.IsAny<JournalTocStructure>()
                )
            )
            .Returns(
                (string _, JournalConfig _, JournalTocStructure s) =>
                    string.Join("\n", s.RootEntries.Select(e => e.File))
            );

        var trackingChanges = new ChangeDetectionResult { DeletedFiles = ["going-away.md"] };
        var naiveConfigChanges = new JournalRegistrationDriftResult();
        var sut = CreateSut();

        // Act
        var report = sut.BuildDryRunReport(
            _testPath,
            trackingChanges,
            naiveConfigChanges,
            includeToc: true,
            renameTocTarget: null
        );

        // Assert — projected config drift includes the pending removal
        report.ConfigChanges.ShouldNotBeNull();
        report.ConfigChanges!.FilesToRemove.ShouldContain("going-away.md");

        // TOC preview should NOT include the deleted entry
        report.TocPreview.ShouldNotBeNull();
        report.TocPreview!.PreviewContent.ShouldNotContain("going-away.md");
    }

    // ── todo 23: config + toc (no tracking) ──────────────────────────────────

    [Fact]
    public void BuildDryRunReport_Should_ReflectCurrentConfigDriftInToc_When_ConfigAndTocPresentButNoTracking()
    {
        // Arrange — "unregistered.md" is in configChanges.FilesToAdd but not yet in config;
        // trackingChanges is null so no projection occurs — effectiveConfigChanges = configChanges.
        var config = CreateTestConfig("1a-TableOfContents.md");
        MockJournalConfiguration.Setup(jc => jc.Read(_testPath)).Returns(config);
        MockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(false);

        MockTableOfContentsService
            .Setup(toc =>
                toc.PreviewTableOfContents(
                    _testPath,
                    It.IsAny<JournalConfig>(),
                    It.IsAny<JournalTocStructure>()
                )
            )
            .Returns(
                (string _, JournalConfig _, JournalTocStructure s) =>
                    string.Join("\n", s.RootEntries.Select(e => e.File))
            );

        var configChanges = new JournalRegistrationDriftResult { FilesToAdd = ["unregistered.md"] };
        var sut = CreateSut();

        // Act — no tracking changes passed (null)
        var report = sut.BuildDryRunReport(
            _testPath,
            trackingChanges: null,
            configChanges: configChanges,
            includeToc: true,
            renameTocTarget: null
        );

        // Assert — TOC preview includes the file that config drift would add
        report.TocPreview.ShouldNotBeNull();
        report.TocPreview!.PreviewContent.ShouldContain("unregistered.md");
    }

    // ── todo 24a: tracking + toc (no config) — TOC unchanged ─────────────────

    [Fact]
    public void BuildDryRunReport_Should_UseCurrentConfigInToc_When_TrackingAndTocPresentButNoConfig()
    {
        // Arrange — configChanges is null so no projection occurs and TOC uses current config.
        var config = CreateTestConfig("1a-TableOfContents.md", ["existing.md"]);
        MockJournalConfiguration.Setup(jc => jc.Read(_testPath)).Returns(config);
        MockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(false);

        // Current (non-projected) preview contains only "existing.md"
        MockTableOfContentsService
            .Setup(toc => toc.PreviewTableOfContents(_testPath))
            .Returns("existing.md");

        var trackingChanges = new ChangeDetectionResult { AddedFiles = ["pending.md"] };
        var sut = CreateSut();

        // Act — configChanges is null (--tracking --toc, no --config)
        var report = sut.BuildDryRunReport(
            _testPath,
            trackingChanges,
            configChanges: null,
            includeToc: true,
            renameTocTarget: null
        );

        // Assert — TOC preview uses current .journalrc; "pending.md" is NOT in the preview
        // because config is out of scope (tracking alone doesn't mutate .journalrc)
        report.TocPreview.ShouldNotBeNull();
        report.TocPreview!.PreviewContent.ShouldNotContain("pending.md");
    }

    // ── todo 24b: toc only — TOC uses current config ─────────────────────────

    [Fact]
    public void BuildDryRunReport_Should_UseCurrentConfigInToc_When_TocOnly()
    {
        // Arrange
        var config = CreateTestConfig("1a-TableOfContents.md", ["note.md"]);
        MockJournalConfiguration.Setup(jc => jc.Read(_testPath)).Returns(config);
        MockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(false);

        MockTableOfContentsService
            .Setup(toc => toc.PreviewTableOfContents(_testPath))
            .Returns("note.md");

        var sut = CreateSut();

        // Act — no tracking changes, no config changes (--toc only)
        var report = sut.BuildDryRunReport(
            _testPath,
            trackingChanges: null,
            configChanges: null,
            includeToc: true,
            renameTocTarget: null
        );

        // Assert — TOC preview reflects only what's currently in .journalrc
        report.TocPreview.ShouldNotBeNull();
        report.TocPreview!.PreviewContent.ShouldContain("note.md");
    }

    #endregion
}
