using markdown_journal_cli;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.Configuration.Models;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.Tracking;
using markdown_journal_cli.Infrastructure.Transactions;
using markdown_journal_cli.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace markdown_journal_cli.Tests.Services;

/// <summary>
/// Unit tests for JournalFileUpdateService covering all public methods.
/// </summary>
public class JournalFileUpdateServiceTests
{
    private readonly Mock<IFileSystem> _mockFileSystem;
    private readonly Mock<IJournalConfiguration> _mockJournalConfiguration;
    private readonly Mock<IEntryFormatterService> _mockEntryFormatter;
    private readonly Mock<ITableOfContentsService> _mockTableOfContentsService;
    private readonly Mock<IFileTracking> _mockFileTracking;
    private readonly Mock<IMarkdownLinkRewriter> _mockMarkdownLinkRewriter;
    private readonly JournalFileUpdateService _service;

    private const string Directory = "/test/journal";
    private const string OldFile = "old_file.md";
    private const string NewFile = "new_file.md";
    private const string TestFile = "test_file.md";
    private const string TocFile = "1a-TableOfContents.md";

    public JournalFileUpdateServiceTests()
    {
        _mockFileSystem = new Mock<IFileSystem>();
        _mockJournalConfiguration = new Mock<IJournalConfiguration>();
        _mockEntryFormatter = new Mock<IEntryFormatterService>();
        _mockTableOfContentsService = new Mock<ITableOfContentsService>();
        _mockFileTracking = new Mock<IFileTracking>();
        _mockMarkdownLinkRewriter = new Mock<IMarkdownLinkRewriter>();

        var journalSettings = Microsoft.Extensions.Options.Options.Create(
            new JournalSettings
            {
                AppName = "testapp",
                JournalConfigFileName = ".journalrc",
                TableOfContentsFileName = "1a-TableOfContents",
                TitleSpaceSeparator = "_",
                HeadingSeparator = "-",
                DateFormat = "MM/dd/yyyy",
            }
        );

        _service = new JournalFileUpdateService(
            _mockFileSystem.Object,
            _mockJournalConfiguration.Object,
            _mockEntryFormatter.Object,
            _mockTableOfContentsService.Object,
            journalSettings,
            NullLogger<JournalFileUpdateService>.Instance,
            _mockFileTracking.Object,
            _mockMarkdownLinkRewriter.Object,
            NoOpFileTransactionCoordinator.Instance,
            NoOpRollbackReporter.Instance
        );
    }

    #region RenameEntry Tests

    [Fact]
    public void RenameEntry_FileExists_RenamesFileAndUpdatesConfig()
    {
        // Arrange
        var oldPath = $"{Directory}/{OldFile}";
        var newPath = $"{Directory}/{NewFile}";

        _mockFileSystem.Setup(fs => fs.CombinePaths(Directory, OldFile)).Returns(oldPath);
        _mockFileSystem.Setup(fs => fs.CombinePaths(Directory, NewFile)).Returns(newPath);
        _mockFileSystem.Setup(fs => fs.FileExists(oldPath)).Returns(true);

        // Act
        _service.RenameEntry(Directory, OldFile, NewFile);

        // Assert
        _mockFileSystem.Verify(fs => fs.RenameFile(oldPath, newPath), Times.Once);
        _mockJournalConfiguration.Verify(
            jc => jc.UpdateFileReferences(Directory, OldFile, NewFile),
            Times.Once
        );
        _mockFileTracking.Verify(
            ft => ft.RenameFileInIndex(Directory, OldFile, NewFile),
            Times.Once
        );
    }

