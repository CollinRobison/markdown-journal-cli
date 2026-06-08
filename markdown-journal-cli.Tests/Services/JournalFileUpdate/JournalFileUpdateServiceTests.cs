using markdown_journal_cli;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.Configuration.Models;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.Tracking;
using markdown_journal_cli.Infrastructure.Transactions;
using markdown_journal_cli.Services;
using markdown_journal_cli.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace markdown_journal_cli.Tests.Services;

/// <summary>
/// Unit tests for JournalFileUpdateService covering all public methods.
/// </summary>
public class JournalFileUpdateServiceTests : ServiceTestBase
{
    private readonly Mock<IMarkdownLinkRewriter> _mockMarkdownLinkRewriter;
    private readonly JournalFileUpdateService _service;

    private const string Directory = "/test/journal";
    private const string OldFile = "old_file.md";
    private const string NewFile = "new_file.md";
    private const string TestFile = "test_file.md";
    private const string TocFile = "1a-TableOfContents.md";

    public JournalFileUpdateServiceTests()
    {
        _mockMarkdownLinkRewriter = new Mock<IMarkdownLinkRewriter>();

        _service = new JournalFileUpdateService(
            MockFileSystem.Object,
            MockJournalConfiguration.Object,
            MockEntryFormatterService.Object,
            MockTableOfContentsService.Object,
            JournalSettings,
            NullLogger<JournalFileUpdateService>(),
            MockFileTracking.Object,
            _mockMarkdownLinkRewriter.Object,
            NoOpCoordinator,
            NoOpReporter
        );
    }

    #region RenameEntry Tests

    [Fact]
    public void RenameEntry_Should_RenameFileAndUpdateConfig_When_FileExists()
    {
        // Arrange
        var oldPath = $"{Directory}/{OldFile}";
        var newPath = $"{Directory}/{NewFile}";

        MockFileSystem.Setup(fs => fs.CombinePaths(Directory, OldFile)).Returns(oldPath);
        MockFileSystem.Setup(fs => fs.CombinePaths(Directory, NewFile)).Returns(newPath);
        MockFileSystem.Setup(fs => fs.FileExists(oldPath)).Returns(true);

        // Act
        _service.RenameEntry(Directory, OldFile, NewFile);

        // Assert
        MockFileSystem.Verify(fs => fs.RenameFile(oldPath, newPath), Times.Once);
        MockJournalConfiguration.Verify(
            jc => jc.UpdateFileReferences(Directory, OldFile, NewFile),
            Times.Once
        );
        MockFileTracking.Verify(
            ft => ft.RenameFileInIndex(Directory, OldFile, NewFile),
            Times.Once
        );
    }

