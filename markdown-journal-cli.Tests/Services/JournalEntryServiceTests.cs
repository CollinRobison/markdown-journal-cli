using markdown_journal_cli.Exceptions;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.JournalTemplates;
using markdown_journal_cli.Infrastructure.Tracking;
using markdown_journal_cli.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;

namespace markdown_journal_cli.Tests.Services;

/// <summary>
/// Unit tests for JournalEntryService covering all public AddEntry behavior.
/// </summary>
public class JournalEntryServiceTests
{
    private readonly Mock<IFileSystem> _mockFileSystem;
    private readonly Mock<IJournalConfiguration> _mockJournalConfiguration;
    private readonly Mock<IEntryFormatterService> _mockEntryFormatter;
    private readonly Mock<ITemplateManager> _mockTemplateManager;
    private readonly Mock<IFileTracking> _mockFileTracking;
    private readonly Mock<ITableOfContentsService> _mockTocService;
    private readonly IOptions<JournalSettings> _journalSettings;
    private readonly JournalEntryService _service;

    private const string JournalPath = "/test/journal";
    private const string JournalrcPath = "/test/journal/.journalrc";
    private const string TrackingPath = "/test/journal/.md-journal";

    public JournalEntryServiceTests()
    {
        _mockFileSystem = new Mock<IFileSystem>();
        _mockJournalConfiguration = new Mock<IJournalConfiguration>();
        _mockEntryFormatter = new Mock<IEntryFormatterService>();
        _mockTemplateManager = new Mock<ITemplateManager>();
        _mockFileTracking = new Mock<IFileTracking>();
        _mockTocService = new Mock<ITableOfContentsService>();

        _journalSettings = Options.Create(new JournalSettings
        {
            AppName = "md-journal",
            JournalConfigFileName = ".journalrc",
            TableOfContentsFileName = "1a-TableOfContents",
        });

        SetupDefaultMockBehaviors();

        _service = new JournalEntryService(
            _mockFileSystem.Object,
            _mockJournalConfiguration.Object,
            _journalSettings,
            _mockEntryFormatter.Object,
            _mockTemplateManager.Object,
            _mockFileTracking.Object,
            _mockTocService.Object,
            NullLogger<JournalEntryService>.Instance
        );
    }

