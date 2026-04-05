using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.Configuration.Models;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.Transactions;
using markdown_journal_cli.Infrastructure.JournalTemplates;
using markdown_journal_cli.Infrastructure.Tracking;
using markdown_journal_cli.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;

namespace markdown_journal_cli.Tests.Services;

/// <summary>
/// Unit tests for NewJournalService covering initialization behavior.
/// </summary>
public class NewJournalServiceTests
{
    private readonly Mock<IFileSystem> _mockFileSystem;
    private readonly Mock<ITemplateManager> _mockTemplateManager;
    private readonly Mock<IJournalConfiguration> _mockJournalConfiguration;
    private readonly Mock<IFileTracking> _mockFileTracking;
    private readonly IOptions<JournalSettings> _journalSettings;
    private readonly NewJournalService _service;

    private const string JournalDirectory = "/test/journal";
    private const string JournalName = "TestJournal";

    public NewJournalServiceTests()
    {
        _mockFileSystem = new Mock<IFileSystem>();
        _mockTemplateManager = new Mock<ITemplateManager>();
        _mockJournalConfiguration = new Mock<IJournalConfiguration>();
        _mockFileTracking = new Mock<IFileTracking>();

        _journalSettings = Options.Create(
            new JournalSettings
            {
                TableOfContentsFileName = "1a-TableOfContents",
                IntroductionFileName = "1b-Intro",
                IntroductionTitle = "Introduction",
                JournalEntryTemplateFileName = "1c-Journal-Entry-Template",
                JournalEntryTemplateTitle = "Journal Entry Template",
                AllJournalsFileName = "1h-All-My-Journals",
                AllJournalsTitle = "All My Journals",
            }
        );

        _mockTemplateManager
            .Setup(tm =>
                tm.GenerateFromTemplate(It.IsAny<string>(), It.IsAny<Dictionary<string, object>?>())
            )
            .Returns("generated content");

        _service = new NewJournalService(
            _mockFileSystem.Object,
            _mockTemplateManager.Object,
            _mockJournalConfiguration.Object,
            _mockFileTracking.Object,
            _journalSettings,
            NoOpFileTransactionCoordinator.Instance,
            NoOpRollbackReporter.Instance,
            NullLogger<NewJournalService>.Instance
        );
    }

    #region Constructor Validation

