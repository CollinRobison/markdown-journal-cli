using markdown_journal_cli.Commands.Remove;
using markdown_journal_cli.Exceptions;
using markdown_journal_cli.Infrastructure.DependencyInjection;
using markdown_journal_cli.Services.RemoveEntry;
using markdown_journal_cli.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using Xunit;

namespace markdown_journal_cli.Tests.Commands.Remove;

/// <summary>
/// Unit tests for <see cref="RemoveEntryCommand"/> covering confirmation flow,
/// --force flag, --clean-refs flag, and all error paths.
/// </summary>
public class RemoveEntryCommandTests : CommandTestBase
{
    private const string TestPath = "/test/journal";
    private const string TestFileName = "old_notes";
    private const string TestFileNameMd = "old_notes.md";

    private readonly TestConsole _console;
    private readonly Mock<IRemoveEntryService> _mockRemoveEntryService;

    public RemoveEntryCommandTests()
    {
        _console = new TestConsole();
        _mockRemoveEntryService = new Mock<IRemoveEntryService>();

        _mockRemoveEntryService
            .Setup(s => s.RemoveEntry(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(new RemoveEntryResult(true, true, true, Array.Empty<string>()));
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private RemoveEntryCommand CreateCommand() =>
        new RemoveEntryCommand(
            _console,
            _mockRemoveEntryService.Object,
            NullLogger<RemoveEntryCommand>.Instance
        );

    private static CommandContext CreateCommandContext() =>
        new CommandContext([], Mock.Of<IRemainingArguments>(), "entry", null);

    // ------------------------------------------------------------------
    // Happy paths
    // ------------------------------------------------------------------

    [Fact]
    public void Execute_Should_RemoveEntryWithoutPrompt_When_ForceIsSet()
    {
        // Arrange
        var settings = new RemoveEntrySettings
        {
            FilePath = TestPath,
            FileName = TestFileName,
            Force = true,
        };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(0);
        _mockRemoveEntryService.Verify(
            s => s.RemoveEntry(TestPath, TestFileName, false),
            Times.Once
        );
        _console.Output.ShouldNotContain("Are you sure");
        _console.Output.ShouldContain("Success:");
    }

    [Fact]
    public void Execute_Should_RemoveEntry_When_UserConfirms()
    {
        // Arrange
        _console.Input.PushTextWithEnter("y");
        var settings = new RemoveEntrySettings
        {
            FilePath = TestPath,
            FileName = TestFileName,
            Force = false,
        };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(0);
        _mockRemoveEntryService.Verify(
            s => s.RemoveEntry(TestPath, TestFileName, false),
            Times.Once
        );
        _console.Output.ShouldContain("Success:");
    }

    [Fact]
    public void Execute_Should_CancelAndReturnZero_When_UserDenies()
    {
        // Arrange
        _console.Input.PushTextWithEnter("n");
        var settings = new RemoveEntrySettings
        {
            FilePath = TestPath,
            FileName = TestFileName,
            Force = false,
        };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(0);
        _mockRemoveEntryService.Verify(
            s => s.RemoveEntry(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()),
            Times.Never
        );
        _console.Output.ShouldContain("Removal cancelled.");
    }

    [Fact]
    public void Execute_Should_CallCleanRefsOnService_When_CleanRefsSet()
    {
        // Arrange
        var modifiedFiles = new[] { "other_entry.md", "another.md" };
        _mockRemoveEntryService
            .Setup(s => s.RemoveEntry(TestPath, TestFileName, true))
            .Returns(new RemoveEntryResult(true, true, true, modifiedFiles));

        var settings = new RemoveEntrySettings
        {
            FilePath = TestPath,
            FileName = TestFileName,
            Force = true,
            CleanRefs = true,
        };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(0);
        _mockRemoveEntryService.Verify(
            s => s.RemoveEntry(TestPath, TestFileName, true),
            Times.Once
        );
        _console.Output.ShouldContain("Stripped links: other_entry.md");
        _console.Output.ShouldContain("Stripped links: another.md");
        _console.Output.ShouldContain("Cleaned dead references in 2 file(s).");
    }

    // ------------------------------------------------------------------
    // Error cases
    // ------------------------------------------------------------------

    [Fact]
    public void Execute_Should_ReturnOneWithErrorMessage_When_JournalrcNotFound()
    {
        // Arrange
        _mockRemoveEntryService
            .Setup(s => s.RemoveEntry(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .Throws(new JournalrcNotFoundException(TestPath));

        var settings = new RemoveEntrySettings
        {
            FilePath = TestPath,
            FileName = TestFileName,
            Force = true,
        };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(1);
        _console.Output.ShouldContain("Error:");
    }

    [Fact]
    public void Execute_Should_ReturnOneWithErrorMessage_When_TrackingIndexNotFound()
    {
        // Arrange
        _mockRemoveEntryService
            .Setup(s => s.RemoveEntry(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .Throws(new TrackingIndexNotFoundException(TestPath, ".mdjournal"));

        var settings = new RemoveEntrySettings
        {
            FilePath = TestPath,
            FileName = TestFileName,
            Force = true,
        };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(1);
        _console.Output.ShouldContain("Error:");
    }

    [Fact]
    public void Execute_Should_ReturnOneWithErrorMessage_When_FileIsProtected()
    {
        // Arrange
        _mockRemoveEntryService
            .Setup(s => s.RemoveEntry(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .Throws(new ProtectedJournalFileException(".journalrc"));

        var settings = new RemoveEntrySettings
        {
            FilePath = TestPath,
            FileName = ".journalrc",
            Force = true,
        };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(1);
        _console.Output.ShouldContain("Error:");
        _console.Output.ShouldContain("protected journal file");
    }

    [Fact]
    public void Execute_Should_ReturnOneWithErrorMessage_When_FileNotFound()
    {
        // Arrange
        _mockRemoveEntryService
            .Setup(s => s.RemoveEntry(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .Throws(
                new FileNotFoundException(
                    $"Entry file '{TestFileNameMd}' not found at '{TestPath}'."
                )
            );

        var settings = new RemoveEntrySettings
        {
            FilePath = TestPath,
            FileName = TestFileName,
            Force = true,
        };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(1);
        _console.Output.ShouldContain("Error:");
    }

    [Fact]
    public void Execute_Should_ReturnOneWithErrorMessage_When_UnexpectedExceptionThrown()
    {
        // Arrange
        _mockRemoveEntryService
            .Setup(s => s.RemoveEntry(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .Throws(new InvalidOperationException("Something went wrong."));

        var settings = new RemoveEntrySettings
        {
            FilePath = TestPath,
            FileName = TestFileName,
            Force = true,
        };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(1);
        _console.Output.ShouldContain("An unexpected error occurred");
    }

    [Fact]
    public void Execute_Should_EscapeMarkupCorrectly_When_FileNameContainsMarkup()
    {
        // Arrange — a filename with characters that could be misread as Spectre markup
        _mockRemoveEntryService
            .Setup(s => s.RemoveEntry(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(new RemoveEntryResult(true, true, true, Array.Empty<string>()));

        var settings = new RemoveEntrySettings
        {
            FilePath = TestPath,
            FileName = "entry_[bold]_notes",
            Force = true,
        };

        // Act — should not throw a markup parse exception
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(0);
        _console.Output.ShouldContain("Success:");
    }

    [Fact]
    public void Execute_Should_ReturnError_When_FileNotFoundAndForceNotSet()
    {
        // The guard checks must be evaluated before the confirmation prompt so
        // the user is never asked to confirm an action that was never possible.
        _mockRemoveEntryService
            .Setup(s => s.ValidatePreconditions(TestPath, TestFileName, false))
            .Throws(
                new FileNotFoundException(
                    $"Entry file '{TestFileName}.md' not found at '{TestPath}'."
                )
            );

        var settings = new RemoveEntrySettings
        {
            FilePath = TestPath,
            FileName = TestFileName,
            Force = false,
        };

        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        result.ShouldBe(1);
        _console.Output.ShouldContain("Error:");
        // Confirm the prompt was NEVER shown — the error surfaced before it
        _console.Output.ShouldNotContain("Are you sure");
        _mockRemoveEntryService.Verify(
            s => s.RemoveEntry(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()),
            Times.Never
        );
    }

    [Fact]
    public void Execute_Should_PassCleanRefsToValidatePreconditions_When_ForceNotSet()
    {
        // The command must forward CleanRefs to ValidatePreconditions so the pre-flight
        // check mirrors the relaxed file-existence guard used by RemoveEntry itself.
        _console.Input.PushTextWithEnter("y");
        var settings = new RemoveEntrySettings
        {
            FilePath = TestPath,
            FileName = TestFileName,
            Force = false,
            CleanRefs = true,
        };

        CreateCommand().Execute(CreateCommandContext(), settings);

        _mockRemoveEntryService.Verify(
            s => s.ValidatePreconditions(TestPath, TestFileName, true),
            Times.Once
        );
    }

    [Fact]
    public void Execute_Should_ShowConfirmationAndSucceed_When_FileAbsentAndCleanRefsSet()
    {
        // ValidatePreconditions does NOT throw when the file is absent and cleanRefs=true;
        // the command should therefore show the prompt and proceed normally.
        _console.Input.PushTextWithEnter("y");
        var modifiedFiles = new[] { "linked-entry.md" };
        _mockRemoveEntryService
            .Setup(s => s.RemoveEntry(TestPath, TestFileName, true))
            .Returns(new RemoveEntryResult(false, true, true, modifiedFiles));

        var settings = new RemoveEntrySettings
        {
            FilePath = TestPath,
            FileName = TestFileName,
            Force = false,
            CleanRefs = true,
        };

        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        result.ShouldBe(0);
        _console.Output.ShouldContain("Are you sure");
        _console.Output.ShouldContain("Success:");
        _console.Output.ShouldContain("Stripped links: linked-entry.md");
    }

    [Fact]
    public void Execute_Should_ReturnError_When_JournalrcMissingAndForceNotSet()
    {
        _mockRemoveEntryService
            .Setup(s => s.ValidatePreconditions(TestPath, TestFileName, false))
            .Throws(new JournalrcNotFoundException(TestPath));

        var settings = new RemoveEntrySettings
        {
            FilePath = TestPath,
            FileName = TestFileName,
            Force = false,
        };

        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        result.ShouldBe(1);
        _console.Output.ShouldContain("Error:");
        _console.Output.ShouldNotContain("Are you sure");
    }

    [Fact]
    public void Execute_Should_NotCallValidatePreconditions_When_ForceIsSet()
    {
        // With --force, the confirmation path (and its preflight validation) is skipped entirely.
        var settings = new RemoveEntrySettings
        {
            FilePath = TestPath,
            FileName = TestFileName,
            Force = true,
        };

        CreateCommand().Execute(CreateCommandContext(), settings);

        _mockRemoveEntryService.Verify(
            s => s.ValidatePreconditions(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()),
            Times.Never
        );
    }

    [Fact]
    public void Execute_Should_NotPrintRemovedFromConfig_When_AlreadyAbsent()
    {
        // FR-007: when nothing was removed the removal header must not appear and
        // the final message must indicate nothing was found rather than showing "Success:".
        _mockRemoveEntryService
            .Setup(s => s.RemoveEntry(TestPath, TestFileName, true))
            .Returns(new RemoveEntryResult(false, false, false, Array.Empty<string>()));

        var settings = new RemoveEntrySettings
        {
            FilePath = TestPath,
            FileName = TestFileName,
            Force = true,
            CleanRefs = true,
        };

        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        result.ShouldBe(0);
        _console.Output.ShouldNotContain("removed from config");
        _console.Output.ShouldNotContain("removed from tracking");
        _console.Output.ShouldNotContain("Success:");
        _console.Output.ShouldContain("not found in the journal");
        _console.Output.ShouldContain("No dead references found.");
    }

    [Fact]
    public void Execute_Should_PrintNoDeadRefsMessage_When_CleanRefsSetAndNoLinksFound()
    {
        // FR-008: when --clean-refs is set and no dead links are found, a clear message is shown.
        _mockRemoveEntryService
            .Setup(s => s.RemoveEntry(TestPath, TestFileName, true))
            .Returns(new RemoveEntryResult(true, true, true, Array.Empty<string>()));

        var settings = new RemoveEntrySettings
        {
            FilePath = TestPath,
            FileName = TestFileName,
            Force = true,
            CleanRefs = true,
        };

        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        result.ShouldBe(0);
        _console.Output.ShouldContain("No dead references found.");
        _console.Output.ShouldNotContain("Cleaned dead references in 0");
    }
}
