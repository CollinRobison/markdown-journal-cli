using markdown_journal_cli;
using markdown_journal_cli.Commands.Add;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.Tracking;
using markdown_journal_cli.Services;
using markdown_journal_cli.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;

namespace markdown_journal_cli.Tests.Commands.Add;

public class AddJournalrcCommandTests
{
    private readonly TestFileSystem _fileSystem;
    private readonly TestConsole _console;
    private readonly JournalConfiguration _journalConfiguration;
    private readonly JournalConfigGenerator _configGenerator;
    private readonly JournalSettings _journalSettings;
    private readonly AddJournalrc _command;

    public AddJournalrcCommandTests()
    {
        _fileSystem = new TestFileSystem();
        _console = new TestConsole();
        _journalSettings = new JournalSettings();
        _journalConfiguration = new JournalConfiguration(
            _fileSystem,
            Options.Create(_journalSettings),
            NullLogger<JournalConfiguration>.Instance
        );
        
        var tocParser = new TableOfContentsMarkdownParser();
        var hashService = new HashService();
        var fileTracking = new FileTracking(_fileSystem, Options.Create(_journalSettings), hashService);
        var entryFormatter = new EntryFormatterService(Options.Create(_journalSettings));
        
        _configGenerator = new JournalConfigGenerator(
            _fileSystem,
            tocParser,
            fileTracking,
            entryFormatter,
            _journalConfiguration,
            Options.Create(_journalSettings)
        );
        
        _command = new AddJournalrc(
            _console,
            _fileSystem,
            _configGenerator,
            Options.Create(_journalSettings)
        );
    }

    [Fact]
    public void Execute_JournalrcAlreadyExists_ReturnsErrorCode()
    {
        // Arrange
        var directory = "/test/journal";
        _fileSystem.CreateDirectory(directory);
        _fileSystem.CreateFile(directory, _journalSettings.JournalConfigFileName, "{}");
        
        var settings = new AddJournalrcSettings { FilePath = directory };
        // Context not used in Execute method

        // Act
        var result = _command.Execute(null!, settings);

        // Assert
        Assert.Equal(1, result);
        Assert.Contains("already exists", _console.Output);
    }

    [Fact]
    public void Execute_WithValidTocFile_GeneratesFromToc()
    {
        // Arrange
        var directory = "/test/journal";
        var tocContent = @"# Table of Contents
- [Introduction](1a-intro.md)
## Work
- [Meeting Notes](work-meeting.md)";

        _fileSystem.CreateDirectory(directory);
        _fileSystem.CreateFile(directory, $"{_journalSettings.TableOfContentsFileName}.md", tocContent);
        
        var settings = new AddJournalrcSettings { FilePath = directory };
        // Context not used in Execute method

        // Act
        var result = _command.Execute(null!, settings);

        // Assert
        Assert.Equal(0, result);
        Assert.True(_fileSystem.FileExists($"{directory}/{_journalSettings.JournalConfigFileName}"));
        Assert.Contains("table of contents", _console.Output);
        Assert.Contains("2 entries", _console.Output);
    }

    [Fact]
    public void Execute_WithCustomTocFileName_UsesCustomName()
    {
        // Arrange
        var directory = "/test/journal";
        var customTocName = "CustomTOC";
        var tocContent = @"# Table of Contents
- [Entry1](entry1.md)";

        _fileSystem.CreateDirectory(directory);
        _fileSystem.CreateFile(directory, $"{customTocName}.md", tocContent);
        
        var settings = new AddJournalrcSettings 
        { 
            FilePath = directory,
            TableOfContentsFile = customTocName
        };
        // Context not used in Execute method

        // Act
        var result = _command.Execute(null!, settings);

        // Assert
        Assert.Equal(0, result);
        Assert.True(_fileSystem.FileExists($"{directory}/{_journalSettings.JournalConfigFileName}"));
        
        // Verify the config uses the custom TOC filename
        var config = _journalConfiguration.Read(directory);
        Assert.NotNull(config);
        Assert.Equal($"{customTocName}.md", config.TableOfContents.File);
    }