    [Fact]
    public void RenameEntry_Should_NotUpdateTrackingIndex_When_FileNotFound()
    {
        // Arrange
        var oldPath = $"{Directory}/{OldFile}";

        MockFileSystem.Setup(fs => fs.CombinePaths(Directory, OldFile)).Returns(oldPath);
        MockFileSystem.Setup(fs => fs.FileExists(oldPath)).Returns(false);

        // Act & Assert
        Should.Throw<FileNotFoundException>(() =>
            _service.RenameEntry(Directory, OldFile, NewFile)
        );

        MockFileTracking.Verify(
            ft => ft.RenameFileInIndex(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never
        );
    }

    [Fact]
    public void RenameEntry_Should_ThrowFileNotFoundException_When_FileDoesNotExist()
    {
        // Arrange
        var oldPath = $"{Directory}/{OldFile}";

        MockFileSystem.Setup(fs => fs.CombinePaths(Directory, OldFile)).Returns(oldPath);
        MockFileSystem.Setup(fs => fs.FileExists(oldPath)).Returns(false);

        // Act & Assert
        var exception = Should.Throw<FileNotFoundException>(() =>
            _service.RenameEntry(Directory, OldFile, NewFile)
        );

        exception.Message.ShouldContain(OldFile);
        exception.Message.ShouldContain(Directory);

        MockFileSystem.Verify(
            fs => fs.RenameFile(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never
        );
        MockJournalConfiguration.Verify(
            jc =>
                jc.UpdateFileReferences(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never
        );
    }

    #endregion

    #region UpdateEntryLocation Tests

    [Fact]
    public void UpdateEntryLocation_Should_RemoveAndAddEntry_When_TopicPathProvided()
    {
        // Arrange
        var newTopicPath = new[] { "Projects", "2024" };
        var displayName = "Test Entry";

        // Act
        _service.UpdateEntryLocation(Directory, TestFile, newTopicPath, displayName);

        // Assert
        MockJournalConfiguration.Verify(jc => jc.RemoveEntry(Directory, TestFile), Times.Once);
        MockJournalConfiguration.Verify(
            jc => jc.AddEntry(Directory, displayName, TestFile, newTopicPath, null, true, false),
            Times.Once
        );
    }

    [Fact]
    public void UpdateEntryLocation_Should_AddEntryToRoot_When_TopicPathIsEmpty()
    {
        // Arrange
        var emptyTopicPath = Array.Empty<string>();
        var displayName = "Root Entry";

        // Act
        _service.UpdateEntryLocation(Directory, TestFile, emptyTopicPath, displayName);

        // Assert
        MockJournalConfiguration.Verify(jc => jc.RemoveEntry(Directory, TestFile), Times.Once);
        MockJournalConfiguration.Verify(
            jc => jc.AddEntry(Directory, displayName, TestFile, null, null, true, false),
            Times.Once
        );
    }

    #endregion

    #region UpdateEntryDisplayName Tests

    [Fact]
    public void UpdateEntryDisplayName_Should_UpdateSuccessfully_When_EntryExists()
    {
        // Arrange
        var newDisplayName = "New Display Name";
        MockJournalConfiguration
            .Setup(jc => jc.UpdateEntryName(Directory, TestFile, newDisplayName))
            .Returns(true);

        // Act
        _service.UpdateEntryDisplayName(Directory, TestFile, newDisplayName);

        // Assert
        MockJournalConfiguration.Verify(
            jc => jc.UpdateEntryName(Directory, TestFile, newDisplayName),
            Times.Once
        );
    }

    [Fact]
    public void UpdateEntryDisplayName_Should_LogWarning_When_EntryDoesNotExist()
    {
        // Arrange
        var newDisplayName = "New Display Name";
        MockJournalConfiguration
            .Setup(jc => jc.UpdateEntryName(Directory, TestFile, newDisplayName))
            .Returns(false);

        // Act
        _service.UpdateEntryDisplayName(Directory, TestFile, newDisplayName);

        // Assert - should complete without exception even if entry not found
        MockJournalConfiguration.Verify(
            jc => jc.UpdateEntryName(Directory, TestFile, newDisplayName),
            Times.Once
        );
    }

    #endregion

    #region SetIgnoreStatus Tests

    [Fact]
    public void SetIgnoreStatus_Should_AddToIgnoreList_When_StatusIsTrue()
    {
        // Arrange

        // Act
        _service.SetIgnoreStatus(Directory, TestFile, true);

        // Assert
        MockJournalConfiguration.Verify(jc => jc.RemoveEntry(Directory, TestFile), Times.Once);
        MockJournalConfiguration.Verify(jc => jc.AddIgnoreEntry(Directory, TestFile), Times.Once);
    }

    [Fact]
    public void SetIgnoreStatus_Should_RemoveFromIgnoreList_When_StatusIsFalse()
    {
        // Arrange
        MockJournalConfiguration
            .Setup(jc => jc.Update(Directory, It.IsAny<Action<JournalConfig>>()))
            .Callback<string, Action<JournalConfig>>(
                (dir, action) =>
                {
                    var config = new JournalConfig
                    {
                        JournalName = "Test",
                        TableOfContents = new TableOfContents
                        {
                            File = "toc.md",
                            IgnoreFiles = new[] { TestFile, "other_file.md" },
                        },
                    };
                    action(config);

                    // Verify the ignore files were updated correctly
                    config.TableOfContents.IgnoreFiles.ShouldNotContain(TestFile);
                    config.TableOfContents.IgnoreFiles.ShouldContain("other_file.md");
                }
            );

        MockFileSystem.Setup(fs => fs.GetFileNameWithoutExtension(TestFile)).Returns("test");

        // Act
        _service.SetIgnoreStatus(Directory, TestFile, false);

        // Assert
        MockJournalConfiguration.Verify(
            jc => jc.Update(Directory, It.IsAny<Action<JournalConfig>>()),
            Times.Once
        );

        // Verify that AddEntry was called to add the file back to the structure
        MockJournalConfiguration.Verify(
            jc => jc.AddEntry(Directory, "", TestFile, null, null, true, false),
            Times.Once
        );
    }

    [Fact]
    public void SetIgnoreStatus_Should_HandleGracefully_When_IgnoreFilesIsNull()
    {
        // Arrange
        MockJournalConfiguration
            .Setup(jc => jc.Update(Directory, It.IsAny<Action<JournalConfig>>()))
            .Callback<string, Action<JournalConfig>>(
                (dir, action) =>
                {
                    var config = new JournalConfig
                    {
                        JournalName = "Test",
                        TableOfContents = new TableOfContents
                        {
                            File = "toc.md",
                            IgnoreFiles = null,
                        },
                    };
                    action(config);
                }
            );

        MockFileSystem.Setup(fs => fs.GetFileNameWithoutExtension(TestFile)).Returns("test");

        // Act
        _service.SetIgnoreStatus(Directory, TestFile, false);

        // Assert - should complete without exception
        MockJournalConfiguration.Verify(
            jc => jc.Update(Directory, It.IsAny<Action<JournalConfig>>()),
            Times.Once
        );

        // Verify that AddEntry was called to add the file back to the structure
        MockJournalConfiguration.Verify(
            jc => jc.AddEntry(Directory, "", TestFile, null, null, true, false),
            Times.Once
        );
    }

    #endregion

    #region UpdateEntry Tests

    [Fact]
    public void UpdateEntry_Should_UpdateBothNameAndFile_When_DisplayNameMatchesFilename()
    {
        // Arrange
        const string currentFile = "abc-test_2-test_file_10.md";
        const string currentFileWithoutExt = "abc-test_2-test_file_10";
        const string newEntryName = "new_entry_name";
        const string newFileName = "abc-test_2-new_entry_name.md"; // heading prefix preserved
        const string newFileNameWithoutExt = "abc-test_2-new_entry_name";
        const string displayName = "test file 10"; // Matches the last part of filename
        const string newDisplayName = "new entry name";

        var currentEntry = new Entries { Name = displayName, File = currentFile };
        var currentTopicPath = new[] { "abc", "test 2" };

        SetupBasicFileAndJournalrcExists(currentFile);

        MockJournalConfiguration
            .Setup(jc => jc.FindEntry(Directory, currentFile))
            .Returns((currentEntry, currentTopicPath));

        MockEntryFormatterService
            .Setup(ef => ef.AddSpaceSeparators(newEntryName))
            .Returns(newEntryName);

        MockEntryFormatterService
            .Setup(ef => ef.AddHeadingSeparators(It.IsAny<string[]>()))
            .Returns(newFileNameWithoutExt);

        MockEntryFormatterService.Setup(ef => ef.RemoveSpaceSeparators("abc")).Returns("abc");

        MockEntryFormatterService.Setup(ef => ef.RemoveSpaceSeparators("test_2")).Returns("test 2");

        MockEntryFormatterService
            .Setup(ef => ef.RemoveSpaceSeparators("test_file_10"))
            .Returns(displayName);

        MockEntryFormatterService
            .Setup(ef => ef.RemoveSpaceSeparators(newEntryName))
            .Returns(newDisplayName);

        MockFileSystem
            .Setup(fs => fs.CombinePaths(Directory, currentFile))
            .Returns($"{Directory}/{currentFile}");

        MockFileSystem
            .Setup(fs => fs.CombinePaths(Directory, newFileName))
            .Returns($"{Directory}/{newFileName}");

        MockFileSystem
            .Setup(fs => fs.GetFileNameWithoutExtension(currentFile))
            .Returns(currentFileWithoutExt);

        MockFileSystem
            .Setup(fs => fs.GetFileNameWithoutExtension(newFileName))
            .Returns(newFileNameWithoutExt);

        // Act
        _service.UpdateEntry(Directory, currentFileWithoutExt, newEntryName: newEntryName);

        // Assert - should rename file (heading prefix preserved)
        MockFileSystem.Verify(
            fs => fs.RenameFile($"{Directory}/{currentFile}", $"{Directory}/{newFileName}"),
            Times.Once
        );

        // Assert - should update display name because it matched
        MockJournalConfiguration.Verify(
            jc => jc.UpdateEntryName(Directory, newFileName, newDisplayName),
            Times.Once
        );

        // Assert - should regenerate TOC
        MockTableOfContentsService.Verify(
            toc => toc.UpdateTableOfContents(Directory, null, It.IsAny<DateTime>()),
            Times.Once
        );
    }

    [Fact]
    public void UpdateEntry_Should_RenameFileAndMoveEntryInConfig_When_HeadingsChanged()
    {
        // Arrange - "collin-test.md" display="test"; --headings robison should rename the file
        // to "robison-test.md" (new heading prefix + existing entry name) and move entry in config.
        const string currentFile = "collin-test.md";
        const string currentFileWithoutExt = "collin-test";
        const string newFileName = "robison-test.md";
        const string displayName = "test";

        var currentEntry = new Entries { Name = displayName, File = currentFile };
        var currentTopicPath = Array.Empty<string>(); // at root

        SetupBasicFileAndJournalrcExists(currentFile);

        MockJournalConfiguration
            .Setup(jc => jc.FindEntry(Directory, currentFile))
            .Returns((currentEntry, currentTopicPath));

        // DetermineTargetFileName (--headings path): AddHeadingSeparators(["robison","test"])
        MockEntryFormatterService
            .Setup(ef =>
                ef.AddHeadingSeparators(
                    It.Is<string[]>(a => a.Length == 2 && a[0] == "robison" && a[1] == "test")
                )
            )
            .Returns("robison-test");

        // DetermineTargetTopicPath (--headings explicit): SeperateSubheadingString("robison")
        MockEntryFormatterService
            .Setup(ef => ef.SeperateSubheadingString("robison"))
            .Returns(new[] { "robison" });

        MockFileSystem
            .Setup(fs => fs.CombinePaths(Directory, newFileName))
            .Returns($"{Directory}/{newFileName}");

        MockFileSystem.Setup(fs => fs.FileExists($"{Directory}/{newFileName}")).Returns(false);

        MockFileSystem
            .Setup(fs => fs.GetFileNameWithoutExtension(currentFile))
            .Returns(currentFileWithoutExt);

        // Act
        _service.UpdateEntry(Directory, currentFileWithoutExt, newHeadings: "robison");

        // Assert - file renamed from collin-test.md to robison-test.md
        MockFileSystem.Verify(
            fs => fs.RenameFile($"{Directory}/{currentFile}", $"{Directory}/{newFileName}"),
            Times.Once
        );

        // Assert - entry moved to robison heading with display name "test" (unchanged)
        MockJournalConfiguration.Verify(jc => jc.RemoveEntry(Directory, newFileName), Times.Once);
        MockJournalConfiguration.Verify(
            jc =>
                jc.AddEntry(
                    Directory,
                    displayName,
                    newFileName,
                    new[] { "robison" },
                    null,
                    true,
                    false
                ),
            Times.Once
        );

        // Assert - no display name update (it didn't change)
        MockJournalConfiguration.Verify(
            jc => jc.UpdateEntryName(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never
        );

        // Assert - TOC regenerated
        MockTableOfContentsService.Verify(
            toc => toc.UpdateTableOfContents(Directory, null, It.IsAny<DateTime>()),
            Times.Once
        );
    }

    [Fact]
    public void UpdateEntry_Should_PreserveDisplayName_When_DisplayNameDiffersFromFilename()
    {
        // Arrange
        const string currentFile = "abc-test_2-test_file_10.md";
        const string currentFileWithoutExt = "abc-test_2-test_file_10";
        const string newEntryName = "new_entry_name";
        const string newFileName = "abc-test_2-new_entry_name.md"; // heading prefix preserved
        const string newFileNameWithoutExt = "abc-test_2-new_entry_name";
        const string customDisplayName = "My Custom Title"; // Does NOT match filename
        const string expectedFromFile = "test file 10";

        var currentEntry = new Entries { Name = customDisplayName, File = currentFile };
        var currentTopicPath = new[] { "abc", "test 2" };

        SetupBasicFileAndJournalrcExists(currentFile);

        MockJournalConfiguration
            .Setup(jc => jc.FindEntry(Directory, currentFile))
            .Returns((currentEntry, currentTopicPath));

        MockEntryFormatterService
            .Setup(ef => ef.AddSpaceSeparators(newEntryName))
            .Returns(newEntryName);

        MockEntryFormatterService
            .Setup(ef => ef.AddHeadingSeparators(It.IsAny<string[]>()))
            .Returns(newFileNameWithoutExt);

        MockEntryFormatterService.Setup(ef => ef.RemoveSpaceSeparators("abc")).Returns("abc");

        MockEntryFormatterService.Setup(ef => ef.RemoveSpaceSeparators("test_2")).Returns("test 2");

        MockEntryFormatterService
            .Setup(ef => ef.RemoveSpaceSeparators("test_file_10"))
            .Returns(expectedFromFile);

        MockFileSystem
            .Setup(fs => fs.CombinePaths(Directory, currentFile))
            .Returns($"{Directory}/{currentFile}");

        MockFileSystem
            .Setup(fs => fs.CombinePaths(Directory, newFileName))
            .Returns($"{Directory}/{newFileName}");

        MockFileSystem
            .Setup(fs => fs.GetFileNameWithoutExtension(currentFile))
            .Returns(currentFileWithoutExt);

        MockFileSystem
            .Setup(fs => fs.GetFileNameWithoutExtension(newFileName))
            .Returns(newFileNameWithoutExt);

        // Act
        _service.UpdateEntry(Directory, currentFileWithoutExt, newEntryName: newEntryName);

        // Assert - should rename file (heading prefix preserved)
        MockFileSystem.Verify(
            fs => fs.RenameFile($"{Directory}/{currentFile}", $"{Directory}/{newFileName}"),
            Times.Once
        );

        // Assert - should NOT update display name (preserved custom name)
        MockJournalConfiguration.Verify(
            jc => jc.UpdateEntryName(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never,
            "Display name should be preserved when it differs from filename pattern"
        );

        // Assert - should regenerate TOC
        MockTableOfContentsService.Verify(
            toc => toc.UpdateTableOfContents(Directory, null, It.IsAny<DateTime>()),
            Times.Once
        );
    }

    [Fact]
    public void UpdateEntry_Should_AlwaysUpdateDisplayName_When_TitleProvided()
    {
        // Arrange
        const string currentFile = "test_file.md";
        const string currentFileWithoutExt = "test_file";
        const string newTitle = "Explicit New Title";
        const string newDisplayName = "Explicit New Title";

        var currentEntry = new Entries { Name = "old title", File = currentFile };
        var currentTopicPath = Array.Empty<string>();

        SetupBasicFileAndJournalrcExists(currentFile);

        MockJournalConfiguration
            .Setup(jc => jc.FindEntry(Directory, currentFile))
            .Returns((currentEntry, currentTopicPath));

        MockEntryFormatterService
            .Setup(ef => ef.RemoveSpaceSeparators(newTitle))
            .Returns(newDisplayName);

        MockFileSystem
            .Setup(fs => fs.CombinePaths(Directory, currentFile))
            .Returns($"{Directory}/{currentFile}");

        // Act
        _service.UpdateEntry(Directory, currentFileWithoutExt, newEntryTitle: newTitle);

        // Assert - should NOT rename file (no --name)
        MockFileSystem.Verify(
            fs => fs.RenameFile(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never
        );

        // Assert - should update display name
        MockJournalConfiguration.Verify(
            jc => jc.UpdateEntryName(Directory, currentFile, newDisplayName),
            Times.Once
        );

        // Assert - should regenerate TOC
        MockTableOfContentsService.Verify(
            toc => toc.UpdateTableOfContents(Directory, null, It.IsAny<DateTime>()),
            Times.Once
        );
    }

    [Fact]
    public void UpdateEntry_Should_GivePrecedenceToTitle_When_BothNameAndTitleProvided()
    {
        // Arrange
        const string currentFile = "test_file.md";
        const string currentFileWithoutExt = "test_file";
        const string newEntryName = "new_file_name";
        const string newFileName = "new_file_name.md";
        const string newTitle = "Explicit Title Override";
        const string displayFromTitle = "Explicit Title Override";
        const string displayName = "test file";

        var currentEntry = new Entries { Name = displayName, File = currentFile };
        var currentTopicPath = Array.Empty<string>();

        SetupBasicFileAndJournalrcExists(currentFile);

        MockJournalConfiguration
            .Setup(jc => jc.FindEntry(Directory, currentFile))
            .Returns((currentEntry, currentTopicPath));

        MockEntryFormatterService
            .Setup(ef => ef.AddSpaceSeparators(newEntryName))
            .Returns(newEntryName);

        MockEntryFormatterService
            .Setup(ef => ef.AddHeadingSeparators(It.IsAny<string[]>()))
            .Returns(newEntryName);

        MockEntryFormatterService
            .Setup(ef => ef.RemoveSpaceSeparators(newTitle))
            .Returns(displayFromTitle);

        MockFileSystem
            .Setup(fs => fs.CombinePaths(Directory, currentFile))
            .Returns($"{Directory}/{currentFile}");

        MockFileSystem
            .Setup(fs => fs.CombinePaths(Directory, newFileName))
            .Returns($"{Directory}/{newFileName}");

        // Act
        _service.UpdateEntry(
            Directory,
            currentFileWithoutExt,
            newEntryName: newEntryName,
            newEntryTitle: newTitle
        );

        // Assert - should rename file
        MockFileSystem.Verify(
            fs => fs.RenameFile($"{Directory}/{currentFile}", $"{Directory}/{newFileName}"),
            Times.Once
        );

        // Assert - should update display name using title, not name
        MockJournalConfiguration.Verify(
            jc => jc.UpdateEntryName(Directory, newFileName, displayFromTitle),
            Times.Once
        );

        // Assert - should regenerate TOC
        MockTableOfContentsService.Verify(
            toc => toc.UpdateTableOfContents(Directory, null, It.IsAny<DateTime>()),
            Times.Once
        );
    }

    [Fact]
    public void UpdateEntry_Should_RenameFileAndUpdateLocation_When_HeadingsProvided()
    {
        // Arrange - test_file.md with --headings Projects-2024_Goals:
        // renames file to Projects-2024_Goals-test_file.md and moves entry in config.
        const string currentFile = "test_file.md";
        const string currentFileWithoutExt = "test_file";
        const string newHeadings = "Projects-2024_Goals";
        const string newFileName = "Projects-2024_Goals-test_file.md";
        var newTopicPath = new[] { "Projects", "2024 Goals" };
        const string displayName = "test file";

        var currentEntry = new Entries { Name = displayName, File = currentFile };
        var currentTopicPath = Array.Empty<string>();

        SetupBasicFileAndJournalrcExists(currentFile);

        MockJournalConfiguration
            .Setup(jc => jc.FindEntry(Directory, currentFile))
            .Returns((currentEntry, currentTopicPath));

        // DetermineTargetTopicPath (--headings explicit)
        MockEntryFormatterService
            .Setup(ef => ef.SeperateSubheadingString(newHeadings))
            .Returns(newTopicPath);

        // DetermineTargetFileName (--headings path):
        // headingParts=["Projects","2024_Goals"], entryNamePart="test_file"
        MockEntryFormatterService
            .Setup(ef =>
                ef.AddHeadingSeparators(
                    It.Is<string[]>(a =>
                        a.Length == 3
                        && a[0] == "Projects"
                        && a[1] == "2024_Goals"
                        && a[2] == "test_file"
                    )
                )
            )
            .Returns("Projects-2024_Goals-test_file");

        MockFileSystem
            .Setup(fs => fs.GetFileNameWithoutExtension(currentFile))
            .Returns(currentFileWithoutExt);

        MockFileSystem
            .Setup(fs => fs.CombinePaths(Directory, newFileName))
            .Returns($"{Directory}/{newFileName}");

        MockFileSystem.Setup(fs => fs.FileExists($"{Directory}/{newFileName}")).Returns(false);

        // Act
        _service.UpdateEntry(Directory, currentFileWithoutExt, newHeadings: newHeadings);

        // Assert - file renamed
        MockFileSystem.Verify(
            fs => fs.RenameFile($"{Directory}/{currentFile}", $"{Directory}/{newFileName}"),
            Times.Once
        );

        // Assert - config entry moved to new heading location with new filename
        MockJournalConfiguration.Verify(jc => jc.RemoveEntry(Directory, newFileName), Times.Once);
        MockJournalConfiguration.Verify(
            jc => jc.AddEntry(Directory, displayName, newFileName, newTopicPath, null, true, false),
            Times.Once
        );

        // Assert - should regenerate TOC
        MockTableOfContentsService.Verify(
            toc => toc.UpdateTableOfContents(Directory, null, It.IsAny<DateTime>()),
            Times.Once
        );
    }

    [Fact]
    public void UpdateEntry_Should_AddToIgnoreList_When_IgnoreFlagSet()
    {
        // Arrange
        const string currentFile = "test_file.md";
        const string currentFileWithoutExt = "test_file";
        const string displayName = "test file";

        var currentEntry = new Entries { Name = displayName, File = currentFile };
        var currentTopicPath = Array.Empty<string>();

        SetupBasicFileAndJournalrcExists(currentFile);

        MockJournalConfiguration
            .Setup(jc => jc.FindEntry(Directory, currentFile))
            .Returns((currentEntry, currentTopicPath));

        MockFileSystem
            .Setup(fs => fs.CombinePaths(Directory, currentFile))
            .Returns($"{Directory}/{currentFile}");

        // Act
        _service.UpdateEntry(Directory, currentFileWithoutExt, ignoreFile: true);

        // Assert
        MockJournalConfiguration.Verify(jc => jc.RemoveEntry(Directory, currentFile), Times.Once);
        MockJournalConfiguration.Verify(
            jc => jc.AddIgnoreEntry(Directory, currentFile),
            Times.Once
        );

        // Assert - should regenerate TOC
        MockTableOfContentsService.Verify(
            toc => toc.UpdateTableOfContents(Directory, null, It.IsAny<DateTime>()),
            Times.Once
        );
    }

    [Fact]
    public void UpdateEntry_Should_RemoveFromIgnoreList_When_UnignoreFlagSet()
    {
        // Arrange
        const string currentFile = "test_file.md";
        const string currentFileWithoutExt = "test_file";
        const string displayName = "test file";

        var currentEntry = new Entries { Name = displayName, File = currentFile };
        var currentTopicPath = Array.Empty<string>();

        SetupBasicFileAndJournalrcExists(currentFile);

        MockJournalConfiguration
            .Setup(jc => jc.FindEntry(Directory, currentFile))
            .Returns((currentEntry, currentTopicPath));

        MockFileSystem
            .Setup(fs => fs.CombinePaths(Directory, currentFile))
            .Returns($"{Directory}/{currentFile}");

        MockFileSystem
            .Setup(fs => fs.GetFileNameWithoutExtension(currentFile))
            .Returns(currentFileWithoutExt);

        MockJournalConfiguration
            .Setup(jc => jc.Update(Directory, It.IsAny<Action<JournalConfig>>()))
            .Callback<string, Action<JournalConfig>>(
                (dir, action) =>
                {
                    var config = new JournalConfig
                    {
                        JournalName = "Test",
                        TableOfContents = new TableOfContents
                        {
                            File = "toc.md",
                            IgnoreFiles = new[] { currentFile },
                        },
                    };
                    action(config);
                }
            );

        // Act
        _service.UpdateEntry(Directory, currentFileWithoutExt, unignoreFile: true);

        // Assert
        MockJournalConfiguration.Verify(
            jc => jc.Update(Directory, It.IsAny<Action<JournalConfig>>()),
            Times.Once
        );

        // Verify that AddEntry was called to add the file back to the structure
        MockJournalConfiguration.Verify(
            jc => jc.AddEntry(Directory, "", currentFile, null, null, true, false),
            Times.Once
        );

        // Assert - should regenerate TOC
        MockTableOfContentsService.Verify(
            toc => toc.UpdateTableOfContents(Directory, null, It.IsAny<DateTime>()),
            Times.Once
        );
    }

    [Fact]
    public void UpdateEntry_Should_ThrowFileNotFoundException_When_FileNotFound()
    {
        // Arrange
        const string currentFile = "nonexistent.md";
        var currentPath = $"{Directory}/{currentFile}";

        MockFileSystem.Setup(fs => fs.CombinePaths(Directory, currentFile)).Returns(currentPath);

        MockFileSystem.Setup(fs => fs.FileExists(currentPath)).Returns(false);

        // Act & Assert
        var exception = Should.Throw<FileNotFoundException>(() =>
            _service.UpdateEntry(Directory, currentFile)
        );

        exception.Message.ShouldContain(currentFile);
        exception.Message.ShouldContain(Directory);
    }

    [Fact]
    public void UpdateEntry_Should_ThrowJournalrcNotFoundException_When_JournalrcNotFound()
    {
        // Arrange
        const string currentFile = "test_file.md";
        var currentPath = $"{Directory}/{currentFile}";
        var journalrcPath = $"{Directory}/.journalrc";

        MockFileSystem.Setup(fs => fs.CombinePaths(Directory, currentFile)).Returns(currentPath);

        MockFileSystem.Setup(fs => fs.FileExists(currentPath)).Returns(true);

        MockFileSystem.Setup(fs => fs.CombinePaths(Directory, ".journalrc")).Returns(journalrcPath);

        MockFileSystem.Setup(fs => fs.FileExists(journalrcPath)).Returns(false);

        // Act & Assert
        Should.Throw<markdown_journal_cli.Exceptions.JournalrcNotFoundException>(() =>
            _service.UpdateEntry(Directory, currentFile)
        );
    }

    [Fact]
    public void UpdateEntry_Should_PreserveHeadingPrefixAndRenameLastSegment_When_NameProvided()
    {
        // Arrange - "collin-entry.md"; -n "robison" should rename to "collin-robison.md",
        // keeping the "collin-" heading prefix and only replacing the last segment.
        const string currentFile = "collin-entry.md";
        const string currentFileWithoutExt = "collin-entry";
        const string newEntryName = "robison";
        const string newFileName = "collin-robison.md"; // heading prefix preserved
        const string newFileNameWithoutExt = "collin-robison";
        const string displayName = "entry";

        var currentEntry = new Entries { Name = displayName, File = currentFile };
        var currentTopicPath = new[] { "collin" };

        SetupBasicFileAndJournalrcExists(currentFile);

        MockJournalConfiguration
            .Setup(jc => jc.FindEntry(Directory, currentFile))
            .Returns((currentEntry, currentTopicPath));

        MockEntryFormatterService
            .Setup(ef => ef.AddSpaceSeparators(newEntryName))
            .Returns(newEntryName);

        MockEntryFormatterService
            .Setup(ef => ef.AddHeadingSeparators(It.IsAny<string[]>()))
            .Returns(newFileNameWithoutExt);

        MockEntryFormatterService.Setup(ef => ef.RemoveSpaceSeparators("collin")).Returns("collin");

        MockEntryFormatterService
            .Setup(ef => ef.RemoveSpaceSeparators(newEntryName))
            .Returns(newEntryName);

        MockEntryFormatterService.Setup(ef => ef.RemoveSpaceSeparators("entry")).Returns("entry");

        MockFileSystem
            .Setup(fs => fs.CombinePaths(Directory, currentFile))
            .Returns($"{Directory}/{currentFile}");

        MockFileSystem
            .Setup(fs => fs.CombinePaths(Directory, newFileName))
            .Returns($"{Directory}/{newFileName}");

        MockFileSystem
            .Setup(fs => fs.GetFileNameWithoutExtension(currentFile))
            .Returns(currentFileWithoutExt);

        MockFileSystem
            .Setup(fs => fs.GetFileNameWithoutExtension(newFileName))
            .Returns(newFileNameWithoutExt);

        // Act
        _service.UpdateEntry(Directory, currentFileWithoutExt, newEntryName: newEntryName);

        // Assert - renamed with prefix intact: collin-entry.md → collin-robison.md
        MockFileSystem.Verify(
            fs => fs.RenameFile($"{Directory}/{currentFile}", $"{Directory}/{newFileName}"),
            Times.Once
        );

        // Assert - topic path unchanged (["collin"] → ["collin"]); only display name updated
        MockJournalConfiguration.Verify(
            jc => jc.UpdateEntryName(Directory, newFileName, newEntryName),
            Times.Once,
            "Display name should update since it matched the old last segment"
        );

        MockJournalConfiguration.Verify(
            jc => jc.RemoveEntry(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never,
            "Entry should not be moved; heading location is unchanged"
        );
    }

    [Fact]
    public void UpdateEntry_Should_RenameFile_When_NameProvidedForRootLevelFile()
    {
        // Arrange - "collin.md" at root; -n "robison" → "robison.md" (no heading prefix to preserve)
        const string currentFile = "collin.md";
        const string currentFileWithoutExt = "collin";
        const string newEntryName = "robison";
        const string newFileName = "robison.md";
        const string displayName = "collin";

        var currentEntry = new Entries { Name = displayName, File = currentFile };
        var currentTopicPath = Array.Empty<string>();

        SetupBasicFileAndJournalrcExists(currentFile);

        MockJournalConfiguration
            .Setup(jc => jc.FindEntry(Directory, currentFile))
            .Returns((currentEntry, currentTopicPath));

        MockEntryFormatterService
            .Setup(ef => ef.AddSpaceSeparators(newEntryName))
            .Returns(newEntryName);

        MockEntryFormatterService
            .Setup(ef => ef.AddHeadingSeparators(It.IsAny<string[]>()))
            .Returns(newEntryName);

        MockEntryFormatterService
            .Setup(ef => ef.RemoveSpaceSeparators(currentFileWithoutExt))
            .Returns(displayName);

        MockEntryFormatterService
            .Setup(ef => ef.RemoveSpaceSeparators(newEntryName))
            .Returns(newEntryName);

        MockFileSystem
            .Setup(fs => fs.CombinePaths(Directory, currentFile))
            .Returns($"{Directory}/{currentFile}");

        MockFileSystem
            .Setup(fs => fs.CombinePaths(Directory, newFileName))
            .Returns($"{Directory}/{newFileName}");

        MockFileSystem
            .Setup(fs => fs.GetFileNameWithoutExtension(currentFile))
            .Returns(currentFileWithoutExt);

        // Act
        _service.UpdateEntry(Directory, currentFileWithoutExt, newEntryName: newEntryName);

        // Assert - file renamed at root level
        MockFileSystem.Verify(
            fs => fs.RenameFile($"{Directory}/{currentFile}", $"{Directory}/{newFileName}"),
            Times.Once
        );

        // Assert - display name updated (it matched old filename)
        MockJournalConfiguration.Verify(
            jc => jc.UpdateEntryName(Directory, newFileName, newEntryName),
            Times.Once
        );

        // Assert - not moved to a new heading (still at root)
        MockJournalConfiguration.Verify(
            jc => jc.RemoveEntry(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never
        );
    }

    [Fact]
    public void UpdateEntry_Should_GivePrecedenceToHeadingsPrefix_When_BothNameAndHeadingsProvided()
    {
        // Arrange - "birdie_bird.md" already tracked under Nat heading in config.
        // Running: -n birdie_bird -h nat
        // Expected: file renamed to "nat-birdie_bird.md"; entry stays in Nat heading (heading unchanged).
        const string currentFile = "birdie_bird.md";
        const string currentFileWithoutExt = "birdie_bird";
        const string newEntryName = "birdie_bird";
        const string newHeadings = "nat";
        const string newFileName = "nat-birdie_bird.md";
        const string newFileNameWithoutExt = "nat-birdie_bird";
        const string displayName = "birdie bird";

        var currentEntry = new Entries { Name = displayName, File = currentFile };
        var currentTopicPath = new[] { "Nat" }; // already under Nat heading

        SetupBasicFileAndJournalrcExists(currentFile);

        MockJournalConfiguration
            .Setup(jc => jc.FindEntry(Directory, currentFile))
            .Returns((currentEntry, currentTopicPath));

        MockEntryFormatterService
            .Setup(ef => ef.AddSpaceSeparators(newEntryName))
            .Returns(newEntryName);

        MockEntryFormatterService
            .Setup(ef => ef.AddHeadingSeparators(It.IsAny<string[]>()))
            .Returns(newFileNameWithoutExt);

        MockEntryFormatterService
            .Setup(ef => ef.SeperateSubheadingString(newHeadings))
            .Returns(new[] { "nat" });

        MockEntryFormatterService
            .Setup(ef => ef.RemoveSpaceSeparators(currentFileWithoutExt))
            .Returns(displayName);

        MockEntryFormatterService
            .Setup(ef => ef.RemoveSpaceSeparators(newEntryName))
            .Returns(displayName);

        MockFileSystem
            .Setup(fs => fs.CombinePaths(Directory, currentFile))
            .Returns($"{Directory}/{currentFile}");

        MockFileSystem
            .Setup(fs => fs.CombinePaths(Directory, newFileName))
            .Returns($"{Directory}/{newFileName}");

        MockFileSystem
            .Setup(fs => fs.GetFileNameWithoutExtension(currentFile))
            .Returns(currentFileWithoutExt);

        MockFileSystem
            .Setup(fs => fs.GetFileNameWithoutExtension(newFileName))
            .Returns(newFileNameWithoutExt);

        // Act
        _service.UpdateEntry(
            Directory,
            currentFileWithoutExt,
            newEntryName: newEntryName,
            newHeadings: newHeadings
        );

        // Assert - file renamed to include heading prefix from -h
        MockFileSystem.Verify(
            fs => fs.RenameFile($"{Directory}/{currentFile}", $"{Directory}/{newFileName}"),
            Times.Once
        );

        // Assert - heading didn't change (Nat == nat case-insensitively) so entry not relocated
        MockJournalConfiguration.Verify(
            jc => jc.RemoveEntry(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never,
            "Entry should not be relocated; it is already in the Nat heading"
        );

        // Assert - TOC regenerated
        MockTableOfContentsService.Verify(
            toc => toc.UpdateTableOfContents(Directory, null, It.IsAny<DateTime>()),
            Times.Once
        );
    }

    /// <summary>
    /// Helper method to setup basic file and .journalrc existence checks
    /// </summary>
    private void SetupBasicFileAndJournalrcExists(string fileName)
    {
        var filePath = $"{Directory}/{fileName}";
        var journalrcPath = $"{Directory}/.journalrc";

        MockFileSystem.Setup(fs => fs.CombinePaths(Directory, fileName)).Returns(filePath);

        MockFileSystem.Setup(fs => fs.FileExists(filePath)).Returns(true);

        MockFileSystem.Setup(fs => fs.CombinePaths(Directory, ".journalrc")).Returns(journalrcPath);

        MockFileSystem.Setup(fs => fs.FileExists(journalrcPath)).Returns(true);
    }

    #endregion

    #region Backlink Update Tests

    private void SetupRenameScenario(string currentFile, string newName, string newFile)
    {
        SetupBasicFileAndJournalrcExists(currentFile);

        MockJournalConfiguration
            .Setup(jc => jc.FindEntry(Directory, currentFile))
            .Returns((null, Array.Empty<string>()));

        var currentStem = System.IO.Path.GetFileNameWithoutExtension(currentFile);
        var newStem = System.IO.Path.GetFileNameWithoutExtension(newFile);

        MockFileSystem
            .Setup(fs => fs.GetFileNameWithoutExtension(currentFile))
            .Returns(currentStem);
        MockFileSystem.Setup(fs => fs.GetFileNameWithoutExtension(newFile)).Returns(newStem);

        MockEntryFormatterService.Setup(ef => ef.AddSpaceSeparators(newName)).Returns(newName);
        MockEntryFormatterService
            .Setup(ef => ef.AddHeadingSeparators(It.IsAny<string[]>()))
            .Returns(newStem);
        MockEntryFormatterService
            .Setup(ef => ef.RemoveSpaceSeparators(currentStem))
            .Returns(currentStem.Replace("_", " "));
        MockEntryFormatterService
            .Setup(ef => ef.RemoveSpaceSeparators(newName))
            .Returns(newName.Replace("_", " "));

        var targetFilePath = $"{Directory}/{newFile}";
        MockFileSystem.Setup(fs => fs.CombinePaths(Directory, newFile)).Returns(targetFilePath);
        MockFileSystem.Setup(fs => fs.FileExists(targetFilePath)).Returns(false);
    }

    [Fact]
    public void UpdateEntry_Should_CallReplaceLinksInDirectory_When_RenameOccurs()
    {
        // Arrange
        const string currentFile = "old_entry.md";
        const string newName = "new_entry";
        const string expectedNewFile = "new_entry.md";

        SetupRenameScenario(currentFile, newName, expectedNewFile);

        // Act
        _service.UpdateEntry(Directory, currentFile, newEntryName: newName, updateBacklinks: true);

        // Assert
        _mockMarkdownLinkRewriter.Verify(
            r =>
                r.ReplaceLinksInDirectory(
                    Directory,
                    currentFile,
                    expectedNewFile,
                    It.Is<IReadOnlyCollection<string>>(ex =>
                        ex.Contains(expectedNewFile, StringComparer.OrdinalIgnoreCase)
                        && ex.Contains(TocFile, StringComparer.OrdinalIgnoreCase)
                    )
                ),
            Times.Once
        );
    }

    [Fact]
    public void UpdateEntry_Should_NotCallReplaceLinksInDirectory_When_NoRenameOccurs()
    {
        // Arrange — only a title change, file stays the same
        const string currentFile = "my_entry.md";
        const string stem = "my_entry";
        SetupBasicFileAndJournalrcExists(currentFile);

        MockJournalConfiguration
            .Setup(jc => jc.FindEntry(Directory, currentFile))
            .Returns(
                (
                    new markdown_journal_cli.Infrastructure.Configuration.Models.Entries
                    {
                        Name = "My Entry",
                        File = currentFile,
                    },
                    Array.Empty<string>()
                )
            );

        MockFileSystem.Setup(fs => fs.GetFileNameWithoutExtension(currentFile)).Returns(stem);
        MockEntryFormatterService
            .Setup(ef => ef.RemoveSpaceSeparators("New Title"))
            .Returns("New Title");

        // Act — title-only: no file rename
        _service.UpdateEntry(Directory, currentFile, newEntryTitle: "New Title");

        // Assert — no rename means no backlink scan
        _mockMarkdownLinkRewriter.Verify(
            r =>
                r.ReplaceLinksInDirectory(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<IReadOnlyCollection<string>>()
                ),
            Times.Never
        );
    }

    [Fact]
    public void UpdateEntry_Should_NotCallReplaceLinksInDirectory_When_UpdateBacklinksFalse()
    {
        // Arrange
        const string currentFile = "old_entry.md";
        const string newName = "new_entry";
        const string expectedNewFile = "new_entry.md";

        SetupRenameScenario(currentFile, newName, expectedNewFile);

        // Act — opt-out flag
        _service.UpdateEntry(Directory, currentFile, newEntryName: newName, updateBacklinks: false);

        // Assert — rewriter must NOT be called
        _mockMarkdownLinkRewriter.Verify(
            r =>
                r.ReplaceLinksInDirectory(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<IReadOnlyCollection<string>>()
                ),
            Times.Never
        );
    }

    [Fact]
    public void UpdateEntry_Should_ExcludeTocFileFromReplaceLinks()
    {
        // Arrange
        const string currentFile = "old_entry.md";
        const string newName = "new_entry";
        const string expectedNewFile = "new_entry.md";

        SetupRenameScenario(currentFile, newName, expectedNewFile);

        // Act
        _service.UpdateEntry(Directory, currentFile, newEntryName: newName, updateBacklinks: true);

        // Assert — TOC file must be in the exclusion list
        _mockMarkdownLinkRewriter.Verify(
            r =>
                r.ReplaceLinksInDirectory(
                    Directory,
                    currentFile,
                    expectedNewFile,
                    It.Is<IReadOnlyCollection<string>>(ex =>
                        ex.Contains(TocFile, StringComparer.OrdinalIgnoreCase)
                    )
                ),
            Times.Once
        );
    }

    [Fact]
    public void UpdateEntry_Should_ExcludeRenamedFileFromReplaceLinks()
    {
        // Arrange
        const string currentFile = "old_entry.md";
        const string newName = "new_entry";
        const string expectedNewFile = "new_entry.md";

        SetupRenameScenario(currentFile, newName, expectedNewFile);

        // Act
        _service.UpdateEntry(Directory, currentFile, newEntryName: newName, updateBacklinks: true);

        // Assert — the newly renamed file itself must be excluded
        _mockMarkdownLinkRewriter.Verify(
            r =>
                r.ReplaceLinksInDirectory(
                    Directory,
                    currentFile,
                    expectedNewFile,
                    It.Is<IReadOnlyCollection<string>>(ex =>
                        ex.Contains(expectedNewFile, StringComparer.OrdinalIgnoreCase)
                    )
                ),
            Times.Once
        );
    }

    #endregion
}
