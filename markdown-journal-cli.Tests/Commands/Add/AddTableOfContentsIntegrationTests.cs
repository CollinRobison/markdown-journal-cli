using System.Text.Json;
using markdown_journal_cli.Commands.Add;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.Tracking;
using markdown_journal_cli.Infrastructure.Transactions;
using markdown_journal_cli.Services;
using markdown_journal_cli.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Spectre.Console.Testing;
using Xunit;

namespace markdown_journal_cli.Tests.Commands.Add;

/// <summary>
/// Integration tests for AddTableOfContents command using real services and file operations.
/// These tests validate actual file I/O and end-to-end scenarios.
/// </summary>
public class AddTableOfContentsIntegrationTests : JournalIntegrationTestBase
{
    // Use TOC-specific journal settings (TOC file name = "TOC", not the default)
    private readonly IOptions<JournalSettings> _tocJournalSettings;
    private readonly IJournalConfiguration _journalConfiguration;
    private readonly ITableOfContentsService _tocGenerator;
    private readonly AddTableOfContents _command;
    private readonly TestConsole _console;

    public AddTableOfContentsIntegrationTests() : base("TestJournal")
    {
        _tocJournalSettings = Options.Create(
            new JournalSettings
            {
                AppName = "md-journal",
                JournalConfigFileName = ".journalrc",
                DefaultJournalName = "TestJournal",
                TableOfContentsFileName = "TOC",
                TableOfContentsTitle = "Table of Contents",
                MetadataDirName = ".mdjournal",
                TrackingFileName = ".journalindex",
                TocStructureFileName = ".journaltoc",
            }
        );

        // Use real services backed by base-class FileSystem
        var tocStructureRepository = new JournalTocStructureRepository(FileSystem, _tocJournalSettings);
        _journalConfiguration = new JournalConfiguration(
            FileSystem,
            _tocJournalSettings,
            NullLogger<JournalConfiguration>.Instance,
            Mock.Of<IFileTracking>(),
            tocStructureRepository
        );
        _tocGenerator = new TableOfContentsService(
            FileSystem,
            _journalConfiguration,
            _tocJournalSettings,
            NullLogger<TableOfContentsService>.Instance,
            tocStructureRepository
        );

        _console = new TestConsole();
        _command = new AddTableOfContents(
            _console,
            FileSystem,
            _journalConfiguration,
            _tocGenerator,
            _tocJournalSettings,
            NoOpFileTransactionCoordinator.Instance,
            NoOpRollbackReporter.Instance
        );
    }

    #region Integration Tests

    [Fact]
    public void Execute_Should_CreateRealTocFile_When_JournalIsInitialized()
    {
        // Arrange
        InitializeTestJournal();

        var settings = new AddTableOfContentsSettings { FilePath = JournalPath };

        // Act
        var result = _command.Execute(null!, settings);

        // Assert
        result.ShouldBe(0);
        _console.Output.ShouldContain("Success");
        _console.Output.ShouldContain("Created");

        // Verify TOC file was created
        var tocPath = Path.Combine(JournalPath, "TOC.md");
        File.Exists(tocPath).ShouldBeTrue();

        // Verify TOC has correct content
        var tocContent = File.ReadAllText(tocPath);
        tocContent.ShouldContain("Table of Contents");
    }

    [Fact]
    public void Execute_Should_UpdateJournalrcConfig_When_TocNameDiffers()
    {
        // Arrange
        InitializeTestJournalWithDifferentTocName("OldTOC");

        var settings = new AddTableOfContentsSettings { FilePath = JournalPath };

        // Act
        var result = _command.Execute(null!, settings);

        // Assert
        result.ShouldBe(0);

        // Verify config was updated
        var journalrcPath = Path.Combine(JournalPath, ".journalrc");
        var journalrcContent = File.ReadAllText(journalrcPath);
        var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(journalrcContent);

        config.ShouldNotBeNull();
        var tocConfig = config["tableOfContents"].GetProperty("file").GetString();
        tocConfig.ShouldBe("TOC.md");

        // Verify TOC file was created with the new name
        var tocPath = Path.Combine(JournalPath, "TOC.md");
        File.Exists(tocPath).ShouldBeTrue();
    }

    [Fact]
    public void Execute_Should_WarnAndNotOverwrite_When_TocAlreadyExists()
    {
        // Arrange
        InitializeTestJournal();

        var tocPath = Path.Combine(JournalPath, "TOC.md");
        var existingContent = "# Existing TOC Content\n- Entry 1\n- Entry 2";
        File.WriteAllText(tocPath, existingContent);

        var settings = new AddTableOfContentsSettings { FilePath = JournalPath };

        // Act
        var result = _command.Execute(null!, settings);

        // Assert
        result.ShouldBe(1);
        _console.Output.ShouldContain("Warning");
        _console.Output.ShouldContain("already exists");

        // Verify original content was NOT overwritten
        var actualContent = File.ReadAllText(tocPath);
        actualContent.ShouldBe(existingContent);
    }

    [Fact]
    public void Execute_Should_CreateCustomNamedToc_When_NameSpecified()
    {
        // Arrange
        InitializeTestJournal();

        var customName = "MyCustomTableOfContents";
        var settings = new AddTableOfContentsSettings
        {
            FilePath = JournalPath,
            TableOfContentsName = customName,
        };

        // Act
        var result = _command.Execute(null!, settings);

        // Assert
        result.ShouldBe(0);
        _console.Output.ShouldContain("Success");
        _console.Output.ShouldContain($"{customName}.md");

        // Verify custom TOC file was created
        var tocPath = Path.Combine(JournalPath, $"{customName}.md");
        File.Exists(tocPath).ShouldBeTrue();

        // Verify config was updated
        var journalrcPath = Path.Combine(JournalPath, ".journalrc");
        var journalrcContent = File.ReadAllText(journalrcPath);
        var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(journalrcContent);

        var tocConfig = config!["tableOfContents"].GetProperty("file").GetString();
        tocConfig.ShouldBe($"{customName}.md");
    }

