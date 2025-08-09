using Spectre.Console.Testing;
using Spectre.Console.Cli;
using markdown_journal_cli.Commands.New;
using Xunit;
using Shouldly;

namespace markdown_journal_cli.Tests.Commands;

public class NewCommandTests
{
    [Fact]
    public void Should_Create_New_Journal_With_Default_Name()
    {
        // Given
        var console = new TestConsole();
        var app = new CommandAppTester();
        app.Configure(config =>
        {
            config.AddCommand<NewCommand>("new");
        });

        // When
        var result = app.Run(new[] { "new" });

        // Then
        result.ExitCode.ShouldBe(0);
        result.Output.ShouldContain("MyJournal"); // Default name from Settings
    }

    [Fact]
    public void Should_Create_New_Journal_With_Custom_Name()
    {
        // Given
        var console = new TestConsole();
        var app = new CommandAppTester();
        app.Configure(config =>
        {
            config.AddCommand<NewCommand>("new");
        });

        // When
        var result = app.Run(new[] { "new", "CustomJournal" });

        // Then
        result.ExitCode.ShouldBe(0);
        result.Output.ShouldContain("CustomJournal");
    }

    [Fact]
    public void Should_Return_Error_When_Journal_Already_Exists()
    {
        // Given
        var console = new TestConsole();
        var app = new CommandAppTester();
        app.Configure(config =>
        {
            config.AddCommand<NewCommand>("new");
        });

        // Create the journal first
        var firstResult = app.Run(new[] { "new", "ExistingJournal" });
        firstResult.ExitCode.ShouldBe(0);

        // When - Try to create the same journal again
        var result = app.Run(new[] { "new", "ExistingJournal" });

        // Then
        result.ExitCode.ShouldBe(1);
        result.Output.ShouldContain("Error");
        result.Output.ShouldContain("already exists");
    }

    [Fact]
    public void Should_Create_Journal_In_Custom_Path()
    {
        // Given
        var console = new TestConsole();
        var app = new CommandAppTester();
        app.Configure(config =>
        {
            config.AddCommand<NewCommand>("new");
        });

        var customPath = Path.Combine(Path.GetTempPath(), "custom_journals");

        // When
        var result = app.Run(new[] { "new", "PathJournal", "--path", customPath });

        // Then
        result.ExitCode.ShouldBe(0);
        result.Output.ShouldContain("PathJournal");
        result.Output.ShouldContain(customPath);
    }
}