    [Fact]
    public void RenameEntry_FileExists_DoesNotUpdateTrackingIndex_WhenFileNotFound()
    {
        // Arrange
        var oldPath = $"{Directory}/{OldFile}";

        _mockFileSystem.Setup(fs => fs.CombinePaths(Directory, OldFile)).Returns(oldPath);
        _mockFileSystem.Setup(fs => fs.FileExists(oldPath)).Returns(false);

        // Act & Assert
        Should.Throw<FileNotFoundException>(() =>
            _service.RenameEntry(Directory, OldFile, NewFile)
        );

        _mockFileTracking.Verify(
            ft => ft.RenameFileInIndex(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never
        );
    }

    [Fact]
    public void RenameEntry_FileDoesNotExist_ThrowsFileNotFoundException()
    {
        // Arrange
        var oldPath = $"{Directory}/{OldFile}";

        _mockFileSystem.Setup(fs => fs.CombinePaths(Directory, OldFile)).Returns(oldPath);
        _mockFileSystem.Setup(fs => fs.FileExists(oldPath)).Returns(false);

        // Act & Assert
        var exception = Should.Throw<FileNotFoundException>(() =>
            _service.RenameEntry(Directory, OldFile, NewFile)
        );

        exception.Message.ShouldContain(OldFile);
        exception.Message.ShouldContain(Directory);

        _mockFileSystem.Verify(
            fs => fs.RenameFile(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never
        );
        _mockJournalConfiguration.Verify(
            jc =>
                jc.UpdateFileReferences(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never
        );
    }

    #endregion

    #region UpdateEntryLocation Tests

    [Fact]
    public void UpdateEntryLocation_WithTopicPath_RemovesAndAddsEntry()
    {
        // Arrange
        var newTopicPath = new[] { "Projects", "2024" };
        var displayName = "Test Entry";

        // Act
        _service.UpdateEntryLocation(Directory, TestFile, newTopicPath, displayName);

        // Assert
        _mockJournalConfiguration.Verify(jc => jc.RemoveEntry(Directory, TestFile), Times.Once);
        _mockJournalConfiguration.Verify(
            jc => jc.AddEntry(Directory, displayName, TestFile, newTopicPath, null, true, false),
            Times.Once
        );
    }

    [Fact]
    public void UpdateEntryLocation_WithEmptyTopicPath_AddsToRoot()
    {
        // Arrange
        var emptyTopicPath = Array.Empty<string>();
        var displayName = "Root Entry";

        // Act
        _service.UpdateEntryLocation(Directory, TestFile, emptyTopicPath, displayName);

        // Assert
        _mockJournalConfiguration.Verify(jc => jc.RemoveEntry(Directory, TestFile), Times.Once);
        _mockJournalConfiguration.Verify(
            jc => jc.AddEntry(Directory, displayName, TestFile, null, null, true, false),
            Times.Once
        );
    }

    #endregion

    #region UpdateEntryDisplayName Tests

    [Fact]
    public void UpdateEntryDisplayName_EntryExists_UpdatesSuccessfully()
    {
        // Arrange
        var newDisplayName = "New Display Name";
        _mockJournalConfiguration
            .Setup(jc => jc.UpdateEntryName(Directory, TestFile, newDisplayName))
            .Returns(true);

        // Act
        _service.UpdateEntryDisplayName(Directory, TestFile, newDisplayName);

        // Assert
        _mockJournalConfiguration.Verify(
            jc => jc.UpdateEntryName(Directory, TestFile, newDisplayName),
            Times.Once
        );
    }

    [Fact]
    public void UpdateEntryDisplayName_EntryDoesNotExist_LogsWarning()
    {
        // Arrange
        var newDisplayName = "New Display Name";
        _mockJournalConfiguration
            .Setup(jc => jc.UpdateEntryName(Directory, TestFile, newDisplayName))
            .Returns(false);

        // Act
        _service.UpdateEntryDisplayName(Directory, TestFile, newDisplayName);

        // Assert - should complete without exception even if entry not found
        _mockJournalConfiguration.Verify(
            jc => jc.UpdateEntryName(Directory, TestFile, newDisplayName),
            Times.Once
        );
    }

    #endregion

    #region SetIgnoreStatus Tests

    [Fact]
    public void SetIgnoreStatus_True_AddsToIgnoreList()
    {
        // Arrange

        // Act
        _service.SetIgnoreStatus(Directory, TestFile, true);

        // Assert
        _mockJournalConfiguration.Verify(jc => jc.RemoveEntry(Directory, TestFile), Times.Once);
        _mockJournalConfiguration.Verify(jc => jc.AddIgnoreEntry(Directory, TestFile), Times.Once);
    }

    [Fact]
    public void SetIgnoreStatus_False_RemovesFromIgnoreList()
    {
        // Arrange
        _mockJournalConfiguration
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
                            RootEntries = Array.Empty<Entries>(),
                            Structure = new Structure { Topics = Array.Empty<Topic>() },
                            IgnoreFiles = new[] { TestFile, "other_file.md" },
                        },
                    };
                    action(config);

                    // Verify the ignore files were updated correctly
                    config.TableOfContents.IgnoreFiles.ShouldNotContain(TestFile);
                    config.TableOfContents.IgnoreFiles.ShouldContain("other_file.md");
                }
            );

        _mockFileSystem.Setup(fs => fs.GetFileNameWithoutExtension(TestFile)).Returns("test");

        // Act
        _service.SetIgnoreStatus(Directory, TestFile, false);

        // Assert
        _mockJournalConfiguration.Verify(
            jc => jc.Update(Directory, It.IsAny<Action<JournalConfig>>()),
            Times.Once
        );

