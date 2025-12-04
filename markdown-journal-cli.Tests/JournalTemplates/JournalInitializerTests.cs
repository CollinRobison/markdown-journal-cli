using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.Configuration.Objects;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.JournalTemplates;
using markdown_journal_cli.Tests.Infrastructure;
using Microsoft.Extensions.Options;

namespace markdown_journal_cli.Tests.JournalTemplates;

public class JournalInitializerTests
{
    private readonly TestFileSystem _testFileSystem;
    private readonly TestTemplateManager _testTemplateManager;
    private readonly TestJournalConfiguration _testJournalConfiguration;
    private readonly IOptions<JournalSettings> _journalSettings;
    private readonly JournalInitializer _journalInitializer;

    public JournalInitializerTests()
    {
        _testFileSystem = new TestFileSystem();
        _testTemplateManager = new TestTemplateManager();
        _testJournalConfiguration = new TestJournalConfiguration();
        _journalSettings = Options.Create(new JournalSettings
        {
            AppName = "md-journal",
            JournalConfigFileName = ".journalrc",
            DefaultJournalName = "MyJournal",
            TableOfContentsFileName = "1a-TableOfContents",
            TableOfContentsTitle = "Table of Contents",
            IntroductionFileName = "1b-Intro",
            IntroductionTitle = "Introduction",
            JournalEntryTemplateFileName = "1c-Journal-Entry-Template",
            JournalEntryTemplateTitle = "Journal Entry Template",
            AllJournalsFileName = "1h-All-My-Journals",
            AllJournalsTitle = "All My Journals"
        });
        _journalInitializer = new JournalInitializer(_testFileSystem, _testTemplateManager, _testJournalConfiguration, _journalSettings);
    }

    [Fact]
    public void Initialize_WithValidParameters_CreatesDirectoryAndFiles()
    {
        // Arrange
        string journalDirectory = "/test/journal";
        string journalName = "TestJournal";

        // Act
        _journalInitializer.Initialize(journalDirectory, journalName);

        // Assert
        Assert.Contains(journalDirectory, _testFileSystem.GetAllDirectories());
        Assert.True(_testFileSystem.FileExists("/test/journal/1a-TableOfContents.md"));
        Assert.True(_testFileSystem.FileExists("/test/journal/1b-Intro.md"));
        Assert.True(_testFileSystem.FileExists("/test/journal/1c-Journal-Entry-Template.md"));
        Assert.True(_testFileSystem.FileExists("/test/journal/1h-All-My-Journals.md"));
    }

    [Fact]
    public void Initialize_WithValidParameters_CreatesCorrectTemplateFiles()
    {
        // Arrange
        string journalDirectory = "/test/journal";
        string journalName = "TestJournal";

        // Act
        _journalInitializer.Initialize(journalDirectory, journalName);

        // Assert
        Assert.Contains("table-of-contents", _testTemplateManager.GeneratedTemplates.Keys);
        Assert.Equal(3, _testTemplateManager.GeneratedTemplates["journal-entry"].Count);
    }

    [Fact]
    public void Initialize_WithValidParameters_CreatesJournalConfiguration()
    {
        // Arrange
        string journalDirectory = "/test/journal";
        string journalName = "TestJournal";

        // Act
        _journalInitializer.Initialize(journalDirectory, journalName);

        // Assert
        Assert.Single(_testJournalConfiguration.CreatedConfigurations);
        var config = _testJournalConfiguration.CreatedConfigurations[journalDirectory];
        Assert.Equal(journalName, config.JournalName);
        Assert.Equal(3, config.TableOfContents.RootEntries.Length);
        Assert.Contains(config.TableOfContents.RootEntries, re => re.Name == "Introduction" && re.File == "1b-Intro.md");
        Assert.Contains(config.TableOfContents.RootEntries, re => re.Name == "Journal Entry Template" && re.File == "1c-Journal-Entry-Template.md");
        Assert.Contains(config.TableOfContents.RootEntries, re => re.Name == "All My Journals" && re.File == "1h-All-My-Journals.md");
    }

