using System.Text.Json;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.Configuration.Models;
using markdown_journal_cli.Infrastructure.Tracking;
using markdown_journal_cli.Tests.Infrastructure.FileSystem;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Xunit;
namespace markdown_journal_cli.Tests.Infrastructure.Configuration;

/// <summary>
/// Unit tests for the <see cref="JournalConfiguration"/> class.
/// </summary>
public class JournalConfigurationTests
{
    private readonly TestFileSystem _fileSystem;
    private readonly JournalConfiguration _journalConfiguration;
    private readonly string _testDirectory;

    private readonly IOptions<JournalSettings> _journalSettings;

    public JournalConfigurationTests()
    {
        _fileSystem = new TestFileSystem();
        _journalSettings = Options.Create(
            new JournalSettings { JournalConfigFileName = ".journalrc" }
        );
        _journalConfiguration = new JournalConfiguration(
            _fileSystem,
            _journalSettings,
            NullLogger<JournalConfiguration>.Instance,
            Mock.Of<IFileTracking>()
        );
        _testDirectory = "/test/directory";
    }

    private JournalConfig CreateTestConfig()
    {
        return new JournalConfig
        {
            JournalName = "Test Journal",
            TableOfContents = new TableOfContents
            {
                File = "toc.md",
                Extensions = [".md", ".txt"],
                Structure = new Structure
                {
                    Topics =
                    [
                        new Topic
                        {
                            Name = "General",
                            Entries = [],
                            Subtopics = null,
                        },
                    ],
                },
                RootEntries = [new Entries { Name = "Home", File = "1a-home.md" }],
            },
        };
    }

    [Fact]
    public void Create_ShouldCreateJournalrcFile_WhenFileDoesNotExist()
    {
        // Arrange
        var config = CreateTestConfig();

        // Act
        _journalConfiguration.Create(_testDirectory, config);

        // Assert
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        _fileSystem.FileExists(journalrcPath).ShouldBeTrue();

        var fileContent = _fileSystem.GetFileContent(journalrcPath);
        fileContent.ShouldNotBeNull();
        var savedConfig = JsonSerializer.Deserialize<JournalConfig>(fileContent);

        savedConfig.ShouldNotBeNull();
        savedConfig.JournalName.ShouldBe("Test Journal");
        savedConfig.TableOfContents.File.ShouldBe("toc.md");
    }

    [Fact]
    public void Create_ShouldNotOverwriteFile_WhenJournalrcAlreadyExists()
    {
        // Arrange
        var config = CreateTestConfig();
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        _fileSystem.CreateFile(_testDirectory, ".journalrc", "existing content");

        // Act
        _journalConfiguration.Create(_testDirectory, config);

        // Assert
        var fileContent = _fileSystem.GetFileContent(journalrcPath);
        fileContent.ShouldBe("existing content");
    }

    [Fact]
    public void Create_ShouldHandleDirectoryPathWithJournalrc()
    {
        // Arrange
        var config = CreateTestConfig();
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");

        // Act
        _journalConfiguration.Create(journalrcPath, config);

        // Assert
        _fileSystem.FileExists(journalrcPath).ShouldBeTrue();
    }

    [Fact]
    public void Delete_ShouldRemoveJournalrcFile_WhenFileExists()
    {
        // Arrange
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        _fileSystem.CreateFile(_testDirectory, ".journalrc", "test content");

        // Act
        _journalConfiguration.Delete(_testDirectory);

        // Assert
        _fileSystem.FileExists(journalrcPath).ShouldBeFalse();
    }

    [Fact]
    public void Delete_ShouldNotThrow_WhenFileDoesNotExist()
    {
        // Act & Assert
        Should.NotThrow(() => _journalConfiguration.Delete(_testDirectory));
    }

    [Fact]
    public void Delete_ShouldHandleDirectoryPathWithJournalrc()
    {
        // Arrange
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        _fileSystem.CreateFile(_testDirectory, ".journalrc", "test content");

        // Act
        _journalConfiguration.Delete(journalrcPath);

        // Assert
        _fileSystem.FileExists(journalrcPath).ShouldBeFalse();
    }

    [Fact]
    public void Update_ShouldModifyExistingConfig_WhenFileExists()
    {
        // Arrange
        var originalConfig = CreateTestConfig();
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            originalConfig,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        // Act
        _journalConfiguration.Update(
            _testDirectory,
            config =>
            {
                config.JournalName = "Updated Journal Name";
                config.TableOfContents.File = "updated-toc.md";
            }
        );

        // Assert
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        updatedContent.ShouldNotBeNull();
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        updatedConfig.JournalName.ShouldBe("Updated Journal Name");
        updatedConfig.TableOfContents.File.ShouldBe("updated-toc.md");
        // Verify other properties are preserved
        updatedConfig.TableOfContents.Extensions.ShouldBe(
            originalConfig.TableOfContents.Extensions
        );
    }

    [Fact]
    public void Update_ShouldPreserveUnmodifiedProperties()
    {
        // Arrange
        var originalConfig = CreateTestConfig();
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            originalConfig,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        // Act - Only modify one property
        _journalConfiguration.Update(
            _testDirectory,
            config =>
            {
                config.JournalName = "Only This Changed";
            }
        );

        // Assert
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        updatedContent.ShouldNotBeNull();
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        updatedConfig.JournalName.ShouldBe("Only This Changed");
        // All other properties should remain unchanged
        updatedConfig.TableOfContents.File.ShouldBe(originalConfig.TableOfContents.File);
        updatedConfig.TableOfContents.Extensions.ShouldBe(
            originalConfig.TableOfContents.Extensions
        );
        updatedConfig.TableOfContents.Structure.Topics.Length.ShouldBe(
            originalConfig.TableOfContents.Structure.Topics.Length
        );
    }

    [Fact]
    public void Update_ShouldNotThrow_WhenFileDoesNotExist()
    {
        // Act & Assert
        Should.NotThrow(() =>
            _journalConfiguration.Update(
                _testDirectory,
                config =>
                {
                    config.JournalName = "This won't be applied";
                }
            )
        );
    }

    [Fact]
    public void Update_ShouldHandleInvalidJson()
    {
        // Arrange
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        _fileSystem.CreateFile(_testDirectory, ".journalrc", "invalid json content");

        // Act & Assert
        Should.NotThrow(() =>
            _journalConfiguration.Update(
                _testDirectory,
                config =>
                {
                    config.JournalName = "This won't be applied";
                }
            )
        );

        // Verify the file content remains unchanged
        var fileContent = _fileSystem.GetFileContent(journalrcPath);
        fileContent.ShouldBe("invalid json content");
    }

    [Fact]
    public void Update_ShouldHandleDirectoryPathWithJournalrc()
    {
        // Arrange
        var originalConfig = CreateTestConfig();
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            originalConfig,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        // Act
        _journalConfiguration.Update(
            journalrcPath,
            config =>
            {
                config.JournalName = "Updated via full path";
            }
        );

        // Assert
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        updatedContent.ShouldNotBeNull();
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        updatedConfig.JournalName.ShouldBe("Updated via full path");
    }

    #region AddRootEntry Tests

    [Fact]
    public void AddRootEntry_ShouldAddNewRootEntry_WhenEntryDoesNotExist()
    {
        // Arrange
        var config = CreateTestConfig();
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        // Act
        _journalConfiguration.AddRootEntry(_testDirectory, "New Entry", "1e-New-Entry.md");

        // Assert
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        updatedConfig.TableOfContents.RootEntries.Length.ShouldBe(2);
        updatedConfig.TableOfContents.RootEntries[0].Name.ShouldBe("Home");
        updatedConfig.TableOfContents.RootEntries[1].Name.ShouldBe("New Entry");
        updatedConfig.TableOfContents.RootEntries[1].File.ShouldBe("1e-New-Entry.md");
    }

    [Fact]
    public void AddRootEntry_ShouldNotAddDuplicate_WhenFileAlreadyExists()
    {
        // Arrange
        var config = CreateTestConfig();
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        // Act
        _journalConfiguration.AddRootEntry(_testDirectory, "Home Again", "1a-home.md");

        // Assert
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        updatedConfig.TableOfContents.RootEntries.Length.ShouldBe(1);
        updatedConfig.TableOfContents.RootEntries[0].Name.ShouldBe("Home");
    }

    [Fact]
    public void AddRootEntry_ShouldBeCaseInsensitive_WhenCheckingDuplicates()
    {
        // Arrange
        var config = CreateTestConfig();
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        // Act
        _journalConfiguration.AddRootEntry(_testDirectory, "Home Again", "1A-HOME.MD");

        // Assert
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        updatedConfig.TableOfContents.RootEntries.Length.ShouldBe(1);
    }

    #endregion

    #region AddTopicEntry Tests

    [Fact]
    public void AddTopicEntry_ShouldCreateNewTopicAndAddEntry_WhenTopicDoesNotExist()
    {
        // Arrange
        var config = CreateTestConfig();
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        // Act
        _journalConfiguration.AddTopicEntry(
            _testDirectory,
            ["Learning"],
            "Getting Started",
            "Learning-Getting_Started.md"
        );

        // Assert
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        updatedConfig.TableOfContents.Structure.Topics.Length.ShouldBe(2);

        var learningTopic = updatedConfig.TableOfContents.Structure.Topics.FirstOrDefault(t =>
            t.Name == "Learning"
        );
        learningTopic.ShouldNotBeNull();
        learningTopic.Entries.Length.ShouldBe(1);
        learningTopic.Entries[0].Name.ShouldBe("Getting Started");
        learningTopic.Entries[0].File.ShouldBe("Learning-Getting_Started.md");
    }

    [Fact]
    public void AddTopicEntry_ShouldAddToExistingTopic_WhenTopicExists()
    {
        // Arrange
        var config = CreateTestConfig();
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        // Act
        _journalConfiguration.AddTopicEntry(
            _testDirectory,
            ["General"],
            "New Entry",
            "General-New_Entry.md"
        );

        // Assert
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        updatedConfig.TableOfContents.Structure.Topics.Length.ShouldBe(1);

        var generalTopic = updatedConfig.TableOfContents.Structure.Topics[0];
        generalTopic.Name.ShouldBe("General");
        generalTopic.Entries.Length.ShouldBe(1);
        generalTopic.Entries[0].Name.ShouldBe("New Entry");
    }

