using markdown_journal_cli.Tests.Infrastructure;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.Configuration.Models;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;

namespace markdown_journal_cli.Tests.Services;

/// <summary>
/// Unit tests for TableOfContentsService covering TOC generation and update behavior.
/// </summary>
public class TableOfContentsServiceTests : ServiceTestBase
{
    private readonly TableOfContentsService _service;

    private const string JournalDirectory = "/test/journal";
    private const string TocFile = "1a-TableOfContents.md";
    private string TocFilePath => Path.Combine(JournalDirectory, TocFile);

    public TableOfContentsServiceTests()
    {
        // Default: TOC file does not exist
        MockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(false);

        // Default: config reads a minimal valid journal config
        MockJournalConfiguration.Setup(jc => jc.Read(JournalDirectory)).Returns(BuildConfig());

        _service = new TableOfContentsService(
            MockFileSystem.Object,
            MockJournalConfiguration.Object,
            JournalSettings,
            NullLogger<TableOfContentsService>()
        );
    }

    private static JournalConfig BuildConfig(
        Entries[]? rootEntries = null,
        Topic[]? topics = null,
        string[]? ignoreFiles = null
    )
    {
        return new JournalConfig
        {
            JournalName = "Test Journal",
            TableOfContents = new TableOfContents
            {
                File = TocFile,
                IgnoreFiles = ignoreFiles,
                Structure = new Structure { Topics = topics ?? [] },
                RootEntries = rootEntries ?? [],
            },
        };
    }

