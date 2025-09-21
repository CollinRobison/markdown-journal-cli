using System.Text.Json;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.Configuration.Objects;
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

    public JournalConfigurationTests()
    {
        _fileSystem = new TestFileSystem();
        _journalConfiguration = new JournalConfiguration(_fileSystem);
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
                    Topics = [
                        new Topic { Name = "General", Subtopics = null }
                    ]
                },
                IndexCache = new IndexCache
                {
                    UpdatedAt = DateTime.Now,
                    Topics = [
                        new Topic { Name = "General", Subtopics = null }
                    ]
                },
                RootEntries = [
                    new RootEntries { Name = "Home", File = "home.md" }
                ]
            }
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
        var originalJson = JsonSerializer.Serialize(originalConfig, new JsonSerializerOptions { WriteIndented = true });
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);
        
        // Act
        _journalConfiguration.Update(_testDirectory, config =>
        {
            config.JournalName = "Updated Journal Name";
            config.TableOfContents.File = "updated-toc.md";
        });
        
        // Assert
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        updatedContent.ShouldNotBeNull();
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);
        
        updatedConfig.ShouldNotBeNull();
        updatedConfig.JournalName.ShouldBe("Updated Journal Name");
        updatedConfig.TableOfContents.File.ShouldBe("updated-toc.md");
        // Verify other properties are preserved
        updatedConfig.TableOfContents.Extensions.ShouldBe(originalConfig.TableOfContents.Extensions);
    }

    [Fact]
    public void Update_ShouldPreserveUnmodifiedProperties()
    {
        // Arrange
        var originalConfig = CreateTestConfig();
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var originalJson = JsonSerializer.Serialize(originalConfig, new JsonSerializerOptions { WriteIndented = true });
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);
        
        // Act - Only modify one property
        _journalConfiguration.Update(_testDirectory, config =>
        {
            config.JournalName = "Only This Changed";
        });
        
        // Assert
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        updatedContent.ShouldNotBeNull();
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);
        
        updatedConfig.ShouldNotBeNull();
        updatedConfig.JournalName.ShouldBe("Only This Changed");
        // All other properties should remain unchanged
        updatedConfig.TableOfContents.File.ShouldBe(originalConfig.TableOfContents.File);
        updatedConfig.TableOfContents.Extensions.ShouldBe(originalConfig.TableOfContents.Extensions);
        updatedConfig.TableOfContents.Structure.Topics.Length.ShouldBe(originalConfig.TableOfContents.Structure.Topics.Length);
    }

    [Fact]
    public void Update_ShouldNotThrow_WhenFileDoesNotExist()
    {
        // Act & Assert
        Should.NotThrow(() => _journalConfiguration.Update(_testDirectory, config =>
        {
            config.JournalName = "This won't be applied";
        }));
    }

    [Fact]
    public void Update_ShouldHandleInvalidJson()
    {
        // Arrange
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        _fileSystem.CreateFile(_testDirectory, ".journalrc", "invalid json content");
        
        // Act & Assert
        Should.NotThrow(() => _journalConfiguration.Update(_testDirectory, config =>
        {
            config.JournalName = "This won't be applied";
        }));
        
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
        var originalJson = JsonSerializer.Serialize(originalConfig, new JsonSerializerOptions { WriteIndented = true });
        _fileSystem.CreateFile(_testDirectory, ".journalrc", originalJson);
        
        // Act
        _journalConfiguration.Update(journalrcPath, config =>
        {
            config.JournalName = "Updated via full path";
        });
        
        // Assert
        var updatedContent = _fileSystem.GetFileContent(journalrcPath);
        updatedContent.ShouldNotBeNull();
        var updatedConfig = JsonSerializer.Deserialize<JournalConfig>(updatedContent);
        
        updatedConfig.ShouldNotBeNull();
        updatedConfig.JournalName.ShouldBe("Updated via full path");
    }
}