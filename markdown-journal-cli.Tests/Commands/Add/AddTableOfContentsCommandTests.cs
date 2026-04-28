using markdown_journal_cli.Commands.Add;
using markdown_journal_cli.Infrastructure.Transactions;
using markdown_journal_cli.Infrastructure.Transactions.Models;
using markdown_journal_cli.Services.AddToc;
using markdown_journal_cli.Tests.Infrastructure;
using Moq;
using Shouldly;
using Spectre.Console.Testing;
using Xunit;

namespace markdown_journal_cli.Tests.Commands.Add;

/// <summary>
/// Unit tests for <see cref="AddTableOfContents"/> command.
/// Verifies that the command maps <see cref="AddTocResult"/> values to the correct
/// exit codes and console messages, and enforces the mutually-exclusive flag guard.
/// </summary>
public class AddTableOfContentsCommandTests : CommandTestBase
{
    private const string JournalDir = "/test/journal";

    private readonly TestConsole _console;
    private readonly Mock<IAddTocService> _mockAddTocService;

    public AddTableOfContentsCommandTests()
    {
        _console = new TestConsole();
        _mockAddTocService = new Mock<IAddTocService>();
    }

    private AddTableOfContents CreateCommand() =>
        new AddTableOfContents(
            _console,
            _mockAddTocService.Object,
            NoOpRollbackReporter.Instance
        );

    private static AddTableOfContentsSettings DefaultSettings(
        bool structureOnly = false,
        bool mdOnly = false,
        string? tocName = null
    ) =>
        new AddTableOfContentsSettings
        {
            FilePath = JournalDir,
            StructureOnly = structureOnly,
            MdOnly = mdOnly,
            TableOfContentsName = tocName,
        };

    #region Result Mapping

    [Fact]
    public void Execute_ReturnsZero_AndSuccessMessage_WhenServiceReturnsCreated()
    {
        _mockAddTocService
            .Setup(s => s.Execute(JournalDir, false, false, null))
            .Returns(AddTocResult.Created);

        var result = CreateCommand().Execute(null!, DefaultSettings());

        result.ShouldBe(0);
        _console.Output.ShouldContain("Success");
    }

    [Fact]
    public void Execute_ReturnsZero_AndSuccessMessage_WhenServiceReturnsPartiallyCreated()
    {
        _mockAddTocService
            .Setup(s => s.Execute(JournalDir, false, false, null))
            .Returns(AddTocResult.PartiallyCreated);

        var result = CreateCommand().Execute(null!, DefaultSettings());

        result.ShouldBe(0);
        _console.Output.ShouldContain("Success");
    }

    [Fact]
    public void Execute_ReturnsOne_AndWarningMessage_WhenServiceReturnsAlreadyExists()
    {
        _mockAddTocService
            .Setup(s => s.Execute(JournalDir, false, false, null))
            .Returns(AddTocResult.AlreadyExists);

        var result = CreateCommand().Execute(null!, DefaultSettings());

        result.ShouldBe(1);
        _console.Output.ShouldContain("Warning");
    }

    #endregion

    #region Flag Handling

    [Fact]
    public void Execute_PassesStructureOnly_ToService_WhenFlagIsSet()
    {
        _mockAddTocService
            .Setup(s => s.Execute(JournalDir, true, false, null))
            .Returns(AddTocResult.Created);

        var result = CreateCommand().Execute(null!, DefaultSettings(structureOnly: true));

        result.ShouldBe(0);
        _mockAddTocService.Verify(s => s.Execute(JournalDir, true, false, null), Times.Once);
    }

    [Fact]
    public void Execute_PassesMdOnly_ToService_WhenFlagIsSet()
    {
        _mockAddTocService
            .Setup(s => s.Execute(JournalDir, false, true, null))
            .Returns(AddTocResult.Created);

        var result = CreateCommand().Execute(null!, DefaultSettings(mdOnly: true));

        result.ShouldBe(0);
        _mockAddTocService.Verify(s => s.Execute(JournalDir, false, true, null), Times.Once);
    }

    [Fact]
    public void Execute_PassesTocName_ToService_WhenNameIsSet()
    {
        _mockAddTocService
            .Setup(s => s.Execute(JournalDir, false, false, "MyCustomToc"))
            .Returns(AddTocResult.Created);

        var result = CreateCommand().Execute(null!, DefaultSettings(tocName: "MyCustomToc"));

        result.ShouldBe(0);
        _mockAddTocService.Verify(s => s.Execute(JournalDir, false, false, "MyCustomToc"), Times.Once);
    }

    [Fact]
    public void Execute_ReturnsOne_AndErrorMessage_WhenBothFlagsAreSet()
    {
        var result = CreateCommand().Execute(null!, DefaultSettings(structureOnly: true, mdOnly: true));

        result.ShouldBe(1);
        _console.Output.ShouldContain("Error");
        // Service should NOT be called when flags are mutually exclusive
        _mockAddTocService.Verify(
            s => s.Execute(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<string>()),
            Times.Never
        );
    }

    [Fact]
    public void Execute_ReturnsOne_AndErrorMessage_WhenNameAndStructureOnlyAreSet()
    {
        var result = CreateCommand().Execute(null!, DefaultSettings(structureOnly: true, tocName: "MyToc"));

        result.ShouldBe(1);
        _console.Output.ShouldContain("Error");
        // Service should NOT be called when --name and --structure-only conflict
        _mockAddTocService.Verify(
            s => s.Execute(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<string>()),
            Times.Never
        );
    }

    #endregion

    #region Error Handling

    [Fact]
    public void Execute_ReturnsOne_AndErrorMessage_WhenServiceThrows()
    {
        _mockAddTocService
            .Setup(s => s.Execute(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<string>()))
            .Throws(new InvalidOperationException("Config not found"));

        var result = CreateCommand().Execute(null!, DefaultSettings());

        result.ShouldBe(1);
        _console.Output.ShouldContain("Error");
    }

    [Fact]
    public void Execute_ReturnsTwo_WhenServiceThrowsFullyRestoredRollbackCompletedException()
    {
        _mockAddTocService
            .Setup(s => s.Execute(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<string>()))
            .Throws(new RollbackCompletedException(
                new RollbackResult([], []),
                new IOException("simulated fault")
            ));

        var result = CreateCommand().Execute(null!, DefaultSettings());

        // Fully restored → exit code 2 (handled by JournalCommand base class)
        result.ShouldBe(2);
    }

    #endregion
}

