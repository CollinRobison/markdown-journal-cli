using markdown_journal_cli.Tests.Infrastructure;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.Configuration.Models;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.JournalTemplates;
using markdown_journal_cli.Infrastructure.Tracking;
using markdown_journal_cli.Infrastructure.Transactions;
using markdown_journal_cli.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;

namespace markdown_journal_cli.Tests.Services;

/// <summary>
/// Unit tests for NewJournalService covering initialization behavior.
/// </summary>
public class NewJournalServiceTests : ServiceTestBase
{
    private readonly IOptions<JournalSettings> _journalSettings;
    private readonly NewJournalService _service;

    private const string JournalDirectory = "/test/journal";
    private const string JournalName = "TestJournal";

    public NewJournalServiceTests()
    {
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

        MockTemplateManager
            .Setup(tm =>
                tm.GenerateFromTemplate(It.IsAny<string>(), It.IsAny<Dictionary<string, object>?>())
            )
            .Returns("generated content");

        _service = new NewJournalService(
            MockFileSystem.Object,
            MockTemplateManager.Object,
            MockJournalConfiguration.Object,
            MockFileTracking.Object,
            _journalSettings,
            NoOpCoordinator,
            NoOpReporter,
            NullLogger<NewJournalService>()
        );
    }

    #region Constructor Validation

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_FileSystemIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new NewJournalService(
                null!,
                MockTemplateManager.Object,
                MockJournalConfiguration.Object,
                MockFileTracking.Object,
                _journalSettings,
                NoOpCoordinator,
                NoOpReporter,
                NullLogger<NewJournalService>()
            )
        );
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_TemplateManagerIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new NewJournalService(
                MockFileSystem.Object,
                null!,
                MockJournalConfiguration.Object,
                MockFileTracking.Object,
                _journalSettings,
                NoOpCoordinator,
                NoOpReporter,
                NullLogger<NewJournalService>()
            )
        );
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_JournalConfigurationIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new NewJournalService(
                MockFileSystem.Object,
                MockTemplateManager.Object,
                null!,
                MockFileTracking.Object,
                _journalSettings,
                NoOpCoordinator,
                NoOpReporter,
                NullLogger<NewJournalService>()
            )
        );
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_FileTrackingIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new NewJournalService(
                MockFileSystem.Object,
                MockTemplateManager.Object,
                MockJournalConfiguration.Object,
                null!,
                _journalSettings,
                NoOpCoordinator,
                NoOpReporter,
                NullLogger<NewJournalService>()
            )
        );
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_LoggerIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new NewJournalService(
                MockFileSystem.Object,
                MockTemplateManager.Object,
                MockJournalConfiguration.Object,
                MockFileTracking.Object,
                _journalSettings,
                NoOpCoordinator,
                NoOpReporter,
                null!
            )
        );
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_TxCoordinatorIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new NewJournalService(
                MockFileSystem.Object,
                MockTemplateManager.Object,
                MockJournalConfiguration.Object,
                MockFileTracking.Object,
                _journalSettings,
                null!,
                NoOpReporter,
                NullLogger<NewJournalService>()
            )
        );
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_RollbackReporterIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new NewJournalService(
                MockFileSystem.Object,
                MockTemplateManager.Object,
                MockJournalConfiguration.Object,
                MockFileTracking.Object,
                _journalSettings,
                NoOpCoordinator,
                null!,
                NullLogger<NewJournalService>()
            )
        );
    }

    #endregion

    #region Input Validation

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Initialize_Should_ThrowArgumentException_When_JournalDirectoryIsInvalid(string? journalDirectory)
    {
        Should.Throw<ArgumentException>(() => _service.Initialize(journalDirectory!, JournalName));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Initialize_Should_ThrowArgumentException_When_JournalNameIsInvalid(string? journalName)
    {
        Should.Throw<ArgumentException>(() => _service.Initialize(JournalDirectory, journalName!));
    }

    #endregion

    #region Directory Creation

    [Fact]
    public void Initialize_Should_CreateJournalDirectory()
    {
        _service.Initialize(JournalDirectory, JournalName);

        MockFileSystem.Verify(fs => fs.CreateDirectory(JournalDirectory), Times.Once);
    }

    #endregion

    #region File Creation

    [Fact]
    public void Initialize_Should_CreateExactlyFourMarkdownFiles()
    {
        _service.Initialize(JournalDirectory, JournalName);

        MockFileSystem.Verify(
            fs => fs.CreateMarkdownFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Exactly(4)
        );
    }

    [Fact]
    public void Initialize_Should_CreateTableOfContentsFile()
    {
        _service.Initialize(JournalDirectory, JournalName);

        MockFileSystem.Verify(
            fs => fs.CreateMarkdownFile(JournalDirectory, "1a-TableOfContents", It.IsAny<string>()),
            Times.Once
        );
    }

    [Fact]
    public void Initialize_Should_CreateIntroductionFile()
    {
        _service.Initialize(JournalDirectory, JournalName);

        MockFileSystem.Verify(
            fs => fs.CreateMarkdownFile(JournalDirectory, "1b-Intro", It.IsAny<string>()),
            Times.Once
        );
    }

    [Fact]
    public void Initialize_Should_CreateJournalEntryTemplateFile()
    {
        _service.Initialize(JournalDirectory, JournalName);

        MockFileSystem.Verify(
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
    public void Initialize_Should_CreateAllMyJournalsFile()
    {
        _service.Initialize(JournalDirectory, JournalName);

        MockFileSystem.Verify(
            fs => fs.CreateMarkdownFile(JournalDirectory, "1h-All-My-Journals", It.IsAny<string>()),
            Times.Once
        );
    }

    #endregion

    #region Template Usage

    [Fact]
    public void Initialize_Should_GenerateTableOfContentsTemplateWithNullParams()
    {
        _service.Initialize(JournalDirectory, JournalName);

        MockTemplateManager.Verify(
            tm => tm.GenerateFromTemplate("table-of-contents", null),
            Times.Once
        );
    }

    [Fact]
    public void Initialize_Should_GenerateJournalEntryTemplateForIntroduction_When_TitleParamProvided()
    {
        _service.Initialize(JournalDirectory, JournalName);

        MockTemplateManager.Verify(
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
    public void Initialize_Should_GenerateJournalEntryTemplateForIntroduction_When_AddSourceBlockIsFalse()
    {
        _service.Initialize(JournalDirectory, JournalName);

        MockTemplateManager.Verify(
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
    public void Initialize_Should_GenerateJournalEntryTemplateForAllMyJournals_When_TitleParamProvided()
    {
        _service.Initialize(JournalDirectory, JournalName);

        MockTemplateManager.Verify(
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
    public void Initialize_Should_GenerateJournalEntryTemplateForAllMyJournals_When_AddSourceBlockIsFalse()
    {
        _service.Initialize(JournalDirectory, JournalName);

        MockTemplateManager.Verify(
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
    public void Initialize_Should_GenerateJournalEntryTemplateWithNullParams_When_EntryTemplateFileIsTarget()
    {
        _service.Initialize(JournalDirectory, JournalName);

        // The JournalEntryTemplate file uses null params (no title/body customization)
        MockTemplateManager.Verify(
            tm => tm.GenerateFromTemplate("journal-entry", null),
            Times.Once
        );
    }

    #endregion

    #region Configuration Creation

    [Fact]
    public void Initialize_Should_CallJournalConfigurationCreateOnce()
    {
        _service.Initialize(JournalDirectory, JournalName);

        MockJournalConfiguration.Verify(
            jc => jc.Create(JournalDirectory, It.IsAny<JournalConfig>()),
            Times.Once
        );
    }

    [Fact]
    public void Initialize_Should_SetCorrectJournalNameInConfiguration()
    {
        JournalConfig? capturedConfig = null;
        MockJournalConfiguration
            .Setup(jc => jc.Create(It.IsAny<string>(), It.IsAny<JournalConfig>()))
            .Callback<string, JournalConfig>((_, config) => capturedConfig = config);

        _service.Initialize(JournalDirectory, JournalName);

        capturedConfig.ShouldNotBeNull();
        capturedConfig.JournalName.ShouldBe(JournalName);
    }

    [Fact]
    public void Initialize_Should_SetExactlyThreeRootEntriesInConfiguration()
    {
        JournalConfig? capturedConfig = null;
        MockJournalConfiguration
            .Setup(jc => jc.Create(It.IsAny<string>(), It.IsAny<JournalConfig>()))
            .Callback<string, JournalConfig>((_, config) => capturedConfig = config);

        _service.Initialize(JournalDirectory, JournalName);

        capturedConfig.ShouldNotBeNull();
        capturedConfig.TableOfContents.RootEntries.Length.ShouldBe(3);
    }

    [Fact]
    public void Initialize_Should_EnsureAllRootEntriesHaveMdExtension()
    {
        JournalConfig? capturedConfig = null;
        MockJournalConfiguration
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
    public void Initialize_Should_CallFileTrackingLoadIndexWithJournalDirectory()
    {
        _service.Initialize(JournalDirectory, JournalName);

        MockFileTracking.Verify(ft => ft.LoadIndex(JournalDirectory), Times.Once);
    }

    [Fact]
    public void Initialize_Should_CallFileTrackingUpdateIndexWithJournalDirectory()
    {
        _service.Initialize(JournalDirectory, JournalName);

        MockFileTracking.Verify(ft => ft.UpdateIndex(JournalDirectory), Times.Once);
    }

    #endregion
}
