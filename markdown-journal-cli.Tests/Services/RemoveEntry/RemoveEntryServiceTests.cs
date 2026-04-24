using markdown_journal_cli.Exceptions;
using markdown_journal_cli.Tests.Infrastructure;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.Configuration.Models;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.Tracking;
using markdown_journal_cli.Infrastructure.Tracking.Models;
using markdown_journal_cli.Infrastructure.Transactions;
using markdown_journal_cli.Services;
using markdown_journal_cli.Services.RemoveEntry;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Xunit;

namespace markdown_journal_cli.Tests.Services.RemoveEntry;

/// <summary>
/// Unit tests for <see cref="RemoveEntryService"/> covering orchestration sequence,
/// protected-file guard, normalisation, and clean-refs behaviour.
/// </summary>
public class RemoveEntryServiceTests : ServiceTestBase
{
    private const string JournalPath = "/test/journal";
    private const string JournalrcPath = "/test/journal/.journalrc";
    private const string TrackingPath = "/test/journal/.md-journal";
    private const string EntryFileName = "my_entry.md";
    private const string EntryFilePath = "/test/journal/my_entry.md";

    private readonly Mock<IMarkdownLinkRewriter> _mockLinkRewriter;
    private readonly RemoveEntryService _service;

    public RemoveEntryServiceTests()
    {
        _mockLinkRewriter = new Mock<IMarkdownLinkRewriter>();

        SetupDefaultMockBehaviors();

        _service = new RemoveEntryService(
            MockFileSystem.Object,
            MockJournalConfiguration.Object,
            MockFileTracking.Object,
            MockTableOfContentsService.Object,
            _mockLinkRewriter.Object,
            JournalSettings,
            NoOpCoordinator,
            NoOpReporter,
            NullLogger<RemoveEntryService>()
        );
    }

