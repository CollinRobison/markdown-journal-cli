using markdown_journal_cli.Exceptions;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.Configuration.Models;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.Tracking;
using markdown_journal_cli.Services;
using markdown_journal_cli.Tests.Infrastructure.FileSystem;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;

namespace markdown_journal_cli.Tests.Services;

/// <summary>
/// Unit tests for <see cref="InitJournalService"/> covering the full initialisation contract.
/// </summary>
public class InitJournalServiceTests
{
    private readonly Mock<IFileSystem> _mockFileSystem;
    private readonly Mock<IJournalConfiguration> _mockJournalConfiguration;
    private readonly Mock<IFileTracking> _mockFileTracking;
    private readonly Mock<ITableOfContentsService> _mockTableOfContentsService;
    private readonly IOptions<JournalSettings> _journalSettings;
    private readonly InitJournalService _service;

    private const string JournalDirectory = "/test/journal";
    private const string JournalName = "TestJournal";
    private const string DefaultTocName = "1a-TableOfContents";

    public InitJournalServiceTests()
    {
        _mockFileSystem = new Mock<IFileSystem>();
        _mockJournalConfiguration = new Mock<IJournalConfiguration>();
        _mockFileTracking = new Mock<IFileTracking>();
        _mockTableOfContentsService = new Mock<ITableOfContentsService>();

        _journalSettings = Options.Create(
            new JournalSettings
            {
                TableOfContentsFileName = DefaultTocName,
                JournalConfigFileName = ".journalrc",
            }
        );

        _mockFileSystem
            .Setup(f => f.CombinePaths(It.IsAny<string[]>()))
            .Returns((string[] parts) => Path.Combine(parts));

        _service = new InitJournalService(
            _mockFileSystem.Object,
            _mockJournalConfiguration.Object,
            _mockFileTracking.Object,
            _mockTableOfContentsService.Object,
            _journalSettings
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

        _mockFileSystem.Verify(f => f.CreateDirectory(It.IsAny<string>()), Times.Never);
    }

    #endregion

    #region Table of Contents

    [Fact]
    public void Initialize_CreatesTocFile_WhenItDoesNotExist()
    {
        _mockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);

        _service.Initialize(JournalDirectory, JournalName, null);

        _mockTableOfContentsService.Verify(
            t => t.UpdateTableOfContents(JournalDirectory, It.IsAny<DateTime?>(), It.IsAny<DateTime?>()),
            Times.Once
        );
    }

    [Fact]
    public void Initialize_ThrowsException_WhenTocFileAlreadyExists()
    {
        _mockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);

        Should.Throw<TocFileAlreadyExistsException>(() =>
            _service.Initialize(JournalDirectory, JournalName, null)
        );
    }

    [Fact]
    public void Initialize_UsesTocNameFromParameter_WhenProvided()
    {
        const string customToc = "my-custom-toc";
        JournalConfig? capturedConfig = null;
        _mockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);
        _mockJournalConfiguration
            .Setup(c => c.Create(It.IsAny<string>(), It.IsAny<JournalConfig>()))
            .Callback<string, JournalConfig>((_, cfg) => capturedConfig = cfg);

        _service.Initialize(JournalDirectory, JournalName, customToc);

        capturedConfig.ShouldNotBeNull();
        capturedConfig!.TableOfContents.File.ShouldBe($"{customToc}.md");
    }

    [Fact]
    public void Initialize_UsesDefaultTocName_WhenParameterIsNull()
    {
        JournalConfig? capturedConfig = null;
        _mockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);
        _mockJournalConfiguration
            .Setup(c => c.Create(It.IsAny<string>(), It.IsAny<JournalConfig>()))
            .Callback<string, JournalConfig>((_, cfg) => capturedConfig = cfg);

        _service.Initialize(JournalDirectory, JournalName, null);

        capturedConfig.ShouldNotBeNull();
        capturedConfig!.TableOfContents.File.ShouldBe($"{DefaultTocName}.md");
    }

    #endregion

    #region Journal configuration

    [Fact]
    public void Initialize_CallsJournalConfigurationCreate()
    {
        _mockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);

        _service.Initialize(JournalDirectory, JournalName, null);

        _mockJournalConfiguration.Verify(
            c => c.Create(JournalDirectory, It.IsAny<JournalConfig>()),
            Times.Once
        );
    }

    [Fact]
    public void Initialize_ConfigurationHasCorrectJournalName()
    {
        JournalConfig? capturedConfig = null;
        _mockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);
        _mockJournalConfiguration
            .Setup(c => c.Create(It.IsAny<string>(), It.IsAny<JournalConfig>()))
            .Callback<string, JournalConfig>((_, cfg) => capturedConfig = cfg);

        _service.Initialize(JournalDirectory, JournalName, null);

        capturedConfig.ShouldNotBeNull();
        capturedConfig!.JournalName.ShouldBe(JournalName);
    }

    [Fact]
    public void Initialize_ConfigurationTocFileReferenceMatchesResolvedTocName()
    {
        const string customToc = "custom-toc";
        JournalConfig? capturedConfig = null;
        _mockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);
        _mockJournalConfiguration
            .Setup(c => c.Create(It.IsAny<string>(), It.IsAny<JournalConfig>()))
            .Callback<string, JournalConfig>((_, cfg) => capturedConfig = cfg);

        _service.Initialize(JournalDirectory, JournalName, customToc);

        capturedConfig.ShouldNotBeNull();
        capturedConfig!.TableOfContents.File.ShouldBe($"{customToc}.md");
    }

    #endregion

    #region File tracking index

    [Fact]
    public void Initialize_CallsFileTrackingLoadIndex()
    {
        _mockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);

        _service.Initialize(JournalDirectory, JournalName, null);

        _mockFileTracking.Verify(ft => ft.LoadIndex(JournalDirectory), Times.Once);
    }

    [Fact]
    public void Initialize_CallsFileTrackingUpdateIndex()
    {
        _mockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);

        _service.Initialize(JournalDirectory, JournalName, null);

        _mockFileTracking.Verify(ft => ft.UpdateIndex(JournalDirectory), Times.Once);
    }

    #endregion

    #region Template files not created

    [Fact]
    public void Initialize_DoesNotCreateIntroductionFile()
    {
        _mockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);

        _service.Initialize(JournalDirectory, JournalName, null);

        _mockFileSystem.Verify(
            f => f.CreateMarkdownFile(It.IsAny<string>(), It.Is<string>(n => n.Contains("Intro")), It.IsAny<string>()),
            Times.Never
        );
    }

    [Fact]
    public void Initialize_DoesNotCreateJournalEntryTemplateFile()
    {
        _mockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);

        _service.Initialize(JournalDirectory, JournalName, null);

        _mockFileSystem.Verify(
            f => f.CreateMarkdownFile(It.IsAny<string>(), It.Is<string>(n => n.Contains("Template")), It.IsAny<string>()),
            Times.Never
        );
    }

    [Fact]
    public void Initialize_DoesNotCreateAllMyJournalsFile()
    {
        _mockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);

        _service.Initialize(JournalDirectory, JournalName, null);

        _mockFileSystem.Verify(
            f => f.CreateMarkdownFile(It.IsAny<string>(), It.Is<string>(n => n.Contains("Journals")), It.IsAny<string>()),
            Times.Never
        );
    }

    #endregion
}
