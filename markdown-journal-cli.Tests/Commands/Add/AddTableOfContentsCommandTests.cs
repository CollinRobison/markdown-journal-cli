using markdown_journal_cli;
using markdown_journal_cli.Commands.Add;
using markdown_journal_cli.Exceptions;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.Configuration.Models;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.JournalTemplates;
using markdown_journal_cli.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Spectre.Console.Testing;
using Xunit;

namespace markdown_journal_cli.Tests.Commands.Add;

/// <summary>
/// Unit tests for AddTableOfContents command covering positive, negative, and edge cases.
/// </summary>
public class AddTableOfContentsCommandTests
{
    private readonly TestFileSystem _fileSystem;
    private readonly TestConsole _console;
    private readonly Mock<IJournalConfiguration> _mockJournalConfiguration;
    private readonly Mock<ITableOfContentsGenerator> _mockTocGenerator;
    private readonly JournalSettings _journalSettings;
    private readonly AddTableOfContents _command;

    public AddTableOfContentsCommandTests()
    {
        _fileSystem = new TestFileSystem();
        _console = new TestConsole();
        _mockJournalConfiguration = new Mock<IJournalConfiguration>();
        _mockTocGenerator = new Mock<ITableOfContentsGenerator>();
        _journalSettings = new JournalSettings
        {
            JournalConfigFileName = ".journalrc",
            TableOfContentsFileName = "TOC"
        };

        _command = new AddTableOfContents(
            _console,
            _fileSystem,
            _mockJournalConfiguration.Object,
            _mockTocGenerator.Object,
            Options.Create(_journalSettings)
        );
    }

    #region Positive Cases

    [Fact]
    public void Execute_SuccessfullyCreatesToc_WhenJournalrcExistsAndTocDoesNotExist()
    {
        // Arrange
        var directory = "/test/journal";
        var tocFile = $"{_journalSettings.TableOfContentsFileName}.md";
        
        _fileSystem.CreateDirectory(directory);
        _fileSystem.CreateFile(directory, _journalSettings.JournalConfigFileName, "{}");

        var config = new JournalConfig
        {
            TableOfContents = new TableOfContents { File = tocFile, Structure = new Structure { Topics = [] }, RootEntries = [] }
        };
        _mockJournalConfiguration.Setup(x => x.Read(directory)).Returns(config);

        var settings = new AddTableOfContentsSettings { FilePath = directory };

        // Act
        var result = _command.Execute(null!, settings);

        // Assert
        result.ShouldBe(0);
        _console.Output.ShouldContain("Success");
        _console.Output.ShouldContain("Created");
        _console.Output.ShouldContain(tocFile);
        _mockTocGenerator.Verify(x => x.UpdateTableOfContents(directory, null, null), Times.Once);
    }

    [Fact]
    public void Execute_UpdatesConfigAndCreatesToc_WhenConfigHasDifferentTocName()
    {
        // Arrange
        var directory = "/test/journal";
        var oldTocFile = "old-toc.md";
        var newTocFile = $"{_journalSettings.TableOfContentsFileName}.md";
        
        _fileSystem.CreateDirectory(directory);
        _fileSystem.CreateFile(directory, _journalSettings.JournalConfigFileName, "{}");

        var config = new JournalConfig
        {
            TableOfContents = new TableOfContents 
            { 
                File = oldTocFile,
                Structure = new Structure { Topics = [] },
                RootEntries = []
            }
        };
        _mockJournalConfiguration.Setup(x => x.Read(directory)).Returns(config);

        var settings = new AddTableOfContentsSettings { FilePath = directory };

        // Act
        var result = _command.Execute(null!, settings);

        // Assert
        result.ShouldBe(0);
        _console.Output.ShouldContain("Success");
        _console.Output.ShouldContain("Created");
        
        // Verify config was updated
        _mockJournalConfiguration.Verify(
            x => x.Update(directory, It.IsAny<Action<JournalConfig>>()), 
            Times.Once
        );
        
        _mockTocGenerator.Verify(x => x.UpdateTableOfContents(directory, null, null), Times.Once);
    }