    [Fact]
    public void Constructor_NullFileSystem_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            new NewJournalService(
                null!,
                _mockTemplateManager.Object,
                _mockJournalConfiguration.Object,
                _mockFileTracking.Object,
                _journalSettings,
                NoOpFileTransactionCoordinator.Instance,
                NoOpRollbackReporter.Instance,
                NullLogger<NewJournalService>.Instance
            )
        );
    }

    [Fact]
    public void Constructor_NullTemplateManager_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            new NewJournalService(
                _mockFileSystem.Object,
                null!,
                _mockJournalConfiguration.Object,
                _mockFileTracking.Object,
                _journalSettings,
                NoOpFileTransactionCoordinator.Instance,
                NoOpRollbackReporter.Instance,
                NullLogger<NewJournalService>.Instance
            )
        );
    }

    [Fact]
    public void Constructor_NullJournalConfiguration_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            new NewJournalService(
                _mockFileSystem.Object,
                _mockTemplateManager.Object,
                null!,
                _mockFileTracking.Object,
                _journalSettings,
                NoOpFileTransactionCoordinator.Instance,
                NoOpRollbackReporter.Instance,
                NullLogger<NewJournalService>.Instance
            )
        );
    }

    [Fact]
    public void Constructor_NullFileTracking_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            new NewJournalService(
                _mockFileSystem.Object,
                _mockTemplateManager.Object,
                _mockJournalConfiguration.Object,
                null!,
                _journalSettings,
                NoOpFileTransactionCoordinator.Instance,
                NoOpRollbackReporter.Instance,
                NullLogger<NewJournalService>.Instance
            )
        );
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            new NewJournalService(
                _mockFileSystem.Object,
                _mockTemplateManager.Object,
                _mockJournalConfiguration.Object,
                _mockFileTracking.Object,
                _journalSettings,
                NoOpFileTransactionCoordinator.Instance,
                NoOpRollbackReporter.Instance,
                null!
            )
        );
    }

    [Fact]
    public void Constructor_NullTxCoordinator_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            new NewJournalService(
                _mockFileSystem.Object,
                _mockTemplateManager.Object,
                _mockJournalConfiguration.Object,
                _mockFileTracking.Object,
                _journalSettings,
                null!,
                NoOpRollbackReporter.Instance,
                NullLogger<NewJournalService>.Instance
            )
        );
    }

    [Fact]
    public void Constructor_NullRollbackReporter_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            new NewJournalService(
                _mockFileSystem.Object,
                _mockTemplateManager.Object,
                _mockJournalConfiguration.Object,
                _mockFileTracking.Object,
                _journalSettings,
                NoOpFileTransactionCoordinator.Instance,
                null!,
                NullLogger<NewJournalService>.Instance
            )
        );
    }

    #endregion

    #region Input Validation

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Initialize_InvalidJournalDirectory_ThrowsArgumentException(string? journalDirectory)
    {
        Should.Throw<ArgumentException>(() => _service.Initialize(journalDirectory!, JournalName));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Initialize_InvalidJournalName_ThrowsArgumentException(string? journalName)
    {
        Should.Throw<ArgumentException>(() => _service.Initialize(JournalDirectory, journalName!));
    }

    #endregion

    #region Directory Creation

    [Fact]
    public void Initialize_CreatesJournalDirectory()
    {
        _service.Initialize(JournalDirectory, JournalName);

        _mockFileSystem.Verify(fs => fs.CreateDirectory(JournalDirectory), Times.Once);
    }

    #endregion

    #region File Creation

    [Fact]
    public void Initialize_CreatesExactlyFourMarkdownFiles()
    {
        _service.Initialize(JournalDirectory, JournalName);

        _mockFileSystem.Verify(
            fs => fs.CreateMarkdownFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Exactly(4)
        );
    }

    [Fact]
    public void Initialize_CreatesTableOfContentsFile()
    {
        _service.Initialize(JournalDirectory, JournalName);

        _mockFileSystem.Verify(
            fs => fs.CreateMarkdownFile(JournalDirectory, "1a-TableOfContents", It.IsAny<string>()),
            Times.Once
        );
    }

    [Fact]
    public void Initialize_CreatesIntroductionFile()
    {
        _service.Initialize(JournalDirectory, JournalName);

        _mockFileSystem.Verify(
            fs => fs.CreateMarkdownFile(JournalDirectory, "1b-Intro", It.IsAny<string>()),
            Times.Once
        );
    }

    [Fact]
    public void Initialize_CreatesJournalEntryTemplateFile()
    {
        _service.Initialize(JournalDirectory, JournalName);

        _mockFileSystem.Verify(
            fs =>
                fs.CreateMarkdownFile(
                    JournalDirectory,
                    "1c-Journal-Entry-Template",
                    It.IsAny<string>()
                ),
            Times.Once
        );
    }

    [Fact]
    public void Initialize_CreatesAllMyJournalsFile()
    {
        _service.Initialize(JournalDirectory, JournalName);

        _mockFileSystem.Verify(
            fs => fs.CreateMarkdownFile(JournalDirectory, "1h-All-My-Journals", It.IsAny<string>()),
            Times.Once
        );
    }

    #endregion

    #region Template Usage

    [Fact]
    public void Initialize_GeneratesTableOfContentsTemplateWithNullParams()
    {
        _service.Initialize(JournalDirectory, JournalName);

        _mockTemplateManager.Verify(
            tm => tm.GenerateFromTemplate("table-of-contents", null),
            Times.Once
        );
    }

    [Fact]
    public void Initialize_GeneratesJournalEntryTemplateForIntroductionWithTitleParam()
    {
        _service.Initialize(JournalDirectory, JournalName);

        _mockTemplateManager.Verify(
            tm =>
                tm.GenerateFromTemplate(
                    "journal-entry",
                    It.Is<Dictionary<string, object>>(d =>
                        d != null
                        && d.ContainsKey("title")
                        && d["title"].ToString() == "Introduction"
                    )
                ),
            Times.Once
        );
    }

    [Fact]
    public void Initialize_GeneratesJournalEntryTemplateForIntroductionWithAddSourceBlockFalse()
    {
        _service.Initialize(JournalDirectory, JournalName);

        _mockTemplateManager.Verify(
            tm =>
                tm.GenerateFromTemplate(
                    "journal-entry",
                    It.Is<Dictionary<string, object>>(d =>
                        d != null
                        && d.ContainsKey("addSourceBlock")
                        && d["addSourceBlock"].Equals(false)
                        && d.ContainsKey("title")
                        && d["title"].ToString() == "Introduction"
                    )
                ),
            Times.Once
        );
    }

    [Fact]
    public void Initialize_GeneratesJournalEntryTemplateForAllMyJournalsWithTitleParam()
    {
        _service.Initialize(JournalDirectory, JournalName);

        _mockTemplateManager.Verify(
            tm =>
                tm.GenerateFromTemplate(
                    "journal-entry",
                    It.Is<Dictionary<string, object>>(d =>
                        d != null
                        && d.ContainsKey("title")
                        && d["title"].ToString() == "Journals List"
                    )
                ),
            Times.Once
        );
    }

    [Fact]
    public void Initialize_GeneratesJournalEntryTemplateForAllMyJournalsWithAddSourceBlockFalse()
    {
        _service.Initialize(JournalDirectory, JournalName);

        _mockTemplateManager.Verify(
            tm =>
                tm.GenerateFromTemplate(
                    "journal-entry",
                    It.Is<Dictionary<string, object>>(d =>
                        d != null
                        && d.ContainsKey("addSourceBlock")
                        && d["addSourceBlock"].Equals(false)
                        && d.ContainsKey("title")
                        && d["title"].ToString() == "Journals List"
                    )
                ),
            Times.Once
        );
    }

    [Fact]
    public void Initialize_GeneratesJournalEntryTemplateWithNullParamsForEntryTemplateFile()
    {
        _service.Initialize(JournalDirectory, JournalName);

        // The JournalEntryTemplate file uses null params (no title/body customization)
        _mockTemplateManager.Verify(
            tm => tm.GenerateFromTemplate("journal-entry", null),
            Times.Once
        );
    }

    #endregion

    #region Configuration Creation

    [Fact]
    public void Initialize_CallsJournalConfigurationCreateOnce()
    {
        _service.Initialize(JournalDirectory, JournalName);

        _mockJournalConfiguration.Verify(
            jc => jc.Create(JournalDirectory, It.IsAny<JournalConfig>()),
            Times.Once
        );
    }

    [Fact]
    public void Initialize_ConfigurationHasCorrectJournalName()
    {
        JournalConfig? capturedConfig = null;
        _mockJournalConfiguration
            .Setup(jc => jc.Create(It.IsAny<string>(), It.IsAny<JournalConfig>()))
            .Callback<string, JournalConfig>((_, config) => capturedConfig = config);

        _service.Initialize(JournalDirectory, JournalName);

        capturedConfig.ShouldNotBeNull();
        capturedConfig.JournalName.ShouldBe(JournalName);
    }

    [Fact]
    public void Initialize_ConfigurationHasExactlyThreeRootEntries()
    {
        JournalConfig? capturedConfig = null;
        _mockJournalConfiguration
            .Setup(jc => jc.Create(It.IsAny<string>(), It.IsAny<JournalConfig>()))
            .Callback<string, JournalConfig>((_, config) => capturedConfig = config);

        _service.Initialize(JournalDirectory, JournalName);

        capturedConfig.ShouldNotBeNull();
        capturedConfig.TableOfContents.RootEntries.Length.ShouldBe(3);
    }

    [Fact]
    public void Initialize_AllRootEntriesHaveMdExtension()
    {
        JournalConfig? capturedConfig = null;
        _mockJournalConfiguration
            .Setup(jc => jc.Create(It.IsAny<string>(), It.IsAny<JournalConfig>()))
            .Callback<string, JournalConfig>((_, config) => capturedConfig = config);

        _service.Initialize(JournalDirectory, JournalName);

        capturedConfig.ShouldNotBeNull();
        foreach (var entry in capturedConfig.TableOfContents.RootEntries)
        {
            entry.File.ShouldEndWith(".md");
        }
    }

    #endregion

    #region File Tracking

    [Fact]
    public void Initialize_CallsFileTrackingLoadIndexWithJournalDirectory()
    {
        _service.Initialize(JournalDirectory, JournalName);

        _mockFileTracking.Verify(ft => ft.LoadIndex(JournalDirectory), Times.Once);
    }

    [Fact]
    public void Initialize_CallsFileTrackingUpdateIndexWithJournalDirectory()
    {
        _service.Initialize(JournalDirectory, JournalName);

        _mockFileTracking.Verify(ft => ft.UpdateIndex(JournalDirectory), Times.Once);
    }

    #endregion
}