    [Fact]
    public void AddTopicEntry_ShouldCreateMultiLevelHierarchy_WithComplexPath()
    {
        // Arrange
        var config = CreateTestConfig();
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        // Act - Test with pirates-swords-cutlass-pirates_who_owned-Jack_Sparrow.md
        _journalConfiguration.AddTopicEntry(
            _testDirectory,
            ["pirates", "swords", "cutlass", "pirates who owned"],
            "Jack Sparrow",
            "pirates-swords-cutlass-pirates_who_owned-Jack_Sparrow.md"
        );

        // Assert
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();

        // Navigate the hierarchy
        var piratesTopic = updatedConfig.TableOfContents.Structure.Topics.FirstOrDefault(t =>
            t.Name == "pirates"
        );
        piratesTopic.ShouldNotBeNull();
        piratesTopic.Subtopics.ShouldNotBeNull();

        var swordsTopic = piratesTopic.Subtopics!.FirstOrDefault(t => t.Name == "swords");
        swordsTopic.ShouldNotBeNull();
        swordsTopic.Subtopics.ShouldNotBeNull();

        var cutlassTopic = swordsTopic.Subtopics!.FirstOrDefault(t => t.Name == "cutlass");
        cutlassTopic.ShouldNotBeNull();
        cutlassTopic.Subtopics.ShouldNotBeNull();

        var piratesWhoOwnedTopic = cutlassTopic.Subtopics!.FirstOrDefault(t =>
            t.Name == "pirates who owned"
        );
        piratesWhoOwnedTopic.ShouldNotBeNull();
        piratesWhoOwnedTopic.Entries.Length.ShouldBe(1);
        piratesWhoOwnedTopic.Entries[0].Name.ShouldBe("Jack Sparrow");
        piratesWhoOwnedTopic
            .Entries[0]
            .File.ShouldBe("pirates-swords-cutlass-pirates_who_owned-Jack_Sparrow.md");
    }

    [Fact]
    public void AddTopicEntry_ShouldSortTopicsAlphabetically_WhenSortingEnabled()
    {
        // Arrange
        var config = CreateTestConfig();
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        // Act - Add topics in non-alphabetical order
        _journalConfiguration.AddTopicEntry(
            _testDirectory,
            ["Zebra"],
            "Z Entry",
            "Zebra-Z.md",
            sortAlphabetically: true
        );
        _journalConfiguration.AddTopicEntry(
            _testDirectory,
            ["Apple"],
            "A Entry",
            "Apple-A.md",
            sortAlphabetically: true
        );
        _journalConfiguration.AddTopicEntry(
            _testDirectory,
            ["Banana"],
            "B Entry",
            "Banana-B.md",
            sortAlphabetically: true
        );

        // Assert
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        var topics = updatedConfig.TableOfContents.Structure.Topics;
        topics.Length.ShouldBe(4); // Apple, Banana, General (original), Zebra

        // Check alphabetical order
        topics[0].Name.ShouldBe("Apple");
        topics[1].Name.ShouldBe("Banana");
        topics[2].Name.ShouldBe("General");
        topics[3].Name.ShouldBe("Zebra");
    }

    [Fact]
    public void AddTopicEntry_ShouldSortEntriesByFileName_WhenSortingEnabled()
    {
        // Arrange
        var config = CreateTestConfig();
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        // Act - Add entries with names that would sort differently than files
        _journalConfiguration.AddTopicEntry(
            _testDirectory,
            ["Tech"],
            "Zebra Display Name",
            "Tech-Apple.md"
        );
        _journalConfiguration.AddTopicEntry(
            _testDirectory,
            ["Tech"],
            "Alpha Display Name",
            "Tech-Zebra.md"
        );
        _journalConfiguration.AddTopicEntry(
            _testDirectory,
            ["Tech"],
            "Middle Display Name",
            "Tech-Banana.md"
        );

        // Assert
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        var techTopic = updatedConfig.TableOfContents.Structure.Topics.FirstOrDefault(t =>
            t.Name == "Tech"
        );
        techTopic.ShouldNotBeNull();
        techTopic.Entries.Length.ShouldBe(3);

        // Should be sorted by file name, not display name
        techTopic.Entries[0].File.ShouldBe("Tech-Apple.md");
        techTopic.Entries[0].Name.ShouldBe("Zebra Display Name");
        techTopic.Entries[1].File.ShouldBe("Tech-Banana.md");
        techTopic.Entries[1].Name.ShouldBe("Middle Display Name");
        techTopic.Entries[2].File.ShouldBe("Tech-Zebra.md");
        techTopic.Entries[2].Name.ShouldBe("Alpha Display Name");
    }

    [Fact]
    public void AddTopicEntry_ShouldMaintainInsertionOrder_WhenSortingDisabled()
    {
        // Arrange
        var config = CreateTestConfig();
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        // Act - Add topics in specific order with sorting disabled
        _journalConfiguration.AddTopicEntry(
            _testDirectory,
            ["Zebra"],
            "Z Entry",
            "Zebra-Z.md",
            sortAlphabetically: false
        );
        _journalConfiguration.AddTopicEntry(
            _testDirectory,
            ["Apple"],
            "A Entry",
            "Apple-A.md",
            sortAlphabetically: false
        );
        _journalConfiguration.AddTopicEntry(
            _testDirectory,
            ["Banana"],
            "B Entry",
            "Banana-B.md",
            sortAlphabetically: false
        );

        // Assert
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        var topics = updatedConfig.TableOfContents.Structure.Topics;
        topics.Length.ShouldBe(4);

        // Should maintain insertion order (General was first, then Zebra, Apple, Banana)
        topics[0].Name.ShouldBe("General");
        topics[1].Name.ShouldBe("Zebra");
        topics[2].Name.ShouldBe("Apple");
        topics[3].Name.ShouldBe("Banana");
    }

    [Fact]
    public void AddTopicEntry_ShouldNotAddDuplicateFile_WhenFileExistsInTopic()
    {
        // Arrange
        var config = CreateTestConfig();
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        // Act - Add same file twice
        _journalConfiguration.AddTopicEntry(
            _testDirectory,
            ["Tech"],
            "First Entry",
            "Tech-Tutorial.md"
        );
        _journalConfiguration.AddTopicEntry(
            _testDirectory,
            ["Tech"],
            "Second Entry",
            "Tech-Tutorial.md"
        );

        // Assert
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        var techTopic = updatedConfig.TableOfContents.Structure.Topics.FirstOrDefault(t =>
            t.Name == "Tech"
        );
        techTopic.ShouldNotBeNull();
        techTopic.Entries.Length.ShouldBe(1);
        techTopic.Entries[0].Name.ShouldBe("First Entry");
    }

    [Fact]
    public void AddTopicEntry_ShouldBeCaseInsensitive_WhenCheckingDuplicates()
    {
        // Arrange
        var config = CreateTestConfig();
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        // Act
        _journalConfiguration.AddTopicEntry(_testDirectory, ["Tech"], "First", "Tech-Tutorial.md");
        _journalConfiguration.AddTopicEntry(_testDirectory, ["Tech"], "Second", "TECH-TUTORIAL.MD");

        // Assert
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        var techTopic = updatedConfig.TableOfContents.Structure.Topics.FirstOrDefault(t =>
            t.Name == "Tech"
        );
        techTopic.ShouldNotBeNull();
        techTopic.Entries.Length.ShouldBe(1);
    }

    [Fact]
    public void AddTopicEntry_ShouldRespectMaxDepth_WhenDepthExceeded()
    {
        // Arrange
        var config = CreateTestConfig();
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        // Act - Try to add with depth 4 but max depth 2
        _journalConfiguration.AddTopicEntry(
            _testDirectory,
            ["Level1", "Level2", "Level3", "Level4"],
            "Deep Entry",
            "Deep.md",
            maxDepth: 2
        );

        // Assert
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        // Should not create any new topics since depth was exceeded
        updatedConfig.TableOfContents.Structure.Topics.Length.ShouldBe(1);
    }

    [Fact]
    public void AddTopicEntry_ShouldAllowUnlimitedDepth_WhenMaxDepthIsNull()
    {
        // Arrange
        var config = CreateTestConfig();
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        // Act - Add very deep hierarchy
        _journalConfiguration.AddTopicEntry(
            _testDirectory,
            ["L1", "L2", "L3", "L4", "L5", "L6"],
            "Very Deep Entry",
            "Deep.md",
            maxDepth: null
        );

        // Assert
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();

        // Navigate to deepest level
        var l1 = updatedConfig.TableOfContents.Structure.Topics.FirstOrDefault(t => t.Name == "L1");
        l1.ShouldNotBeNull();
        var l2 = l1.Subtopics?.FirstOrDefault(t => t.Name == "L2");
        l2.ShouldNotBeNull();
        var l3 = l2.Subtopics?.FirstOrDefault(t => t.Name == "L3");
        l3.ShouldNotBeNull();
        var l4 = l3.Subtopics?.FirstOrDefault(t => t.Name == "L4");
        l4.ShouldNotBeNull();
        var l5 = l4.Subtopics?.FirstOrDefault(t => t.Name == "L5");
        l5.ShouldNotBeNull();
        var l6 = l5.Subtopics?.FirstOrDefault(t => t.Name == "L6");
        l6.ShouldNotBeNull();
        l6.Entries.Length.ShouldBe(1);
        l6.Entries[0].Name.ShouldBe("Very Deep Entry");
    }

    [Fact]
    public void AddTopicEntry_ShouldHandleEmptyTopicPath()
    {
        // Arrange
        var config = CreateTestConfig();
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        // Act
        _journalConfiguration.AddTopicEntry(_testDirectory, [], "Entry", "File.md");

        // Assert - Should not modify config
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        updatedConfig.TableOfContents.Structure.Topics.Length.ShouldBe(1);
    }

    [Fact]
    public void AddTopicEntry_ShouldReuseExistingTopics_WhenTopicPathPartiallyExists()
    {
        // Arrange
        var config = CreateTestConfig();
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        // Act - Add entries with overlapping paths
        _journalConfiguration.AddTopicEntry(
            _testDirectory,
            ["Programming", "Languages"],
            "Python",
            "Programming-Languages-Python.md"
        );
        _journalConfiguration.AddTopicEntry(
            _testDirectory,
            ["Programming", "Languages"],
            "JavaScript",
            "Programming-Languages-JavaScript.md"
        );
        _journalConfiguration.AddTopicEntry(
            _testDirectory,
            ["Programming", "Tools"],
            "VSCode",
            "Programming-Tools-VSCode.md"
        );

        // Assert
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();

        var programmingTopic = updatedConfig.TableOfContents.Structure.Topics.FirstOrDefault(t =>
            t.Name == "Programming"
        );
        programmingTopic.ShouldNotBeNull();
        programmingTopic.Subtopics.ShouldNotBeNull();
        programmingTopic.Subtopics!.Length.ShouldBe(2);

        var languagesTopic = programmingTopic.Subtopics!.FirstOrDefault(t => t.Name == "Languages");
        languagesTopic.ShouldNotBeNull();
        languagesTopic.Entries.Length.ShouldBe(2);

        var toolsTopic = programmingTopic.Subtopics!.FirstOrDefault(t => t.Name == "Tools");
        toolsTopic.ShouldNotBeNull();
        toolsTopic.Entries.Length.ShouldBe(1);
    }

    #endregion

    #region Read Tests

    [Fact]
    public void Read_ShouldReturnConfig_WhenFileExists()
    {
        // Arrange
        var config = CreateTestConfig();
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var configJson = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", configJson);

        // Act
        var result = _journalConfiguration.Read(_testDirectory);

        // Assert
        result.ShouldNotBeNull();
        result.JournalName.ShouldBe("Test Journal");
        result.TableOfContents.File.ShouldBe("toc.md");
    }

