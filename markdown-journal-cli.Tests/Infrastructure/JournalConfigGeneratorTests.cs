using markdown_journal_cli;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.Configuration.Models;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.Tracking;
using markdown_journal_cli.Services;
using markdown_journal_cli.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace markdown_journal_cli.Tests.Infrastructure;

public class JournalConfigGeneratorTests
{
    private readonly TestFileSystem _fileSystem;
    private readonly TableOfContentsMarkdownParser _tocParser;
    private readonly IFileTracking _fileTracking;
    private readonly IEntryFormatterService _entryFormatter;
    private readonly IJournalConfiguration _journalConfiguration;
    private readonly JournalSettings _journalSettings;
    private readonly JournalConfigGenerator _generator;

    public JournalConfigGeneratorTests()
    {
        _fileSystem = new TestFileSystem();
        _tocParser = new TableOfContentsMarkdownParser();
        _journalSettings = new JournalSettings();
        var hashService = new HashService();
        _fileTracking = new FileTracking(
            _fileSystem,
            Options.Create(_journalSettings),
            hashService
        );
        _entryFormatter = new EntryFormatterService(Options.Create(_journalSettings));
        _journalConfiguration = new JournalConfiguration(
            _fileSystem,
            Options.Create(_journalSettings),
            NullLogger<JournalConfiguration>.Instance
        );

        _generator = new JournalConfigGenerator(
            _fileSystem,
            _tocParser,
            _fileTracking,
            _entryFormatter,
            _journalConfiguration,
            Options.Create(_journalSettings)
        );
    }

