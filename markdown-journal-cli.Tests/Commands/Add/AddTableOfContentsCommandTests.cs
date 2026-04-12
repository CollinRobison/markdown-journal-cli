using markdown_journal_cli;
using markdown_journal_cli.Commands.Add;
using markdown_journal_cli.Exceptions;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.Configuration.Models;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.Transactions;
using markdown_journal_cli.Services;
using markdown_journal_cli.Tests.Infrastructure;
using markdown_journal_cli.Tests.Infrastructure.FileSystem;
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
public class AddTableOfContentsCommandTests : CommandTestBase
{
    private readonly TestFileSystem _fileSystem;
    private readonly TestConsole _console;
    private readonly JournalSettings _journalSettings;

    public AddTableOfContentsCommandTests()
    {
        _fileSystem = new TestFileSystem();
        _console = new TestConsole();
        _journalSettings = new JournalSettings
        {
            JournalConfigFileName = ".journalrc",
            TableOfContentsFileName = "TOC",
        };
    }

    private AddTableOfContents CreateCommand() =>
        new AddTableOfContents(
            _console,
            _fileSystem,
            MockJournalConfiguration.Object,
            MockTableOfContentsService.Object,
            Options.Create(_journalSettings),
            NoOpFileTransactionCoordinator.Instance,
            NoOpRollbackReporter.Instance
        );

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
            TableOfContents = new TableOfContents
            {
                File = tocFile,
                Structure = new Structure { Topics = [] },
                RootEntries = [],
            },
        };
        MockJournalConfiguration.Setup(x => x.Read(directory)).Returns(config);

        var settings = new AddTableOfContentsSettings { FilePath = directory };

        // Act
        var result = CreateCommand().Execute(null!, settings);

        // Assert
        result.ShouldBe(0);
        _console.Output.ShouldContain("Success");
        _console.Output.ShouldContain("Created");
        _console.Output.ShouldContain(tocFile);
        MockTableOfContentsService.Verify(
            x => x.UpdateTableOfContents(directory, It.IsAny<DateTime>(), It.IsAny<DateTime>()),
            Times.Once
        );
    }

    [Fact]
    public void Execute_UpdatesConfigAndCreatesToc_WhenConfigHasDifferentTocName()
    {
        // Arrange
        var directory = "/test/journal";
        var oldTocFile = "old-toc.md";

        _fileSystem.CreateDirectory(directory);
        _fileSystem.CreateFile(directory, _journalSettings.JournalConfigFileName, "{}");

        var config = new JournalConfig
        {
            TableOfContents = new TableOfContents
            {
                File = oldTocFile,
                Structure = new Structure { Topics = [] },
                RootEntries = [],
            },
        };
        MockJournalConfiguration.Setup(x => x.Read(directory)).Returns(config);

        var settings = new AddTableOfContentsSettings { FilePath = directory };

        // Act
        var result = CreateCommand().Execute(null!, settings);

        // Assert
        result.ShouldBe(0);
        _console.Output.ShouldContain("Success");
        _console.Output.ShouldContain("Created");

        // Verify config was updated
        MockJournalConfiguration.Verify(
            x => x.Update(directory, It.IsAny<Action<JournalConfig>>()),
            Times.Once
        );

        MockTableOfContentsService.Verify(
            x => x.UpdateTableOfContents(directory, It.IsAny<DateTime>(), It.IsAny<DateTime>()),
            Times.Once
        );
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
            TableOfContents = new TableOfContents
            {
                File = "TOC.md",
                Structure = new Structure { Topics = [] },
                RootEntries = [],
            },
        };
        MockJournalConfiguration.Setup(x => x.Read(directory)).Returns(config);

        var settings = new AddTableOfContentsSettings
        {
            FilePath = directory,
            TableOfContentsName = customTocName,
        };

        // Act
        var result = CreateCommand().Execute(null!, settings);

        // Assert
        result.ShouldBe(0);
        _console.Output.ShouldContain(customTocFile);

        // Verify config was updated with custom name
        MockJournalConfiguration.Verify(
            x => x.Update(directory, It.IsAny<Action<JournalConfig>>()),
            Times.Once
        );

        MockTableOfContentsService.Verify(
            x => x.UpdateTableOfContents(directory, It.IsAny<DateTime>(), It.IsAny<DateTime>()),
            Times.Once
        );
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
            TableOfContents = new TableOfContents
            {
                File = tocFile,
                Structure = new Structure { Topics = [] },
                RootEntries = [],
            },
        };
        MockJournalConfiguration.Setup(x => x.Read(directory)).Returns(config);

        var settings = new AddTableOfContentsSettings { FilePath = directory };

        // Act
        var result = CreateCommand().Execute(null!, settings);

        // Assert
        result.ShouldBe(0);

        // Verify config was NOT updated
        MockJournalConfiguration.Verify(
            x => x.Update(It.IsAny<string>(), It.IsAny<Action<JournalConfig>>()),
            Times.Never
        );

        MockTableOfContentsService.Verify(
            x => x.UpdateTableOfContents(directory, It.IsAny<DateTime>(), It.IsAny<DateTime>()),
            Times.Once
        );
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
            TableOfContents = new TableOfContents
            {
                File = tocFile,
                Structure = new Structure { Topics = [] },
                RootEntries = [],
            },
        };
        MockJournalConfiguration.Setup(x => x.Read(directory)).Returns(config);

        var settings = new AddTableOfContentsSettings { FilePath = directory };

        // Act
        var result = CreateCommand().Execute(null!, settings);

        // Assert
        result.ShouldBe(1);
        _console.Output.ShouldContain("Warning");
        _console.Output.ShouldContain("already exists");

        // Verify TOC generator was NOT called
        MockTableOfContentsService.Verify(
            x =>
                x.UpdateTableOfContents(
                    It.IsAny<string>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<DateTime?>()
                ),
            Times.Never
        );

        // Verify config was NOT updated
        MockJournalConfiguration.Verify(
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
        var result = CreateCommand().Execute(null!, settings);

        // Assert
        result.ShouldBe(1);
        _console.Output.ShouldContain("Error");
        _console.Output.ShouldContain("journalrc");

        MockTableOfContentsService.Verify(
            x =>
                x.UpdateTableOfContents(
                    It.IsAny<string>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<DateTime?>()
                ),
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

        MockJournalConfiguration.Setup(x => x.Read(directory)).Returns((JournalConfig?)null);

        var settings = new AddTableOfContentsSettings { FilePath = directory };

        // Act
        var result = CreateCommand().Execute(null!, settings);

        // Assert
        result.ShouldBe(1);
        _console.Output.ShouldContain("Error");
        _console.Output.ShouldContain("Failed to read journal configuration");

        MockTableOfContentsService.Verify(
            x =>
                x.UpdateTableOfContents(
                    It.IsAny<string>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<DateTime?>()
                ),
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
            TableOfContents = new TableOfContents
            {
                File = tocFile,
                Structure = new Structure { Topics = [] },
                RootEntries = [],
            },
        };
        MockJournalConfiguration.Setup(x => x.Read(directory)).Returns(config);
        MockTableOfContentsService
            .Setup(x =>
                x.UpdateTableOfContents(directory, It.IsAny<DateTime>(), It.IsAny<DateTime>())
            )
            .Throws(new InvalidOperationException("TOC generation failed"));

        var settings = new AddTableOfContentsSettings { FilePath = directory };

        // Act
        var result = CreateCommand().Execute(null!, settings);

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
            TableOfContents = new TableOfContents
            {
                File = tocFile,
                Structure = new Structure { Topics = [] },
                RootEntries = [],
            },
        };
        MockJournalConfiguration.Setup(x => x.Read(directory)).Returns(config);

        var settings = new AddTableOfContentsSettings { FilePath = directory };

        // Act
        var result = CreateCommand().Execute(null!, settings);

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
            TableOfContents = new TableOfContents
            {
                File = "TOC.md",
                Structure = new Structure { Topics = [] },
                RootEntries = [],
            },
        };
        MockJournalConfiguration.Setup(x => x.Read(directory)).Returns(config);

        var settings = new AddTableOfContentsSettings
        {
            FilePath = directory,
            TableOfContentsName = customTocName,
        };

        // Act
        var result = CreateCommand().Execute(null!, settings);

        // Assert
        result.ShouldBe(1);
        _console.Output.ShouldContain("Warning");
        _console.Output.ShouldContain("already exists");
        _console.Output.ShouldContain(customTocFile);

        MockTableOfContentsService.Verify(
            x =>
                x.UpdateTableOfContents(
                    It.IsAny<string>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<DateTime?>()
                ),
            Times.Never
        );
    }

    [Fact]
    public void Execute_TocAlreadyExists_ReturnsOne_NotZero()
    {
        // "add toc" must return a non-zero exit code when the TOC already exists
        // so scripts can detect the "nothing done" case.
        var directory = "/test/journal";
        var tocFile = $"{_journalSettings.TableOfContentsFileName}.md";

        _fileSystem.CreateDirectory(directory);
        _fileSystem.CreateFile(directory, _journalSettings.JournalConfigFileName, "{}");
        _fileSystem.CreateFile(directory, tocFile, "# Existing TOC");

        var config = new JournalConfig
        {
            TableOfContents = new TableOfContents
            {
                File = tocFile,
                Structure = new Structure { Topics = [] },
                RootEntries = [],
            },
        };
        MockJournalConfiguration.Setup(x => x.Read(directory)).Returns(config);

        var settings = new AddTableOfContentsSettings { FilePath = directory };

        var result = CreateCommand().Execute(null!, settings);

        result.ShouldBe(1);
        _console.Output.ShouldContain("Warning");
        _console.Output.ShouldContain("already exists");
    }

    [Fact]
    public void Execute_TocNameContainingBrackets_DoesNotThrowMarkupException_WhenAlreadyExists()
    {
        // TOC filenames containing bracket characters (e.g. "toc[2026]") must be
        // escaped before being passed to Spectre.Console markup rendering.
        var directory = "/test/journal";
        var bracketTocName = "toc[2026]";
        var bracketTocFile = $"{bracketTocName}.md";

        _fileSystem.CreateDirectory(directory);
        _fileSystem.CreateFile(directory, _journalSettings.JournalConfigFileName, "{}");
        _fileSystem.CreateFile(directory, bracketTocFile, "# TOC");

        var config = new JournalConfig
        {
            TableOfContents = new TableOfContents
            {
                File = bracketTocFile,
                Structure = new Structure { Topics = [] },
                RootEntries = [],
            },
        };
        MockJournalConfiguration.Setup(x => x.Read(directory)).Returns(config);

        var settings = new AddTableOfContentsSettings
        {
            FilePath = directory,
            TableOfContentsName = bracketTocName,
        };

        // Should warn and return 1 — must NOT throw MarkupException
        var result = CreateCommand().Execute(null!, settings);

        result.ShouldBe(1);
        _console.Output.ShouldContain("Warning");
        _console.Output.ShouldContain("already exists");
    }

    #endregion
}