    [Fact]
    public void Initialize_WithNullJournalDirectory_ThrowsArgumentException()
    {
        // Arrange
        string journalName = "TestJournal";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => _journalInitializer.Initialize(null!, journalName));
        Assert.Contains("Journal directory cannot be null or whitespace", exception.Message);
        Assert.Equal("journalDirectory", exception.ParamName);
    }

    [Fact]
    public void Initialize_WithEmptyJournalDirectory_ThrowsArgumentException()
    {
        // Arrange
        string journalDirectory = "";
        string journalName = "TestJournal";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => _journalInitializer.Initialize(journalDirectory, journalName));
        Assert.Contains("Journal directory cannot be null or whitespace", exception.Message);
        Assert.Equal("journalDirectory", exception.ParamName);
    }

    [Fact]
    public void Initialize_WithWhitespaceJournalDirectory_ThrowsArgumentException()
    {
        // Arrange
        string journalDirectory = "   ";
        string journalName = "TestJournal";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => _journalInitializer.Initialize(journalDirectory, journalName));
        Assert.Contains("Journal directory cannot be null or whitespace", exception.Message);
        Assert.Equal("journalDirectory", exception.ParamName);
    }

    [Fact]
    public void Initialize_WithNullJournalName_ThrowsArgumentException()
    {
        // Arrange
        string journalDirectory = "/test/journal";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => _journalInitializer.Initialize(journalDirectory, null!));
        Assert.Contains("Journal name cannot be null or whitespace", exception.Message);
        Assert.Equal("journalName", exception.ParamName);
    }

    [Fact]
    public void Initialize_WithEmptyJournalName_ThrowsArgumentException()
    {
        // Arrange
        string journalDirectory = "/test/journal";
        string journalName = "";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => _journalInitializer.Initialize(journalDirectory, journalName));
        Assert.Contains("Journal name cannot be null or whitespace", exception.Message);
        Assert.Equal("journalName", exception.ParamName);
    }

    [Fact]
    public void Initialize_WithWhitespaceJournalName_ThrowsArgumentException()
    {
        // Arrange
        string journalDirectory = "/test/journal";
        string journalName = "   ";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => _journalInitializer.Initialize(journalDirectory, journalName));
        Assert.Contains("Journal name cannot be null or whitespace", exception.Message);
        Assert.Equal("journalName", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullFileSystem_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new JournalInitializer(null!, _testTemplateManager, _testJournalConfiguration, _journalSettings));
        Assert.Equal("fileSystem", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullTemplateManager_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new JournalInitializer(_testFileSystem, null!, _testJournalConfiguration, _journalSettings));
        Assert.Equal("templateManager", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullJournalConfiguration_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new JournalInitializer(_testFileSystem, _testTemplateManager, null!, _journalSettings));
        Assert.Equal("journalConfiguration", exception.ParamName);
    }

    [Fact]
    public void Initialize_CreatesCorrectIntroductionParameters()
    {
        // Arrange
        string journalDirectory = "/test/journal";
        string journalName = "TestJournal";

        // Act
        _journalInitializer.Initialize(journalDirectory, journalName);

        // Assert
        var journalEntryParams = _testTemplateManager.GeneratedTemplates["journal-entry"];
        var introParams = journalEntryParams[0]; // First journal-entry template call should be for introduction
        
        Assert.NotNull(introParams);
        Assert.Equal("Introduction", introParams["title"]);
        Assert.Equal("Add an introduction to your new journal here.", introParams["body"]);
        Assert.Equal(false, introParams["addSourceBlock"]);
    }

    [Fact]
    public void Initialize_CreatesCorrectAllMyJournalsParameters()
    {
        // Arrange
        string journalDirectory = "/test/journal";
        string journalName = "TestJournal";

        // Act
        _journalInitializer.Initialize(journalDirectory, journalName);

        // Assert
        var journalEntryParams = _testTemplateManager.GeneratedTemplates["journal-entry"];
        var allJournalsParams = journalEntryParams[2]; // Third journal-entry template call should be for all journals
        
        Assert.NotNull(allJournalsParams);
        Assert.Equal("Journals List", allJournalsParams["title"]);
        Assert.Contains("example journal 1", (string)allJournalsParams["body"]);
        Assert.Contains("example journal 2", (string)allJournalsParams["body"]);
        Assert.Contains("example journal 3", (string)allJournalsParams["body"]);
        Assert.Equal(false, allJournalsParams["addSourceBlock"]);
    }
}

// Test implementation for IJournalConfiguration
public class TestJournalConfiguration : IJournalConfiguration
{
    public Dictionary<string, JournalConfig> CreatedConfigurations { get; } = new();

    public void Create(string journalDirectory, JournalConfig journalConfig)
    {
        CreatedConfigurations[journalDirectory] = journalConfig;
    }

    public JournalConfig Read(string journalDirectory)
    {
        return CreatedConfigurations.TryGetValue(journalDirectory, out var config) 
            ? config 
            : throw new FileNotFoundException($"No configuration found for {journalDirectory}");
    }

    public void Update(string journalDirectory, JournalConfig journalConfig)
    {
        CreatedConfigurations[journalDirectory] = journalConfig;
    }

    public void Update(string directory, Action<JournalConfig> config)
    {
        if (CreatedConfigurations.TryGetValue(directory, out var existingConfig))
        {
            config(existingConfig);
        }
        else
        {
            throw new FileNotFoundException($"No configuration found for {directory}");
        }
    }

    public void Delete(string directory)
    {
        CreatedConfigurations.Remove(directory);
    }
}

/// <summary>
/// Test implementation of ITemplateManager that tracks template generation calls.
/// </summary>
public class TestTemplateManager : ITemplateManager
{
    public Dictionary<string, List<Dictionary<string, object>?>> GeneratedTemplates { get; } = new();

    public void RegisterTemplate(ITemplateGenerator template)
    {
        // Not needed for tests
    }

    public string GenerateFromTemplate(string templateName, Dictionary<string, object>? parameters)
    {
        if (!GeneratedTemplates.ContainsKey(templateName))
        {
            GeneratedTemplates[templateName] = new List<Dictionary<string, object>?>();
        }

        GeneratedTemplates[templateName].Add(parameters);
        return $"Generated content for {templateName}";
    }

    public IEnumerable<string> GetAvailableTemplates()
    {
        return GeneratedTemplates.Keys;
    }
}