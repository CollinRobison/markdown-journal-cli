using markdown_journal_cli.Commands.Update;
using markdown_journal_cli.Exceptions;
using markdown_journal_cli.Services;
using markdown_journal_cli.Tests.Infrastructure;
using Moq;
using Shouldly;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using Xunit;

namespace markdown_journal_cli.Tests.Commands.Update;

/// <summary>
/// Unit tests for <see cref="UpdateEntryCommand"/> covering file rename, display-name update,
/// heading changes, ignore/unignore, and TOC regeneration.
/// </summary>
public class UpdateEntryCommandTests : CommandTestBase
{
    private const string TestPath = "/test/journal";
    private const string TestFileName = "My_Entry.md";
    private const string TestEntryDisplayName = "My Entry";

    private readonly TestConsole _console;
    private readonly Mock<IJournalFileUpdateService> _mockFileUpdateService;

    public UpdateEntryCommandTests()
    {
        _console = new TestConsole();
        _mockFileUpdateService = new Mock<IJournalFileUpdateService>();
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private UpdateEntryCommand CreateCommand() =>
        new UpdateEntryCommand(_console, _mockFileUpdateService.Object);

    private static CommandContext CreateCommandContext() =>
        new CommandContext([], Mock.Of<IRemainingArguments>(), "file", null);

    // ------------------------------------------------------------------

    #region Error Cases

    [Fact]
    public void Execute_ReturnsOne_WhenFileDoesNotExist()
    {
        // Arrange
        var settings = new UpdateEntrySettings
        {
            FilePath = TestPath,
            FileName = "nonexistent.md",
            EntryName = null!,
        };

        _mockFileUpdateService
            .Setup(s =>
                s.UpdateEntry(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>()
                )
            )
            .Throws(
                new FileNotFoundException("File 'nonexistent.md' not found at '/test/journal'.")
            );

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(1);
        _console.Output.ShouldContain("Error:");
    }

    [Fact]
    public void Execute_ReturnsOne_WhenJournalrcNotFound()
    {
        // Arrange
        var noRcPath = "/test/no-rc";

        var settings = new UpdateEntrySettings
        {
            FilePath = noRcPath,
            FileName = TestFileName,
            EntryName = null!,
        };

        _mockFileUpdateService
            .Setup(s =>
                s.UpdateEntry(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>()
                )
            )
            .Throws(new JournalrcNotFoundException(noRcPath));

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(1);
        _console.Output.ShouldContain("Error:");
        _console.Output.ShouldContain(".journalrc");
    }

    #endregion

    #region File Name Normalization

    [Fact]
    public void Execute_NormalizesFileNameWithoutExtension()
    {
        // Arrange — pass filename without .md extension
        var settings = new UpdateEntrySettings
        {
            FilePath = TestPath,
            FileName = "My_Entry", // no .md
            EntryName = null!,
        };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert — command must have found the file (no error about file not found)
        result.ShouldBe(0);
        _console.Output.ShouldNotContain("not found");
    }

    #endregion

    #region File Rename

    [Fact]
    public void Execute_RenamesFile_WhenNameFlagProvided()
    {
        // Arrange
        var settings = new UpdateEntrySettings
        {
            FilePath = TestPath,
            FileName = TestFileName,
            EntryName = "New Entry",
        };

        // Act
        CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert - UpdateEntry service method was called with the new name
        _mockFileUpdateService.Verify(
            x => x.UpdateEntry(TestPath, TestFileName, "New Entry", null, null, false, false, true),
            Times.Once
        );
    }

    #endregion

    #region Display Name Update

    [Fact]
    public void Execute_UpdatesDisplayNameOnly_WhenTitleFlagProvided()
    {
        // Arrange - only --title provided; no rename, no heading change
        var settings = new UpdateEntrySettings
        {
            FilePath = TestPath,
            FileName = TestFileName,
            EntryName = null!,
            EntryTitle = "Custom Title",
        };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(0);
        _mockFileUpdateService.Verify(
            x =>
                x.UpdateEntry(
                    TestPath,
                    TestFileName,
                    null,
                    "Custom Title",
                    null,
                    false,
                    false,
                    true
                ),
            Times.Once
        );
    }

    [Fact]
    public void Execute_PrefersTitleOverNameForDisplayName_WhenBothProvided()
    {
        // Arrange - both --name and --title provided; title wins for TOC display name
        var settings = new UpdateEntrySettings
        {
            FilePath = TestPath,
            FileName = TestFileName,
            EntryName = "New Entry",
            EntryTitle = "Override Title",
        };

        // Act
        CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert - UpdateEntry is called with both name and title
        _mockFileUpdateService.Verify(
            x =>
                x.UpdateEntry(
                    TestPath,
                    TestFileName,
                    "New Entry",
                    "Override Title",
                    null,
                    false,
                    false,
                    true
                ),
            Times.Once
        );
    }

    #endregion

    #region Heading Changes

    [Fact]
    public void Execute_UpdatesHeadings_WhenHeadingsFlagProvided()
    {
        // Arrange
        var settings = new UpdateEntrySettings
        {
            FilePath = TestPath,
            FileName = TestFileName,
            EntryName = null!,
            Headings = "Projects-2024",
        };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert - UpdateEntry is called with the new headings
        result.ShouldBe(0);
        _mockFileUpdateService.Verify(
            x =>
                x.UpdateEntry(
                    TestPath,
                    TestFileName,
                    null,
                    null,
                    "Projects-2024",
                    false,
                    false,
                    true
                ),
            Times.Once
        );
    }

    #endregion

    #region Ignore / Unignore

    [Fact]
    public void Execute_AddsToIgnoreList_WhenIgnoreFlagProvided()
    {
        // Arrange
        var settings = new UpdateEntrySettings
        {
            FilePath = TestPath,
            FileName = TestFileName,
            EntryName = null!,
            IgnoreFile = true,
        };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(0);
        _mockFileUpdateService.Verify(
            x => x.UpdateEntry(TestPath, TestFileName, null, null, null, true, false, true),
            Times.Once
        );
    }

    [Fact]
    public void Execute_RemovesFromIgnoreList_WhenUnignoreFlagProvided()
    {
        // Arrange
        var settings = new UpdateEntrySettings
        {
            FilePath = TestPath,
            FileName = TestFileName,
            EntryName = null!,
            UnignoreFile = true,
        };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert — UpdateEntry is called with unignoreFile = true
        result.ShouldBe(0);
        _mockFileUpdateService.Verify(
            x => x.UpdateEntry(TestPath, TestFileName, null, null, null, false, true, true),
            Times.Once
        );
    }

    #endregion

    #region Success Cases

    [Fact]
    public void Execute_CallsUpdateEntry_WithAllParameters()
    {
        // Arrange
        var settings = new UpdateEntrySettings
        {
            FilePath = TestPath,
            FileName = TestFileName,
            EntryName = "New_Name",
            EntryTitle = "Updated Title",
            Headings = "Projects-2024",
            IgnoreFile = false,
            UnignoreFile = false,
        };

        // Act
        CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert
        _mockFileUpdateService.Verify(
            x =>
                x.UpdateEntry(
                    TestPath,
                    TestFileName,
                    "New_Name",
                    "Updated Title",
                    "Projects-2024",
                    false,
                    false,
                    true
                ),
            Times.Once
        );
    }

    #endregion

    #region No-Backlinks Flag

    [Fact]
    public void Execute_PassesUpdateBacklinks_True_ByDefault()
    {
        // Arrange — --no-backlinks not set; default is to update backlinks
        var settings = new UpdateEntrySettings
        {
            FilePath = TestPath,
            FileName = TestFileName,
            EntryName = "New_Name",
        };

        // Act
        CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert
        _mockFileUpdateService.Verify(
            x => x.UpdateEntry(TestPath, TestFileName, "New_Name", null, null, false, false, true),
            Times.Once
        );
    }

    [Fact]
    public void Execute_PassesUpdateBacklinks_False_WhenNoBacklinksFlagSet()
    {
        // Arrange — --no-backlinks present; backlink scan should be skipped
        var settings = new UpdateEntrySettings
        {
            FilePath = TestPath,
            FileName = TestFileName,
            EntryName = "New_Name",
            NoBacklinks = true,
        };

        // Act
        CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert
        _mockFileUpdateService.Verify(
            x => x.UpdateEntry(TestPath, TestFileName, "New_Name", null, null, false, false, false),
            Times.Once
        );
    }

    #endregion

    #region Success

    [Fact]
    public void Execute_ReturnsZero_OnSuccess()
    {
        // Arrange — minimal valid invocation (no actual changes needed)
        var settings = new UpdateEntrySettings
        {
            FilePath = TestPath,
            FileName = TestFileName,
            EntryName = null!,
            EntryTitle = "New Title",
        };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(0);
        _console.Output.ShouldContain("Success:");
    }

    #endregion
}