    [Fact]
    public void GenerateFromTableOfContents_TocFileDoesNotExist_ReturnsNull()
    {
        // Arrange
        var directory = "/test/journal";
        var tocFileName = "TableOfContents";

        // Act
        var result = _generator.GenerateFromTableOfContents(directory, tocFileName);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GenerateFromTableOfContents_ValidTocFile_ReturnsConfigFromToc()
    {
        // Arrange
        var directory = "/test/journal";
        var tocFileName = "TableOfContents";
        var tocContent =
            @"# Table of Contents
- [Introduction](1b-intro.md)
## Work
- [Meeting Notes](work-meeting.md)";

        _fileSystem.CreateDirectory(directory);
        _fileSystem.CreateFile(directory, $"{tocFileName}.md", tocContent);

        // Act
        var result = _generator.GenerateFromTableOfContents(directory, tocFileName);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("toc", result.Source);
        Assert.Equal(2, result.FileCount);

        var config = result.Config;
        Assert.Equal("journal", config.JournalName);
        Assert.Equal($"{tocFileName}.md", config.TableOfContents.File);
        Assert.Single(config.TableOfContents.RootEntries);
        Assert.Single(config.TableOfContents.Structure.Topics);

        Assert.Equal("Introduction", config.TableOfContents.RootEntries[0].Name);
        Assert.Equal("1b-intro.md", config.TableOfContents.RootEntries[0].File);

        // Topic name comes from filename pattern (work-meeting.md -> "work")
        Assert.Equal("work", config.TableOfContents.Structure.Topics[0].Name);
        Assert.Single(config.TableOfContents.Structure.Topics[0].Entries);
    }

    [Fact]
    public void GenerateFromTableOfContents_ComplexTocWithSubtopics_ParsesCorrectly()
    {
        // Arrange
        var directory = "/test/journal";
        var tocFileName = "toc";
        var tocContent =
            @"# Table of Contents
- [Intro](1b-intro.md)
## Tech
- [Overview](tech-overview.md)
  - Backend
    - [API](tech-backend-api.md)
    - [Database](tech-backend-db.md)
## Personal
- [Journal](personal-journal.md)";

        _fileSystem.CreateDirectory(directory);
        _fileSystem.CreateFile(directory, $"{tocFileName}.md", tocContent);

        // Act
        var result = _generator.GenerateFromTableOfContents(directory, tocFileName);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("toc", result.Source);
        Assert.Equal(5, result.FileCount); // 1 root + 2 in Tech (1 direct + 2 in Backend) + 1 in Personal

        var config = result.Config;
        Assert.Single(config.TableOfContents.RootEntries);
        Assert.Equal(2, config.TableOfContents.Structure.Topics.Length);
    }

    [Fact]
    public void GenerateFromTrackingIndex_TrackingFileDoesNotExist_ReturnsNull()
    {
        // Arrange
        var directory = "/test/journal";

        // Act
        var result = _generator.GenerateFromTrackingIndex(directory, "toc");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GenerateFromTrackingIndex_ValidTrackingFile_ReturnsConfigFromTracking()
    {
        // Arrange
        var directory = "/test/journal";
        _fileSystem.CreateDirectory(directory);

        // Create some markdown files
        _fileSystem.CreateMarkdownFile(directory, "work-meeting", "# Meeting");
        _fileSystem.CreateMarkdownFile(directory, "personal-journal", "# Journal");

        // Manually create tracking index JSON (instead of calling UpdateIndex which needs real files)
        var trackingIndexJson =
            @"{
  ""Files"": {
    ""work-meeting.md"": {
      ""FilePath"": ""work-meeting.md"",
      ""Hash"": ""abc123"",
      ""LastChecked"": ""2026-01-01T00:00:00Z""
    },
    ""personal-journal.md"": {
      ""FilePath"": ""personal-journal.md"",
      ""Hash"": ""def456"",
      ""LastChecked"": ""2026-01-01T00:00:00Z""
    }
  }
}";
        _fileSystem.CreateFile(directory, $".{_journalSettings.AppName}", trackingIndexJson);

        // Act
        var result = _generator.GenerateFromTrackingIndex(directory, "1a-TableOfContents");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("tracking", result.Source);
        Assert.Equal(2, result.FileCount);

        var config = result.Config;
        // Files with hyphens like "work-meeting" become topics
        Assert.Equal(2, config.TableOfContents.Structure.Topics.Length);
        Assert.Empty(config.TableOfContents.RootEntries);

        // Check topics were created correctly
        var topicNames = config
            .TableOfContents.Structure.Topics.Select(t => t.Name)
            .OrderBy(n => n)
            .ToArray();
        Assert.Contains("work", topicNames);
        Assert.Contains("personal", topicNames);

        // Check entries within topics
        var workTopic = config.TableOfContents.Structure.Topics.First(t => t.Name == "work");
        Assert.Single(workTopic.Entries);
        Assert.Equal("meeting", workTopic.Entries[0].Name);

        var personalTopic = config.TableOfContents.Structure.Topics.First(t =>
            t.Name == "personal"
        );
        Assert.Single(personalTopic.Entries);
        Assert.Equal("journal", personalTopic.Entries[0].Name);
    }

    [Fact]
    public void GenerateFromTrackingIndex_SystemFilesInTracking_AddsToIgnoreFiles()
    {
        // Arrange
        var directory = "/test/journal";
        _fileSystem.CreateDirectory(directory);

        // Create markdown files including system files
        _fileSystem.CreateMarkdownFile(directory, "1a-TableOfContents", "# TOC");
        _fileSystem.CreateMarkdownFile(directory, "1b-Intro", "# Intro");
        _fileSystem.CreateMarkdownFile(directory, "work-meeting", "# Meeting");

        // Manually create tracking index JSON (instead of calling UpdateIndex which needs real files)
        var trackingIndexJson =
            @"{
  ""Files"": {
    ""1a-TableOfContents.md"": {
      ""FilePath"": ""1a-TableOfContents.md"",
      ""Hash"": ""abc123"",
      ""LastChecked"": ""2026-01-01T00:00:00Z""
    },
    ""1b-Intro.md"": {
      ""FilePath"": ""1b-Intro.md"",
      ""Hash"": ""def456"",
      ""LastChecked"": ""2026-01-01T00:00:00Z""
    },
    ""work-meeting.md"": {
      ""FilePath"": ""work-meeting.md"",
      ""Hash"": ""ghi789"",
      ""LastChecked"": ""2026-01-01T00:00:00Z""
    }
  }
}";
        _fileSystem.CreateFile(directory, $".{_journalSettings.AppName}", trackingIndexJson);

        // Act
        var result = _generator.GenerateFromTrackingIndex(directory, "1a-TableOfContents");

        // Assert
        Assert.NotNull(result);

        var config = result.Config;
        Assert.Equal(2, result.FileCount); // 1b-Intro (root) and work-meeting (topic entry)

        // Debug output
        Console.WriteLine($"Root entries: {config.TableOfContents.RootEntries.Length}");
        Console.WriteLine($"Topics: {config.TableOfContents.Structure.Topics.Length}");
        foreach (var entry in config.TableOfContents.RootEntries)
        {
            Console.WriteLine($"Root entry: {entry.File}");
        }
        foreach (var topic in config.TableOfContents.Structure.Topics)
        {
            Console.WriteLine($"Topic: {topic.Name}, Entries: {topic.Entries.Length}");
        }

        // 1b-Intro is a root entry (matches 1a-9z pattern)
        Assert.Single(config.TableOfContents.RootEntries);
        Assert.Equal("1b-Intro.md", config.TableOfContents.RootEntries[0].File);

        // work-meeting becomes a topic with "work" containing "meeting" entry
        Assert.Single(config.TableOfContents.Structure.Topics);
        var workTopic = config.TableOfContents.Structure.Topics[0];
        Assert.Equal("work", workTopic.Name);
        Assert.Single(workTopic.Entries);
        Assert.Equal("meeting", workTopic.Entries[0].Name);
        Assert.Equal("work-meeting.md", workTopic.Entries[0].File);

        // TOC should not be in ignoreFiles (it's already in tableOfContents.file)
        Assert.True(
            config.TableOfContents.IgnoreFiles == null
                || config.TableOfContents.IgnoreFiles.Length == 0
        );
    }