    [Fact]
    public void Execute_Should_CreateValidTocStructure_When_EntriesExist()
    {
        // Arrange
        InitializeTestJournalWithEntries();

        var settings = new AddTableOfContentsSettings { FilePath = JournalPath };

        // Act
        var result = _command.Execute(null!, settings);

        // Assert
        result.ShouldBe(0);

        // Verify TOC file was created with entries
        var tocPath = Path.Combine(JournalPath, "TOC.md");
        File.Exists(tocPath).ShouldBeTrue();

        var tocContent = File.ReadAllText(tocPath);
        tocContent.ShouldContain("Table of Contents");
        tocContent.ShouldContain("Entry1");
        tocContent.ShouldContain("Entry2");
        tocContent.ShouldContain("entry1.md");
        tocContent.ShouldContain("entry2.md");
    }

    [Fact]
    public void Execute_Should_FailGracefully_When_DirectoryDoesNotExist()
    {
        // Arrange
        var nonExistentDirectory = Path.Combine(JournalPath, "nonexistent");
        var settings = new AddTableOfContentsSettings { FilePath = nonExistentDirectory };

        // Act
        var result = _command.Execute(null!, settings);

        // Assert
        result.ShouldBe(1);
        _console.Output.ShouldContain("Error");
    }

    [Fact]
    public void Execute_Should_FailGracefully_When_JournalrcMissing()
    {
        // Arrange - Create directory but no journalrc
        var settings = new AddTableOfContentsSettings { FilePath = JournalPath };

        // Act
        var result = _command.Execute(null!, settings);

        // Assert
        result.ShouldBe(1);
        _console.Output.ShouldContain("Error");
        _console.Output.ShouldContain("journalrc");

        // Verify no TOC file was created
        var tocPath = Path.Combine(JournalPath, "TOC.md");
        File.Exists(tocPath).ShouldBeFalse();
    }

    #endregion

    #region Helper Methods

    private void InitializeTestJournal()
    {
        // Create .journalrc file (structure/rootEntries now in .journaltoc)
        var journalrcPath = Path.Combine(JournalPath, ".journalrc");
        var journalrcContent = JsonSerializer.Serialize(
            new
            {
                journalName = "TestJournal",
                tableOfContents = new
                {
                    file = "TOC.md",
                    extensions = new[] { ".md" },
                    ignoreFiles = Array.Empty<string>(),
                },
            },
            new JsonSerializerOptions { WriteIndented = true }
        );
        File.WriteAllText(journalrcPath, journalrcContent);

        // Create .mdjournal metadata directory and empty .journaltoc
        var metadataDir = Path.Combine(JournalPath, ".mdjournal");
        Directory.CreateDirectory(metadataDir);
        File.WriteAllText(
            Path.Combine(metadataDir, ".journaltoc"),
            """{"Structure":{"Topics":[]},"RootEntries":[]}"""
        );
    }

    private void InitializeTestJournalWithDifferentTocName(string oldTocName)
    {
        // Create .journalrc file with different TOC name (structure/rootEntries now in .journaltoc)
        var journalrcPath = Path.Combine(JournalPath, ".journalrc");
        var journalrcContent = JsonSerializer.Serialize(
            new
            {
                journalName = "TestJournal",
                tableOfContents = new
                {
                    file = $"{oldTocName}.md",
                    extensions = new[] { ".md" },
                    ignoreFiles = Array.Empty<string>(),
                },
            },
            new JsonSerializerOptions { WriteIndented = true }
        );
        File.WriteAllText(journalrcPath, journalrcContent);

        // Create .mdjournal metadata directory and empty .journaltoc
        var metadataDir = Path.Combine(JournalPath, ".mdjournal");
        Directory.CreateDirectory(metadataDir);
        File.WriteAllText(
            Path.Combine(metadataDir, ".journaltoc"),
            """{"Structure":{"Topics":[]},"RootEntries":[]}"""
        );
    }

    private void InitializeTestJournalWithEntries()
    {
        // Create .journalrc file (entries now in .journaltoc)
        var journalrcPath = Path.Combine(JournalPath, ".journalrc");
        var journalrcContent = JsonSerializer.Serialize(
            new
            {
                journalName = "TestJournal",
                tableOfContents = new
                {
                    file = "TOC.md",
                    extensions = new[] { ".md" },
                    ignoreFiles = Array.Empty<string>(),
                },
            },
            new JsonSerializerOptions { WriteIndented = true }
        );
        File.WriteAllText(journalrcPath, journalrcContent);

        // Create .mdjournal metadata directory with root entries in .journaltoc
        var metadataDir = Path.Combine(JournalPath, ".mdjournal");
        Directory.CreateDirectory(metadataDir);
        File.WriteAllText(
            Path.Combine(metadataDir, ".journaltoc"),
            """{"Structure":{"Topics":[]},"RootEntries":[{"Name":"Entry1","File":"entry1.md"},{"Name":"Entry2","File":"entry2.md"}]}"""
        );

        // Create the actual entry files
        File.WriteAllText(Path.Combine(JournalPath, "entry1.md"), "# Entry1\nContent");
        File.WriteAllText(Path.Combine(JournalPath, "entry2.md"), "# Entry2\nContent");
    }

    #endregion
}
