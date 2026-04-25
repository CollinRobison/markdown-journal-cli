using markdown_journal_cli.Exceptions;
using markdown_journal_cli.Tests.Infrastructure;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.Tracking;
using markdown_journal_cli.Infrastructure.Transactions;
using markdown_journal_cli.Services;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;

namespace markdown_journal_cli.Tests.Services;

/// <summary>
/// Unit tests for <see cref="InitJournalService"/> covering the full initialisation contract.
/// </summary>
public class InitJournalServiceTests : ServiceTestBase
{
    private readonly Mock<IJournalConfigGenerator> _mockJournalConfigGenerator;
    private readonly InitJournalService _service;

    private const string JournalDirectory = "/test/journal";
    private const string JournalName = "TestJournal";
    private const string DefaultTocName = "1a-TableOfContents";

    public InitJournalServiceTests()
    {
        _mockJournalConfigGenerator = new Mock<IJournalConfigGenerator>();

        _service = new InitJournalService(
            MockFileSystem.Object,
            _mockJournalConfigGenerator.Object,
            MockFileTracking.Object,
            MockTableOfContentsService.Object,
            JournalSettings,
            NoOpCoordinator,
            NoOpReporter
        );
    }

    #region Guard clauses

    [Fact]
    public void Initialize_Should_ThrowArgumentException_When_DirectoryIsNull()
    {
        Should.Throw<ArgumentException>(() => _service.Initialize(null!, JournalName, null));
    }

    [Fact]
    public void Initialize_Should_ThrowArgumentException_When_DirectoryIsWhitespace()
    {
        Should.Throw<ArgumentException>(() => _service.Initialize("   ", JournalName, null));
    }

    [Fact]
    public void Initialize_Should_ThrowArgumentException_When_JournalNameIsNull()
    {
        Should.Throw<ArgumentException>(() => _service.Initialize(JournalDirectory, null!, null));
    }

    [Fact]
    public void Initialize_Should_ThrowArgumentException_When_JournalNameIsWhitespace()
    {
        Should.Throw<ArgumentException>(() => _service.Initialize(JournalDirectory, "  ", null));
    }

    #endregion

    #region Directory behaviour

    [Fact]
    public void Initialize_Should_CreateMetadataDirectory()
    {
        var metadataDir = Path.Combine(JournalDirectory, ".mdjournal");

        _service.Initialize(JournalDirectory, JournalName, null);

        MockFileSystem.Verify(f => f.CreateDirectory(metadataDir), Times.Once);
    }

    #endregion

    #region Metadata Directory Layout

    [Fact]
    public void Initialize_Should_LoadTrackingIndex_Into_JournalDirectory()
    {
        MockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);

        _service.Initialize(JournalDirectory, JournalName, null);

