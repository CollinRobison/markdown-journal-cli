using markdown_journal_cli.Exceptions;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.Configuration.Models;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.Transactions;
using markdown_journal_cli.Infrastructure.Tracking;
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
public class RemoveEntryServiceTests
{
    private const string JournalPath = "/test/journal";
    private const string JournalrcPath = "/test/journal/.journalrc";
    private const string TrackingPath = "/test/journal/.md-journal";
    private const string EntryFileName = "my_entry.md";
    private const string EntryFilePath = "/test/journal/my_entry.md";

    private readonly Mock<IFileSystem> _mockFileSystem;
    private readonly Mock<IJournalConfiguration> _mockJournalConfiguration;
    private readonly Mock<IFileTracking> _mockFileTracking;
    private readonly Mock<ITableOfContentsService> _mockTocService;
    private readonly Mock<IMarkdownLinkRewriter> _mockLinkRewriter;
    private readonly IOptions<JournalSettings> _journalSettings;
    private readonly RemoveEntryService _service;

    public RemoveEntryServiceTests()
    {
        _mockFileSystem = new Mock<IFileSystem>();
        _mockJournalConfiguration = new Mock<IJournalConfiguration>();
        _mockFileTracking = new Mock<IFileTracking>();
        _mockTocService = new Mock<ITableOfContentsService>();
        _mockLinkRewriter = new Mock<IMarkdownLinkRewriter>();

        _journalSettings = Options.Create(new JournalSettings
        {
            AppName = "md-journal",
            JournalConfigFileName = ".journalrc",
            TableOfContentsFileName = "1a-TableOfContents",
        });

        SetupDefaultMockBehaviors();

        _service = new RemoveEntryService(
            _mockFileSystem.Object,
            _mockJournalConfiguration.Object,
            _mockFileTracking.Object,
            _mockTocService.Object,
            _mockLinkRewriter.Object,
            _journalSettings,
            NoOpFileTransactionCoordinator.Instance,
            NoOpRollbackReporter.Instance,
            NullLogger<RemoveEntryService>.Instance
        );
    }

    private void SetupDefaultMockBehaviors()
    {
        _mockFileSystem.Setup(fs => fs.FileExists(JournalrcPath)).Returns(true);
        _mockFileSystem.Setup(fs => fs.FileExists(TrackingPath)).Returns(true);
        _mockFileSystem.Setup(fs => fs.FileExists(EntryFilePath)).Returns(true);
        _mockFileSystem
            .Setup(fs => fs.CombinePaths(It.IsAny<string[]>()))
            .Returns((string[] paths) => Path.Combine(paths));

        _mockJournalConfiguration
            .Setup(jc => jc.Read(JournalPath))
            .Returns(new JournalConfig
            {
                TableOfContents = new TableOfContents
                {
                    File = "1a-TableOfContents.md",
                    Structure = new Structure { Topics = [] },
                    RootEntries = [],
                }
            });

        _mockLinkRewriter
            .Setup(r => r.StripLinksInDirectory(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>?>()))
            .Returns(Array.Empty<string>());
    }

    // ------------------------------------------------------------------
    // Happy paths
    // ------------------------------------------------------------------

    [Fact]
    public void RemoveEntry_DeletesFile_UpdatesConfig_UpdatesTracking_UpdatesToc()
    {
        // Act
        var result = _service.RemoveEntry(JournalPath, EntryFileName, cleanRefs: false);

        // Assert
        result.ShouldBeEmpty();
        _mockFileSystem.Verify(fs => fs.DeleteFile(EntryFilePath), Times.Once);
        _mockJournalConfiguration.Verify(jc => jc.RemoveEntry(JournalPath, EntryFileName), Times.Once);
        _mockFileTracking.Verify(ft => ft.RemoveFileFromIndex(JournalPath, EntryFileName), Times.Once);
        _mockTocService.Verify(
            t => t.UpdateTableOfContents(JournalPath, null, It.IsAny<DateTime?>()),
            Times.Once
        );
    }

    [Fact]
    public void RemoveEntry_WithCleanRefs_CallsStripLinksInDirectory()
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
    public void RemoveEntry_WithCleanRefs_UpdatesTrackingForEachModifiedFile()
    {
        // Arrange
        var modifiedFiles = new[] { "other.md", "another.md" };
        _mockLinkRewriter
            .Setup(r => r.StripLinksInDirectory(JournalPath, EntryFileName, null))
            .Returns(modifiedFiles);

        // Act
        var result = _service.RemoveEntry(JournalPath, EntryFileName, cleanRefs: true);

        // Assert
        result.ShouldBe(modifiedFiles);
        _mockFileTracking.Verify(ft => ft.UpdateFileInIndex(JournalPath, "other.md"), Times.Once);
        _mockFileTracking.Verify(ft => ft.UpdateFileInIndex(JournalPath, "another.md"), Times.Once);
    }

    [Fact]
    public void RemoveEntry_WithoutCleanRefs_DoesNotCallStripLinksInDirectory()
    {
        // Act
        _service.RemoveEntry(JournalPath, EntryFileName, cleanRefs: false);

        // Assert
        _mockLinkRewriter.Verify(
            r => r.StripLinksInDirectory(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>?>()),
            Times.Never
        );
    }

    // ------------------------------------------------------------------
    // Error cases
    // ------------------------------------------------------------------

    [Fact]
    public void RemoveEntry_JournalrcNotFound_ThrowsJournalrcNotFoundException()
    {
        // Arrange
        _mockFileSystem.Setup(fs => fs.FileExists(JournalrcPath)).Returns(false);

        // Act & Assert
        Should.Throw<JournalrcNotFoundException>(() =>
            _service.RemoveEntry(JournalPath, EntryFileName, cleanRefs: false)
        );
    }