    [Fact]
    public void Execute_UsesCustomTocName_WhenProvidedInSettings()
    {
        // Arrange
        var directory = "/test/journal";
        var customTocName = "CustomTableOfContents";
        var customTocFile = $"{customTocName}.md";
        
        _fileSystem.CreateDirectory(directory);
        _fileSystem.CreateFile(directory, _journalSettings.JournalConfigFileName, "{}");

        var config = new JournalConfig
        {
            TableOfContents = new TableOfContents { File = "TOC.md", Structure = new Structure { Topics = [] }, RootEntries = [] }
        };
        _mockJournalConfiguration.Setup(x => x.Read(directory)).Returns(config);

        var settings = new AddTableOfContentsSettings 
        { 
            FilePath = directory,
            TableOfContentsName = customTocName
        };

        // Act
        var result = _command.Execute(null!, settings);

        // Assert
        result.ShouldBe(0);
        _console.Output.ShouldContain(customTocFile);
        
        // Verify config was updated with custom name
        _mockJournalConfiguration.Verify(
            x => x.Update(directory, It.IsAny<Action<JournalConfig>>()), 
            Times.Once
        );
        
        _mockTocGenerator.Verify(x => x.UpdateTableOfContents(directory, null, null), Times.Once);
    }

    [Fact]
    public void Execute_DoesNotUpdateConfig_WhenTocNameMatchesConfig()
    {
        // Arrange
        var directory = "/test/journal";
        var tocFile = $"{_journalSettings.TableOfContentsFileName}.md";
        
        _fileSystem.CreateDirectory(directory);
        _fileSystem.CreateFile(directory, _journalSettings.JournalConfigFileName, "{}");

        var config = new JournalConfig
        {
            TableOfContents = new TableOfContents { File = tocFile, Structure = new Structure { Topics = [] }, RootEntries = [] }
        };
        _mockJournalConfiguration.Setup(x => x.Read(directory)).Returns(config);

        var settings = new AddTableOfContentsSettings { FilePath = directory };

        // Act
        var result = _command.Execute(null!, settings);

        // Assert
        result.ShouldBe(0);
        
        // Verify config was NOT updated
        _mockJournalConfiguration.Verify(
            x => x.Update(It.IsAny<string>(), It.IsAny<Action<JournalConfig>>()), 
            Times.Never
        );
        
        _mockTocGenerator.Verify(x => x.UpdateTableOfContents(directory, null, null), Times.Once);
    }

    #endregion

    #region Negative Cases

    [Fact]
    public void Execute_ReturnsWarning_WhenTocFileAlreadyExists()
    {
        // Arrange
        var directory = "/test/journal";
        var tocFile = $"{_journalSettings.TableOfContentsFileName}.md";
        
        _fileSystem.CreateDirectory(directory);
        _fileSystem.CreateFile(directory, _journalSettings.JournalConfigFileName, "{}");
        _fileSystem.CreateFile(directory, tocFile, "# Existing TOC");

        var config = new JournalConfig
        {
            TableOfContents = new TableOfContents { File = tocFile, Structure = new Structure { Topics = [] }, RootEntries = [] }
        };
        _mockJournalConfiguration.Setup(x => x.Read(directory)).Returns(config);

        var settings = new AddTableOfContentsSettings { FilePath = directory };

        // Act
        var result = _command.Execute(null!, settings);

        // Assert
        result.ShouldBe(0);
        _console.Output.ShouldContain("Warning");
        _console.Output.ShouldContain("already exists");
        
        // Verify TOC generator was NOT called
        _mockTocGenerator.Verify(
            x => x.UpdateTableOfContents(It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()), 
            Times.Never
        );
        
        // Verify config was NOT updated
        _mockJournalConfiguration.Verify(
            x => x.Update(It.IsAny<string>(), It.IsAny<Action<JournalConfig>>()), 
            Times.Never
        );
    }

    [Fact]
    public void Execute_ThrowsJournalrcNotFoundException_WhenJournalrcDoesNotExist()
    {
        // Arrange
        var directory = "/test/journal";
        _fileSystem.CreateDirectory(directory);

        var settings = new AddTableOfContentsSettings { FilePath = directory };

        // Act
        var result = _command.Execute(null!, settings);

        // Assert
        result.ShouldBe(1);
        _console.Output.ShouldContain("Error");
        _console.Output.ShouldContain("journalrc");
        
        _mockTocGenerator.Verify(
            x => x.UpdateTableOfContents(It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()), 
            Times.Never
        );
    }

