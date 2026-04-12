using markdown_journal_cli.Commands.Add;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.DependencyInjection;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.Tracking;
using markdown_journal_cli.Infrastructure.Transactions;
using markdown_journal_cli.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using Xunit;

namespace markdown_journal_cli.Tests.Commands.Add;

/// <summary>
/// Unit tests for AddFileTracking command covering positive, negative, and edge cases.
/// </summary>
public class AddFileTrackingCommandTests : CommandTestBase
{
    protected override void SetupDefaultBehaviors()
    {
        // Default: .journalrc exists
        MockFileSystem
            .Setup(fs => fs.FileExists(It.Is<string>(s => s.Contains(".journalrc"))))
            .Returns(true);

        // Default: tracking file doesn't exist yet
        MockFileSystem
            .Setup(fs => fs.FileExists(It.Is<string>(s => s.Contains(".md-journal"))))
            .Returns(false);
    }

    private CommandAppTester BuildAddFileTrackingApp() =>
        BuildApp(
            config =>
            {
                config.SetApplicationName(JournalSettings.Value.AppName);
                config.AddBranch<AddSettings>(
                    "add",
                    add => { add.AddCommand<AddFileTracking>("tracking"); }
                );
            },
            services => { services.AddSingleton<AddFileTracking>(); }
        );

    #region Positive Cases

    [Fact]
    public void Execute_ShouldCreateTrackingFile_WhenJournalrcExistsAndTrackingFileDoesNotExist()
    {
        // Arrange
        var testPath = "/test/journal";

        // Act
        var result = BuildAddFileTrackingApp().Run(new[] { "add", "--path", testPath, "tracking" });

        // Assert
        result.ExitCode.ShouldBe(0);
        MockFileTracking.Verify(ft => ft.LoadIndex(testPath), Times.Once);
        MockFileTracking.Verify(ft => ft.UpdateIndex(testPath), Times.Once);
    }

    [Fact]
    public void Execute_ShouldCreateTrackingFile_WhenIgnoreJournalConfigFlagIsSet()
    {
        // Arrange
        var testPath = "/test/journal";
        MockFileSystem
            .Setup(fs => fs.FileExists(It.Is<string>(s => s.Contains(".journalrc"))))
            .Returns(false);

        // Act
        var result = BuildAddFileTrackingApp().Run(new[] { "add", "--path", testPath, "tracking", "--ignoreconfig" });

        // Assert
        result.ExitCode.ShouldBe(0);
        MockFileTracking.Verify(ft => ft.LoadIndex(testPath), Times.Once);
        MockFileTracking.Verify(ft => ft.UpdateIndex(testPath), Times.Once);
    }

    #endregion

    #region Negative Cases

    [Fact]
    public void Execute_ShouldReturnError_WhenJournalrcDoesNotExist()
    {
        // Arrange
        var testPath = "/test/journal";
        MockFileSystem
            .Setup(fs => fs.FileExists(It.Is<string>(s => s.Contains(".journalrc"))))
            .Returns(false);

        // Act
        var result = BuildAddFileTrackingApp().Run(new[] { "add", "--path", testPath, "tracking" });

        // Assert
        result.ExitCode.ShouldBe(1);
        MockFileTracking.Verify(ft => ft.LoadIndex(It.IsAny<string>()), Times.Never);
        MockFileTracking.Verify(ft => ft.UpdateIndex(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Execute_ShouldReturnWarning_WhenTrackingFileAlreadyExists()
    {
        // Arrange
        var testPath = "/test/journal";
        MockFileSystem
            .Setup(fs => fs.FileExists(It.Is<string>(s => s.Contains(".md-journal"))))
            .Returns(true);

        // Act
        var result = BuildAddFileTrackingApp().Run(new[] { "add", "--path", testPath, "tracking" });

        // Assert
        result.ExitCode.ShouldBe(0); // Returns 0 with warning, not error
        MockFileTracking.Verify(ft => ft.LoadIndex(It.IsAny<string>()), Times.Never);
        MockFileTracking.Verify(ft => ft.UpdateIndex(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Execute_ShouldHandleUnexpectedErrors_Gracefully()
    {
        // Arrange
        var testPath = "/test/journal";
        MockFileTracking
            .Setup(ft => ft.LoadIndex(It.IsAny<string>()))
            .Throws(new InvalidOperationException("Unexpected error"));

        // Act
        var result = BuildAddFileTrackingApp().Run(new[] { "add", "--path", testPath, "tracking" });

        // Assert
        result.ExitCode.ShouldBe(1);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Execute_ShouldUseDefaultPath_WhenPathNotSpecified()
    {
        // Arrange
        var expectedPath = ".";

        // Act
        var result = BuildAddFileTrackingApp().Run(new[] { "add", "tracking" });

        // Assert
        result.ExitCode.ShouldBe(0);
        MockFileTracking.Verify(ft => ft.LoadIndex(expectedPath), Times.Once);
        MockFileTracking.Verify(ft => ft.UpdateIndex(expectedPath), Times.Once);
    }

    [Fact]
    public void Execute_ShouldCallFileTrackingInCorrectOrder()
    {
        // Arrange
        var testPath = "/test/journal";
        var callOrder = new List<string>();

        MockFileTracking
            .Setup(ft => ft.LoadIndex(It.IsAny<string>()))
            .Callback(() => callOrder.Add("LoadIndex"));
        MockFileTracking
            .Setup(ft => ft.UpdateIndex(It.IsAny<string>()))
            .Callback(() => callOrder.Add("UpdateIndex"));

        // Act
        var result = BuildAddFileTrackingApp().Run(new[] { "add", "--path", testPath, "tracking" });

        // Assert
        result.ExitCode.ShouldBe(0);
        callOrder.ShouldBe(new[] { "LoadIndex", "UpdateIndex" });
    }

    [Fact]
    public void Execute_ShouldCheckJournalrcBeforeTrackingFile()
    {
        // Arrange
        var testPath = "/test/journal";
        var checkOrder = new List<string>();

        MockFileSystem
            .Setup(fs => fs.FileExists(It.Is<string>(s => s.Contains(".journalrc"))))
            .Callback(() => checkOrder.Add("journalrc"))
            .Returns(true);
        MockFileSystem
            .Setup(fs => fs.FileExists(It.Is<string>(s => s.Contains(".md-journal"))))
            .Callback(() => checkOrder.Add("tracking"))
            .Returns(false);

        // Act
        var result = BuildAddFileTrackingApp().Run(new[] { "add", "--path", testPath, "tracking" });

        // Assert
        result.ExitCode.ShouldBe(0);
        checkOrder[0].ShouldBe("journalrc");
        checkOrder[1].ShouldBe("tracking");
    }

    #endregion
}
