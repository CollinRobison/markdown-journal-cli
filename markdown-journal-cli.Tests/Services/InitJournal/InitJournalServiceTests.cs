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
    public void Initialize_ThrowsArgumentException_WhenDirectoryIsNull()
    {
        Should.Throw<ArgumentException>(() => _service.Initialize(null!, JournalName, null));
    }

    [Fact]
    public void Initialize_ThrowsArgumentException_WhenDirectoryIsWhitespace()
    {
        Should.Throw<ArgumentException>(() => _service.Initialize("   ", JournalName, null));
    }

    [Fact]
    public void Initialize_ThrowsArgumentException_WhenJournalNameIsNull()
    {
        Should.Throw<ArgumentException>(() => _service.Initialize(JournalDirectory, null!, null));
    }

    [Fact]
    public void Initialize_ThrowsArgumentException_WhenJournalNameIsWhitespace()
    {
        Should.Throw<ArgumentException>(() => _service.Initialize(JournalDirectory, "  ", null));
    }

    #endregion

    #region Directory behaviour

    [Fact]
    public void Initialize_DoesNotCreateDirectory()
    {
        _service.Initialize(JournalDirectory, JournalName, null);

        MockFileSystem.Verify(f => f.CreateDirectory(It.IsAny<string>()), Times.Never);
    }

    #endregion

    #region Table of Contents

    [Fact]
    public void Initialize_CreatesTocFile_WhenItDoesNotExist()
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
    public void Initialize_ThrowsException_WhenTocFileAlreadyExists()
    {
        MockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);

        Should.Throw<TocFileAlreadyExistsException>(() =>
            _service.Initialize(JournalDirectory, JournalName, null)
        );
    }

    [Fact]
    public void Initialize_UsesTocNameFromParameter_WhenProvided()
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
    public void Initialize_UsesDefaultTocName_WhenParameterIsNull()
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
    public void Initialize_ConfigGenerationUsesCorrectJournalName()
    {
        MockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);

        _service.Initialize(JournalDirectory, JournalName, null);

        _mockJournalConfigGenerator.Verify(
            g => g.GenerateFromTrackingIndex(JournalDirectory, It.IsAny<string>(), JournalName),
            Times.Once
        );
    }

    [Fact]
    public void Initialize_ConfigGenerationUsesResolvedTocFileName()
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
    public void Initialize_CallsFileTrackingLoadIndex()
    {
        MockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);

        _service.Initialize(JournalDirectory, JournalName, null);

        MockFileTracking.Verify(ft => ft.LoadIndex(JournalDirectory), Times.Once);
    }

    [Fact]
    public void Initialize_CallsFileTrackingUpdateIndex()
    {
        MockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);

        _service.Initialize(JournalDirectory, JournalName, null);

        MockFileTracking.Verify(ft => ft.UpdateIndex(JournalDirectory), Times.Exactly(2));
    }

    #endregion

    #region Template files not created

    [Fact]
    public void Initialize_DoesNotCreateIntroductionFile()
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
    public void Initialize_DoesNotCreateJournalEntryTemplateFile()
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
    public void Initialize_DoesNotCreateAllMyJournalsFile()
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
