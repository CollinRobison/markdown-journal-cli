using System.Text.Json;
using markdown_journal_cli.Commands.Add;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.JournalTemplates;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Spectre.Console.Testing;
using Xunit;

namespace markdown_journal_cli.Tests.Commands.Add;

/// <summary>
/// Integration tests for AddTableOfContents command using real services and file operations.
/// These tests validate actual file I/O and end-to-end scenarios.
/// </summary>
public class AddTableOfContentsIntegrationTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly IFileSystem _fileSystem;
    private readonly IJournalConfiguration _journalConfiguration;
    private readonly ITableOfContentsGenerator _tocGenerator;
    private readonly IOptions<JournalSettings> _journalSettings;
    private readonly AddTableOfContents _command;
    private readonly TestConsole _console;

    public AddTableOfContentsIntegrationTests()
    {
        // Create temporary test directory
        _testDirectory = Path.Combine(Path.GetTempPath(), $"journal-test-toc-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        _journalSettings = Options.Create(
            new JournalSettings
            {
                AppName = "md-journal",
                JournalConfigFileName = ".journalrc",
                DefaultJournalName = "TestJournal",
                TableOfContentsFileName = "TOC",
                TableOfContentsTitle = "Table of Contents"
            }
        );

        // Use real services
        _fileSystem = new FileSystem(NullLogger<FileSystem>.Instance);
        _journalConfiguration = new JournalConfiguration(
            _fileSystem,
            _journalSettings,
            NullLogger<JournalConfiguration>.Instance
        );
        _tocGenerator = new TableOfContentsGenerator(
            _fileSystem,
            _journalConfiguration,
            _journalSettings
        );

        _console = new TestConsole();
        _command = new AddTableOfContents(
            _console,
            _fileSystem,
            _journalConfiguration,
            _tocGenerator,
            _journalSettings
        );
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    #region Integration Tests

    [Fact]
    public void Execute_CreatesRealTocFile_WhenJournalIsInitialized()
    {
        // Arrange
        InitializeTestJournal();

        var settings = new AddTableOfContentsSettings { FilePath = _testDirectory };

        // Act
        var result = _command.Execute(null!, settings);

        // Assert
        result.ShouldBe(0);
        _console.Output.ShouldContain("Success");
        _console.Output.ShouldContain("Created");

        // Verify TOC file was created
        var tocPath = Path.Combine(_testDirectory, "TOC.md");
        File.Exists(tocPath).ShouldBeTrue();

        // Verify TOC has correct content
        var tocContent = File.ReadAllText(tocPath);
        tocContent.ShouldContain("Table of Contents");
    }

    [Fact]
    public void Execute_UpdatesJournalrcConfig_WhenTocNameDiffers()
    {
        // Arrange
        InitializeTestJournalWithDifferentTocName("OldTOC");

        var settings = new AddTableOfContentsSettings { FilePath = _testDirectory };

        // Act
        var result = _command.Execute(null!, settings);

        // Assert
        result.ShouldBe(0);

        // Verify config was updated
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var journalrcContent = File.ReadAllText(journalrcPath);
        var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(journalrcContent);
        
        config.ShouldNotBeNull();
        var tocConfig = config["tableOfContents"].GetProperty("file").GetString();
        tocConfig.ShouldBe("TOC.md");

        // Verify TOC file was created with the new name
        var tocPath = Path.Combine(_testDirectory, "TOC.md");
        File.Exists(tocPath).ShouldBeTrue();
    }

    [Fact]
    public void Execute_WarnsAndDoesNotOverwrite_WhenTocAlreadyExists()
    {
        // Arrange
        InitializeTestJournal();
        
        var tocPath = Path.Combine(_testDirectory, "TOC.md");
        var existingContent = "# Existing TOC Content\n- Entry 1\n- Entry 2";
        File.WriteAllText(tocPath, existingContent);

        var settings = new AddTableOfContentsSettings { FilePath = _testDirectory };

        // Act
        var result = _command.Execute(null!, settings);

        // Assert
        result.ShouldBe(0);
        _console.Output.ShouldContain("Warning");
        _console.Output.ShouldContain("already exists");

        // Verify original content was NOT overwritten
        var actualContent = File.ReadAllText(tocPath);
        actualContent.ShouldBe(existingContent);
    }

    [Fact]
    public void Execute_CreatesCustomNamedToc_WhenNameSpecified()
    {
        // Arrange
        InitializeTestJournal();

        var customName = "MyCustomTableOfContents";
        var settings = new AddTableOfContentsSettings 
        { 
            FilePath = _testDirectory,
            TableOfContentsName = customName
        };

        // Act
        var result = _command.Execute(null!, settings);

        // Assert
        result.ShouldBe(0);
        _console.Output.ShouldContain("Success");
        _console.Output.ShouldContain($"{customName}.md");

        // Verify custom TOC file was created
        var tocPath = Path.Combine(_testDirectory, $"{customName}.md");
        File.Exists(tocPath).ShouldBeTrue();

        // Verify config was updated
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var journalrcContent = File.ReadAllText(journalrcPath);
        var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(journalrcContent);
        
        var tocConfig = config!["tableOfContents"].GetProperty("file").GetString();
        tocConfig.ShouldBe($"{customName}.md");
    }

    [Fact]
    public void Execute_CreatesValidTocStructure_WithExistingEntries()
    {
        // Arrange
        InitializeTestJournalWithEntries();

        var settings = new AddTableOfContentsSettings { FilePath = _testDirectory };

        // Act
        var result = _command.Execute(null!, settings);

        // Assert
        result.ShouldBe(0);

        // Verify TOC file was created with entries
        var tocPath = Path.Combine(_testDirectory, "TOC.md");
        File.Exists(tocPath).ShouldBeTrue();

        var tocContent = File.ReadAllText(tocPath);
        tocContent.ShouldContain("Table of Contents");
        tocContent.ShouldContain("Entry1");
        tocContent.ShouldContain("Entry2");
        tocContent.ShouldContain("entry1.md");
        tocContent.ShouldContain("entry2.md");
    }

    [Fact]
    public void Execute_FailsGracefully_WhenDirectoryDoesNotExist()
    {
        // Arrange
        var nonExistentDirectory = Path.Combine(_testDirectory, "nonexistent");
        var settings = new AddTableOfContentsSettings { FilePath = nonExistentDirectory };

        // Act
        var result = _command.Execute(null!, settings);

        // Assert
        result.ShouldBe(1);
        _console.Output.ShouldContain("Error");
    }

    [Fact]
    public void Execute_FailsGracefully_WhenJournalrcMissing()
    {
        // Arrange - Create directory but no journalrc
        var settings = new AddTableOfContentsSettings { FilePath = _testDirectory };

        // Act
        var result = _command.Execute(null!, settings);

        // Assert
        result.ShouldBe(1);
        _console.Output.ShouldContain("Error");
        _console.Output.ShouldContain("journalrc");

        // Verify no TOC file was created
        var tocPath = Path.Combine(_testDirectory, "TOC.md");
        File.Exists(tocPath).ShouldBeFalse();
    }

    #endregion

    #region Helper Methods

    private void InitializeTestJournal()
    {
        // Create .journalrc file with proper structure
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var journalrcContent = JsonSerializer.Serialize(new
        {
            journalName = "TestJournal",
            tableOfContents = new
            {
                file = "TOC.md",
                extensions = new[] { ".md" },
                ignoreFiles = Array.Empty<string>(),
                structure = new
                {
                    topics = Array.Empty<object>()
                },
                rootEntries = Array.Empty<object>()
            }
        }, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(journalrcPath, journalrcContent);
    }

    private void InitializeTestJournalWithDifferentTocName(string oldTocName)
    {
        // Create .journalrc file with different TOC name
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var journalrcContent = JsonSerializer.Serialize(new
        {
            journalName = "TestJournal",
            tableOfContents = new
            {
                file = $"{oldTocName}.md",
                extensions = new[] { ".md" },
                ignoreFiles = Array.Empty<string>(),
                structure = new
                {
                    topics = Array.Empty<object>()
                },
                rootEntries = Array.Empty<object>()
            }
        }, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(journalrcPath, journalrcContent);
    }

    private void InitializeTestJournalWithEntries()
    {
        // Create .journalrc file with entries
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var journalrcContent = JsonSerializer.Serialize(new
        {
            journalName = "TestJournal",
            tableOfContents = new
            {
                file = "TOC.md",
                extensions = new[] { ".md" },
                ignoreFiles = Array.Empty<string>(),
                structure = new
                {
                    topics = Array.Empty<object>()
                },
                rootEntries = new[]
                {
                    new { name = "Entry1", file = "entry1.md" },
                    new { name = "Entry2", file = "entry2.md" }
                }
            }
        }, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(journalrcPath, journalrcContent);

        // Create the actual entry files
        File.WriteAllText(Path.Combine(_testDirectory, "entry1.md"), "# Entry1\nContent");
        File.WriteAllText(Path.Combine(_testDirectory, "entry2.md"), "# Entry2\nContent");
    }

    #endregion
}