    [Fact]
    public void Execute_ReturnsError_WhenJournalConfigReadReturnsNull()
    {
        // Arrange
        var directory = "/test/journal";
        
        _fileSystem.CreateDirectory(directory);
        _fileSystem.CreateFile(directory, _journalSettings.JournalConfigFileName, "{}");

        _mockJournalConfiguration.Setup(x => x.Read(directory)).Returns((JournalConfig?)null);

        var settings = new AddTableOfContentsSettings { FilePath = directory };

        // Act
        var result = _command.Execute(null!, settings);

        // Assert
        result.ShouldBe(1);
        _console.Output.ShouldContain("Error");
        _console.Output.ShouldContain("Failed to read journal configuration");
        
        _mockTocGenerator.Verify(
            x => x.UpdateTableOfContents(It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()), 
            Times.Never
        );
    }

    [Fact]
    public void Execute_ReturnsError_WhenTocGeneratorThrowsException()
    {
        // Arrange
        var directory = "/test/journal";
        var tocFile = $"{_journalSettings.TableOfContentsFileName}.md";
        
        _fileSystem.CreateDirectory(directory);
        _fileSystem.CreateFile(directory, _journalSettings.JournalConfigFileName, "{}");

        var config = new JournalConfig
        {
            TableOfContents = new TableOfContents { File = tocFile, Structure = new Structure { Topics = [] }, RootEntries = [] }
        };
        _mockJournalConfiguration.Setup(x => x.Read(directory)).Returns(config);
        _mockTocGenerator
            .Setup(x => x.UpdateTableOfContents(directory, null, null))
            .Throws(new InvalidOperationException("TOC generation failed"));

        var settings = new AddTableOfContentsSettings { FilePath = directory };

        // Act
        var result = _command.Execute(null!, settings);

        // Assert
        result.ShouldBe(1);
        _console.Output.ShouldContain("Error");
        _console.Output.ShouldContain("unexpected error");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Execute_HandlesDirectoryWithTrailingSlash()
    {
        // Arrange
        var directory = "/test/journal/";
        var tocFile = $"{_journalSettings.TableOfContentsFileName}.md";
        
        _fileSystem.CreateDirectory(directory);
        _fileSystem.CreateFile(directory, _journalSettings.JournalConfigFileName, "{}");

        var config = new JournalConfig
        {
            TableOfContents = new TableOfContents { File = tocFile, Structure = new Structure { Topics = [] }, RootEntries = [] }
        };
        _mockJournalConfiguration.Setup(x => x.Read(directory)).Returns(config);

        var settings = new AddTableOfContentsSettings { FilePath = directory };

        // Act
        var result = _command.Execute(null!, settings);

        // Assert
        result.ShouldBe(0);
        _console.Output.ShouldContain("Success");
    }

    [Fact]
    public void Execute_WarnsForExistingCustomNamedToc()
    {
        // Arrange
        var directory = "/test/journal";
        var customTocName = "MyCustomTOC";
        var customTocFile = $"{customTocName}.md";
        
        _fileSystem.CreateDirectory(directory);
        _fileSystem.CreateFile(directory, _journalSettings.JournalConfigFileName, "{}");
        _fileSystem.CreateFile(directory, customTocFile, "# Custom TOC");

        var config = new JournalConfig
        {
            TableOfContents = new TableOfContents { File = "TOC.md", Structure = new Structure { Topics = [] }, RootEntries = [] }
        };
        _mockJournalConfiguration.Setup(x => x.Read(directory)).Returns(config);

        var settings = new AddTableOfContentsSettings 
        { 
            FilePath = directory,
            TableOfContentsName = customTocName
        };

        // Act
        var result = _command.Execute(null!, settings);

        // Assert
        result.ShouldBe(0);
        _console.Output.ShouldContain("Warning");
        _console.Output.ShouldContain("already exists");
        _console.Output.ShouldContain(customTocFile);
        
        _mockTocGenerator.Verify(
            x => x.UpdateTableOfContents(It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()), 
            Times.Never
        );
    }

    #endregion
}