    private void SetupDefaultMockBehaviors()
    {
        MockFileSystem.Setup(fs => fs.FileExists(JournalrcPath)).Returns(true);
        MockFileSystem.Setup(fs => fs.FileExists(TrackingPath)).Returns(true);
        MockFileSystem.Setup(fs => fs.FileExists(EntryFilePath)).Returns(true);
        MockFileSystem
            .Setup(fs => fs.CombinePaths(It.IsAny<string[]>()))
            .Returns((string[] paths) => Path.Combine(paths));

        MockJournalConfiguration
            .Setup(jc => jc.Read(JournalPath))
            .Returns(
                new JournalConfig
                {
                    TableOfContents = new TableOfContents
                    {
                        File = "1a-TableOfContents.md",
                    },
                }
            );

        _mockLinkRewriter
            .Setup(r =>
                r.StripLinksInDirectory(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<IReadOnlyCollection<string>?>()
                )
            )
            .Returns(Array.Empty<string>());

        MockJournalConfiguration
            .Setup(jc => jc.RemoveEntry(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(true);

        MockFileTracking
            .Setup(ft => ft.LoadIndex(It.IsAny<string>()))
            .Returns(new JournalIndex
            {
                Files = new Dictionary<string, FileState>
                {
                    [EntryFileName] = new FileState { FilePath = EntryFilePath, Hash = "abc", LastChecked = DateTime.Now },
                },
            });
    }

    // ------------------------------------------------------------------
    // Happy paths
    // ------------------------------------------------------------------

    [Fact]
    public void RemoveEntry_Should_DeleteFileAndUpdateConfigAndTrackingAndToc()
    {
        // Act
        var result = _service.RemoveEntry(JournalPath, EntryFileName, cleanRefs: false);

        // Assert
        result.StrippedLinkFiles.ShouldBeEmpty();
        result.FileExistedOnDisk.ShouldBeTrue();
        result.RemovedFromConfig.ShouldBeTrue();
        result.RemovedFromTracking.ShouldBeTrue();
        MockFileSystem.Verify(fs => fs.DeleteFile(EntryFilePath), Times.Once);
        MockJournalConfiguration.Verify(
            jc => jc.RemoveEntry(JournalPath, EntryFileName),
            Times.Once
        );
        MockFileTracking.Verify(
            ft => ft.RemoveFileFromIndex(JournalPath, EntryFileName),
            Times.Once
        );
        MockTableOfContentsService.Verify(
            t => t.UpdateTableOfContents(JournalPath, null, It.IsAny<DateTime?>()),
            Times.Once
        );
    }

    [Fact]
    public void RemoveEntry_Should_CallStripLinksInDirectory_When_CleanRefsIsTrue()
    {
        // Act
        _service.RemoveEntry(JournalPath, EntryFileName, cleanRefs: true);

        // Assert
        _mockLinkRewriter.Verify(
            r => r.StripLinksInDirectory(JournalPath, EntryFileName, null),
            Times.Once
        );
    }

    [Fact]
    public void RemoveEntry_Should_UpdateTrackingForEachModifiedFile_When_CleanRefsIsTrue()
    {
        // Arrange
        var modifiedFiles = new[] { "other.md", "another.md" };
        _mockLinkRewriter
            .Setup(r => r.StripLinksInDirectory(JournalPath, EntryFileName, null))
            .Returns(modifiedFiles);

        // Act
        var result = _service.RemoveEntry(JournalPath, EntryFileName, cleanRefs: true);

        // Assert
        result.StrippedLinkFiles.ShouldBe(modifiedFiles);
        MockFileTracking.Verify(ft => ft.UpdateFileInIndex(JournalPath, "other.md"), Times.Once);
        MockFileTracking.Verify(ft => ft.UpdateFileInIndex(JournalPath, "another.md"), Times.Once);
    }

    [Fact]
    public void RemoveEntry_Should_NotCallStripLinksInDirectory_When_CleanRefsIsFalse()
    {
        // Act
        _service.RemoveEntry(JournalPath, EntryFileName, cleanRefs: false);

        // Assert
        _mockLinkRewriter.Verify(
            r =>
                r.StripLinksInDirectory(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<IReadOnlyCollection<string>?>()
                ),
            Times.Never
        );
    }

    // ------------------------------------------------------------------
    // Error cases
    // ------------------------------------------------------------------

    [Fact]
    public void RemoveEntry_Should_ThrowJournalrcNotFoundException_When_JournalrcNotFound()
    {
        // Arrange
        MockFileSystem.Setup(fs => fs.FileExists(JournalrcPath)).Returns(false);

        // Act & Assert
        Should.Throw<JournalrcNotFoundException>(() =>
            _service.RemoveEntry(JournalPath, EntryFileName, cleanRefs: false)
        );
    }

    [Fact]
    public void RemoveEntry_Should_ThrowTrackingIndexNotFoundException_When_TrackingIndexNotFound()
    {
        // Arrange
        MockFileSystem.Setup(fs => fs.FileExists(TrackingPath)).Returns(false);

        // Act & Assert
        Should.Throw<TrackingIndexNotFoundException>(() =>
            _service.RemoveEntry(JournalPath, EntryFileName, cleanRefs: false)
        );
    }

    [Fact]
    public void RemoveEntry_Should_ThrowProtectedJournalFileException_When_TargetingJournalConfig()
    {
        // Act & Assert
        Should.Throw<ProtectedJournalFileException>(() =>
            _service.RemoveEntry(JournalPath, ".journalrc", cleanRefs: false)
        );
        MockFileSystem.Verify(fs => fs.DeleteFile(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void RemoveEntry_Should_ThrowProtectedJournalFileException_When_TargetingTrackingIndex()
    {
        // Act & Assert
        Should.Throw<ProtectedJournalFileException>(() =>
            _service.RemoveEntry(JournalPath, ".md-journal", cleanRefs: false)
        );
        MockFileSystem.Verify(fs => fs.DeleteFile(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void RemoveEntry_Should_ThrowProtectedJournalFileException_When_TargetingTocFile()
    {
        // Act & Assert
        Should.Throw<ProtectedJournalFileException>(() =>
            _service.RemoveEntry(JournalPath, "1a-TableOfContents.md", cleanRefs: false)
        );
        MockFileSystem.Verify(fs => fs.DeleteFile(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void RemoveEntry_Should_ThrowProtectedJournalFileException_When_TargetingTocFileCaseInsensitively()
    {
        // Arrange
        MockFileSystem
            .Setup(fs => fs.CombinePaths(JournalPath, "1A-TABLEOFCONTENTS.md"))
            .Returns("/test/journal/1A-TABLEOFCONTENTS.md");

        // Act & Assert
        Should.Throw<ProtectedJournalFileException>(() =>
            _service.RemoveEntry(JournalPath, "1A-TABLEOFCONTENTS.md", cleanRefs: false)
        );
    }

    [Fact]
    public void RemoveEntry_Should_ThrowProtectedJournalFileException_When_TargetingRenamedTocFile()
    {
        // Arrange — user renamed the TOC to my-toc.md via update journal --rename-toc
        MockJournalConfiguration
            .Setup(jc => jc.Read(JournalPath))
            .Returns(
                new JournalConfig
                {
                    TableOfContents = new TableOfContents
                    {
                        File = "my-toc.md",
                    },
                }
            );

        // Act & Assert
        Should.Throw<ProtectedJournalFileException>(() =>
            _service.RemoveEntry(JournalPath, "my-toc.md", cleanRefs: false)
        );
    }

    [Fact]
    public void RemoveEntry_Should_ThrowFileNotFoundException_When_FileNotFound()
    {
        // Arrange
        MockFileSystem.Setup(fs => fs.FileExists(EntryFilePath)).Returns(false);

        // Act & Assert
        Should.Throw<FileNotFoundException>(() =>
            _service.RemoveEntry(JournalPath, EntryFileName, cleanRefs: false)
        );
        MockFileSystem.Verify(fs => fs.DeleteFile(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void RemoveEntry_Should_SucceedWithoutDeleting_When_FileAbsentAndCleanRefsIsTrue()
    {
        // Arrange — file is missing from disk
        MockFileSystem.Setup(fs => fs.FileExists(EntryFilePath)).Returns(false);

        // Act — should NOT throw
        var result = _service.RemoveEntry(JournalPath, EntryFileName, cleanRefs: true);

        // Assert — no delete attempted
        result.StrippedLinkFiles.ShouldBeEmpty();
        result.FileExistedOnDisk.ShouldBeFalse();
        MockFileSystem.Verify(fs => fs.DeleteFile(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void RemoveEntry_Should_StillUpdateConfigAndTrackingAndToc_When_FileAbsentAndCleanRefsIsTrue()
    {
        // Arrange
        MockFileSystem.Setup(fs => fs.FileExists(EntryFilePath)).Returns(false);

        // Act
        _service.RemoveEntry(JournalPath, EntryFileName, cleanRefs: true);

        // Assert — all metadata cleanup still happens
        MockJournalConfiguration.Verify(
            jc => jc.RemoveEntry(JournalPath, EntryFileName),
            Times.Once
        );
        MockFileTracking.Verify(
            ft => ft.RemoveFileFromIndex(JournalPath, EntryFileName),
            Times.Once
        );
        MockTableOfContentsService.Verify(
            t => t.UpdateTableOfContents(JournalPath, null, It.IsAny<DateTime?>()),
            Times.Once
        );
    }

    [Fact]
    public void RemoveEntry_Should_StillStripLinksInDirectory_When_FileAbsentAndCleanRefsIsTrue()
    {
        // Arrange
        MockFileSystem.Setup(fs => fs.FileExists(EntryFilePath)).Returns(false);
        var modifiedFiles = new[] { "other.md" };
        _mockLinkRewriter
            .Setup(r => r.StripLinksInDirectory(JournalPath, EntryFileName, null))
            .Returns(modifiedFiles);

        // Act
        var result = _service.RemoveEntry(JournalPath, EntryFileName, cleanRefs: true);

        // Assert
        result.StrippedLinkFiles.ShouldBe(modifiedFiles);
        _mockLinkRewriter.Verify(
            r => r.StripLinksInDirectory(JournalPath, EntryFileName, null),
            Times.Once
        );
    }

    [Fact]
    public void ValidatePreconditions_Should_NotThrow_When_FileAbsentAndCleanRefsIsTrue()
    {
        // Arrange
        MockFileSystem.Setup(fs => fs.FileExists(EntryFilePath)).Returns(false);

        // Act & Assert — should not throw
        Should.NotThrow(() =>
            _service.ValidatePreconditions(JournalPath, EntryFileName, cleanRefs: true)
        );
    }

    [Fact]
    public void ValidatePreconditions_Should_ThrowFileNotFoundException_When_FileAbsentAndCleanRefsFalse()
    {
        // Arrange
        MockFileSystem.Setup(fs => fs.FileExists(EntryFilePath)).Returns(false);

        // Act & Assert — existing behaviour preserved when cleanRefs is false
        Should.Throw<FileNotFoundException>(() =>
            _service.ValidatePreconditions(JournalPath, EntryFileName, cleanRefs: false)
        );
    }

    // ------------------------------------------------------------------
    // Filename normalisation
    // ------------------------------------------------------------------

    [Fact]
    public void RemoveEntry_Should_NormaliseAndResolveFileNameCorrectly_When_FileNameLacksExtension()
    {
        // Arrange — pass "my_entry" (no .md), expect service to append .md
        MockFileSystem
            .Setup(fs => fs.CombinePaths(JournalPath, EntryFileName))
            .Returns(EntryFilePath);

        // Act
        _service.RemoveEntry(JournalPath, "my_entry", cleanRefs: false);

        // Assert — delete and config/tracking calls use normalised name
        MockFileSystem.Verify(fs => fs.DeleteFile(EntryFilePath), Times.Once);
        MockJournalConfiguration.Verify(
            jc => jc.RemoveEntry(JournalPath, EntryFileName),
            Times.Once
        );
    }

    [Fact]
    public void RemoveEntry_Should_ResolveFileNameCorrectly_When_FileNameHasExtension()
    {
        // Arrange — pass "my_entry.md" (already has .md)
        MockFileSystem
            .Setup(fs => fs.CombinePaths(JournalPath, EntryFileName))
            .Returns(EntryFilePath);

        // Act
        _service.RemoveEntry(JournalPath, EntryFileName, cleanRefs: false);

        // Assert — no double extension
        MockFileSystem.Verify(fs => fs.DeleteFile(EntryFilePath), Times.Once);
        MockJournalConfiguration.Verify(
            jc => jc.RemoveEntry(JournalPath, EntryFileName),
            Times.Once
        );
    }

    // ------------------------------------------------------------------
    // Orchestration order
    // ------------------------------------------------------------------

    [Fact]
    public void RemoveEntry_Should_CheckProtectedFileBeforeDelete()
    {
        // Arrange — verify that targeting a protected file never reaches DeleteFile,
        // even though the entry file "exists" in the mock.
        MockFileSystem
            .Setup(fs => fs.FileExists(It.Is<string>(s => s.Contains("1a-TableOfContents"))))
            .Returns(true);

        // Act & Assert
        Should.Throw<ProtectedJournalFileException>(() =>
            _service.RemoveEntry(JournalPath, "1a-TableOfContents.md", cleanRefs: false)
        );

        MockFileSystem.Verify(fs => fs.DeleteFile(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void RemoveEntry_Should_ReturnRemovedFromConfigFalse_When_EntryNotInConfig()
    {
        // Arrange
        MockJournalConfiguration
            .Setup(jc => jc.RemoveEntry(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(false);

        // Act
        var result = _service.RemoveEntry(JournalPath, EntryFileName, cleanRefs: false);

        // Assert
        result.RemovedFromConfig.ShouldBeFalse();
    }

    [Fact]
    public void RemoveEntry_Should_ReturnRemovedFromConfigTrue_When_EntryWasInConfig()
    {
        // Arrange
        MockJournalConfiguration
            .Setup(jc => jc.RemoveEntry(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(true);

        // Act
        var result = _service.RemoveEntry(JournalPath, EntryFileName, cleanRefs: false);

        // Assert
        result.RemovedFromConfig.ShouldBeTrue();
    }

    [Fact]
    public void RemoveEntry_Should_ReturnRemovedFromTrackingFalse_When_EntryNotInIndex()
    {
        // Arrange
        MockFileTracking
            .Setup(ft => ft.LoadIndex(It.IsAny<string>()))
            .Returns(new JournalIndex());

        // Act
        var result = _service.RemoveEntry(JournalPath, EntryFileName, cleanRefs: false);

        // Assert
        result.RemovedFromTracking.ShouldBeFalse();
    }
}