        // Verify that AddEntry was called to add the file back to the structure
        _mockJournalConfiguration.Verify(
            jc => jc.AddEntry(Directory, "", TestFile, null, null, true, false),
            Times.Once
        );
    }

    [Fact]
    public void SetIgnoreStatus_False_WithNullIgnoreFiles_HandlesGracefully()
    {
        // Arrange
        _mockJournalConfiguration
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
                            RootEntries = Array.Empty<Entries>(),
                            Structure = new Structure { Topics = Array.Empty<Topic>() },
                            IgnoreFiles = null,
                        },
                    };
                    action(config);
                }
            );

        _mockFileSystem.Setup(fs => fs.GetFileNameWithoutExtension(TestFile)).Returns("test");

        // Act
        _service.SetIgnoreStatus(Directory, TestFile, false);

        // Assert - should complete without exception
        _mockJournalConfiguration.Verify(
            jc => jc.Update(Directory, It.IsAny<Action<JournalConfig>>()),
            Times.Once
        );

        // Verify that AddEntry was called to add the file back to the structure
        _mockJournalConfiguration.Verify(
            jc => jc.AddEntry(Directory, "", TestFile, null, null, true, false),
            Times.Once
        );
    }

    #endregion

    #region UpdateEntry Tests

    [Fact]
    public void UpdateEntry_WithName_WhenDisplayNameMatchesFilename_UpdatesBoth()
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

        _mockJournalConfiguration
            .Setup(jc => jc.FindEntry(Directory, currentFile))
            .Returns((currentEntry, currentTopicPath));

        _mockEntryFormatter.Setup(ef => ef.AddSpaceSeparators(newEntryName)).Returns(newEntryName);

        _mockEntryFormatter
            .Setup(ef => ef.AddHeadingSeparators(It.IsAny<string[]>()))
            .Returns(newFileNameWithoutExt);

        _mockEntryFormatter.Setup(ef => ef.RemoveSpaceSeparators("abc")).Returns("abc");

        _mockEntryFormatter.Setup(ef => ef.RemoveSpaceSeparators("test_2")).Returns("test 2");

        _mockEntryFormatter
            .Setup(ef => ef.RemoveSpaceSeparators("test_file_10"))
            .Returns(displayName);

        _mockEntryFormatter
            .Setup(ef => ef.RemoveSpaceSeparators(newEntryName))
            .Returns(newDisplayName);

        _mockFileSystem
            .Setup(fs => fs.CombinePaths(Directory, currentFile))
            .Returns($"{Directory}/{currentFile}");

        _mockFileSystem
            .Setup(fs => fs.CombinePaths(Directory, newFileName))
            .Returns($"{Directory}/{newFileName}");

        _mockFileSystem
            .Setup(fs => fs.GetFileNameWithoutExtension(currentFile))
            .Returns(currentFileWithoutExt);

        _mockFileSystem
            .Setup(fs => fs.GetFileNameWithoutExtension(newFileName))
            .Returns(newFileNameWithoutExt);

        // Act
        _service.UpdateEntry(Directory, currentFileWithoutExt, newEntryName: newEntryName);

        // Assert - should rename file (heading prefix preserved)
        _mockFileSystem.Verify(
            fs => fs.RenameFile($"{Directory}/{currentFile}", $"{Directory}/{newFileName}"),
            Times.Once
        );

        // Assert - should update display name because it matched
        _mockJournalConfiguration.Verify(
            jc => jc.UpdateEntryName(Directory, newFileName, newDisplayName),
            Times.Once
        );

        // Assert - should regenerate TOC
        _mockTableOfContentsService.Verify(
            toc => toc.UpdateTableOfContents(Directory, null, It.IsAny<DateTime>()),
            Times.Once
        );
    }

    [Fact]
    public void UpdateEntry_WithHeadings_RenamesFileAndMovesEntryInConfig()
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

        _mockJournalConfiguration
            .Setup(jc => jc.FindEntry(Directory, currentFile))
            .Returns((currentEntry, currentTopicPath));

        // DetermineTargetFileName (--headings path): AddHeadingSeparators(["robison","test"])
        _mockEntryFormatter
            .Setup(ef =>
                ef.AddHeadingSeparators(
                    It.Is<string[]>(a => a.Length == 2 && a[0] == "robison" && a[1] == "test")
                )
            )
            .Returns("robison-test");

        // DetermineTargetTopicPath (--headings explicit): SeperateSubheadingString("robison")
        _mockEntryFormatter
            .Setup(ef => ef.SeperateSubheadingString("robison"))
            .Returns(new[] { "robison" });

        _mockFileSystem
            .Setup(fs => fs.CombinePaths(Directory, newFileName))
            .Returns($"{Directory}/{newFileName}");

        _mockFileSystem.Setup(fs => fs.FileExists($"{Directory}/{newFileName}")).Returns(false);

        _mockFileSystem
            .Setup(fs => fs.GetFileNameWithoutExtension(currentFile))
            .Returns(currentFileWithoutExt);

        // Act
        _service.UpdateEntry(Directory, currentFileWithoutExt, newHeadings: "robison");

        // Assert - file renamed from collin-test.md to robison-test.md
        _mockFileSystem.Verify(
            fs => fs.RenameFile($"{Directory}/{currentFile}", $"{Directory}/{newFileName}"),
            Times.Once
        );

        // Assert - entry moved to robison heading with display name "test" (unchanged)
        _mockJournalConfiguration.Verify(jc => jc.RemoveEntry(Directory, newFileName), Times.Once);
        _mockJournalConfiguration.Verify(
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
        _mockJournalConfiguration.Verify(
            jc => jc.UpdateEntryName(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never
        );

        // Assert - TOC regenerated
        _mockTableOfContentsService.Verify(
            toc => toc.UpdateTableOfContents(Directory, null, It.IsAny<DateTime>()),
            Times.Once
        );
    }

    [Fact]
    public void UpdateEntry_WithName_WhenDisplayNameDiffersFromFilename_PreservesDisplayName()
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

        _mockJournalConfiguration
            .Setup(jc => jc.FindEntry(Directory, currentFile))
            .Returns((currentEntry, currentTopicPath));

        _mockEntryFormatter.Setup(ef => ef.AddSpaceSeparators(newEntryName)).Returns(newEntryName);

        _mockEntryFormatter
            .Setup(ef => ef.AddHeadingSeparators(It.IsAny<string[]>()))
            .Returns(newFileNameWithoutExt);

        _mockEntryFormatter.Setup(ef => ef.RemoveSpaceSeparators("abc")).Returns("abc");

        _mockEntryFormatter.Setup(ef => ef.RemoveSpaceSeparators("test_2")).Returns("test 2");

        _mockEntryFormatter
            .Setup(ef => ef.RemoveSpaceSeparators("test_file_10"))
            .Returns(expectedFromFile);

        _mockFileSystem
            .Setup(fs => fs.CombinePaths(Directory, currentFile))
            .Returns($"{Directory}/{currentFile}");

        _mockFileSystem
            .Setup(fs => fs.CombinePaths(Directory, newFileName))
            .Returns($"{Directory}/{newFileName}");

        _mockFileSystem
            .Setup(fs => fs.GetFileNameWithoutExtension(currentFile))
            .Returns(currentFileWithoutExt);

        _mockFileSystem
            .Setup(fs => fs.GetFileNameWithoutExtension(newFileName))
            .Returns(newFileNameWithoutExt);

        // Act
        _service.UpdateEntry(Directory, currentFileWithoutExt, newEntryName: newEntryName);

        // Assert - should rename file (heading prefix preserved)
        _mockFileSystem.Verify(
            fs => fs.RenameFile($"{Directory}/{currentFile}", $"{Directory}/{newFileName}"),
            Times.Once
        );

        // Assert - should NOT update display name (preserved custom name)
        _mockJournalConfiguration.Verify(
            jc => jc.UpdateEntryName(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never,
            "Display name should be preserved when it differs from filename pattern"
        );

        // Assert - should regenerate TOC
        _mockTableOfContentsService.Verify(
            toc => toc.UpdateTableOfContents(Directory, null, It.IsAny<DateTime>()),
            Times.Once
        );
    }

    [Fact]
    public void UpdateEntry_WithTitle_AlwaysUpdatesDisplayName()
    {
        // Arrange
        const string currentFile = "test_file.md";
        const string currentFileWithoutExt = "test_file";
        const string newTitle = "Explicit New Title";
        const string newDisplayName = "Explicit New Title";

        var currentEntry = new Entries { Name = "old title", File = currentFile };
        var currentTopicPath = Array.Empty<string>();

        SetupBasicFileAndJournalrcExists(currentFile);

        _mockJournalConfiguration
            .Setup(jc => jc.FindEntry(Directory, currentFile))
            .Returns((currentEntry, currentTopicPath));

        _mockEntryFormatter.Setup(ef => ef.RemoveSpaceSeparators(newTitle)).Returns(newDisplayName);

        _mockFileSystem
            .Setup(fs => fs.CombinePaths(Directory, currentFile))
            .Returns($"{Directory}/{currentFile}");

        // Act
        _service.UpdateEntry(Directory, currentFileWithoutExt, newEntryTitle: newTitle);

        // Assert - should NOT rename file (no --name)
        _mockFileSystem.Verify(
            fs => fs.RenameFile(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never
        );

        // Assert - should update display name
        _mockJournalConfiguration.Verify(
            jc => jc.UpdateEntryName(Directory, currentFile, newDisplayName),
            Times.Once
        );

        // Assert - should regenerate TOC
        _mockTableOfContentsService.Verify(
            toc => toc.UpdateTableOfContents(Directory, null, It.IsAny<DateTime>()),
            Times.Once
        );
    }

    [Fact]
    public void UpdateEntry_WithBothNameAndTitle_TitleTakesPrecedence()
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

        _mockJournalConfiguration
            .Setup(jc => jc.FindEntry(Directory, currentFile))
            .Returns((currentEntry, currentTopicPath));

        _mockEntryFormatter.Setup(ef => ef.AddSpaceSeparators(newEntryName)).Returns(newEntryName);

        _mockEntryFormatter
            .Setup(ef => ef.AddHeadingSeparators(It.IsAny<string[]>()))
            .Returns(newEntryName);

        _mockEntryFormatter
            .Setup(ef => ef.RemoveSpaceSeparators(newTitle))
            .Returns(displayFromTitle);

        _mockFileSystem
            .Setup(fs => fs.CombinePaths(Directory, currentFile))
            .Returns($"{Directory}/{currentFile}");

        _mockFileSystem
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
        _mockFileSystem.Verify(
            fs => fs.RenameFile($"{Directory}/{currentFile}", $"{Directory}/{newFileName}"),
            Times.Once
        );

        // Assert - should update display name using title, not name
        _mockJournalConfiguration.Verify(
            jc => jc.UpdateEntryName(Directory, newFileName, displayFromTitle),
            Times.Once
        );

        // Assert - should regenerate TOC
        _mockTableOfContentsService.Verify(
            toc => toc.UpdateTableOfContents(Directory, null, It.IsAny<DateTime>()),
            Times.Once
        );
    }

    [Fact]
    public void UpdateEntry_WithHeadings_RenamesFileAndUpdatesLocation()
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

        _mockJournalConfiguration
            .Setup(jc => jc.FindEntry(Directory, currentFile))
            .Returns((currentEntry, currentTopicPath));

        // DetermineTargetTopicPath (--headings explicit)
        _mockEntryFormatter
            .Setup(ef => ef.SeperateSubheadingString(newHeadings))
            .Returns(newTopicPath);

        // DetermineTargetFileName (--headings path):
        // headingParts=["Projects","2024_Goals"], entryNamePart="test_file"
        _mockEntryFormatter
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

        _mockFileSystem
            .Setup(fs => fs.GetFileNameWithoutExtension(currentFile))
            .Returns(currentFileWithoutExt);

        _mockFileSystem
            .Setup(fs => fs.CombinePaths(Directory, newFileName))
            .Returns($"{Directory}/{newFileName}");

        _mockFileSystem.Setup(fs => fs.FileExists($"{Directory}/{newFileName}")).Returns(false);

        // Act
        _service.UpdateEntry(Directory, currentFileWithoutExt, newHeadings: newHeadings);

        // Assert - file renamed
        _mockFileSystem.Verify(
            fs => fs.RenameFile($"{Directory}/{currentFile}", $"{Directory}/{newFileName}"),
            Times.Once
        );

        // Assert - config entry moved to new heading location with new filename
        _mockJournalConfiguration.Verify(jc => jc.RemoveEntry(Directory, newFileName), Times.Once);
        _mockJournalConfiguration.Verify(
            jc => jc.AddEntry(Directory, displayName, newFileName, newTopicPath, null, true, false),
            Times.Once
        );

        // Assert - should regenerate TOC
        _mockTableOfContentsService.Verify(
            toc => toc.UpdateTableOfContents(Directory, null, It.IsAny<DateTime>()),
            Times.Once
        );
    }

    [Fact]
    public void UpdateEntry_WithIgnoreFlag_AddsToIgnoreList()
    {
        // Arrange
        const string currentFile = "test_file.md";
        const string currentFileWithoutExt = "test_file";
        const string displayName = "test file";

        var currentEntry = new Entries { Name = displayName, File = currentFile };
        var currentTopicPath = Array.Empty<string>();

        SetupBasicFileAndJournalrcExists(currentFile);

        _mockJournalConfiguration
            .Setup(jc => jc.FindEntry(Directory, currentFile))
            .Returns((currentEntry, currentTopicPath));

        _mockFileSystem
            .Setup(fs => fs.CombinePaths(Directory, currentFile))
            .Returns($"{Directory}/{currentFile}");

        // Act
        _service.UpdateEntry(Directory, currentFileWithoutExt, ignoreFile: true);

        // Assert
        _mockJournalConfiguration.Verify(jc => jc.RemoveEntry(Directory, currentFile), Times.Once);
        _mockJournalConfiguration.Verify(
            jc => jc.AddIgnoreEntry(Directory, currentFile),
            Times.Once
        );

        // Assert - should regenerate TOC
        _mockTableOfContentsService.Verify(
            toc => toc.UpdateTableOfContents(Directory, null, It.IsAny<DateTime>()),
            Times.Once
        );
    }

    [Fact]
    public void UpdateEntry_WithUnignoreFlag_RemovesFromIgnoreList()
    {
        // Arrange
        const string currentFile = "test_file.md";
        const string currentFileWithoutExt = "test_file";
        const string displayName = "test file";

        var currentEntry = new Entries { Name = displayName, File = currentFile };
        var currentTopicPath = Array.Empty<string>();

        SetupBasicFileAndJournalrcExists(currentFile);

        _mockJournalConfiguration
            .Setup(jc => jc.FindEntry(Directory, currentFile))
            .Returns((currentEntry, currentTopicPath));

        _mockFileSystem
            .Setup(fs => fs.CombinePaths(Directory, currentFile))
            .Returns($"{Directory}/{currentFile}");

        _mockFileSystem
            .Setup(fs => fs.GetFileNameWithoutExtension(currentFile))
            .Returns(currentFileWithoutExt);

        _mockJournalConfiguration
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
                            RootEntries = Array.Empty<Entries>(),
                            Structure = new Structure { Topics = Array.Empty<Topic>() },
                            IgnoreFiles = new[] { currentFile },
                        },
                    };
                    action(config);
                }
            );

        // Act
        _service.UpdateEntry(Directory, currentFileWithoutExt, unignoreFile: true);

        // Assert
        _mockJournalConfiguration.Verify(
            jc => jc.Update(Directory, It.IsAny<Action<JournalConfig>>()),
            Times.Once
        );

        // Verify that AddEntry was called to add the file back to the structure
        _mockJournalConfiguration.Verify(
            jc => jc.AddEntry(Directory, "", currentFile, null, null, true, false),
            Times.Once
        );

        // Assert - should regenerate TOC
        _mockTableOfContentsService.Verify(
            toc => toc.UpdateTableOfContents(Directory, null, It.IsAny<DateTime>()),
            Times.Once
        );
    }

    [Fact]
    public void UpdateEntry_FileNotFound_ThrowsFileNotFoundException()
    {
        // Arrange
        const string currentFile = "nonexistent.md";
        var currentPath = $"{Directory}/{currentFile}";

        _mockFileSystem.Setup(fs => fs.CombinePaths(Directory, currentFile)).Returns(currentPath);

        _mockFileSystem.Setup(fs => fs.FileExists(currentPath)).Returns(false);

        // Act & Assert
        var exception = Should.Throw<FileNotFoundException>(() =>
            _service.UpdateEntry(Directory, currentFile)
        );

        exception.Message.ShouldContain(currentFile);
        exception.Message.ShouldContain(Directory);
    }

    [Fact]
    public void UpdateEntry_JournalrcNotFound_ThrowsJournalrcNotFoundException()
    {
        // Arrange
        const string currentFile = "test_file.md";
        var currentPath = $"{Directory}/{currentFile}";
        var journalrcPath = $"{Directory}/.journalrc";

        _mockFileSystem.Setup(fs => fs.CombinePaths(Directory, currentFile)).Returns(currentPath);

        _mockFileSystem.Setup(fs => fs.FileExists(currentPath)).Returns(true);

        _mockFileSystem
            .Setup(fs => fs.CombinePaths(Directory, ".journalrc"))
            .Returns(journalrcPath);

        _mockFileSystem.Setup(fs => fs.FileExists(journalrcPath)).Returns(false);

        // Act & Assert
        Should.Throw<markdown_journal_cli.Exceptions.JournalrcNotFoundException>(() =>
            _service.UpdateEntry(Directory, currentFile)
        );
    }

    [Fact]
    public void UpdateEntry_WithName_PreservesHeadingPrefixAndRenamesLastSegment()
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

        _mockJournalConfiguration
            .Setup(jc => jc.FindEntry(Directory, currentFile))
            .Returns((currentEntry, currentTopicPath));

        _mockEntryFormatter.Setup(ef => ef.AddSpaceSeparators(newEntryName)).Returns(newEntryName);

        _mockEntryFormatter
            .Setup(ef => ef.AddHeadingSeparators(It.IsAny<string[]>()))
            .Returns(newFileNameWithoutExt);

        _mockEntryFormatter.Setup(ef => ef.RemoveSpaceSeparators("collin")).Returns("collin");

        _mockEntryFormatter
            .Setup(ef => ef.RemoveSpaceSeparators(newEntryName))
            .Returns(newEntryName);

        _mockEntryFormatter.Setup(ef => ef.RemoveSpaceSeparators("entry")).Returns("entry");

        _mockFileSystem
            .Setup(fs => fs.CombinePaths(Directory, currentFile))
            .Returns($"{Directory}/{currentFile}");

        _mockFileSystem
            .Setup(fs => fs.CombinePaths(Directory, newFileName))
            .Returns($"{Directory}/{newFileName}");

        _mockFileSystem
            .Setup(fs => fs.GetFileNameWithoutExtension(currentFile))
            .Returns(currentFileWithoutExt);

        _mockFileSystem
            .Setup(fs => fs.GetFileNameWithoutExtension(newFileName))
            .Returns(newFileNameWithoutExt);

        // Act
        _service.UpdateEntry(Directory, currentFileWithoutExt, newEntryName: newEntryName);

        // Assert - renamed with prefix intact: collin-entry.md → collin-robison.md
        _mockFileSystem.Verify(
            fs => fs.RenameFile($"{Directory}/{currentFile}", $"{Directory}/{newFileName}"),
            Times.Once
        );

        // Assert - topic path unchanged (["collin"] → ["collin"]); only display name updated
        _mockJournalConfiguration.Verify(
            jc => jc.UpdateEntryName(Directory, newFileName, newEntryName),
            Times.Once,
            "Display name should update since it matched the old last segment"
        );

        _mockJournalConfiguration.Verify(
            jc => jc.RemoveEntry(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never,
            "Entry should not be moved; heading location is unchanged"
        );
    }

    [Fact]
    public void UpdateEntry_WithName_OnRootLevelFile_RenamesFile()
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

        _mockJournalConfiguration
            .Setup(jc => jc.FindEntry(Directory, currentFile))
            .Returns((currentEntry, currentTopicPath));

        _mockEntryFormatter.Setup(ef => ef.AddSpaceSeparators(newEntryName)).Returns(newEntryName);

        _mockEntryFormatter
            .Setup(ef => ef.AddHeadingSeparators(It.IsAny<string[]>()))
            .Returns(newEntryName);

        _mockEntryFormatter
            .Setup(ef => ef.RemoveSpaceSeparators(currentFileWithoutExt))
            .Returns(displayName);

        _mockEntryFormatter
            .Setup(ef => ef.RemoveSpaceSeparators(newEntryName))
            .Returns(newEntryName);

        _mockFileSystem
            .Setup(fs => fs.CombinePaths(Directory, currentFile))
            .Returns($"{Directory}/{currentFile}");

        _mockFileSystem
            .Setup(fs => fs.CombinePaths(Directory, newFileName))
            .Returns($"{Directory}/{newFileName}");

        _mockFileSystem
            .Setup(fs => fs.GetFileNameWithoutExtension(currentFile))
            .Returns(currentFileWithoutExt);

        // Act
        _service.UpdateEntry(Directory, currentFileWithoutExt, newEntryName: newEntryName);

        // Assert - file renamed at root level
        _mockFileSystem.Verify(
            fs => fs.RenameFile($"{Directory}/{currentFile}", $"{Directory}/{newFileName}"),
            Times.Once
        );

        // Assert - display name updated (it matched old filename)
        _mockJournalConfiguration.Verify(
            jc => jc.UpdateEntryName(Directory, newFileName, newEntryName),
            Times.Once
        );

        // Assert - not moved to a new heading (still at root)
        _mockJournalConfiguration.Verify(
            jc => jc.RemoveEntry(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never
        );
    }

    [Fact]
    public void UpdateEntry_WithNameAndHeadings_HeadingsPrefixTakesPrecedence()
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

        _mockJournalConfiguration
            .Setup(jc => jc.FindEntry(Directory, currentFile))
            .Returns((currentEntry, currentTopicPath));

        _mockEntryFormatter.Setup(ef => ef.AddSpaceSeparators(newEntryName)).Returns(newEntryName);

        _mockEntryFormatter
            .Setup(ef => ef.AddHeadingSeparators(It.IsAny<string[]>()))
            .Returns(newFileNameWithoutExt);

        _mockEntryFormatter
            .Setup(ef => ef.SeperateSubheadingString(newHeadings))
            .Returns(new[] { "nat" });

        _mockEntryFormatter
            .Setup(ef => ef.RemoveSpaceSeparators(currentFileWithoutExt))
            .Returns(displayName);

        _mockEntryFormatter
            .Setup(ef => ef.RemoveSpaceSeparators(newEntryName))
            .Returns(displayName);

        _mockFileSystem
            .Setup(fs => fs.CombinePaths(Directory, currentFile))
            .Returns($"{Directory}/{currentFile}");

        _mockFileSystem
            .Setup(fs => fs.CombinePaths(Directory, newFileName))
            .Returns($"{Directory}/{newFileName}");

        _mockFileSystem
            .Setup(fs => fs.GetFileNameWithoutExtension(currentFile))
            .Returns(currentFileWithoutExt);

        _mockFileSystem
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
        _mockFileSystem.Verify(
            fs => fs.RenameFile($"{Directory}/{currentFile}", $"{Directory}/{newFileName}"),
            Times.Once
        );

        // Assert - heading didn't change (Nat == nat case-insensitively) so entry not relocated
        _mockJournalConfiguration.Verify(
            jc => jc.RemoveEntry(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never,
            "Entry should not be relocated; it is already in the Nat heading"
        );

        // Assert - TOC regenerated
        _mockTableOfContentsService.Verify(
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

        _mockFileSystem.Setup(fs => fs.CombinePaths(Directory, fileName)).Returns(filePath);

        _mockFileSystem.Setup(fs => fs.FileExists(filePath)).Returns(true);

        _mockFileSystem
            .Setup(fs => fs.CombinePaths(Directory, ".journalrc"))
            .Returns(journalrcPath);

        _mockFileSystem.Setup(fs => fs.FileExists(journalrcPath)).Returns(true);
    }

    #endregion

    #region Backlink Update Tests

    private void SetupRenameScenario(string currentFile, string newName, string newFile)
    {
        SetupBasicFileAndJournalrcExists(currentFile);

        _mockJournalConfiguration
            .Setup(jc => jc.FindEntry(Directory, currentFile))
            .Returns((null, Array.Empty<string>()));

        var currentStem = System.IO.Path.GetFileNameWithoutExtension(currentFile);
        var newStem = System.IO.Path.GetFileNameWithoutExtension(newFile);

        _mockFileSystem
            .Setup(fs => fs.GetFileNameWithoutExtension(currentFile))
            .Returns(currentStem);
        _mockFileSystem.Setup(fs => fs.GetFileNameWithoutExtension(newFile)).Returns(newStem);

        _mockEntryFormatter.Setup(ef => ef.AddSpaceSeparators(newName)).Returns(newName);
        _mockEntryFormatter
            .Setup(ef => ef.AddHeadingSeparators(It.IsAny<string[]>()))
            .Returns(newStem);
        _mockEntryFormatter
            .Setup(ef => ef.RemoveSpaceSeparators(currentStem))
            .Returns(currentStem.Replace("_", " "));
        _mockEntryFormatter
            .Setup(ef => ef.RemoveSpaceSeparators(newName))
            .Returns(newName.Replace("_", " "));

        var targetFilePath = $"{Directory}/{newFile}";
        _mockFileSystem.Setup(fs => fs.CombinePaths(Directory, newFile)).Returns(targetFilePath);
        _mockFileSystem.Setup(fs => fs.FileExists(targetFilePath)).Returns(false);
    }

    [Fact]
    public void UpdateEntry_CallsReplaceLinksInDirectory_WhenRenameOccurs()
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
    public void UpdateEntry_DoesNotCallReplaceLinksInDirectory_WhenNoRenameOccurs()
    {
        // Arrange — only a title change, file stays the same
        const string currentFile = "my_entry.md";
        const string stem = "my_entry";
        SetupBasicFileAndJournalrcExists(currentFile);

        _mockJournalConfiguration
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

        _mockFileSystem.Setup(fs => fs.GetFileNameWithoutExtension(currentFile)).Returns(stem);
        _mockEntryFormatter.Setup(ef => ef.RemoveSpaceSeparators("New Title")).Returns("New Title");

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
    public void UpdateEntry_DoesNotCallReplaceLinksInDirectory_WhenUpdateBacklinksFalse()
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
    public void UpdateEntry_ExcludesTocFile_FromReplaceLinks()
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
    public void UpdateEntry_ExcludesRenamedFile_FromReplaceLinks()
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
