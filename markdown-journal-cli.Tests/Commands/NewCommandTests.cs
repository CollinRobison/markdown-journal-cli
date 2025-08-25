using Spectre.Console.Testing;
using Spectre.Console.Cli;
using markdown_journal_cli.Commands.New;
using markdown_journal_cli.Infrastructure;
using markdown_journal_cli.Tests.Infrastructure;
using Xunit;
using Shouldly;
using markdown_journal_cli.JournalTemplates;

namespace markdown_journal_cli.Tests.Commands;

public class NewCommandTests
{
    private readonly TestConsole _console;
    private readonly TestFileSystem _fileSystem;
    private readonly CommandAppTester _app;

    private readonly TemplateManager _templateManager;

    public NewCommandTests()
    {
        _console = new TestConsole();
        _fileSystem = new TestFileSystem();
        _templateManager = new TemplateManager();

        var registrar = new TypeRegistrar()
            .RegisterInstance(_console)
            .RegisterInstance<IFileSystem>(_fileSystem)
            .RegisterInstance<ITemplateManager>(_templateManager);

        _app = new CommandAppTester(registrar);
        _app.Configure(config =>
        {
            config.SetApplicationName("md-journal");
            config.PropagateExceptions();
            config.AddCommand<NewCommand>("new")
                .WithDescription("Creates a new markdown journal.");
        });
    }

    [Fact]
    public void Should_Create_New_Journal_With_Default_Name()
    {
        // When
        var result = _app.Run(["new", "MyJournal"]);

        // Then
        result.ExitCode.ShouldBe(0);
        result.Output.ShouldContain("MyJournal");
        _fileSystem.DirectoryExists("./MyJournal").ShouldBeTrue();
    }

    [Fact]
    public void Should_Create_New_Journal_With_Custom_Name()
    {
        // When
        var result = _app.Run(["new", "CustomJournal"]);

        // Then
        result.ExitCode.ShouldBe(0);
        result.Output.ShouldContain("CustomJournal");
        _fileSystem.DirectoryExists("./CustomJournal").ShouldBeTrue();
    }

    [Fact]
    public void Should_Return_Error_When_Journal_Already_Exists()
    {
        // Given
        var journalName = "ExistingJournal";
        var path = Path.Combine(".", journalName);
        _fileSystem.CreateDirectory(path);

        // When
        var result = _app.Run(["new", journalName]);

        // Then
        result.ExitCode.ShouldBe(1);
        result.Output.ShouldContain("Error");
        result.Output.ShouldContain("already exists");
    }

    [Theory]
    [InlineData("--path")]
    [InlineData("-p")]
    public void Should_Create_Journal_In_Custom_Path(string pathOption)
    {
        // Given
        var customPath = Path.Combine("custom", "journals");
        var journalName = "PathJournal";
        var expectedPath = Path.Combine(customPath, journalName);

        // When
        var result = _app.Run(["new", journalName, $"{pathOption}", customPath]);

        // Then
        result.ExitCode.ShouldBe(0);
        result.Output.ShouldContain(journalName);
        result.Output.ShouldContain(customPath);
        _fileSystem.DirectoryExists(expectedPath).ShouldBeTrue();
    }

    [Fact]
    public void Should_Validate_Journal_Name_For_Invalid_Characters()
    {
        // Given
        var invalidName = "Invalid/Name";

        // When
        var exception = Should.Throw<CommandRuntimeException>(() =>
            _app.Run(["new", invalidName]));

        // Then
        exception.Message.ShouldContain("invalid characters");
    }

    [Fact]
    public void Should_Validate_Empty_Journal_Name()
    {
        // When
        var exception = Should.Throw<CommandRuntimeException>(() =>
            _app.Run(["new", ""]));

        // Then
        exception.Message.ShouldContain("cannot be empty");
    }
}