    [Fact]
    public void Read_ShouldReturnNull_WhenFileDoesNotExist()
    {
        // Act
        var result = _journalConfiguration.Read(_testDirectory);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void Read_ShouldReturnNull_WhenJsonIsInvalid()
    {
        // Arrange
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        _fileSystem.CreateFile(_testDirectory, ".journalrc", "invalid json");

        // Act
        var result = _journalConfiguration.Read(_testDirectory);

        // Assert
        result.ShouldBeNull();
    }

    #endregion

    #region UpdateEntryName Tests

    [Fact]
    public void UpdateEntryName_ShouldUpdateRootEntry_WhenFileExistsInRootEntries()
    {
        // Arrange
        var config = CreateTestConfig();
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        // Act
        var result = _journalConfiguration.UpdateEntryName(
            _testDirectory,
            "1a-home.md",
            "Updated Home"
        );

        // Assert
        result.ShouldBeTrue();
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        updatedConfig.TableOfContents.RootEntries.Length.ShouldBe(1);
        updatedConfig.TableOfContents.RootEntries[0].Name.ShouldBe("Updated Home");
        updatedConfig.TableOfContents.RootEntries[0].File.ShouldBe("1a-home.md");
    }

    [Fact]
    public void UpdateEntryName_ShouldUpdateTopicEntry_WhenFileExistsInTopic()
    {
        // Arrange
        var config = CreateTestConfig();
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        // Add an entry first
        _journalConfiguration.AddTopicEntry(
            _testDirectory,
            ["Tech"],
            "Original Name",
            "Tech-Tutorial.md"
        );

        // Act
        var result = _journalConfiguration.UpdateEntryName(
            _testDirectory,
            "Tech-Tutorial.md",
            "Updated Tutorial"
        );

        // Assert
        result.ShouldBeTrue();
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        var techTopic = updatedConfig.TableOfContents.Structure.Topics.FirstOrDefault(t =>
            t.Name == "Tech"
        );
        techTopic.ShouldNotBeNull();
        techTopic.Entries.Length.ShouldBe(1);
        techTopic.Entries[0].Name.ShouldBe("Updated Tutorial");
        techTopic.Entries[0].File.ShouldBe("Tech-Tutorial.md");
    }

    [Fact]
    public void UpdateEntryName_ShouldUpdateDeeplyNestedEntry_WhenFileExistsInSubtopic()
    {
        // Arrange
        var config = CreateTestConfig();
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        // Add deeply nested entry
        _journalConfiguration.AddTopicEntry(
            _testDirectory,
            ["pirates", "swords", "cutlass", "pirates who owned"],
            "Jack Sparrow",
            "pirates-swords-cutlass-pirates_who_owned-Jack_Sparrow.md"
        );

        // Act
        var result = _journalConfiguration.UpdateEntryName(
            _testDirectory,
            "pirates-swords-cutlass-pirates_who_owned-Jack_Sparrow.md",
            "Captain Jack Sparrow"
        );

        // Assert
        result.ShouldBeTrue();
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();

        // Navigate to the deeply nested entry
        var piratesTopic = updatedConfig.TableOfContents.Structure.Topics.FirstOrDefault(t =>
            t.Name == "pirates"
        );
        var swordsTopic = piratesTopic?.Subtopics?.FirstOrDefault(t => t.Name == "swords");
        var cutlassTopic = swordsTopic?.Subtopics?.FirstOrDefault(t => t.Name == "cutlass");
        var piratesWhoOwnedTopic = cutlassTopic?.Subtopics?.FirstOrDefault(t =>
            t.Name == "pirates who owned"
        );

        piratesWhoOwnedTopic.ShouldNotBeNull();
        piratesWhoOwnedTopic.Entries.Length.ShouldBe(1);
        piratesWhoOwnedTopic.Entries[0].Name.ShouldBe("Captain Jack Sparrow");
        piratesWhoOwnedTopic
            .Entries[0]
            .File.ShouldBe("pirates-swords-cutlass-pirates_who_owned-Jack_Sparrow.md");
    }

    [Fact]
    public void UpdateEntryName_ShouldBeCaseInsensitive_WhenSearchingForFile()
    {
        // Arrange
        var config = CreateTestConfig();
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        _journalConfiguration.AddTopicEntry(
            _testDirectory,
            ["Tech"],
            "Original",
            "Tech-Tutorial.md"
        );

        // Act - Search with different case
        var result = _journalConfiguration.UpdateEntryName(
            _testDirectory,
            "TECH-TUTORIAL.MD",
            "Updated Name"
        );

        // Assert
        result.ShouldBeTrue();
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        var techTopic = updatedConfig.TableOfContents.Structure.Topics.FirstOrDefault(t =>
            t.Name == "Tech"
        );
        techTopic.ShouldNotBeNull();
        techTopic.Entries[0].Name.ShouldBe("Updated Name");
    }

    [Fact]
    public void UpdateEntryName_ShouldReturnFalse_WhenFileDoesNotExist()
    {
        // Arrange
        var config = CreateTestConfig();
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        // Act
        var result = _journalConfiguration.UpdateEntryName(
            _testDirectory,
            "NonExistent.md",
            "New Name"
        );

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void UpdateEntryName_ShouldReturnFalse_WhenConfigDoesNotExist()
    {
        // Act
        var result = _journalConfiguration.UpdateEntryName(
            _testDirectory,
            "any-file.md",
            "New Name"
        );

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void UpdateEntryName_ShouldUpdateFirstOccurrence_WhenMultipleTopicsExist()
    {
        // Arrange
        var config = CreateTestConfig();
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        // Add entries in different topics
        _journalConfiguration.AddTopicEntry(
            _testDirectory,
            ["Tech", "Languages"],
            "Python Basics",
            "Tech-Languages-Python.md"
        );
        _journalConfiguration.AddTopicEntry(
            _testDirectory,
            ["Learning"],
            "Advanced Python",
            "Learning-Python.md"
        );

        // Act - Update the first one added
        var result = _journalConfiguration.UpdateEntryName(
            _testDirectory,
            "Tech-Languages-Python.md",
            "Python 101"
        );

        // Assert
        result.ShouldBeTrue();
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();

        var techTopic = updatedConfig.TableOfContents.Structure.Topics.FirstOrDefault(t =>
            t.Name == "Tech"
        );
        var languagesTopic = techTopic?.Subtopics?.FirstOrDefault(t => t.Name == "Languages");
        languagesTopic.ShouldNotBeNull();
        languagesTopic.Entries[0].Name.ShouldBe("Python 101");

        // Verify the other entry wasn't changed
        var learningTopic = updatedConfig.TableOfContents.Structure.Topics.FirstOrDefault(t =>
            t.Name == "Learning"
        );
        learningTopic.ShouldNotBeNull();
        learningTopic.Entries[0].Name.ShouldBe("Advanced Python");
    }

    [Fact]
    public void UpdateEntryName_ShouldPreserveOtherProperties_WhenUpdating()
    {
        // Arrange
        var config = CreateTestConfig();
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        _journalConfiguration.AddTopicEntry(
            _testDirectory,
            ["Tech"],
            "Original",
            "Tech-Tutorial.md"
        );
        _journalConfiguration.AddTopicEntry(
            _testDirectory,
            ["Tech"],
            "Another Entry",
            "Tech-Another.md"
        );

        // Act
        var result = _journalConfiguration.UpdateEntryName(
            _testDirectory,
            "Tech-Tutorial.md",
            "Updated"
        );

        // Assert
        result.ShouldBeTrue();
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();

        // Verify journal name and other properties are unchanged
        updatedConfig.JournalName.ShouldBe("Test Journal");
        updatedConfig.TableOfContents.File.ShouldBe("toc.md");

        var techTopic = updatedConfig.TableOfContents.Structure.Topics.FirstOrDefault(t =>
            t.Name == "Tech"
        );
        techTopic.ShouldNotBeNull();
        techTopic.Entries.Length.ShouldBe(2);

        // Entries are sorted alphabetically by file name
        techTopic.Entries[0].File.ShouldBe("Tech-Another.md");
        techTopic.Entries[0].Name.ShouldBe("Another Entry");
        techTopic.Entries[1].File.ShouldBe("Tech-Tutorial.md");
        techTopic.Entries[1].Name.ShouldBe("Updated");
    }

    #endregion

    #region AddEntry Tests

    [Theory]
    [InlineData("1a")]
    [InlineData("2b")]
    [InlineData("5h")]
    [InlineData("9z")]
    [InlineData("1A")]
    [InlineData("9Z")]
    [InlineData("3z-test_file")]
    [InlineData("1a-Introduction")]
    [InlineData("readme")]
    [InlineData("README")]
    [InlineData("ReadMe")]
    public void AddEntry_ShouldAddAsRootEntry_WhenFileNameMatchesRootPattern(string fileName)
    {
        // Arrange
        var config = CreateTestConfig();
        config.TableOfContents.RootEntries = [];
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        // Act
        _journalConfiguration.AddEntry(_testDirectory, "Root Entry", $"{fileName}.md");

        // Assert
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        updatedConfig.TableOfContents.RootEntries.Length.ShouldBe(1);
        updatedConfig.TableOfContents.RootEntries[0].Name.ShouldBe("Root Entry");
        updatedConfig.TableOfContents.RootEntries[0].File.ShouldBe($"{fileName}.md");
    }

    [Theory]
    [InlineData("0a")] // 0 not allowed
    [InlineData("10a")] // multiple digits
    [InlineData("1")] // missing letter
    [InlineData("a1")] // reversed order
    [InlineData("1ab")] // multiple letters
    [InlineData("learning")] // full word
    [InlineData("Tech-Tutorial")]
    [InlineData("3zebra")] // looks like root but continues with letters
    [InlineData("3z_ebra")] // has underscore instead of hyphen
    [InlineData("2bapple")] // looks like root but continues
    public void AddEntry_ShouldAddAsTopicEntry_WhenFileNameDoesNotMatchRootPattern(string fileName)
    {
        // Arrange
        var config = CreateTestConfig();
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        // Act
        _journalConfiguration.AddEntry(
            _testDirectory,
            "Topic Entry",
            $"{fileName}.md",
            topicPath: ["Learning"]
        );

        // Assert
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();

        var learningTopic = updatedConfig.TableOfContents.Structure.Topics.FirstOrDefault(t =>
            t.Name == "Learning"
        );
        learningTopic.ShouldNotBeNull();
        learningTopic.Entries.Length.ShouldBe(1);
        learningTopic.Entries[0].Name.ShouldBe("Topic Entry");
        learningTopic.Entries[0].File.ShouldBe($"{fileName}.md");
    }

    [Fact]
    public void AddEntry_ShouldAddAsRootEntry_WithFullPathInFileName()
    {
        // Arrange
        var config = CreateTestConfig();
        config.TableOfContents.RootEntries = [];
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        // Act
        _journalConfiguration.AddEntry(_testDirectory, "Root Entry", "some/path/3c.md");

        // Assert
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        updatedConfig.TableOfContents.RootEntries.Length.ShouldBe(1);
        updatedConfig.TableOfContents.RootEntries[0].Name.ShouldBe("Root Entry");
        updatedConfig.TableOfContents.RootEntries[0].File.ShouldBe("some/path/3c.md");
    }

    [Fact]
    public void AddEntry_ShouldNotAddTopicEntry_WhenTopicPathIsNull()
    {
        // Arrange
        var config = CreateTestConfig();
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        // Act
        _journalConfiguration.AddEntry(
            _testDirectory,
            "Topic Entry",
            "learning.md",
            topicPath: null
        );

        // Assert
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        // Should parse filename and create "learning" topic
        var learningTopic = updatedConfig.TableOfContents.Structure.Topics.FirstOrDefault(t =>
            t.Name == "learning"
        );
        learningTopic.ShouldNotBeNull();
        learningTopic.Entries.Length.ShouldBe(1);
        learningTopic.Entries[0].Name.ShouldBe("Topic Entry");
        learningTopic.Entries[0].File.ShouldBe("learning.md");
    }

    [Fact]
    public void AddEntry_ShouldNotAddTopicEntry_WhenTopicPathIsEmpty()
    {
        // Arrange
        var config = CreateTestConfig();
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        // Act
        _journalConfiguration.AddEntry(_testDirectory, "Topic Entry", "learning.md", topicPath: []);

        // Assert
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        // Should parse filename and create "learning" topic
        var learningTopic = updatedConfig.TableOfContents.Structure.Topics.FirstOrDefault(t =>
            t.Name == "learning"
        );
        learningTopic.ShouldNotBeNull();
        learningTopic.Entries.Length.ShouldBe(1);
        learningTopic.Entries[0].Name.ShouldBe("Topic Entry");
        learningTopic.Entries[0].File.ShouldBe("learning.md");
    }

    [Fact]
    public void AddEntry_ShouldParseTopicFromFilename_WithUnderscores()
    {
        // Arrange
        var config = CreateTestConfig();
        config.TableOfContents.Structure.Topics = [];
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        // Act - filename with underscores should be converted to spaces
        _journalConfiguration.AddEntry(_testDirectory, "New Entry", "new_entry.md");

        // Assert
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        var newEntryTopic = updatedConfig.TableOfContents.Structure.Topics.FirstOrDefault(t =>
            t.Name == "new entry"
        );
        newEntryTopic.ShouldNotBeNull();
        newEntryTopic.Entries.Length.ShouldBe(1);
        newEntryTopic.Entries[0].Name.ShouldBe("New Entry");
        newEntryTopic.Entries[0].File.ShouldBe("new_entry.md");
    }

    [Fact]
    public void AddEntry_ShouldParseNestedTopicsFromFilename_WithHyphens()
    {
        // Arrange
        var config = CreateTestConfig();
        config.TableOfContents.Structure.Topics = [];
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        // Act - filename with hyphens should create nested topics
        _journalConfiguration.AddEntry(_testDirectory, "Tutorial", "Learning-Rust-Tutorial.md");

        // Assert
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        var learningTopic = updatedConfig.TableOfContents.Structure.Topics.FirstOrDefault(t =>
            t.Name == "Learning"
        );
        learningTopic.ShouldNotBeNull();
        learningTopic.Subtopics.ShouldNotBeNull();

        var rustTopic = learningTopic.Subtopics.FirstOrDefault(t => t.Name == "Rust");
        rustTopic.ShouldNotBeNull();
        rustTopic.Entries.Length.ShouldBe(1);
        rustTopic.Entries[0].Name.ShouldBe("Tutorial");
        rustTopic.Entries[0].File.ShouldBe("Learning-Rust-Tutorial.md");
    }

    [Fact]
    public void AddEntry_ShouldParseTopicFromFilename_WithBothSeparators()
    {
        // Arrange
        var config = CreateTestConfig();
        config.TableOfContents.Structure.Topics = [];
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        // Act - filename with both underscores and hyphens
        _journalConfiguration.AddEntry(
            _testDirectory,
            "Rust Programming",
            "Learning-Rust_Programming.md"
        );

        // Assert
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        var learningTopic = updatedConfig.TableOfContents.Structure.Topics.FirstOrDefault(t =>
            t.Name == "Learning"
        );
        learningTopic.ShouldNotBeNull();
        learningTopic.Entries.Length.ShouldBe(1);
        learningTopic.Entries[0].Name.ShouldBe("Rust Programming");
        learningTopic.Entries[0].File.ShouldBe("Learning-Rust_Programming.md");
    }

    [Fact]
    public void AddEntry_ShouldPassThroughParameters_ToAddTopicEntry()
    {
        // Arrange
        var config = CreateTestConfig();
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        // Act - Add multiple entries with sorting
        _journalConfiguration.AddEntry(
            _testDirectory,
            "Z Entry",
            "learning-z.md",
            topicPath: ["Learning"],
            sortAlphabetically: true
        );
        _journalConfiguration.AddEntry(
            _testDirectory,
            "A Entry",
            "learning-a.md",
            topicPath: ["Learning"],
            sortAlphabetically: true
        );

        // Assert
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();

        var learningTopic = updatedConfig.TableOfContents.Structure.Topics.FirstOrDefault(t =>
            t.Name == "Learning"
        );
        learningTopic.ShouldNotBeNull();
        learningTopic.Entries.Length.ShouldBe(2);
        // Should be sorted alphabetically by file name
        learningTopic.Entries[0].File.ShouldBe("learning-a.md");
        learningTopic.Entries[1].File.ShouldBe("learning-z.md");
    }

    #endregion

    #region AddIgnoreEntry Tests

    [Fact]
    public void AddIgnoreEntry_ShouldAddFileToIgnoreList_WhenFileDoesNotExist()
    {
        // Arrange
        var config = CreateTestConfig();
        config.TableOfContents.IgnoreFiles = [];
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        // Act
        _journalConfiguration.AddIgnoreEntry(_testDirectory, "ignored-file.md");

        // Assert
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        updatedConfig.TableOfContents.IgnoreFiles.ShouldNotBeNull();
        updatedConfig.TableOfContents.IgnoreFiles.Length.ShouldBe(1);
        updatedConfig.TableOfContents.IgnoreFiles[0].ShouldBe("ignored-file.md");
    }

    [Fact]
    public void AddIgnoreEntry_ShouldNotAddDuplicate_WhenFileAlreadyExists()
    {
        // Arrange
        var config = CreateTestConfig();
        config.TableOfContents.IgnoreFiles = ["existing-file.md"];
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        // Act
        _journalConfiguration.AddIgnoreEntry(_testDirectory, "existing-file.md");

        // Assert
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        updatedConfig.TableOfContents.IgnoreFiles.ShouldNotBeNull();
        updatedConfig.TableOfContents.IgnoreFiles.Length.ShouldBe(1);
        updatedConfig.TableOfContents.IgnoreFiles[0].ShouldBe("existing-file.md");
    }

    [Fact]
    public void AddIgnoreEntry_ShouldHandleCaseInsensitiveDuplicates()
    {
        // Arrange
        var config = CreateTestConfig();
        config.TableOfContents.IgnoreFiles = ["existing-file.md"];
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        // Act
        _journalConfiguration.AddIgnoreEntry(_testDirectory, "EXISTING-FILE.MD");

        // Assert
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        updatedConfig.TableOfContents.IgnoreFiles.ShouldNotBeNull();
        updatedConfig.TableOfContents.IgnoreFiles.Length.ShouldBe(1);
        updatedConfig.TableOfContents.IgnoreFiles[0].ShouldBe("existing-file.md");
    }

    [Fact]
    public void AddIgnoreEntry_ShouldAddMultipleFiles()
    {
        // Arrange
        var config = CreateTestConfig();
        config.TableOfContents.IgnoreFiles = [];
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        // Act
        _journalConfiguration.AddIgnoreEntry(_testDirectory, "file1.md");
        _journalConfiguration.AddIgnoreEntry(_testDirectory, "file2.md");
        _journalConfiguration.AddIgnoreEntry(_testDirectory, "file3.md");

        // Assert
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        updatedConfig.TableOfContents.IgnoreFiles.ShouldNotBeNull();
        updatedConfig.TableOfContents.IgnoreFiles.Length.ShouldBe(3);
        updatedConfig.TableOfContents.IgnoreFiles.ShouldContain("file1.md");
        updatedConfig.TableOfContents.IgnoreFiles.ShouldContain("file2.md");
        updatedConfig.TableOfContents.IgnoreFiles.ShouldContain("file3.md");
    }

    [Fact]
    public void AddIgnoreEntry_ShouldInitializeIgnoreFilesArray_WhenNull()
    {
        // Arrange
        var config = CreateTestConfig();
        config.TableOfContents.IgnoreFiles = null;
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        // Act
        _journalConfiguration.AddIgnoreEntry(_testDirectory, "new-ignored-file.md");

        // Assert
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        updatedConfig.TableOfContents.IgnoreFiles.ShouldNotBeNull();
        updatedConfig.TableOfContents.IgnoreFiles.Length.ShouldBe(1);
        updatedConfig.TableOfContents.IgnoreFiles[0].ShouldBe("new-ignored-file.md");
    }

    #endregion

    #region AddEntry TOC File Exclusion Tests

    [Fact]
    public void AddEntry_ShouldSkipTocFile_WhenAddingRootEntry()
    {
        // Arrange
        var config = CreateTestConfig();
        config.TableOfContents.File = "toc.md";
        config.TableOfContents.RootEntries = [];
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        // Act - try to add TOC file as root entry
        _journalConfiguration.AddEntry(_testDirectory, "Table of Contents", "toc.md");

        // Assert
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        updatedConfig.TableOfContents.RootEntries.Length.ShouldBe(0);
    }

    [Fact]
    public void AddEntry_ShouldSkipTocFile_WhenAddingTopicEntry()
    {
        // Arrange
        var config = CreateTestConfig();
        config.TableOfContents.File = "toc.md";
        config.TableOfContents.Structure.Topics = [];
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        // Act - try to add TOC file as topic entry
        _journalConfiguration.AddEntry(
            _testDirectory,
            "Table of Contents",
            "toc.md",
            topicPath: ["General"]
        );

        // Assert
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        updatedConfig.TableOfContents.Structure.Topics.Length.ShouldBe(0);
    }

    [Fact]
    public void AddEntry_ShouldSkipTocFile_CaseInsensitive()
    {
        // Arrange
        var config = CreateTestConfig();
        config.TableOfContents.File = "toc.md";
        config.TableOfContents.RootEntries = [];
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        // Act - try to add TOC file with different casing
        _journalConfiguration.AddEntry(_testDirectory, "Table of Contents", "TOC.md");
        _journalConfiguration.AddEntry(_testDirectory, "Table of Contents", "Toc.MD");

        // Assert
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        updatedConfig.TableOfContents.RootEntries.Length.ShouldBe(0);
    }

    [Fact]
    public void AddEntry_ShouldSkipDefaultTocFile_WhenConfigFileNotSpecified()
    {
        // Arrange
        var settings = new JournalSettings
        {
            JournalConfigFileName = ".journalrc",
            TableOfContentsFileName = "toc", // Default TOC filename without extension
        };
        var journalConfig = new JournalConfiguration(
            _fileSystem,
            Options.Create(settings),
            NullLogger<JournalConfiguration>.Instance,
            Mock.Of<IFileTracking>()
        );

        var config = CreateTestConfig();
        config.TableOfContents.File = null!; // Config doesn't specify TOC file
        config.TableOfContents.RootEntries = [];
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        // Act - try to add default TOC file
        journalConfig.AddEntry(_testDirectory, "Table of Contents", "toc.md");

        // Assert
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        updatedConfig.TableOfContents.RootEntries.Length.ShouldBe(0);
    }

    [Fact]
    public void AddEntry_ShouldAddFile_WhenNotTocFile()
    {
        // Arrange
        var config = CreateTestConfig();
        config.TableOfContents.File = "toc.md";
        config.TableOfContents.RootEntries = [];
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        // Act - add a different file that's not the TOC
        _journalConfiguration.AddEntry(_testDirectory, "Intro", "1a-intro.md");

        // Assert
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        updatedConfig.TableOfContents.RootEntries.Length.ShouldBe(1);
        updatedConfig.TableOfContents.RootEntries[0].File.ShouldBe("1a-intro.md");
    }

    #endregion

    #region AddEntry with ignoreFile Tests

    [Fact]
    public void AddEntry_ShouldAddToIgnoreList_WhenIgnoreFileIsTrue()
    {
        // Arrange
        var config = CreateTestConfig();
        config.TableOfContents.RootEntries = [];
        config.TableOfContents.IgnoreFiles = [];
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        // Act
        _journalConfiguration.AddEntry(
            _testDirectory,
            "Root Entry",
            "1a-Introduction.md",
            ignoreFile: true
        );

        // Assert
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        // Should NOT be added to root entries (only to ignore list)
        updatedConfig.TableOfContents.RootEntries.Length.ShouldBe(0);
        // Should be added to ignore files
        updatedConfig.TableOfContents.IgnoreFiles.ShouldNotBeNull();
        updatedConfig.TableOfContents.IgnoreFiles.Length.ShouldBe(1);
        updatedConfig.TableOfContents.IgnoreFiles[0].ShouldBe("1a-Introduction.md");
    }

    [Fact]
    public void AddEntry_ShouldNotAddToIgnoreList_WhenIgnoreFileIsFalse()
    {
        // Arrange
        var config = CreateTestConfig();
        config.TableOfContents.RootEntries = [];
        config.TableOfContents.IgnoreFiles = [];
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        // Act
        _journalConfiguration.AddEntry(
            _testDirectory,
            "Root Entry",
            "1a-Introduction.md",
            ignoreFile: false
        );

        // Assert
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        // Should be added to root entries
        updatedConfig.TableOfContents.RootEntries.Length.ShouldBe(1);
        updatedConfig.TableOfContents.RootEntries[0].File.ShouldBe("1a-Introduction.md");
        // But NOT to ignore files
        updatedConfig.TableOfContents.IgnoreFiles.ShouldNotBeNull();
        updatedConfig.TableOfContents.IgnoreFiles.Length.ShouldBe(0);
    }

    [Fact]
    public void AddEntry_ShouldAddTopicEntryToIgnoreList_WhenIgnoreFileIsTrue()
    {
        // Arrange
        var config = CreateTestConfig();
        config.TableOfContents.Structure.Topics = [];
        config.TableOfContents.IgnoreFiles = [];
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        // Act
        _journalConfiguration.AddEntry(
            _testDirectory,
            "Topic Entry",
            "Learning-Rust.md",
            topicPath: ["Learning"],
            ignoreFile: true
        );

        // Assert
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        // Should NOT be added to topic structure (only to ignore list)
        updatedConfig.TableOfContents.Structure.Topics.Length.ShouldBe(0);
        // Should be added to ignore files
        updatedConfig.TableOfContents.IgnoreFiles.ShouldNotBeNull();
        updatedConfig.TableOfContents.IgnoreFiles.Length.ShouldBe(1);
        updatedConfig.TableOfContents.IgnoreFiles[0].ShouldBe("Learning-Rust.md");
    }

    [Fact]
    public void AddEntry_ShouldNotDuplicateInIgnoreList_WhenAlreadyIgnored()
    {
        // Arrange
        var config = CreateTestConfig();
        config.TableOfContents.RootEntries = [];
        config.TableOfContents.IgnoreFiles = ["1a-Introduction.md"];
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        // Act
        _journalConfiguration.AddEntry(
            _testDirectory,
            "Root Entry",
            "1a-Introduction.md",
            ignoreFile: true
        );

        // Assert
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        // Should NOT be added to root entries (only to ignore list)
        updatedConfig.TableOfContents.RootEntries.Length.ShouldBe(0);
        // Should NOT duplicate in ignore files
        updatedConfig.TableOfContents.IgnoreFiles.ShouldNotBeNull();
        updatedConfig.TableOfContents.IgnoreFiles.Length.ShouldBe(1);
        updatedConfig.TableOfContents.IgnoreFiles[0].ShouldBe("1a-Introduction.md");
    }

    #endregion

    #region Natural Sort Tests

    [Fact]
    public void AddTopicEntry_ShouldUseNaturalSort_ForNumericFilenames()
    {
        // Arrange
        var config = CreateTestConfig();
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        // Act - Add entries with numeric filenames in non-sorted order
        _journalConfiguration.AddTopicEntry(
            _testDirectory,
            ["Test"],
            "Entry 10",
            "test_file_10.md"
        );
        _journalConfiguration.AddTopicEntry(_testDirectory, ["Test"], "Entry 5", "test_file_5.md");
        _journalConfiguration.AddTopicEntry(
            _testDirectory,
            ["Test"],
            "Entry 100",
            "test_file_100.md"
        );
        _journalConfiguration.AddTopicEntry(_testDirectory, ["Test"], "Entry 1", "test_file_1.md");
        _journalConfiguration.AddTopicEntry(
            _testDirectory,
            ["Test"],
            "Entry 20",
            "test_file_20.md"
        );

        // Assert
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        var testTopic = updatedConfig.TableOfContents.Structure.Topics.FirstOrDefault(t =>
            t.Name == "Test"
        );
        testTopic.ShouldNotBeNull();
        testTopic.Entries.Length.ShouldBe(5);

        // Should be sorted naturally: 1, 5, 10, 20, 100
        testTopic.Entries[0].File.ShouldBe("test_file_1.md");
        testTopic.Entries[1].File.ShouldBe("test_file_5.md");
        testTopic.Entries[2].File.ShouldBe("test_file_10.md");
        testTopic.Entries[3].File.ShouldBe("test_file_20.md");
        testTopic.Entries[4].File.ShouldBe("test_file_100.md");
    }

    [Fact]
    public void AddTopicEntry_ShouldUseNaturalSort_ForMixedAlphanumeric()
    {
        // Arrange
        var config = CreateTestConfig();
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        // Act - Add entries with mixed alphanumeric patterns
        _journalConfiguration.AddTopicEntry(_testDirectory, ["Code"], "Chapter 2", "chapter2.md");
        _journalConfiguration.AddTopicEntry(_testDirectory, ["Code"], "Chapter 10", "chapter10.md");
        _journalConfiguration.AddTopicEntry(_testDirectory, ["Code"], "Chapter 1", "chapter1.md");
        _journalConfiguration.AddTopicEntry(_testDirectory, ["Code"], "Chapter 20", "chapter20.md");

        // Assert
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        var codeTopic = updatedConfig.TableOfContents.Structure.Topics.FirstOrDefault(t =>
            t.Name == "Code"
        );
        codeTopic.ShouldNotBeNull();
        codeTopic.Entries.Length.ShouldBe(4);

        // Should be sorted naturally: chapter1, chapter2, chapter10, chapter20
        codeTopic.Entries[0].File.ShouldBe("chapter1.md");
        codeTopic.Entries[1].File.ShouldBe("chapter2.md");
        codeTopic.Entries[2].File.ShouldBe("chapter10.md");
        codeTopic.Entries[3].File.ShouldBe("chapter20.md");
    }

    [Fact]
    public void AddTopicEntry_ShouldUseNaturalSort_CaseInsensitive()
    {
        // Arrange
        var config = CreateTestConfig();
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        // Act - Add entries with different cases
        _journalConfiguration.AddTopicEntry(_testDirectory, ["Files"], "Entry Z", "File_10.md");
        _journalConfiguration.AddTopicEntry(_testDirectory, ["Files"], "Entry A", "file_2.md");
        _journalConfiguration.AddTopicEntry(_testDirectory, ["Files"], "Entry B", "FILE_1.md");

        // Assert
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        var filesTopic = updatedConfig.TableOfContents.Structure.Topics.FirstOrDefault(t =>
            t.Name == "Files"
        );
        filesTopic.ShouldNotBeNull();
        filesTopic.Entries.Length.ShouldBe(3);

        // Should be sorted case-insensitively with natural numeric sorting
        filesTopic.Entries[0].File.ShouldBe("FILE_1.md");
        filesTopic.Entries[1].File.ShouldBe("file_2.md");
        filesTopic.Entries[2].File.ShouldBe("File_10.md");
    }

    [Fact]
    public void AddTopicEntry_ShouldUseNaturalSort_ForTopicNames()
    {
        // Arrange
        var config = CreateTestConfig();
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        // Act - Add topics with numeric names
        _journalConfiguration.AddTopicEntry(
            _testDirectory,
            ["Topic 10"],
            "Entry",
            "topic10-entry.md"
        );
        _journalConfiguration.AddTopicEntry(
            _testDirectory,
            ["Topic 2"],
            "Entry",
            "topic2-entry.md"
        );
        _journalConfiguration.AddTopicEntry(
            _testDirectory,
            ["Topic 1"],
            "Entry",
            "topic1-entry.md"
        );
        _journalConfiguration.AddTopicEntry(
            _testDirectory,
            ["Topic 20"],
            "Entry",
            "topic20-entry.md"
        );

        // Assert
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        var topics = updatedConfig.TableOfContents.Structure.Topics;

        // Find the "Topic X" entries (excluding "General")
        var topicEntries = topics.Where(t => t.Name.StartsWith("Topic")).ToArray();
        topicEntries.Length.ShouldBe(4);

        // Should be sorted naturally: Topic 1, Topic 2, Topic 10, Topic 20
        topicEntries[0].Name.ShouldBe("Topic 1");
        topicEntries[1].Name.ShouldBe("Topic 2");
        topicEntries[2].Name.ShouldBe("Topic 10");
        topicEntries[3].Name.ShouldBe("Topic 20");
    }

    [Fact]
    public void AddTopicEntry_ShouldUseNaturalSort_WithLeadingZeros()
    {
        // Arrange
        var config = CreateTestConfig();
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { WriteIndented = true }
        );
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);

        // Act - Add entries with leading zeros
        _journalConfiguration.AddTopicEntry(_testDirectory, ["Docs"], "Entry 10", "doc_010.md");
        _journalConfiguration.AddTopicEntry(_testDirectory, ["Docs"], "Entry 2", "doc_002.md");
        _journalConfiguration.AddTopicEntry(_testDirectory, ["Docs"], "Entry 100", "doc_100.md");
        _journalConfiguration.AddTopicEntry(_testDirectory, ["Docs"], "Entry 1", "doc_001.md");

        // Assert
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        var docsTopic = updatedConfig.TableOfContents.Structure.Topics.FirstOrDefault(t =>
            t.Name == "Docs"
        );
        docsTopic.ShouldNotBeNull();
        docsTopic.Entries.Length.ShouldBe(4);

        // Should be sorted naturally by numeric value: 1, 2, 10, 100
        docsTopic.Entries[0].File.ShouldBe("doc_001.md");
        docsTopic.Entries[1].File.ShouldBe("doc_002.md");
        docsTopic.Entries[2].File.ShouldBe("doc_010.md");
        docsTopic.Entries[3].File.ShouldBe("doc_100.md");
    }

    #endregion

    #region RemoveEntry Tests

    [Fact]
    public void RemoveEntry_ShouldRemoveRootEntry()
    {
        // Arrange
        var config = CreateTestConfig();
        _journalConfiguration.Create(_testDirectory, config);

        // Act
        var result = _journalConfiguration.RemoveEntry(_testDirectory, "1a-home.md");

        // Assert
        result.ShouldBeTrue();
        var updatedConfig = _journalConfiguration.Read(_testDirectory);
        updatedConfig.ShouldNotBeNull();
        updatedConfig.TableOfContents.RootEntries.ShouldBeEmpty();
    }

    [Fact]
    public void RemoveEntry_ShouldRemoveTopicEntry()
    {
        // Arrange
        var config = new JournalConfig
        {
            JournalName = "Test",
            TableOfContents = new TableOfContents
            {
                File = "toc.md",
                Extensions = [".md"],
                Structure = new Structure
                {
                    Topics =
                    [
                        new Topic
                        {
                            Name = "Learning",
                            Entries =
                            [
                                new Entries { Name = "Rust", File = "Learning-Rust.md" },
                                new Entries { Name = "Go", File = "Learning-Go.md" },
                            ],
                        },
                    ],
                },
                RootEntries = [],
            },
        };
        _journalConfiguration.Create(_testDirectory, config);

        // Act
        var result = _journalConfiguration.RemoveEntry(_testDirectory, "Learning-Rust.md");

        // Assert
        result.ShouldBeTrue();
        var updated = _journalConfiguration.Read(_testDirectory);
        updated.ShouldNotBeNull();
        var topic = updated.TableOfContents.Structure.Topics.FirstOrDefault(t =>
            t.Name == "Learning"
        );
        topic.ShouldNotBeNull();
        topic.Entries.Length.ShouldBe(1);
        topic.Entries[0].File.ShouldBe("Learning-Go.md");
    }

    [Fact]
    public void RemoveEntry_ShouldCleanUpEmptyTopics()
    {
        // Arrange
        var config = new JournalConfig
        {
            JournalName = "Test",
            TableOfContents = new TableOfContents
            {
                File = "toc.md",
                Extensions = [".md"],
                Structure = new Structure
                {
                    Topics =
                    [
                        new Topic
                        {
                            Name = "OnlyTopic",
                            Entries =
                            [
                                new Entries
                                {
                                    Name = "Only Entry",
                                    File = "OnlyTopic-Only_Entry.md",
                                },
                            ],
                        },
                    ],
                },
                RootEntries = [],
            },
        };
        _journalConfiguration.Create(_testDirectory, config);

        // Act
        var result = _journalConfiguration.RemoveEntry(_testDirectory, "OnlyTopic-Only_Entry.md");

        // Assert
        result.ShouldBeTrue();
        var updated = _journalConfiguration.Read(_testDirectory);
        updated.ShouldNotBeNull();
        updated.TableOfContents.Structure.Topics.ShouldBeEmpty();
    }

    [Fact]
    public void RemoveEntry_ShouldReturnFalse_WhenEntryNotFound()
    {
        // Arrange
        var config = CreateTestConfig();
        _journalConfiguration.Create(_testDirectory, config);

        // Act
        var result = _journalConfiguration.RemoveEntry(_testDirectory, "nonexistent.md");

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void RemoveEntry_ShouldRemoveFromSubtopics()
    {
        // Arrange
        var config = new JournalConfig
        {
            JournalName = "Test",
            TableOfContents = new TableOfContents
            {
                File = "toc.md",
                Extensions = [".md"],
                Structure = new Structure
                {
                    Topics =
                    [
                        new Topic
                        {
                            Name = "Programming",
                            Entries = [],
                            Subtopics =
                            [
                                new Topic
                                {
                                    Name = "Rust",
                                    Entries =
                                    [
                                        new Entries
                                        {
                                            Name = "Basics",
                                            File = "Programming-Rust-Basics.md",
                                        },
                                    ],
                                },
                            ],
                        },
                    ],
                },
                RootEntries = [],
            },
        };
        _journalConfiguration.Create(_testDirectory, config);

        // Act
        var result = _journalConfiguration.RemoveEntry(
            _testDirectory,
            "Programming-Rust-Basics.md"
        );

        // Assert
        result.ShouldBeTrue();
        var updated = _journalConfiguration.Read(_testDirectory);
        updated.ShouldNotBeNull();
        // Both the subtopic and parent should be cleaned up since they're now empty
        updated.TableOfContents.Structure.Topics.ShouldBeEmpty();
    }

    [Fact]
    public void RemoveEntry_IsCaseInsensitive()
    {
        // Arrange
        var config = CreateTestConfig();
        _journalConfiguration.Create(_testDirectory, config);

        // Act
        var result = _journalConfiguration.RemoveEntry(_testDirectory, "1A-HOME.MD");

        // Assert
        result.ShouldBeTrue();
        var updated = _journalConfiguration.Read(_testDirectory);
        updated.ShouldNotBeNull();
        updated.TableOfContents.RootEntries.ShouldBeEmpty();
    }

    #endregion

    #region RegenerateStructure Tests

    [Fact]
    public void RegenerateStructure_ShouldRebuildFromFileList()
    {
        // Arrange
        var config = CreateTestConfig();
        _journalConfiguration.Create(_testDirectory, config);

        var files = new[] { "1b-Introduction.md", "Learning-Rust.md", "Learning-Go.md" };

        // Act
        _journalConfiguration.RegenerateStructure(_testDirectory, files);

        // Assert
        var updated = _journalConfiguration.Read(_testDirectory);
        updated.ShouldNotBeNull();
        updated.TableOfContents.RootEntries.Length.ShouldBe(1);
        updated.TableOfContents.RootEntries[0].File.ShouldBe("1b-Introduction.md");

        var learningTopic = updated.TableOfContents.Structure.Topics.FirstOrDefault(t =>
            t.Name == "Learning"
        );
        learningTopic.ShouldNotBeNull();
        learningTopic.Entries.Length.ShouldBe(2);
    }

    [Fact]
    public void RegenerateStructure_ShouldPreserveJournalName()
    {
        // Arrange
        var config = new JournalConfig
        {
            JournalName = "My Custom Journal",
            TableOfContents = new TableOfContents
            {
                File = "toc.md",
                Extensions = [".md"],
                IgnoreFiles = ["secret.md"],
                Structure = new Structure { Topics = [] },
                RootEntries = [new Entries { Name = "Old", File = "old.md" }],
            },
        };
        _journalConfiguration.Create(_testDirectory, config);

        // Act
        _journalConfiguration.RegenerateStructure(_testDirectory, ["1a-Home.md"]);

        // Assert
        var updated = _journalConfiguration.Read(_testDirectory);
        updated.ShouldNotBeNull();
        updated.JournalName.ShouldBe("My Custom Journal");
        updated.TableOfContents.IgnoreFiles.ShouldContain("secret.md");
    }

    [Fact]
    public void RegenerateStructure_ShouldClearOldEntries()
    {
        // Arrange
        var config = new JournalConfig
        {
            JournalName = "Test",
            TableOfContents = new TableOfContents
            {
                File = "toc.md",
                Extensions = [".md"],
                Structure = new Structure
                {
                    Topics =
                    [
                        new Topic
                        {
                            Name = "OldTopic",
                            Entries = [new Entries { Name = "Old", File = "OldTopic-Old.md" }],
                        },
                    ],
                },
                RootEntries = [new Entries { Name = "OldRoot", File = "1a-OldRoot.md" }],
            },
        };
        _journalConfiguration.Create(_testDirectory, config);

        // Act — regenerate with completely different files
        _journalConfiguration.RegenerateStructure(_testDirectory, ["NewTopic-New_Entry.md"]);

        // Assert
        var updated = _journalConfiguration.Read(_testDirectory);
        updated.ShouldNotBeNull();
        updated.TableOfContents.RootEntries.ShouldBeEmpty();
        updated.TableOfContents.Structure.Topics.Any(t => t.Name == "OldTopic").ShouldBeFalse();
        updated.TableOfContents.Structure.Topics.Any(t => t.Name == "NewTopic").ShouldBeTrue();
    }

    #endregion

    #region TOC File Auto-Cleanup Tests

    [Fact]
    public void Update_ShouldRemoveTocFileFromEntries_WhenTocFileChanges()
    {
        // Arrange - create config with "old-toc.md" as a root entry
        var config = CreateTestConfig();
        config.TableOfContents.File = "toc.md";
        config.TableOfContents.RootEntries =
        [
            new Entries { Name = "Home", File = "1a-home.md" },
            new Entries { Name = "Old TOC", File = "old-toc.md" },
        ];
        _journalConfiguration.Create(_testDirectory, config);

        // Act - change TOC file to "old-toc.md"
        _journalConfiguration.Update(
            _testDirectory,
            c =>
            {
                c.TableOfContents.File = "old-toc.md";
            }
        );

        // Assert - "old-toc.md" should be removed from root entries
        var updated = _journalConfiguration.Read(_testDirectory);
        updated.ShouldNotBeNull();
        updated.TableOfContents.File.ShouldBe("old-toc.md");
        updated.TableOfContents.RootEntries.Length.ShouldBe(1);
        updated.TableOfContents.RootEntries[0].File.ShouldBe("1a-home.md");
    }

    [Fact]
    public void Update_ShouldRemoveTocFileFromTopics_WhenTocFileChanges()
    {
        // Arrange - create config with "new-toc.md" as a topic entry
        var config = CreateTestConfig();
        config.TableOfContents.File = "toc.md";
        config.TableOfContents.Structure.Topics =
        [
            new Topic
            {
                Name = "newtoc",
                Entries = [new Entries { Name = "Newtoc", File = "new-toc.md" }],
                Subtopics = null,
            },
        ];
        _journalConfiguration.Create(_testDirectory, config);

        // Act - change TOC file to "new-toc.md"
        _journalConfiguration.Update(
            _testDirectory,
            c =>
            {
                c.TableOfContents.File = "new-toc.md";
            }
        );

        // Assert - "new-toc.md" should be removed from topics, and empty topic should be cleaned up
        var updated = _journalConfiguration.Read(_testDirectory);
        updated.ShouldNotBeNull();
        updated.TableOfContents.File.ShouldBe("new-toc.md");
        updated.TableOfContents.Structure.Topics.ShouldBeEmpty();
    }

    [Fact]
    public void Update_ShouldNotRemoveEntries_WhenTocFileDoesNotChange()
    {
        // Arrange
        var config = CreateTestConfig();
        config.TableOfContents.File = "toc.md";
        config.TableOfContents.RootEntries =
        [
            new Entries { Name = "Home", File = "1a-home.md" },
            new Entries { Name = "Other", File = "other.md" },
        ];
        _journalConfiguration.Create(_testDirectory, config);

        // Act - update something else, not TOC file
        _journalConfiguration.Update(
            _testDirectory,
            c =>
            {
                c.JournalName = "New Name";
            }
        );

        // Assert - entries should remain unchanged
        var updated = _journalConfiguration.Read(_testDirectory);
        updated.ShouldNotBeNull();
        updated.TableOfContents.RootEntries.Length.ShouldBe(2);
    }

    [Fact]
    public void Update_ShouldHandleCaseInsensitive_WhenRemovingTocFile()
    {
        // Arrange - create config with "NewTOC.md" as entry (different casing)
        var config = CreateTestConfig();
        config.TableOfContents.File = "toc.md";
        config.TableOfContents.RootEntries =
        [
            new Entries { Name = "Home", File = "1a-home.md" },
            new Entries { Name = "New TOC", File = "NewTOC.md" },
        ];
        _journalConfiguration.Create(_testDirectory, config);

        // Act - change TOC file to "newtoc.md" (different casing)
        _journalConfiguration.Update(
            _testDirectory,
            c =>
            {
                c.TableOfContents.File = "newtoc.md";
            }
        );

        // Assert - "NewTOC.md" should be removed despite case difference
        var updated = _journalConfiguration.Read(_testDirectory);
        updated.ShouldNotBeNull();
        updated.TableOfContents.RootEntries.Length.ShouldBe(1);
        updated.TableOfContents.RootEntries[0].File.ShouldBe("1a-home.md");
    }

    #endregion

    #region FindEntry Tests

    [Fact]
    public void FindEntry_ShouldReturnRootEntry_WhenFileIsInRootEntries()
    {
        // Arrange
        var config = CreateTestConfig();
        config.TableOfContents.RootEntries =
        [
            new Entries { Name = "Home", File = "1a-home.md" },
            new Entries { Name = "Introduction", File = "1b-intro.md" },
        ];
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        _fileSystem.CreateFile(
            _testDirectory,
            ".journalrc",
            JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true })
        );

        // Act
        var (entry, topicPath) = _journalConfiguration.FindEntry(_testDirectory, "1a-home.md");

        // Assert
        entry.ShouldNotBeNull();
        entry.Name.ShouldBe("Home");
        entry.File.ShouldBe("1a-home.md");
        topicPath.ShouldBeEmpty();
    }