    #region Constructor Validation

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_FileSystemIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new TableOfContentsService(
                null!,
                MockJournalConfiguration.Object,
                JournalSettings,
                NullLogger<TableOfContentsService>()
            )
        );
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_JournalConfigurationIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new TableOfContentsService(
                MockFileSystem.Object,
                null!,
                JournalSettings,
                NullLogger<TableOfContentsService>()
            )
        );
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_LoggerIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new TableOfContentsService(
                MockFileSystem.Object,
                MockJournalConfiguration.Object,
                JournalSettings,
                null!
            )
        );
    }

    #endregion

    #region Input Validation

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateTableOfContents_Should_ThrowArgumentException_When_DirectoryIsInvalid(string? directory)
    {
        Should.Throw<ArgumentException>(() => _service.UpdateTableOfContents(directory!));
    }

    [Fact]
    public void UpdateTableOfContents_Should_ThrowInvalidOperationException_When_ConfigReadReturnsNull()
    {
        MockJournalConfiguration
            .Setup(jc => jc.Read(JournalDirectory))
            .Returns((JournalConfig?)null);

        Should.Throw<InvalidOperationException>(() =>
            _service.UpdateTableOfContents(JournalDirectory)
        );
    }

    #endregion

    #region File Operations

    [Fact]
    public void UpdateTableOfContents_Should_CallUpdateFileWithCorrectPaths()
    {
        MockFileSystem
            .Setup(fs => fs.CombinePaths(JournalDirectory, TocFile))
            .Returns(TocFilePath);

        _service.UpdateTableOfContents(JournalDirectory);

        MockFileSystem.Verify(
            fs => fs.UpdateFile(JournalDirectory, TocFile, It.IsAny<string>()),
            Times.Once
        );
    }

    [Fact]
    public void UpdateTableOfContents_Should_ReadExistingContent_When_TocFileExists()
    {
        MockFileSystem
            .Setup(fs => fs.CombinePaths(JournalDirectory, TocFile))
            .Returns(TocFilePath);
        MockFileSystem.Setup(fs => fs.FileExists(TocFilePath)).Returns(true);
        MockFileSystem
            .Setup(fs => fs.GetFileContent(TocFilePath))
            .Returns("# Table of Contents\n");

        _service.UpdateTableOfContents(JournalDirectory);

        MockFileSystem.Verify(fs => fs.GetFileContent(TocFilePath), Times.Once);
    }

    [Fact]
    public void UpdateTableOfContents_Should_NotReadContent_When_TocFileDoesNotExist()
    {
        MockFileSystem.Setup(fs => fs.FileExists(TocFilePath)).Returns(false);

        _service.UpdateTableOfContents(JournalDirectory);

        MockFileSystem.Verify(fs => fs.GetFileContent(It.IsAny<string>()), Times.Never);
    }

    #endregion

    #region Date Handling

    [Fact]
    public void UpdateTableOfContents_Should_NotOutputDateLines_When_NoDatesProvided()
    {
        string? captured = null;
        MockFileSystem
            .Setup(fs => fs.UpdateFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((_, _, content) => captured = content);

        _service.UpdateTableOfContents(JournalDirectory);

        captured.ShouldNotBeNull();
        captured.ShouldNotContain("Created:");
        captured.ShouldNotContain("Last Edited:");
    }

    [Fact]
    public void UpdateTableOfContents_Should_PreserveCreatedDate_When_ExistingTocHasCreatedDateAndNoNewDateGiven()
    {
        MockFileSystem
            .Setup(fs => fs.CombinePaths(JournalDirectory, TocFile))
            .Returns(TocFilePath);
        MockFileSystem.Setup(fs => fs.FileExists(TocFilePath)).Returns(true);
        MockFileSystem
            .Setup(fs => fs.GetFileContent(TocFilePath))
            .Returns("Created: 01/15/2025\n# Table of Contents\n");

        string? captured = null;
        MockFileSystem
            .Setup(fs => fs.UpdateFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((_, _, content) => captured = content);

        _service.UpdateTableOfContents(JournalDirectory);

        captured.ShouldNotBeNull();
        captured.ShouldContain("Created: 01/15/2025");
    }

    [Fact]
    public void UpdateTableOfContents_Should_OverrideCreatedDate_When_ExistingTocHasCreatedDateAndNewDateGiven()
    {
        MockFileSystem.Setup(fs => fs.FileExists(TocFilePath)).Returns(true);
        MockFileSystem
            .Setup(fs => fs.GetFileContent(TocFilePath))
            .Returns("Created: 01/15/2025\n# Table of Contents\n");

        string? captured = null;
        MockFileSystem
            .Setup(fs => fs.UpdateFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((_, _, content) => captured = content);

        _service.UpdateTableOfContents(JournalDirectory, createdDate: new DateTime(2026, 3, 1));

        captured.ShouldNotBeNull();
        captured.ShouldContain("Created: 03/01/2026");
        captured.ShouldNotContain("Created: 01/15/2025");
    }

    [Fact]
    public void UpdateTableOfContents_Should_OutputLastEditedDate_When_LastEditedDateProvided()
    {
        string? captured = null;
        MockFileSystem
            .Setup(fs => fs.UpdateFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((_, _, content) => captured = content);

        _service.UpdateTableOfContents(JournalDirectory, lastEditedDate: new DateTime(2026, 3, 11));

        captured.ShouldNotBeNull();
        captured.ShouldContain("Last Edited: 03/11/2026");
    }

    [Fact]
    public void UpdateTableOfContents_Should_OutputBothDateLines_When_BothDatesProvided()
    {
        string? captured = null;
        MockFileSystem
            .Setup(fs => fs.UpdateFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((_, _, content) => captured = content);

        _service.UpdateTableOfContents(
            JournalDirectory,
            createdDate: new DateTime(2025, 1, 15),
            lastEditedDate: new DateTime(2026, 3, 11)
        );

        captured.ShouldNotBeNull();
        captured.ShouldContain("Created: 01/15/2025");
        captured.ShouldContain("Last Edited: 03/11/2026");
    }

    [Fact]
    public void UpdateTableOfContents_Should_PlaceBlankLineBeforeTocTitle_When_DatesPresent()
    {
        string? captured = null;
        MockFileSystem
            .Setup(fs => fs.UpdateFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((_, _, content) => captured = content);

        _service.UpdateTableOfContents(JournalDirectory, createdDate: new DateTime(2025, 1, 15));

        captured.ShouldNotBeNull();
        // AppendLine for date + AppendLine() for blank + AppendLine for title produces "\n\n# Table..."
        captured.ShouldContain("\n\n# Table of Contents");
    }

    #endregion

    #region Content Structure

    [Fact]
    public void UpdateTableOfContents_Should_AlwaysIncludeTocTitle()
    {
        string? captured = null;
        MockFileSystem
            .Setup(fs => fs.UpdateFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((_, _, content) => captured = content);

        _service.UpdateTableOfContents(JournalDirectory);

        captured.ShouldNotBeNull();
        captured.ShouldContain("# Table of Contents");
    }

    [Fact]
    public void UpdateTableOfContents_Should_RenderRootEntriesAsListItems()
    {
        var entries = new[]
        {
            new Entries { Name = "Introduction", File = "1b-Intro.md" },
            new Entries { Name = "My Entry", File = "my-entry.md" },
        };
        MockJournalConfiguration
            .Setup(jc => jc.Read(JournalDirectory))
            .Returns(BuildConfig(rootEntries: entries));

        string? captured = null;
        MockFileSystem
            .Setup(fs => fs.UpdateFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((_, _, content) => captured = content);

        _service.UpdateTableOfContents(JournalDirectory);

        captured.ShouldNotBeNull();
        captured.ShouldContain("- [Introduction](1b-Intro.md)");
        captured.ShouldContain("- [My Entry](my-entry.md)");
    }

    [Fact]
    public void UpdateTableOfContents_Should_ExcludeTocFileFromRootEntries()
    {
        var entries = new[]
        {
            new Entries { Name = "Table of Contents", File = TocFile },
            new Entries { Name = "Introduction", File = "1b-Intro.md" },
        };
        MockJournalConfiguration
            .Setup(jc => jc.Read(JournalDirectory))
            .Returns(BuildConfig(rootEntries: entries));

        string? captured = null;
        MockFileSystem
            .Setup(fs => fs.UpdateFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((_, _, content) => captured = content);

        _service.UpdateTableOfContents(JournalDirectory);

        captured.ShouldNotBeNull();
        captured.ShouldNotContain($"[Table of Contents]({TocFile})");
        captured.ShouldContain("- [Introduction](1b-Intro.md)");
    }

    [Fact]
    public void UpdateTableOfContents_Should_ExcludeIgnoreFilesFromRootEntries()
    {
        var entries = new[]
        {
            new Entries { Name = "Ignored Entry", File = "ignored.md" },
            new Entries { Name = "Visible Entry", File = "visible.md" },
        };
        MockJournalConfiguration
            .Setup(jc => jc.Read(JournalDirectory))
            .Returns(BuildConfig(rootEntries: entries, ignoreFiles: ["ignored.md"]));

        string? captured = null;
        MockFileSystem
            .Setup(fs => fs.UpdateFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((_, _, content) => captured = content);

        _service.UpdateTableOfContents(JournalDirectory);

        captured.ShouldNotBeNull();
        captured.ShouldNotContain("[Ignored Entry](ignored.md)");
        captured.ShouldContain("- [Visible Entry](visible.md)");
    }

    [Fact]
    public void UpdateTableOfContents_Should_MatchIgnoreFilesCaseInsensitively()
    {
        var entries = new[]
        {
            new Entries { Name = "Ignored Entry", File = "Ignored.md" },
        };
        MockJournalConfiguration
            .Setup(jc => jc.Read(JournalDirectory))
            .Returns(BuildConfig(rootEntries: entries, ignoreFiles: ["ignored.md"]));

        string? captured = null;
        MockFileSystem
            .Setup(fs => fs.UpdateFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((_, _, content) => captured = content);

        _service.UpdateTableOfContents(JournalDirectory);

        captured.ShouldNotBeNull();
        captured.ShouldNotContain("[Ignored Entry](Ignored.md)");
    }

    #endregion

    #region Topic Generation

    [Fact]
    public void UpdateTableOfContents_Should_RenderHeadingAndEntries_When_TopicHasEntries()
    {
        var topic = new Topic
        {
            Name = "Technology",
            Entries = [new Entries { Name = "Dotnet Entry", File = "dotnet-entry.md" }],
        };
        MockJournalConfiguration
            .Setup(jc => jc.Read(JournalDirectory))
            .Returns(BuildConfig(topics: [topic]));

        string? captured = null;
        MockFileSystem
            .Setup(fs => fs.UpdateFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((_, _, content) => captured = content);

        _service.UpdateTableOfContents(JournalDirectory);

        captured.ShouldNotBeNull();
        captured.ShouldContain("## Technology");
        captured.ShouldContain("  - [Dotnet Entry](dotnet-entry.md)");
    }

    [Fact]
    public void UpdateTableOfContents_Should_NotCapitalizeTopicHeading_When_CapitalizeTopicHeadingsFalse()
    {
        var settings = Options.Create(
            new JournalSettings
            {
                TableOfContentsTitle = "Table of Contents",
                CapitalizeTopicHeadings = false,
            }
        );
        var service = new TableOfContentsService(
            MockFileSystem.Object,
            MockJournalConfiguration.Object,
            settings,
            NullLogger<TableOfContentsService>()
        );

        var topic = new Topic
        {
            Name = "my topic",
            Entries = [new Entries { Name = "Entry", File = "entry.md" }],
        };
        MockJournalConfiguration
            .Setup(jc => jc.Read(JournalDirectory))
            .Returns(BuildConfig(topics: [topic]));

        string? captured = null;
        MockFileSystem
            .Setup(fs => fs.UpdateFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((_, _, content) => captured = content);

        service.UpdateTableOfContents(JournalDirectory);

        captured.ShouldNotBeNull();
        captured.ShouldContain("## my topic");
    }

    [Fact]
    public void UpdateTableOfContents_Should_OmitTopicFromOutput_When_AllTopicEntriesIgnored()
    {
        var topic = new Topic
        {
            Name = "Technology",
            Entries = [new Entries { Name = "Ignored", File = "ignored.md" }],
        };
        MockJournalConfiguration
            .Setup(jc => jc.Read(JournalDirectory))
            .Returns(BuildConfig(topics: [topic], ignoreFiles: ["ignored.md"]));

        string? captured = null;
        MockFileSystem
            .Setup(fs => fs.UpdateFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((_, _, content) => captured = content);

        _service.UpdateTableOfContents(JournalDirectory);

        captured.ShouldNotBeNull();
        captured.ShouldNotContain("## Technology");
    }

    [Fact]
    public void UpdateTableOfContents_Should_RenderLinkedHeading_When_SingleEntryTopicNameMatchesTopicName()
    {
        var topic = new Topic
        {
            Name = "Technology",
            Entries = [new Entries { Name = "Technology", File = "technology.md" }],
        };
        MockJournalConfiguration
            .Setup(jc => jc.Read(JournalDirectory))
            .Returns(BuildConfig(topics: [topic]));

        string? captured = null;
        MockFileSystem
            .Setup(fs => fs.UpdateFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((_, _, content) => captured = content);

        _service.UpdateTableOfContents(JournalDirectory);

        captured.ShouldNotBeNull();
        captured.ShouldContain("## [Technology](technology.md)");
    }

    [Fact]
    public void UpdateTableOfContents_Should_RenderPlainHeadingAndEntry_When_SingleEntryTopicNameDoesNotMatchTopicName()
    {
        var topic = new Topic
        {
            Name = "Technology",
            Entries = [new Entries { Name = "Dotnet", File = "dotnet.md" }],
        };
        MockJournalConfiguration
            .Setup(jc => jc.Read(JournalDirectory))
            .Returns(BuildConfig(topics: [topic]));

        string? captured = null;
        MockFileSystem
            .Setup(fs => fs.UpdateFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((_, _, content) => captured = content);

        _service.UpdateTableOfContents(JournalDirectory);

        captured.ShouldNotBeNull();
        captured.ShouldContain("## Technology");
        captured.ShouldContain("  - [Dotnet](dotnet.md)");
    }

    [Fact]
    public void UpdateTableOfContents_Should_RenderPlainHeadingAndAllEntries_When_TopicHasMultipleEntries()
    {
        var topic = new Topic
        {
            Name = "Technology",
            Entries =
            [
                new Entries { Name = "Entry One", File = "entry-one.md" },
                new Entries { Name = "Entry Two", File = "entry-two.md" },
            ],
        };
        MockJournalConfiguration
            .Setup(jc => jc.Read(JournalDirectory))
            .Returns(BuildConfig(topics: [topic]));

        string? captured = null;
        MockFileSystem
            .Setup(fs => fs.UpdateFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((_, _, content) => captured = content);

        _service.UpdateTableOfContents(JournalDirectory);

        captured.ShouldNotBeNull();
        captured.ShouldContain("## Technology");
        captured.ShouldContain("  - [Entry One](entry-one.md)");
        captured.ShouldContain("  - [Entry Two](entry-two.md)");
    }

    #endregion

    #region Subtopic Generation

    [Fact]
    public void UpdateTableOfContents_Should_RenderSubtopicAsIndentedListItem()
    {
        var subtopic = new Topic
        {
            Name = "Machine Learning",
            Entries = [new Entries { Name = "ML Entry", File = "ml-entry.md" }],
        };
        var topic = new Topic
        {
            Name = "Technology",
            Entries = [],
            Subtopics = [subtopic],
        };
        MockJournalConfiguration
            .Setup(jc => jc.Read(JournalDirectory))
            .Returns(BuildConfig(topics: [topic]));

        string? captured = null;
        MockFileSystem
            .Setup(fs => fs.UpdateFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((_, _, content) => captured = content);

        _service.UpdateTableOfContents(JournalDirectory);

        captured.ShouldNotBeNull();
        captured.ShouldContain("  - Machine Learning");
    }

    [Fact]
    public void UpdateTableOfContents_Should_RenderSubtopicEntriesAtFourSpaceIndent()
    {
        var subtopic = new Topic
        {
            Name = "Machine Learning",
            Entries = [new Entries { Name = "ML Entry", File = "ml-entry.md" }],
        };
        var topic = new Topic
        {
            Name = "Technology",
            Entries = [],
            Subtopics = [subtopic],
        };
        MockJournalConfiguration
            .Setup(jc => jc.Read(JournalDirectory))
            .Returns(BuildConfig(topics: [topic]));

        string? captured = null;
        MockFileSystem
            .Setup(fs => fs.UpdateFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((_, _, content) => captured = content);

        _service.UpdateTableOfContents(JournalDirectory);

        captured.ShouldNotBeNull();
        captured.ShouldContain("    - [ML Entry](ml-entry.md)");
    }

    #endregion
}

// ---------------------------------------------------------------------------
// PreviewTableOfContents tests (separate class for clarity)
// ---------------------------------------------------------------------------

public class TableOfContentsServicePreviewTests
{
    private readonly IOptions<JournalSettings> _journalSettings;
    private readonly TableOfContentsService _service;

    private const string JournalDirectory = "/test/journal";
    private const string TocFile = "1a-TableOfContents.md";
    private string TocFilePath => Path.Combine(JournalDirectory, TocFile);

    private readonly Mock<IFileSystem> _mockFileSystem;
    private readonly Mock<IJournalConfiguration> _mockJournalConfiguration;

    public TableOfContentsServicePreviewTests()
    {
        _mockFileSystem = new Mock<IFileSystem>();
        _mockJournalConfiguration = new Mock<IJournalConfiguration>();

        _journalSettings = Options.Create(
            new JournalSettings
            {
                TableOfContentsFileName = "1a-TableOfContents",
                TableOfContentsTitle = "Table of Contents",
                CapitalizeTopicHeadings = true,
            }
        );

        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(false);
        _mockJournalConfiguration.Setup(jc => jc.Read(JournalDirectory)).Returns(BuildConfig());

        _service = new TableOfContentsService(
            _mockFileSystem.Object,
            _mockJournalConfiguration.Object,
            _journalSettings,
            NullLogger<TableOfContentsService>.Instance
        );
    }

    private static JournalConfig BuildConfig(
        Entries[]? rootEntries = null,
        Topic[]? topics = null
    ) =>
        new()
        {
            JournalName = "Test",
            TableOfContents = new TableOfContents
            {
                File = TocFile,
                Structure = new Structure { Topics = topics ?? [] },
                RootEntries = rootEntries ?? [],
            },
        };

    [Fact]
    public void PreviewTableOfContents_Should_ReturnGeneratedContentWithoutWritingToDisk()
    {
        _mockJournalConfiguration
            .Setup(jc => jc.Read(JournalDirectory))
            .Returns(
                BuildConfig(rootEntries: [new Entries { Name = "My Entry", File = "my-entry.md" }])
            );

        var result = _service.PreviewTableOfContents(JournalDirectory);

        result.ShouldContain("# Table of Contents");
        result.ShouldContain("[My Entry](my-entry.md)");
        _mockFileSystem.Verify(
            fs => fs.UpdateFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never,
            "PreviewTableOfContents must not write to disk"
        );
    }

    [Fact]
    public void PreviewTableOfContents_Should_PreserveExistingDatesFromTocFile()
    {
        var existingTocContent =
            "Created: 01/15/2024\nLast Edited: 03/01/2024\n\n# Table of Contents\n";
        _mockFileSystem
            .Setup(fs => fs.CombinePaths(JournalDirectory, TocFile))
            .Returns(TocFilePath);
        _mockFileSystem.Setup(fs => fs.FileExists(TocFilePath)).Returns(true);
        _mockFileSystem.Setup(fs => fs.GetFileContent(TocFilePath)).Returns(existingTocContent);

        var result = _service.PreviewTableOfContents(JournalDirectory);

        result.ShouldContain("Created: 01/15/2024");
        result.ShouldContain("Last Edited: 03/01/2024");
    }

    [Fact]
    public void PreviewTableOfContents_Should_ReturnValidContent_When_NoEntries()
    {
        var result = _service.PreviewTableOfContents(JournalDirectory);

        result.ShouldContain("# Table of Contents");
        result.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void PreviewTableOfContents_Should_MatchUpdateTableOfContentsOutput_When_StateIsIdentical()
    {
        var entries = new[]
        {
            new Entries { Name = "Note", File = "note.md" },
        };
        _mockJournalConfiguration
            .Setup(jc => jc.Read(JournalDirectory))
            .Returns(BuildConfig(rootEntries: entries));

        // Capture what UpdateTableOfContents would write
        string? writtenContent = null;
        _mockFileSystem
            .Setup(fs => fs.UpdateFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((_, _, c) => writtenContent = c);

        _service.UpdateTableOfContents(JournalDirectory);
        var previewContent = _service.PreviewTableOfContents(JournalDirectory);

        writtenContent.ShouldNotBeNull();
        previewContent.ShouldBe(
            writtenContent,
            "Preview must produce the same output as live update"
        );
    }

    [Fact]
    public void PreviewTableOfContents_Should_ThrowArgumentException_When_DirectoryIsNullOrWhitespace()
    {
        Should.Throw<ArgumentException>(() => _service.PreviewTableOfContents(string.Empty));
        Should.Throw<ArgumentException>(() => _service.PreviewTableOfContents("   "));
    }

    // ── todo 25: PreviewTableOfContents(string, JournalConfig) overload ──────

    [Fact]
    public void PreviewTableOfContents_Should_GenerateOutputWithoutReadingJournalrc_When_ProjectedConfigProvided()
    {
        // Arrange — projected config has different entries than what's on disk in .journalrc
        var projectedConfig = BuildConfig(
            rootEntries: [new Entries { Name = "Projected Entry", File = "projected-entry.md" }]
        );

        // Verify the mock returns NO config (so we know the overload doesn't call Read())
        _mockJournalConfiguration
            .Setup(jc => jc.Read(JournalDirectory))
            .Returns((JournalConfig?)null);

        // Act
        var content = _service.PreviewTableOfContents(JournalDirectory, projectedConfig);

        // Assert — output is driven entirely by projectedConfig
        content.ShouldContain("projected-entry.md");

        // The projected-config overload must NOT call IJournalConfiguration.Read()
        _mockJournalConfiguration.Verify(jc => jc.Read(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void PreviewTableOfContents_Should_PreserveExistingTocDates_When_ProjectedConfigProvided()
    {
        // Arrange — existing TOC file with known dates
        _mockFileSystem.Setup(fs => fs.FileExists(TocFilePath)).Returns(true);
        _mockFileSystem
            .Setup(fs => fs.GetFileContent(TocFilePath))
            .Returns("Created: 01/15/2024\nLast Edited: 06/01/2024\n\n# Table of Contents\n");
        _mockFileSystem
            .Setup(fs => fs.CombinePaths(JournalDirectory, TocFile))
            .Returns(TocFilePath);

        var projectedConfig = BuildConfig();

        // Act
        var content = _service.PreviewTableOfContents(JournalDirectory, projectedConfig);

        // Assert — existing dates are preserved
        content.ShouldContain("Created: 01/15/2024");
        content.ShouldContain("Last Edited: 06/01/2024");
    }

    [Fact]
    public void PreviewTableOfContents_Should_NotCallUpdateFile_When_ProjectedConfigProvided()
    {
        var projectedConfig = BuildConfig(
            rootEntries: [new Entries { Name = "Note", File = "note.md" }]
        );

        _service.PreviewTableOfContents(JournalDirectory, projectedConfig);

        _mockFileSystem.Verify(
            fs => fs.UpdateFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never
        );
    }

    [Fact]
    public void PreviewTableOfContents_Should_ThrowArgumentException_When_ProjectedConfigAndDirectoryIsNullOrWhitespace()
    {
        var config = BuildConfig();
        Should.Throw<ArgumentException>(() =>
            _service.PreviewTableOfContents(string.Empty, config)
        );
        Should.Throw<ArgumentException>(() => _service.PreviewTableOfContents("   ", config));
    }

    [Fact]
    public void PreviewTableOfContents_Should_ThrowArgumentNullException_When_ProjectedConfigIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            _service.PreviewTableOfContents(JournalDirectory, null!)
        );
    }
}