    private void SetupDefaultMockBehaviors()
    {
        // Journal config and tracking index exist by default
        _mockFileSystem.Setup(fs => fs.FileExists(JournalrcPath)).Returns(true);
        _mockFileSystem.Setup(fs => fs.FileExists(TrackingPath)).Returns(true);

        // Entry file does not yet exist by default
        _mockFileSystem
            .Setup(fs => fs.FileExists(It.Is<string>(s => s.EndsWith(".md"))))
            .Returns(false);

        // Entry formatter: replace underscores with spaces for title, replace spaces with underscores for filename
        _mockEntryFormatter
            .Setup(ef => ef.RemoveSpaceSeparators(It.IsAny<string>()))
            .Returns((string input) => input.Replace("_", " ").Trim());

        _mockEntryFormatter
            .Setup(ef => ef.AddSpaceSeparators(It.IsAny<string>()))
            .Returns((string input) => input.Replace(" ", "_"));

        _mockEntryFormatter
            .Setup(ef => ef.AddHeadingSeparators(It.IsAny<string[]>()))
            .Returns((string[] parts) => string.Join("-", parts));

        _mockEntryFormatter
            .Setup(ef => ef.BuildHeadingArray(It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Array.Empty<string>());

        _mockTemplateManager
            .Setup(tm => tm.GenerateFromTemplate(It.IsAny<string>(), It.IsAny<Dictionary<string, object>?>()))
            .Returns("# Entry Content");
    }

    #region Positive Cases

    [Fact]
    public void AddEntry_WithEntryNameOnly_CreatesFileAndUpdatesAllIndexes()
    {
        // Act
        _service.AddEntry(JournalPath, ignoreFile: false, "MyEntry", heading: null, subheading: null, entryTitleUnformatted: null);

        // Assert
        _mockFileSystem.Verify(
            fs => fs.CreateMarkdownFile(JournalPath, It.IsAny<string>(), It.IsAny<string>()),
            Times.Once
        );
        _mockJournalConfiguration.Verify(
            jc => jc.AddEntry(JournalPath, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<int?>(), It.IsAny<bool>(), It.IsAny<bool>()),
            Times.Once
        );
        _mockFileTracking.Verify(ft => ft.UpdateFileInIndex(JournalPath, It.IsAny<string>()), Times.Once);
        _mockTocService.Verify(toc => toc.UpdateTableOfContents(JournalPath, It.IsAny<DateTime?>(), It.IsAny<DateTime?>()), Times.Once);
    }

    [Fact]
    public void AddEntry_WithHeading_PassesHeadingToConfig()
    {
        // Arrange
        _mockEntryFormatter
            .Setup(ef => ef.BuildHeadingArray("Tech", null))
            .Returns(new[] { "Tech" });

        // Act
        _service.AddEntry(JournalPath, ignoreFile: false, "MyEntry", heading: "Tech", subheading: null, entryTitleUnformatted: null);

        // Assert
        _mockJournalConfiguration.Verify(
            jc => jc.AddEntry(
                JournalPath,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<string[]>(arr => arr != null && arr.Length == 1 && arr[0] == "Tech"),
                It.IsAny<int?>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()
            ),
            Times.Once
        );
    }

    [Fact]
    public void AddEntry_WithSubheading_PassesSubheadingArrayToConfig()
    {
        // Arrange
        _mockEntryFormatter
            .Setup(ef => ef.BuildHeadingArray(null, "AI-ML"))
            .Returns(new[] { "AI", "ML" });

        // Act
        _service.AddEntry(JournalPath, ignoreFile: false, "MyEntry", heading: null, subheading: "AI-ML", entryTitleUnformatted: null);

        // Assert
        _mockJournalConfiguration.Verify(
            jc => jc.AddEntry(
                JournalPath,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<string[]>(arr => arr != null && arr.Length == 2 && arr[0] == "AI" && arr[1] == "ML"),
                It.IsAny<int?>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()
            ),
            Times.Once
        );
    }

    [Fact]
    public void AddEntry_WithHeadingAndSubheading_IncludesAllInTopicPath()
    {
        // Arrange
        _mockEntryFormatter
            .Setup(ef => ef.BuildHeadingArray("Tech", "AI-ML"))
            .Returns(new[] { "Tech", "AI", "ML" });

        // Act
        _service.AddEntry(JournalPath, ignoreFile: false, "MyEntry", heading: "Tech", subheading: "AI-ML", entryTitleUnformatted: null);

        // Assert
        _mockJournalConfiguration.Verify(
            jc => jc.AddEntry(
                JournalPath,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<string[]>(arr => arr != null && arr.Length == 3),
                It.IsAny<int?>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()
            ),
            Times.Once
        );
    }

    [Fact]
    public void AddEntry_WithCustomTitle_UsesCustomTitleInTemplate()
    {
        // Act
        _service.AddEntry(JournalPath, ignoreFile: false, "my_file", heading: null, subheading: null, entryTitleUnformatted: "My Custom Title");

        // Assert
        _mockTemplateManager.Verify(
            tm => tm.GenerateFromTemplate(
                "journal-entry",
                It.Is<Dictionary<string, object>>(d => d["title"].ToString() == "My Custom Title")
            ),
            Times.Once
        );
    }

    [Fact]
    public void AddEntry_WithNullTitle_FallsBackToEntryName()
    {
        // Act
        _service.AddEntry(JournalPath, ignoreFile: false, "MyEntry", heading: null, subheading: null, entryTitleUnformatted: null);

        // Assert: RemoveSpaceSeparators is called with entryName as fallback
        _mockEntryFormatter.Verify(
            ef => ef.RemoveSpaceSeparators("MyEntry"),
            Times.Once
        );
    }

    [Fact]
    public void AddEntry_WithEmptyHeadingArray_PassesNullTopicPathToConfig()
    {
        // Arrange: no heading/subheading → BuildHeadingArray returns empty
        _mockEntryFormatter
            .Setup(ef => ef.BuildHeadingArray(null, null))
            .Returns(Array.Empty<string>());

        // Act
        _service.AddEntry(JournalPath, ignoreFile: false, "MyEntry", heading: null, subheading: null, entryTitleUnformatted: null);

        // Assert: null topicPath passed when headings array is empty
        _mockJournalConfiguration.Verify(
            jc => jc.AddEntry(
                JournalPath,
                It.IsAny<string>(),
                It.IsAny<string>(),
                null,
                It.IsAny<int?>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()
            ),
            Times.Once
        );
    }

    #endregion

    #region IgnoreFile Behavior

    [Fact]
    public void AddEntry_WithIgnoreFileTrue_SkipsTableOfContentsUpdate()
    {
        // Act
        _service.AddEntry(JournalPath, ignoreFile: true, "MyEntry", heading: null, subheading: null, entryTitleUnformatted: null);

        // Assert
        _mockTocService.Verify(
            toc => toc.UpdateTableOfContents(It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()),
            Times.Never
        );
    }

    [Fact]
    public void AddEntry_WithIgnoreFileFalse_UpdatesTableOfContents()
    {
        // Act
        _service.AddEntry(JournalPath, ignoreFile: false, "MyEntry", heading: null, subheading: null, entryTitleUnformatted: null);

        // Assert
        _mockTocService.Verify(
            toc => toc.UpdateTableOfContents(JournalPath, null, It.IsAny<DateTime?>()),
            Times.Once
        );
    }

    [Fact]
    public void AddEntry_WithIgnoreFileTrue_StillUpdatesTrackingIndex()
    {
        // Act
        _service.AddEntry(JournalPath, ignoreFile: true, "MyEntry", heading: null, subheading: null, entryTitleUnformatted: null);

        // Assert: tracking is always updated regardless of ignoreFile
        _mockFileTracking.Verify(ft => ft.UpdateFileInIndex(JournalPath, It.IsAny<string>()), Times.Once);
    }

    #endregion

    #region Error Cases

    [Fact]
    public void AddEntry_WhenJournalrcMissing_ThrowsJournalrcNotFoundException()
    {
        // Arrange
        _mockFileSystem.Setup(fs => fs.FileExists(JournalrcPath)).Returns(false);

        // Act & Assert
        Should.Throw<JournalrcNotFoundException>(() =>
            _service.AddEntry(JournalPath, ignoreFile: false, "MyEntry", null, null, null)
        );
    }

    [Fact]
    public void AddEntry_WhenJournalrcMissing_DoesNotCreateFile()
    {
        // Arrange
        _mockFileSystem.Setup(fs => fs.FileExists(JournalrcPath)).Returns(false);

        // Act
        try { _service.AddEntry(JournalPath, ignoreFile: false, "MyEntry", null, null, null); } catch { }

        // Assert
        _mockFileSystem.Verify(
            fs => fs.CreateMarkdownFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never
        );
    }

    [Fact]
    public void AddEntry_WhenTrackingFileMissing_ThrowsTrackingIndexNotFoundException()
    {
        // Arrange
        _mockFileSystem.Setup(fs => fs.FileExists(TrackingPath)).Returns(false);

        // Act & Assert
        Should.Throw<TrackingIndexNotFoundException>(() =>
            _service.AddEntry(JournalPath, ignoreFile: false, "MyEntry", null, null, null)
        );
    }

    [Fact]
    public void AddEntry_WhenEntryAlreadyExists_ThrowsJournalEntryAlreadyExistsException()
    {
        // Arrange: entry file already exists
        _mockFileSystem
            .Setup(fs => fs.FileExists(It.Is<string>(s => s.EndsWith(".md"))))
            .Returns(true);

        // Act & Assert
        Should.Throw<JournalEntryAlreadyExistsException>(() =>
            _service.AddEntry(JournalPath, ignoreFile: false, "MyEntry", null, null, null)
        );
    }

    [Fact]
    public void AddEntry_WhenEntryAlreadyExists_DoesNotUpdateIndexes()
    {
        // Arrange
        _mockFileSystem
            .Setup(fs => fs.FileExists(It.Is<string>(s => s.EndsWith(".md"))))
            .Returns(true);

        // Act
        try { _service.AddEntry(JournalPath, ignoreFile: false, "MyEntry", null, null, null); } catch { }

        // Assert
        _mockFileTracking.Verify(ft => ft.UpdateFileInIndex(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _mockJournalConfiguration.Verify(
            jc => jc.AddEntry(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<int?>(), It.IsAny<bool>(), It.IsAny<bool>()),
            Times.Never
        );
    }

    #endregion

    #region File Naming

    [Fact]
    public void AddEntry_WithNoHeadingOrSubheading_FileNameContainsOnlyEntryName()
    {
        // Arrange: AddSpaceSeparators turns spaces into underscores
        _mockEntryFormatter.Setup(ef => ef.AddSpaceSeparators("MyEntry")).Returns("MyEntry");
        _mockEntryFormatter.Setup(ef => ef.AddHeadingSeparators(new[] { "MyEntry" })).Returns("MyEntry");

        string? capturedFileName = null;
        _mockFileSystem
            .Setup(fs => fs.CreateMarkdownFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((_, name, _) => capturedFileName = name);

        // Act
        _service.AddEntry(JournalPath, ignoreFile: false, "MyEntry", heading: null, subheading: null, entryTitleUnformatted: null);

        // Assert
        capturedFileName.ShouldNotBeNull();
        capturedFileName.ShouldContain("MyEntry");
        capturedFileName.ShouldEndWith(".md");
    }

    [Fact]
    public void AddEntry_WithHeading_FileNameContainsHeadingPrefix()
    {
        // Arrange
        _mockEntryFormatter.Setup(ef => ef.AddSpaceSeparators("Tech")).Returns("Tech");
        _mockEntryFormatter.Setup(ef => ef.AddSpaceSeparators("MyEntry")).Returns("MyEntry");
        _mockEntryFormatter
            .Setup(ef => ef.AddHeadingSeparators(It.Is<string[]>(a => a.Contains("Tech"))))
            .Returns("Tech-MyEntry");
        _mockEntryFormatter.Setup(ef => ef.BuildHeadingArray("Tech", null)).Returns(new[] { "Tech" });

        string? capturedFileName = null;
        _mockFileSystem
            .Setup(fs => fs.CreateMarkdownFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((_, name, _) => capturedFileName = name);

        // Act
        _service.AddEntry(JournalPath, ignoreFile: false, "MyEntry", heading: "Tech", subheading: null, entryTitleUnformatted: null);

        // Assert
        capturedFileName.ShouldNotBeNull();
        capturedFileName.ShouldContain("Tech");
        capturedFileName.ShouldContain("MyEntry");
    }

    #endregion
}