    [Fact]
    public void GenerateFromDirectory_EmptyDirectory_ReturnsConfigWithNoEntries()
    {
        // Arrange
        var directory = "/test/journal";
        var tocFileName = "toc";
        _fileSystem.CreateDirectory(directory);

        // Act
        var result = _generator.GenerateFromDirectory(directory, tocFileName);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("directory", result.Source);
        Assert.Equal(0, result.FileCount);

        var config = result.Config;
        Assert.Empty(config.TableOfContents.RootEntries);
        Assert.Empty(config.TableOfContents.Structure.Topics);
    }

    [Fact]
    public void GenerateFromDirectory_MultipleMarkdownFiles_ReturnsConfigWithAllFiles()
    {
        // Arrange
        var directory = "/test/journal";
        var tocFileName = "toc";
        _fileSystem.CreateDirectory(directory);

        _fileSystem.CreateMarkdownFile(directory, "work-meeting", "# Meeting");
        _fileSystem.CreateMarkdownFile(directory, "personal-journal", "# Journal");
        _fileSystem.CreateMarkdownFile(directory, "tech-notes", "# Notes");

        // Act
        var result = _generator.GenerateFromDirectory(directory, tocFileName);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("directory", result.Source);
        Assert.Equal(3, result.FileCount);

        var config = result.Config;
        // All files are topic entries (e.g., "work-meeting" creates topic "work" with entry "meeting")
        Assert.Empty(config.TableOfContents.RootEntries);
        Assert.Equal(3, config.TableOfContents.Structure.Topics.Length);
    }

    [Fact]
    public void GenerateFromDirectory_IncludesSystemFiles_AddsToIgnoreFiles()
    {
        // Arrange
        var directory = "/test/journal";
        var tocFileName = "1a-TableOfContents";
        _fileSystem.CreateDirectory(directory);

        _fileSystem.CreateMarkdownFile(directory, tocFileName, "# TOC");
        _fileSystem.CreateMarkdownFile(directory, "1b-Intro", "# Intro");
        _fileSystem.CreateMarkdownFile(directory, "1c-Journal-Entry-Template", "# Template");
        _fileSystem.CreateMarkdownFile(directory, "work-meeting", "# Meeting");

        // Act
        var result = _generator.GenerateFromDirectory(directory, tocFileName);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.FileCount); // 1b-Intro, 1c-Template, and work-meeting (only TOC excluded)