    [Fact]
    public void RemoveEntry_TrackingIndexNotFound_ThrowsTrackingIndexNotFoundException()
    {
        // Arrange
        _mockFileSystem.Setup(fs => fs.FileExists(TrackingPath)).Returns(false);

        // Act & Assert
        Should.Throw<TrackingIndexNotFoundException>(() =>
            _service.RemoveEntry(JournalPath, EntryFileName, cleanRefs: false)
        );
    }

    [Fact]
    public void RemoveEntry_TargetsJournalConfig_ThrowsProtectedJournalFileException()
    {
        // Act & Assert
        Should.Throw<ProtectedJournalFileException>(() =>
            _service.RemoveEntry(JournalPath, ".journalrc", cleanRefs: false)
        );
        _mockFileSystem.Verify(fs => fs.DeleteFile(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void RemoveEntry_TargetsTrackingIndex_ThrowsProtectedJournalFileException()
    {
        // Act & Assert
        Should.Throw<ProtectedJournalFileException>(() =>
            _service.RemoveEntry(JournalPath, ".md-journal", cleanRefs: false)
        );
        _mockFileSystem.Verify(fs => fs.DeleteFile(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void RemoveEntry_TargetsTocFile_ThrowsProtectedJournalFileException()
    {
        // Act & Assert
        Should.Throw<ProtectedJournalFileException>(() =>
            _service.RemoveEntry(JournalPath, "1a-TableOfContents.md", cleanRefs: false)
        );
        _mockFileSystem.Verify(fs => fs.DeleteFile(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void RemoveEntry_TargetsTocFile_CaseInsensitive_ThrowsProtectedJournalFileException()
    {
        // Arrange
        _mockFileSystem
            .Setup(fs => fs.CombinePaths(JournalPath, "1A-TABLEOFCONTENTS.md"))
            .Returns("/test/journal/1A-TABLEOFCONTENTS.md");

        // Act & Assert
        Should.Throw<ProtectedJournalFileException>(() =>
            _service.RemoveEntry(JournalPath, "1A-TABLEOFCONTENTS.md", cleanRefs: false)
        );
    }

    [Fact]
    public void RemoveEntry_TargetsRenamedTocFile_ThrowsProtectedJournalFileException()
    {
        // Arrange — user renamed the TOC to my-toc.md via update journal --rename-toc
        _mockJournalConfiguration
            .Setup(jc => jc.Read(JournalPath))
            .Returns(new JournalConfig
            {
                TableOfContents = new TableOfContents
                {
                    File = "my-toc.md",
                    Structure = new Structure { Topics = [] },
                    RootEntries = [],
                }
            });

        // Act & Assert
        Should.Throw<ProtectedJournalFileException>(() =>
            _service.RemoveEntry(JournalPath, "my-toc.md", cleanRefs: false)
        );
    }

    [Fact]
    public void RemoveEntry_FileNotFound_ThrowsFileNotFoundException()
    {
        // Arrange
        _mockFileSystem.Setup(fs => fs.FileExists(EntryFilePath)).Returns(false);

        // Act & Assert
        Should.Throw<FileNotFoundException>(() =>
            _service.RemoveEntry(JournalPath, EntryFileName, cleanRefs: false)
        );
        _mockFileSystem.Verify(fs => fs.DeleteFile(It.IsAny<string>()), Times.Never);
    }

    // ------------------------------------------------------------------
    // Filename normalisation
    // ------------------------------------------------------------------

    [Fact]
    public void RemoveEntry_FileNameWithoutExtension_NormalisesAndResolvesCorrectly()
    {
        // Arrange — pass "my_entry" (no .md), expect service to append .md
        _mockFileSystem
            .Setup(fs => fs.CombinePaths(JournalPath, EntryFileName))
            .Returns(EntryFilePath);

        // Act
        _service.RemoveEntry(JournalPath, "my_entry", cleanRefs: false);

        // Assert — delete and config/tracking calls use normalised name
        _mockFileSystem.Verify(fs => fs.DeleteFile(EntryFilePath), Times.Once);
        _mockJournalConfiguration.Verify(jc => jc.RemoveEntry(JournalPath, EntryFileName), Times.Once);
    }

    [Fact]
    public void RemoveEntry_FileNameWithExtension_ResolvesCorrectly()
    {
        // Arrange — pass "my_entry.md" (already has .md)
        _mockFileSystem
            .Setup(fs => fs.CombinePaths(JournalPath, EntryFileName))
            .Returns(EntryFilePath);

        // Act
        _service.RemoveEntry(JournalPath, EntryFileName, cleanRefs: false);

        // Assert — no double extension
        _mockFileSystem.Verify(fs => fs.DeleteFile(EntryFilePath), Times.Once);
        _mockJournalConfiguration.Verify(jc => jc.RemoveEntry(JournalPath, EntryFileName), Times.Once);
    }

    // ------------------------------------------------------------------
    // Orchestration order
    // ------------------------------------------------------------------

    [Fact]
    public void RemoveEntry_CallOrder_ProtectedFileCheckBeforeDelete()
    {
        // Arrange — verify that targeting a protected file never reaches DeleteFile,
        // even though the entry file "exists" in the mock.
        _mockFileSystem
            .Setup(fs => fs.FileExists(It.Is<string>(s => s.Contains("1a-TableOfContents"))))
            .Returns(true);

        // Act & Assert
        Should.Throw<ProtectedJournalFileException>(() =>
            _service.RemoveEntry(JournalPath, "1a-TableOfContents.md", cleanRefs: false)
        );

        _mockFileSystem.Verify(fs => fs.DeleteFile(It.IsAny<string>()), Times.Never);
    }
}
