using System.Text.Json;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.Configuration.Models;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace markdown_journal_cli.Tests.Infrastructure;

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
        _journalConfiguration = new JournalConfiguration(_fileSystem, _journalSettings);
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
                    Topics = [new Topic { Name = "General", Entries = [], Subtopics = null }],
                },
                RootEntries = [new Entries { Name = "Home", File = "home.md" }],
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
        _journalConfiguration.AddRootEntry(_testDirectory, "Home Again", "home.md");

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
        _journalConfiguration.AddRootEntry(_testDirectory, "Home Again", "HOME.MD");

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
        
        var learningTopic = updatedConfig.TableOfContents.Structure.Topics
            .FirstOrDefault(t => t.Name == "Learning");
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
        var piratesTopic = updatedConfig.TableOfContents.Structure.Topics
            .FirstOrDefault(t => t.Name == "pirates");
        piratesTopic.ShouldNotBeNull();
        piratesTopic.Subtopics.ShouldNotBeNull();
        
        var swordsTopic = piratesTopic.Subtopics!
            .FirstOrDefault(t => t.Name == "swords");
        swordsTopic.ShouldNotBeNull();
        swordsTopic.Subtopics.ShouldNotBeNull();
        
        var cutlassTopic = swordsTopic.Subtopics!
            .FirstOrDefault(t => t.Name == "cutlass");
        cutlassTopic.ShouldNotBeNull();
        cutlassTopic.Subtopics.ShouldNotBeNull();
        
        var piratesWhoOwnedTopic = cutlassTopic.Subtopics!
            .FirstOrDefault(t => t.Name == "pirates who owned");
        piratesWhoOwnedTopic.ShouldNotBeNull();
        piratesWhoOwnedTopic.Entries.Length.ShouldBe(1);
        piratesWhoOwnedTopic.Entries[0].Name.ShouldBe("Jack Sparrow");
        piratesWhoOwnedTopic.Entries[0].File.ShouldBe("pirates-swords-cutlass-pirates_who_owned-Jack_Sparrow.md");
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
        _journalConfiguration.AddTopicEntry(_testDirectory, ["Zebra"], "Z Entry", "Zebra-Z.md", sortAlphabetically: true);
        _journalConfiguration.AddTopicEntry(_testDirectory, ["Apple"], "A Entry", "Apple-A.md", sortAlphabetically: true);
        _journalConfiguration.AddTopicEntry(_testDirectory, ["Banana"], "B Entry", "Banana-B.md", sortAlphabetically: true);

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
        _journalConfiguration.AddTopicEntry(_testDirectory, ["Tech"], "Zebra Display Name", "Tech-Apple.md");
        _journalConfiguration.AddTopicEntry(_testDirectory, ["Tech"], "Alpha Display Name", "Tech-Zebra.md");
        _journalConfiguration.AddTopicEntry(_testDirectory, ["Tech"], "Middle Display Name", "Tech-Banana.md");

        // Assert
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        var techTopic = updatedConfig.TableOfContents.Structure.Topics
            .FirstOrDefault(t => t.Name == "Tech");
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
        _journalConfiguration.AddTopicEntry(_testDirectory, ["Zebra"], "Z Entry", "Zebra-Z.md", sortAlphabetically: false);
        _journalConfiguration.AddTopicEntry(_testDirectory, ["Apple"], "A Entry", "Apple-A.md", sortAlphabetically: false);
        _journalConfiguration.AddTopicEntry(_testDirectory, ["Banana"], "B Entry", "Banana-B.md", sortAlphabetically: false);

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
        _journalConfiguration.AddTopicEntry(_testDirectory, ["Tech"], "First Entry", "Tech-Tutorial.md");
        _journalConfiguration.AddTopicEntry(_testDirectory, ["Tech"], "Second Entry", "Tech-Tutorial.md");

        // Assert
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        var techTopic = updatedConfig.TableOfContents.Structure.Topics
            .FirstOrDefault(t => t.Name == "Tech");
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
        var techTopic = updatedConfig.TableOfContents.Structure.Topics
            .FirstOrDefault(t => t.Name == "Tech");
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
        _journalConfiguration.AddTopicEntry(
            _testDirectory,
            [],
            "Entry",
            "File.md"
        );

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
        _journalConfiguration.AddTopicEntry(_testDirectory, ["Programming", "Languages"], "Python", "Programming-Languages-Python.md");
        _journalConfiguration.AddTopicEntry(_testDirectory, ["Programming", "Languages"], "JavaScript", "Programming-Languages-JavaScript.md");
        _journalConfiguration.AddTopicEntry(_testDirectory, ["Programming", "Tools"], "VSCode", "Programming-Tools-VSCode.md");

        // Assert
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        
        var programmingTopic = updatedConfig.TableOfContents.Structure.Topics
            .FirstOrDefault(t => t.Name == "Programming");
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
        var result = _journalConfiguration.UpdateEntryName(_testDirectory, "home.md", "Updated Home");

        // Assert
        result.ShouldBeTrue();
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        updatedConfig.TableOfContents.RootEntries.Length.ShouldBe(1);
        updatedConfig.TableOfContents.RootEntries[0].Name.ShouldBe("Updated Home");
        updatedConfig.TableOfContents.RootEntries[0].File.ShouldBe("home.md");
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
        _journalConfiguration.AddTopicEntry(_testDirectory, ["Tech"], "Original Name", "Tech-Tutorial.md");

        // Act
        var result = _journalConfiguration.UpdateEntryName(_testDirectory, "Tech-Tutorial.md", "Updated Tutorial");

        // Assert
        result.ShouldBeTrue();
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        var techTopic = updatedConfig.TableOfContents.Structure.Topics
            .FirstOrDefault(t => t.Name == "Tech");
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
        var piratesTopic = updatedConfig.TableOfContents.Structure.Topics
            .FirstOrDefault(t => t.Name == "pirates");
        var swordsTopic = piratesTopic?.Subtopics?.FirstOrDefault(t => t.Name == "swords");
        var cutlassTopic = swordsTopic?.Subtopics?.FirstOrDefault(t => t.Name == "cutlass");
        var piratesWhoOwnedTopic = cutlassTopic?.Subtopics?.FirstOrDefault(t => t.Name == "pirates who owned");
        
        piratesWhoOwnedTopic.ShouldNotBeNull();
        piratesWhoOwnedTopic.Entries.Length.ShouldBe(1);
        piratesWhoOwnedTopic.Entries[0].Name.ShouldBe("Captain Jack Sparrow");
        piratesWhoOwnedTopic.Entries[0].File.ShouldBe("pirates-swords-cutlass-pirates_who_owned-Jack_Sparrow.md");
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

        _journalConfiguration.AddTopicEntry(_testDirectory, ["Tech"], "Original", "Tech-Tutorial.md");

        // Act - Search with different case
        var result = _journalConfiguration.UpdateEntryName(_testDirectory, "TECH-TUTORIAL.MD", "Updated Name");

        // Assert
        result.ShouldBeTrue();
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        var techTopic = updatedConfig.TableOfContents.Structure.Topics
            .FirstOrDefault(t => t.Name == "Tech");
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
        var result = _journalConfiguration.UpdateEntryName(_testDirectory, "NonExistent.md", "New Name");

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void UpdateEntryName_ShouldReturnFalse_WhenConfigDoesNotExist()
    {
        // Act
        var result = _journalConfiguration.UpdateEntryName(_testDirectory, "any-file.md", "New Name");

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
        _journalConfiguration.AddTopicEntry(_testDirectory, ["Tech", "Languages"], "Python Basics", "Tech-Languages-Python.md");
        _journalConfiguration.AddTopicEntry(_testDirectory, ["Learning"], "Advanced Python", "Learning-Python.md");

        // Act - Update the first one added
        var result = _journalConfiguration.UpdateEntryName(_testDirectory, "Tech-Languages-Python.md", "Python 101");

        // Assert
        result.ShouldBeTrue();
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        
        var techTopic = updatedConfig.TableOfContents.Structure.Topics
            .FirstOrDefault(t => t.Name == "Tech");
        var languagesTopic = techTopic?.Subtopics?.FirstOrDefault(t => t.Name == "Languages");
        languagesTopic.ShouldNotBeNull();
        languagesTopic.Entries[0].Name.ShouldBe("Python 101");
        
        // Verify the other entry wasn't changed
        var learningTopic = updatedConfig.TableOfContents.Structure.Topics
            .FirstOrDefault(t => t.Name == "Learning");
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

        _journalConfiguration.AddTopicEntry(_testDirectory, ["Tech"], "Original", "Tech-Tutorial.md");
        _journalConfiguration.AddTopicEntry(_testDirectory, ["Tech"], "Another Entry", "Tech-Another.md");

        // Act
        var result = _journalConfiguration.UpdateEntryName(_testDirectory, "Tech-Tutorial.md", "Updated");

        // Assert
        result.ShouldBeTrue();
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);

        updatedConfig.ShouldNotBeNull();
        
        // Verify journal name and other properties are unchanged
        updatedConfig.JournalName.ShouldBe("Test Journal");
        updatedConfig.TableOfContents.File.ShouldBe("toc.md");
        
        var techTopic = updatedConfig.TableOfContents.Structure.Topics
            .FirstOrDefault(t => t.Name == "Tech");
        techTopic.ShouldNotBeNull();
        techTopic.Entries.Length.ShouldBe(2);
        
        // Entries are sorted alphabetically by file name
        techTopic.Entries[0].File.ShouldBe("Tech-Another.md");
        techTopic.Entries[0].Name.ShouldBe("Another Entry");
        techTopic.Entries[1].File.ShouldBe("Tech-Tutorial.md");
        techTopic.Entries[1].Name.ShouldBe("Updated");
    }

    #endregion
}