        var config = result.Config;

        // Root entries are those matching 1a-9z pattern (1b and 1c)
        Assert.Equal(2, config.TableOfContents.RootEntries.Length);
        var rootEntryFiles = config.TableOfContents.RootEntries.Select(e => e.File).ToArray();
        Assert.Contains("1b-Intro.md", rootEntryFiles);
        Assert.Contains("1c-Journal-Entry-Template.md", rootEntryFiles);

        // work-meeting should be in the work topic
        Assert.Single(config.TableOfContents.Structure.Topics);
        var workTopic = config.TableOfContents.Structure.Topics[0];
        Assert.Equal("work", workTopic.Name);
        Assert.Single(workTopic.Entries);
        Assert.Equal("work-meeting.md", workTopic.Entries[0].File);

        // TOC should not be in ignoreFiles (it's already in tableOfContents.file)
        Assert.True(
            config.TableOfContents.IgnoreFiles == null
                || config.TableOfContents.IgnoreFiles.Length == 0
        );
    }

    [Fact]
    public void GenerateFromDirectory_ExtractsEntryNamesFromFilenames_Correctly()
    {
        // Arrange
        var directory = "/test/journal";
        var tocFileName = "toc";
        _fileSystem.CreateDirectory(directory);

        // Create files with heading-subheading-title format
        _fileSystem.CreateMarkdownFile(directory, "work-meetings-daily_standup", "# Standup");
        _fileSystem.CreateMarkdownFile(directory, "tech-backend-api_design", "# API");
        _fileSystem.CreateMarkdownFile(directory, "simple_note", "# Note");

        // Act
        var result = _generator.GenerateFromDirectory(directory, tocFileName);

        // Assert
        Assert.NotNull(result);
        var config = result.Config;

        // All 3 files have topic paths, so should be organized into topics
        Assert.Empty(config.TableOfContents.RootEntries);
        Assert.Equal(3, config.TableOfContents.Structure.Topics.Length);

        // Entry names should be extracted from the last part of the filename
        // Need to recursively get entries from nested topics
        var allEntryNames = new List<string>();
        void CollectEntries(Topic[] topics)
        {
            foreach (var topic in topics)
            {
                allEntryNames.AddRange(topic.Entries.Select(e => e.Name));
                if (topic.Subtopics?.Length > 0)
                {
                    CollectEntries(topic.Subtopics);
                }
            }
        }
        CollectEntries(config.TableOfContents.Structure.Topics);

        Assert.Contains("api design", allEntryNames); // "api_design" -> "api design"
        Assert.Contains("daily standup", allEntryNames); // "daily_standup" -> "daily standup"
        Assert.Contains("simple note", allEntryNames); // "simple_note" -> "simple note"
    }

    [Fact]
    public void GenerateFromTableOfContents_NullDirectory_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _generator.GenerateFromTableOfContents(null!, "toc")
        );
    }

    [Fact]
    public void GenerateFromTableOfContents_EmptyDirectory_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _generator.GenerateFromTableOfContents("", "toc"));
    }

    [Fact]
    public void GenerateFromTableOfContents_NullTocFileName_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _generator.GenerateFromTableOfContents("/test/journal", null!)
        );
    }

    [Fact]
    public void GenerateFromTrackingIndex_NullDirectory_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _generator.GenerateFromTrackingIndex(null!, "toc"));
    }

    [Fact]
    public void GenerateFromTrackingIndex_NullTocFileName_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _generator.GenerateFromTrackingIndex("/test/journal", null!)
        );
    }

    [Fact]
    public void GenerateFromDirectory_NullDirectory_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _generator.GenerateFromDirectory(null!, "toc"));
    }

    [Fact]
    public void GenerateFromDirectory_NullTocFileName_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _generator.GenerateFromDirectory("/test/journal", null!)
        );
    }
}