        // LoadIndex wires up the tracking index path (.mdjournal/.journalindex)
        MockFileTracking.Verify(ft => ft.LoadIndex(JournalDirectory), Times.Once);
    }

    [Fact]
    public void Initialize_Should_UpdateTrackingIndex_TwiceForJournalDirectory()
    {
        MockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);

        _service.Initialize(JournalDirectory, JournalName, null);

        // UpdateIndex is called twice: once after LoadIndex, once after TOC creation
        MockFileTracking.Verify(ft => ft.UpdateIndex(JournalDirectory), Times.Exactly(2));
    }

    [Fact]
    public void Initialize_Should_CallConfigGenerator_ToCreate_JournalrcAndTocStructure()
    {
        MockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);

        _service.Initialize(JournalDirectory, JournalName, null);

        // GenerateFromTrackingIndex creates both .journalrc and .mdjournal/.journaltoc
        _mockJournalConfigGenerator.Verify(
            g => g.GenerateFromTrackingIndex(JournalDirectory, It.IsAny<string>(), JournalName),
            Times.Once
        );
    }

    [Fact]
    public void Initialize_Should_CreateMetadataDirectoryBeforeOtherOperations()
    {
        // Verify that when metadata dir doesn't exist it is created exactly once
        var metadataDir = Path.Combine(JournalDirectory, ".mdjournal");
        MockFileSystem.Setup(f => f.DirectoryExists(metadataDir)).Returns(false);
        MockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);

        _service.Initialize(JournalDirectory, JournalName, null);

        MockFileSystem.Verify(f => f.CreateDirectory(metadataDir), Times.Once);
    }

    [Fact]
    public void Initialize_Should_NotCreateMetadataDirectory_When_ItAlreadyExists()
    {
        var metadataDir = Path.Combine(JournalDirectory, ".mdjournal");
        MockFileSystem.Setup(f => f.DirectoryExists(metadataDir)).Returns(true);
        MockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);

        _service.Initialize(JournalDirectory, JournalName, null);

        MockFileSystem.Verify(f => f.CreateDirectory(metadataDir), Times.Never);
    }

    #endregion

    #region Table of Contents

    [Fact]
    public void Initialize_Should_CreateTocFile_When_ItDoesNotExist()
    {
        MockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);

        _service.Initialize(JournalDirectory, JournalName, null);

        MockTableOfContentsService.Verify(
            t =>
                t.UpdateTableOfContents(
                    JournalDirectory,
                    It.IsAny<DateTime?>(),
                    It.IsAny<DateTime?>()
                ),
            Times.Once
        );
    }

    [Fact]
    public void Initialize_Should_ThrowTocFileAlreadyExistsException_When_TocFileAlreadyExists()
    {
        MockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);

        Should.Throw<TocFileAlreadyExistsException>(() =>
            _service.Initialize(JournalDirectory, JournalName, null)
        );
    }

    [Fact]
    public void Initialize_Should_UseTocNameFromParameter_When_TocNameProvided()
    {
        const string customToc = "my-custom-toc";
        MockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);

        _service.Initialize(JournalDirectory, JournalName, customToc);

        _mockJournalConfigGenerator.Verify(
            g => g.GenerateFromTrackingIndex(JournalDirectory, customToc, JournalName),
            Times.Once
        );
    }

    [Fact]
    public void Initialize_Should_UseDefaultTocName_When_TocNameIsNull()
    {
        MockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);

        _service.Initialize(JournalDirectory, JournalName, null);

        _mockJournalConfigGenerator.Verify(
            g => g.GenerateFromTrackingIndex(JournalDirectory, DefaultTocName, JournalName),
            Times.Once
        );
    }

    #endregion

    #region Journal config generation

    [Fact]
    public void Initialize_Should_UseCorrectJournalNameInConfigGeneration()
    {
        MockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);

        _service.Initialize(JournalDirectory, JournalName, null);

        _mockJournalConfigGenerator.Verify(
            g => g.GenerateFromTrackingIndex(JournalDirectory, It.IsAny<string>(), JournalName),
            Times.Once
        );
    }

    [Fact]
    public void Initialize_Should_UseResolvedTocFileNameInConfigGeneration()
    {
        const string customToc = "custom-toc";
        MockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);

        _service.Initialize(JournalDirectory, JournalName, customToc);

        _mockJournalConfigGenerator.Verify(
            g => g.GenerateFromTrackingIndex(JournalDirectory, customToc, JournalName),
            Times.Once
        );
    }

    #endregion

    #region File tracking index

    [Fact]
    public void Initialize_Should_CallFileTrackingLoadIndex()
    {
        MockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);

        _service.Initialize(JournalDirectory, JournalName, null);

        MockFileTracking.Verify(ft => ft.LoadIndex(JournalDirectory), Times.Once);
    }

    [Fact]
    public void Initialize_Should_CallFileTrackingUpdateIndex()
    {
        MockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);

        _service.Initialize(JournalDirectory, JournalName, null);

        MockFileTracking.Verify(ft => ft.UpdateIndex(JournalDirectory), Times.Exactly(2));
    }

    #endregion

    #region Template files not created

    [Fact]
    public void Initialize_Should_NotCreateIntroductionFile()
    {
        MockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);

        _service.Initialize(JournalDirectory, JournalName, null);

        MockFileSystem.Verify(
            f =>
                f.CreateMarkdownFile(
                    It.IsAny<string>(),
                    It.Is<string>(n => n.Contains("Intro")),
                    It.IsAny<string>()
                ),
            Times.Never
        );
    }

    [Fact]
    public void Initialize_Should_NotCreateJournalEntryTemplateFile()
    {
        MockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);

        _service.Initialize(JournalDirectory, JournalName, null);

        MockFileSystem.Verify(
            f =>
                f.CreateMarkdownFile(
                    It.IsAny<string>(),
                    It.Is<string>(n => n.Contains("Template")),
                    It.IsAny<string>()
                ),
            Times.Never
        );
    }

    [Fact]
    public void Initialize_Should_NotCreateAllMyJournalsFile()
    {
        MockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);

        _service.Initialize(JournalDirectory, JournalName, null);

        MockFileSystem.Verify(
            f =>
                f.CreateMarkdownFile(
                    It.IsAny<string>(),
                    It.Is<string>(n => n.Contains("Journals")),
                    It.IsAny<string>()
                ),
            Times.Never
        );
    }

    #endregion
}