    [Fact]
    public void Execute_NoTocButHasTrackingFile_GeneratesFromTracking()
    {
        // Arrange
        var directory = "/test/journal";
        _fileSystem.CreateDirectory(directory);
        
        // Create markdown files
        _fileSystem.CreateMarkdownFile(directory, "entry1", "# Entry 1");
        _fileSystem.CreateMarkdownFile(directory, "entry2", "# Entry 2");
        
        // Manually create tracking index JSON
        var trackingIndexJson = @"{
  ""Files"": {
    ""entry1.md"": {
      ""FilePath"": ""entry1.md"",
      ""Hash"": ""abc123"",
      ""LastChecked"": ""2026-01-01T00:00:00Z""
    },
    ""entry2.md"": {
      ""FilePath"": ""entry2.md"",
      ""Hash"": ""def456"",
      ""LastChecked"": ""2026-01-01T00:00:00Z""
    }
  }
}";
        _fileSystem.CreateFile(directory, $".{_journalSettings.AppName}", trackingIndexJson);
        
        var settings = new AddJournalrcSettings { FilePath = directory };
        // Context not used in Execute method

        // Act
        var result = _command.Execute(null!, settings);

        // Assert
        Assert.Equal(0, result);
        Assert.True(_fileSystem.FileExists($"{directory}/{_journalSettings.JournalConfigFileName}"));
        Assert.Contains("tracking index", _console.Output);
        Assert.Contains("2 entries", _console.Output);
    }

    [Fact]
    public void Execute_NoTocNoTracking_GeneratesFromDirectory()
    {
        // Arrange
        var directory = "/test/journal";
        _fileSystem.CreateDirectory(directory);
        
        _fileSystem.CreateMarkdownFile(directory, "entry1", "# Entry 1");
        _fileSystem.CreateMarkdownFile(directory, "entry2", "# Entry 2");
        _fileSystem.CreateMarkdownFile(directory, "entry3", "# Entry 3");
        
        var settings = new AddJournalrcSettings { FilePath = directory };
        // Context not used in Execute method

        // Act
        var result = _command.Execute(null!, settings);

        // Assert
        Assert.Equal(0, result);
        Assert.True(_fileSystem.FileExists($"{directory}/{_journalSettings.JournalConfigFileName}"));
        Assert.Contains("Scanning directory", _console.Output);
        Assert.Contains("3 entries", _console.Output);
    }

    [Fact]
    public void Execute_EmptyDirectory_GeneratesConfigWithNoEntries()
    {
        // Arrange
        var directory = "/test/journal";
        _fileSystem.CreateDirectory(directory);
        
        var settings = new AddJournalrcSettings { FilePath = directory };
        // Context not used in Execute method

        // Act
        var result = _command.Execute(null!, settings);

        // Assert
        Assert.Equal(0, result);
        Assert.True(_fileSystem.FileExists($"{directory}/{_journalSettings.JournalConfigFileName}"));
        Assert.Contains("0 entries", _console.Output);
    }

    [Fact]
    public void Execute_TocWithComplexStructure_GeneratesCorrectConfig()
    {
        // Arrange
        var directory = "/test/journal";
        var tocContent = @"# Table of Contents
- [Intro](1b-intro.md)

## Tech
- [Overview](tech-overview.md)
  - Backend
    - [API](tech-backend-api.md)
    - [Database](tech-backend-db.md)
  
## Personal
- [Journal](personal-journal.md)
- [Notes](personal-notes.md)";

        _fileSystem.CreateDirectory(directory);
        _fileSystem.CreateFile(directory, $"{_journalSettings.TableOfContentsFileName}.md", tocContent);
        
        var settings = new AddJournalrcSettings { FilePath = directory };
        // Context not used in Execute method

        // Act
        var result = _command.Execute(null!, settings);

        // Assert
        Assert.Equal(0, result);
        
        var config = _journalConfiguration.Read(directory);
        Assert.NotNull(config);
        Assert.Single(config.TableOfContents.RootEntries);
        Assert.Equal(2, config.TableOfContents.Structure.Topics.Length);
    }

    [Fact]
    public void Execute_DirectoryScanIgnoresSystemFiles_Correctly()
    {
        // Arrange
        var directory = "/test/journal";
        _fileSystem.CreateDirectory(directory);
        
        // Don't create TOC file - we want directory scan fallback
        // Create other markdown files
        _fileSystem.CreateMarkdownFile(directory, _journalSettings.IntroductionFileName, "# Intro");
        _fileSystem.CreateMarkdownFile(directory, _journalSettings.JournalEntryTemplateFileName, "# Template");
        
        // Create user files - use underscores as word separators (SpaceSeparator setting)
        _fileSystem.CreateMarkdownFile(directory, "my_entry", "# My Entry");
        
        var settings = new AddJournalrcSettings { FilePath = directory };
        // Context not used in Execute method

        // Act
        var result = _command.Execute(null!, settings);

        // Assert
        Assert.Equal(0, result);
        
        var config = _journalConfiguration.Read(directory);
        Assert.NotNull(config);
        
        // 1b and 1c match root entry pattern (1a-9z), but my_entry does not
        // So 2 root entries (1b-Intro and 1c-Journal-Entry-Template)
        Assert.Equal(2, config.TableOfContents.RootEntries.Length);
        var rootEntryNames = config.TableOfContents.RootEntries.Select(e => e.Name).OrderBy(n => n).ToArray();
        Assert.Contains("Intro", rootEntryNames);
        Assert.Contains("Journal Entry Template", rootEntryNames); // Full name extracted from 1c-Journal-Entry-Template
        
        // my_entry becomes a topic entry since it doesn't match root pattern
        // AddEntry's ParseTopicPathFromFilename splits "my_entry" by "-" (none), returns ["my entry"]
        // ExtractEntryNameFromFilename for topic entries also returns "my entry" (the last part)
        // So we get a topic "my entry" with an entry also named "my entry"
        Assert.Single(config.TableOfContents.Structure.Topics);
        var topic = config.TableOfContents.Structure.Topics[0];
        Assert.Equal("my entry", topic.Name); // "my_entry" -> underscore becomes space
        Assert.Single(topic.Entries); // The entry is added to this topic
        Assert.Equal("my entry", topic.Entries[0].Name); // Same name since no separator
        
        // IgnoreFiles is null - TOC is in tableOfContents.file, not ignoreFiles
        Assert.Null(config.TableOfContents.IgnoreFiles);
    }

    [Fact]
    public void Execute_SetsJournalNameFromDirectory_Correctly()
    {
        // Arrange
        var directory = "/test/MyAwesomeJournal";
        _fileSystem.CreateDirectory(directory);
        _fileSystem.CreateMarkdownFile(directory, "entry", "# Entry");
        
        var settings = new AddJournalrcSettings { FilePath = directory };
        // Context not used in Execute method

        // Act
        var result = _command.Execute(null!, settings);

        // Assert
        Assert.Equal(0, result);
        
        var config = _journalConfiguration.Read(directory);
        Assert.NotNull(config);
        Assert.Equal("MyAwesomeJournal", config.JournalName);
    }

    [Fact]
    public void Execute_PreservesTocFileExtensions_Correctly()
    {
        // Arrange
        var directory = "/test/journal";
        _fileSystem.CreateDirectory(directory);
        _fileSystem.CreateMarkdownFile(directory, "entry", "# Entry");
        
        var settings = new AddJournalrcSettings { FilePath = directory };
        // Context not used in Execute method

        // Act
        var result = _command.Execute(null!, settings);

        // Assert
        Assert.Equal(0, result);
        
        var config = _journalConfiguration.Read(directory);
        Assert.NotNull(config);
        Assert.Equal([".md"], config.TableOfContents.Extensions);
    }
}
