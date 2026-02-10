using markdown_journal_cli.Commands.Add;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.DependencyInjection;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.Tracking;
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
public class AddFileTrackingCommandTests
{
    private readonly Mock<IFileSystem> _mockFileSystem;
    private readonly Mock<IFileTracking> _mockFileTracking;
    private readonly IOptions<JournalSettings> _journalSettings;
    private readonly TestConsole _console;
    private readonly CommandAppTester _app;

    public AddFileTrackingCommandTests()
    {
        _console = new TestConsole();
        _mockFileSystem = new Mock<IFileSystem>();
        _mockFileTracking = new Mock<IFileTracking>();

        _journalSettings = Options.Create(
            new JournalSettings
            {
                AppName = "md-journal",
                JournalConfigFileName = ".journalrc",
            }
        );

        SetupDefaultMockBehaviors();

        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(_console);
        services.AddSingleton(_mockFileSystem.Object);
        services.AddSingleton(_mockFileTracking.Object);
        services.AddSingleton(_journalSettings);
        services.AddSingleton<AddFileTracking>();

        var host = Host.CreateDefaultBuilder().Build();
        var registrar = new TypeRegistrar(host);

        foreach (var service in services.Where(s => s.ImplementationInstance != null))
        {
            registrar.RegisterInstance(service.ServiceType, service.ImplementationInstance);
        }

        _app = new CommandAppTester(registrar);
        _app.Configure(config =>
        {
            config.SetApplicationName(_journalSettings.Value.AppName);
            config.AddBranch<AddSettings>("add", add =>
            {
                add.AddCommand<AddFileTracking>("tracking");
            });
        });
    }

    private void SetupDefaultMockBehaviors()
    {
        // Default: .journalrc exists
        _mockFileSystem.Setup(fs => fs.FileExists(It.Is<string>(s => s.Contains(".journalrc"))))
            .Returns(true);
        
        // Default: tracking file doesn't exist yet
        _mockFileSystem.Setup(fs => fs.FileExists(It.Is<string>(s => s.Contains(".md-journal"))))
            .Returns(false);
    }

    #region Positive Cases

    [Fact]
    public void Execute_ShouldCreateTrackingFile_WhenJournalrcExistsAndTrackingFileDoesNotExist()
    {
        // Arrange
        var testPath = "/test/journal";

        // Act
        var result = _app.Run(new[] { "add", "--path", testPath, "tracking" });

        // Assert
        result.ExitCode.ShouldBe(0);
        _mockFileTracking.Verify(ft => ft.LoadIndex(testPath), Times.Once);
        _mockFileTracking.Verify(ft => ft.UpdateIndex(testPath), Times.Once);
    }

    [Fact]
    public void Execute_ShouldCreateTrackingFile_WhenIgnoreJournalConfigFlagIsSet()
    {
        // Arrange
        var testPath = "/test/journal";
        _mockFileSystem.Setup(fs => fs.FileExists(It.Is<string>(s => s.Contains(".journalrc"))))
            .Returns(false);

        // Act
        var result = _app.Run(new[] { "add", "--path", testPath, "tracking", "--ignoreconfig" });

        // Assert
        result.ExitCode.ShouldBe(0);
        _mockFileTracking.Verify(ft => ft.LoadIndex(testPath), Times.Once);
        _mockFileTracking.Verify(ft => ft.UpdateIndex(testPath), Times.Once);
    }

    #endregion

    #region Negative Cases

    [Fact]
    public void Execute_ShouldReturnError_WhenJournalrcDoesNotExist()
    {
        // Arrange
        var testPath = "/test/journal";
        _mockFileSystem.Setup(fs => fs.FileExists(It.Is<string>(s => s.Contains(".journalrc"))))
            .Returns(false);

        // Act
        var result = _app.Run(new[] { "add", "--path", testPath, "tracking" });

        // Assert
        result.ExitCode.ShouldBe(1);
        _mockFileTracking.Verify(ft => ft.LoadIndex(It.IsAny<string>()), Times.Never);
        _mockFileTracking.Verify(ft => ft.UpdateIndex(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Execute_ShouldReturnWarning_WhenTrackingFileAlreadyExists()
    {
        // Arrange
        var testPath = "/test/journal";
        _mockFileSystem.Setup(fs => fs.FileExists(It.Is<string>(s => s.Contains(".md-journal"))))
            .Returns(true);

        // Act
        var result = _app.Run(new[] { "add", "--path", testPath, "tracking" });

        // Assert
        result.ExitCode.ShouldBe(0); // Returns 0 with warning, not error
        _mockFileTracking.Verify(ft => ft.LoadIndex(It.IsAny<string>()), Times.Never);
        _mockFileTracking.Verify(ft => ft.UpdateIndex(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Execute_ShouldHandleUnexpectedErrors_Gracefully()
    {
        // Arrange
        var testPath = "/test/journal";
        _mockFileTracking.Setup(ft => ft.LoadIndex(It.IsAny<string>()))
            .Throws(new InvalidOperationException("Unexpected error"));

        // Act
        var result = _app.Run(new[] { "add", "--path", testPath, "tracking" });

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
        var result = _app.Run(new[] { "add", "tracking" });

        // Assert
        result.ExitCode.ShouldBe(0);
        _mockFileTracking.Verify(ft => ft.LoadIndex(expectedPath), Times.Once);
        _mockFileTracking.Verify(ft => ft.UpdateIndex(expectedPath), Times.Once);
    }

    [Fact]
    public void Execute_ShouldCallFileTrackingInCorrectOrder()
    {
        // Arrange
        var testPath = "/test/journal";
        var callOrder = new List<string>();
        
        _mockFileTracking.Setup(ft => ft.LoadIndex(It.IsAny<string>()))
            .Callback(() => callOrder.Add("LoadIndex"));
        _mockFileTracking.Setup(ft => ft.UpdateIndex(It.IsAny<string>()))
            .Callback(() => callOrder.Add("UpdateIndex"));

        // Act
        var result = _app.Run(new[] { "add", "--path", testPath, "tracking" });

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
        
        _mockFileSystem.Setup(fs => fs.FileExists(It.Is<string>(s => s.Contains(".journalrc"))))
            .Callback(() => checkOrder.Add("journalrc"))
            .Returns(true);
        _mockFileSystem.Setup(fs => fs.FileExists(It.Is<string>(s => s.Contains(".md-journal"))))
            .Callback(() => checkOrder.Add("tracking"))
            .Returns(false);

        // Act
        var result = _app.Run(new[] { "add", "--path", testPath, "tracking" });

        // Assert
        result.ExitCode.ShouldBe(0);
        checkOrder[0].ShouldBe("journalrc");
        checkOrder[1].ShouldBe("tracking");
    }

    #endregion
}