    [Fact]
    public void FindEntry_ShouldReturnTopicEntry_WhenFileIsInTopic()
    {
        // Arrange
        var config = CreateTestConfig();
        config.TableOfContents.Structure.Topics =
        [
            new Topic
            {
                Name = "Learning",
                Entries = [new Entries { Name = "Rust", File = "learning-rust.md" }],
                Subtopics = null,
            },
        ];
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        _fileSystem.CreateFile(
            _testDirectory,
            ".journalrc",
            JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true })
        );

        // Act
        var (entry, topicPath) = _journalConfiguration.FindEntry(
            _testDirectory,
            "learning-rust.md"
        );

        // Assert
        entry.ShouldNotBeNull();
        entry.Name.ShouldBe("Rust");
        entry.File.ShouldBe("learning-rust.md");
        topicPath.Length.ShouldBe(1);
        topicPath[0].ShouldBe("Learning");
    }

    [Fact]
    public void FindEntry_ShouldReturnNestedEntry_WhenFileIsInSubtopic()
    {
        // Arrange
        var config = CreateTestConfig();
        config.TableOfContents.Structure.Topics =
        [
            new Topic
            {
                Name = "Projects",
                Entries = [],
                Subtopics =
                [
                    new Topic
                    {
                        Name = "2024",
                        Entries = [new Entries { Name = "CLI Tool", File = "cli-tool.md" }],
                        Subtopics = null,
                    },
                ],
            },
        ];
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        _fileSystem.CreateFile(
            _testDirectory,
            ".journalrc",
            JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true })
        );

        // Act
        var (entry, topicPath) = _journalConfiguration.FindEntry(_testDirectory, "cli-tool.md");

        // Assert
        entry.ShouldNotBeNull();
        entry.Name.ShouldBe("CLI Tool");
        entry.File.ShouldBe("cli-tool.md");
        topicPath.Length.ShouldBe(2);
        topicPath[0].ShouldBe("Projects");
        topicPath[1].ShouldBe("2024");
    }

    [Fact]
    public void FindEntry_ShouldBeCaseInsensitive()
    {
        // Arrange
        var config = CreateTestConfig();
        config.TableOfContents.RootEntries = [new Entries { Name = "Home", File = "1a-home.md" }];
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        _fileSystem.CreateFile(
            _testDirectory,
            ".journalrc",
            JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true })
        );

        // Act
        var (entry, topicPath) = _journalConfiguration.FindEntry(_testDirectory, "1A-HOME.MD");

        // Assert
        entry.ShouldNotBeNull();
        entry.Name.ShouldBe("Home");
        entry.File.ShouldBe("1a-home.md");
    }

    [Fact]
    public void FindEntry_ShouldReturnNull_WhenFileNotFound()
    {
        // Arrange
        var config = CreateTestConfig();
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        _fileSystem.CreateFile(
            _testDirectory,
            ".journalrc",
            JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true })
        );

        // Act
        var (entry, topicPath) = _journalConfiguration.FindEntry(
            _testDirectory,
            "nonexistent.md"
        );

        // Assert
        entry.ShouldBeNull();
        topicPath.ShouldBeEmpty();
    }

    [Fact]
    public void FindEntry_ShouldReturnNull_WhenConfigDoesNotExist()
    {
        // Act
        var (entry, topicPath) = _journalConfiguration.FindEntry(_testDirectory, "any-file.md");

        // Assert
        entry.ShouldBeNull();
        topicPath.ShouldBeEmpty();
    }

    #endregion

    #region UpdateFileReferences Tests

    [Fact]
    public void UpdateFileReferences_ShouldUpdateRootEntry()
    {
        // Arrange
        var config = CreateTestConfig();
        config.TableOfContents.RootEntries =
        [
            new Entries { Name = "Home", File = "old-name.md" },
            new Entries { Name = "Other", File = "other.md" },
        ];
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        _fileSystem.CreateFile(
            _testDirectory,
            ".journalrc",
            JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true })
        );

        // Act
        _journalConfiguration.UpdateFileReferences(
            _testDirectory,
            "old-name.md",
            "new-name.md"
        );

        // Assert
        var updated = _journalConfiguration.Read(_testDirectory);
        updated.ShouldNotBeNull();
        updated.TableOfContents.RootEntries.Length.ShouldBe(2);
        updated.TableOfContents.RootEntries[0].File.ShouldBe("new-name.md");
        updated.TableOfContents.RootEntries[1].File.ShouldBe("other.md");
    }

    [Fact]
    public void UpdateFileReferences_ShouldUpdateTopicEntry()
    {
        // Arrange
        var config = CreateTestConfig();
        config.TableOfContents.Structure.Topics =
        [
            new Topic
            {
                Name = "Learning",
                Entries =
                [
                    new Entries { Name = "Rust", File = "old-rust.md" },
                    new Entries { Name = "Python", File = "python.md" },
                ],
                Subtopics = null,
            },
        ];
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        _fileSystem.CreateFile(
            _testDirectory,
            ".journalrc",
            JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true })
        );

        // Act
        _journalConfiguration.UpdateFileReferences(_testDirectory, "old-rust.md", "new-rust.md");

        // Assert
        var updated = _journalConfiguration.Read(_testDirectory);
        updated.ShouldNotBeNull();
        var learningTopic = updated.TableOfContents.Structure.Topics.First(t =>
            t.Name == "Learning"
        );
        learningTopic.Entries.Length.ShouldBe(2);
        learningTopic.Entries[0].File.ShouldBe("new-rust.md");
        learningTopic.Entries[1].File.ShouldBe("python.md");
    }

    [Fact]
    public void UpdateFileReferences_ShouldUpdateNestedEntry()
    {
        // Arrange
        var config = CreateTestConfig();
        config.TableOfContents.Structure.Topics =
        [
            new Topic
            {
                Name = "Projects",
                Entries = [],
                Subtopics =
                [
                    new Topic
                    {
                        Name = "2024",
                        Entries = [new Entries { Name = "CLI", File = "old-cli.md" }],
                        Subtopics = null,
                    },
                ],
            },
        ];
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        _fileSystem.CreateFile(
            _testDirectory,
            ".journalrc",
            JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true })
        );

        // Act
        _journalConfiguration.UpdateFileReferences(_testDirectory, "old-cli.md", "new-cli.md");

        // Assert
        var updated = _journalConfiguration.Read(_testDirectory);
        updated.ShouldNotBeNull();
        var projectsTopic = updated.TableOfContents.Structure.Topics.First(t =>
            t.Name == "Projects"
        );
        var subtopic = projectsTopic.Subtopics![0];
        subtopic.Entries[0].File.ShouldBe("new-cli.md");
    }

    [Fact]
    public void UpdateFileReferences_ShouldUpdateIgnoreList()
    {
        // Arrange
        var config = CreateTestConfig();
        config.TableOfContents.IgnoreFiles = ["old-ignored.md", "other-ignored.md"];
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        _fileSystem.CreateFile(
            _testDirectory,
            ".journalrc",
            JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true })
        );

        // Act
        _journalConfiguration.UpdateFileReferences(
            _testDirectory,
            "old-ignored.md",
            "new-ignored.md"
        );

        // Assert
        var updated = _journalConfiguration.Read(_testDirectory);
        updated.ShouldNotBeNull();
        updated.TableOfContents.IgnoreFiles.ShouldNotBeNull();
        updated.TableOfContents.IgnoreFiles.Length.ShouldBe(2);
        updated.TableOfContents.IgnoreFiles.ShouldContain("new-ignored.md");
        updated.TableOfContents.IgnoreFiles.ShouldContain("other-ignored.md");
    }

    [Fact]
    public void UpdateFileReferences_ShouldUpdateAllOccurrences()
    {
        // Arrange
        var config = CreateTestConfig();
        config.TableOfContents.RootEntries = [new Entries { Name = "Root", File = "file.md" }];
        config.TableOfContents.Structure.Topics =
        [
            new Topic
            {
                Name = "Topic",
                Entries = [new Entries { Name = "Entry", File = "file.md" }],
                Subtopics = null,
            },
        ];
        config.TableOfContents.IgnoreFiles = ["file.md"];
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        _fileSystem.CreateFile(
            _testDirectory,
            ".journalrc",
            JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true })
        );

        // Act
        _journalConfiguration.UpdateFileReferences(_testDirectory, "file.md", "renamed.md");

        // Assert
        var updated = _journalConfiguration.Read(_testDirectory);
        updated.ShouldNotBeNull();
        updated.TableOfContents.RootEntries[0].File.ShouldBe("renamed.md");
        updated.TableOfContents.Structure.Topics[0].Entries[0].File.ShouldBe("renamed.md");
        updated.TableOfContents.IgnoreFiles![0].ShouldBe("renamed.md");
    }

    [Fact]
    public void UpdateFileReferences_ShouldBeCaseInsensitive()
    {
        // Arrange
        var config = CreateTestConfig();
        config.TableOfContents.RootEntries = [new Entries { Name = "Home", File = "FILE.md" }];
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        _fileSystem.CreateFile(
            _testDirectory,
            ".journalrc",
            JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true })
        );

        // Act
        _journalConfiguration.UpdateFileReferences(_testDirectory, "file.MD", "renamed.md");

        // Assert
        var updated = _journalConfiguration.Read(_testDirectory);
        updated.ShouldNotBeNull();
        updated.TableOfContents.RootEntries[0].File.ShouldBe("renamed.md");
    }

    #endregion

    #region DetectConfigChanges

    private (JournalConfiguration config, FileTracking tracking) CreateConfigWithTracking(
        string? appName = "testapp"
    )
    {
        var settings = Options.Create(
            new JournalSettings
            {
                JournalConfigFileName = ".journalrc",
                AppName = appName ?? "testapp",
                TableOfContentsFileName = "1a-TableOfContents",
            }
        );
        var hashService = new markdown_journal_cli.Tests.Infrastructure.Tracking.TestHashService();
        var tracking = new FileTracking(_fileSystem, settings, hashService);
        var config = new JournalConfiguration(
            _fileSystem,
            settings,
            NullLogger<JournalConfiguration>.Instance,
            tracking
        );
        return (config, tracking);
    }

    private void SetupTrackingIndex(FileTracking tracking, string directory, params string[] files)
    {
        foreach (var file in files)
        {
            var fullPath = Path.Combine(directory, file);
            if (!_fileSystem.FileExists(fullPath))
                _fileSystem.CreateFile(directory, file, $"# {file}");
        }
        tracking.UpdateIndex(directory);
    }

    [Fact]
    public void DetectConfigChanges_ReturnsFilesToAdd_WhenTrackedFilesNotInConfig()
    {
        // Arrange
        var (config, tracking) = CreateConfigWithTracking();
        _fileSystem.CreateDirectory(_testDirectory);
        SetupTrackingIndex(tracking, _testDirectory, "Learning-Rust.md");
        config.Create(
            _testDirectory,
            new JournalConfig
            {
                JournalName = "Test",
                TableOfContents = new TableOfContents
                {
                    File = "1a-TableOfContents.md",
                    Structure = new Structure { Topics = [] },
                    RootEntries = [],
                },
            }
        );

        // Act
        var result = config.DetectConfigChanges(_testDirectory);

        // Assert
        result.FilesToAdd.ShouldContain("Learning-Rust.md");
        result.FilesToRemove.ShouldBeEmpty();
        result.HasChanges.ShouldBeTrue();
    }

    [Fact]
    public void DetectConfigChanges_ReturnsFilesToRemove_WhenConfigFilesNotInTracking()
    {
        // Arrange
        var (config, tracking) = CreateConfigWithTracking();
        _fileSystem.CreateDirectory(_testDirectory);
        tracking.UpdateIndex(_testDirectory); // empty index
        config.Create(
            _testDirectory,
            new JournalConfig
            {
                JournalName = "Test",
                TableOfContents = new TableOfContents
                {
                    File = "1a-TableOfContents.md",
                    Structure = new Structure { Topics = [] },
                    RootEntries = [new Entries { Name = "Note", File = "old-note.md" }],
                },
            }
        );

        // Act
        var result = config.DetectConfigChanges(_testDirectory);

        // Assert
        result.FilesToRemove.ShouldContain("old-note.md");
        result.FilesToAdd.ShouldBeEmpty();
        result.HasChanges.ShouldBeTrue();
    }

    [Fact]
    public void DetectConfigChanges_ExcludesTocFile_FromFilesToAdd()
    {
        // Arrange
        var (config, tracking) = CreateConfigWithTracking();
        _fileSystem.CreateDirectory(_testDirectory);
        SetupTrackingIndex(tracking, _testDirectory, "1a-TableOfContents.md");
        config.Create(
            _testDirectory,
            new JournalConfig
            {
                JournalName = "Test",
                TableOfContents = new TableOfContents
                {
                    File = "1a-TableOfContents.md",
                    Structure = new Structure { Topics = [] },
                    RootEntries = [],
                },
            }
        );

        // Act
        var result = config.DetectConfigChanges(_testDirectory);

        // Assert — the TOC file must never appear as a config entry to add
        result.FilesToAdd.ShouldNotContain("1a-TableOfContents.md");
        result.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void DetectConfigChanges_ReturnsEmpty_WhenConfigAndTrackingInSync()
    {
        // Arrange
        var (config, tracking) = CreateConfigWithTracking();
        _fileSystem.CreateDirectory(_testDirectory);
        SetupTrackingIndex(tracking, _testDirectory, "2a-NoteOne.md");
        config.Create(
            _testDirectory,
            new JournalConfig
            {
                JournalName = "Test",
                TableOfContents = new TableOfContents
                {
                    File = "1a-TableOfContents.md",
                    Structure = new Structure { Topics = [] },
                    RootEntries = [new Entries { Name = "NoteOne", File = "2a-NoteOne.md" }],
                },
            }
        );

        // Act
        var result = config.DetectConfigChanges(_testDirectory);

        // Assert
        result.HasChanges.ShouldBeFalse();
        result.FilesToAdd.ShouldBeEmpty();
        result.FilesToRemove.ShouldBeEmpty();
    }

    [Fact]
    public void DetectConfigChanges_ReturnsEmpty_WhenJournalrcDoesNotExist()
    {
        // Arrange
        var (config, tracking) = CreateConfigWithTracking();
        _fileSystem.CreateDirectory(_testDirectory);
        // No .journalrc created

        // Act — should not throw
        var result = config.DetectConfigChanges(_testDirectory);

        // Assert
        result.HasChanges.ShouldBeFalse();
        result.FilesToAdd.ShouldBeEmpty();
        result.FilesToRemove.ShouldBeEmpty();
    }

    [Fact]
    public void DetectConfigChanges_HandlesEmptyTrackingIndex()
    {
        // Arrange
        var (config, tracking) = CreateConfigWithTracking();
        _fileSystem.CreateDirectory(_testDirectory);
        tracking.UpdateIndex(_testDirectory); // empty index
        config.Create(
            _testDirectory,
            new JournalConfig
            {
                JournalName = "Test",
                TableOfContents = new TableOfContents
                {
                    File = "1a-TableOfContents.md",
                    Structure = new Structure { Topics = [] },
                    RootEntries =
                    [
                        new Entries { Name = "A", File = "2a-EntryA.md" },
                        new Entries { Name = "B", File = "2b-EntryB.md" },
                    ],
                },
            }
        );

        // Act
        var result = config.DetectConfigChanges(_testDirectory);

        // Assert — all config entries should appear in FilesToRemove
        result.FilesToRemove.ShouldContain("2a-EntryA.md");
        result.FilesToRemove.ShouldContain("2b-EntryB.md");
        result.FilesToAdd.ShouldBeEmpty();
    }

    [Fact]
    public void DetectConfigChanges_HandlesTopicEntries()
    {
        // Arrange
        var (config, tracking) = CreateConfigWithTracking();
        _fileSystem.CreateDirectory(_testDirectory);
        SetupTrackingIndex(tracking, _testDirectory);
        config.Create(
            _testDirectory,
            new JournalConfig
            {
                JournalName = "Test",
                TableOfContents = new TableOfContents
                {
                    File = "1a-TableOfContents.md",
                    Structure = new Structure
                    {
                        Topics =
                        [
                            new Topic
                            {
                                Name = "Learning",
                                Entries =
                                [
                                    new Entries { Name = "Rust", File = "Learning-Rust.md" },
                                ],
                                Subtopics = null,
                            },
                        ],
                    },
                    RootEntries = [],
                },
            }
        );
        // "Learning-Rust.md" is in config but NOT in tracking (empty index)

        // Act
        var result = config.DetectConfigChanges(_testDirectory);

        // Assert — topic entry should appear in FilesToRemove
        result.FilesToRemove.ShouldContain("Learning-Rust.md");
    }

    [Fact]
    public void DetectConfigChanges_HandlesSubtopicEntries()
    {
        // Arrange
        var (config, tracking) = CreateConfigWithTracking();
        _fileSystem.CreateDirectory(_testDirectory);
        SetupTrackingIndex(tracking, _testDirectory);
        config.Create(
            _testDirectory,
            new JournalConfig
            {
                JournalName = "Test",
                TableOfContents = new TableOfContents
                {
                    File = "1a-TableOfContents.md",
                    Structure = new Structure
                    {
                        Topics =
                        [
                            new Topic
                            {
                                Name = "Learning",
                                Entries = [],
                                Subtopics =
                                [
                                    new Topic
                                    {
                                        Name = "Rust",
                                        Entries =
                                        [
                                            new Entries
                                            {
                                                Name = "Ownership",
                                                File = "Learning-Rust-Ownership.md",
                                            },
                                        ],
                                        Subtopics = null,
                                    },
                                ],
                            },
                        ],
                    },
                    RootEntries = [],
                },
            }
        );
        // "Learning-Rust-Ownership.md" is in config subtopic but NOT in tracking

        // Act
        var result = config.DetectConfigChanges(_testDirectory);

        // Assert — subtopic entry should appear in FilesToRemove
        result.FilesToRemove.ShouldContain("Learning-Rust-Ownership.md");
    }

    [Fact]
    public void DetectConfigChanges_IsCaseInsensitive_ForFileComparison()
    {
        // Arrange — tracking has uppercase path, config has lowercase
        var (config, tracking) = CreateConfigWithTracking();
        _fileSystem.CreateDirectory(_testDirectory);
        SetupTrackingIndex(tracking, _testDirectory, "2a-Note.md");
        config.Create(
            _testDirectory,
            new JournalConfig
            {
                JournalName = "Test",
                TableOfContents = new TableOfContents
                {
                    File = "1a-TableOfContents.md",
                    Structure = new Structure { Topics = [] },
                    RootEntries = [new Entries { Name = "Note", File = "2A-NOTE.MD" }],
                },
            }
        );

        // Act — "2a-Note.md" (tracking) vs "2A-NOTE.MD" (config) — same file, different casing
        var result = config.DetectConfigChanges(_testDirectory);

        // Assert — treated as the same file, so no drift
        result.HasChanges.ShouldBeFalse();
    }

    #endregion
}

