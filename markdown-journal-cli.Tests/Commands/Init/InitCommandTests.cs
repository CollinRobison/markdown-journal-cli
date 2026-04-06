using markdown_journal_cli.Commands.Init;
using markdown_journal_cli.Exceptions;
using markdown_journal_cli.Infrastructure.DependencyInjection;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Services;
using markdown_journal_cli.Tests.Infrastructure.FileSystem;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;

namespace markdown_journal_cli.Tests.Commands.Init;

public class InitCommandTests
{
    private readonly TestConsole _console;
    private readonly TestFileSystem _fileSystem;
    private readonly TestInitJournalService _initService;
    private readonly IOptions<JournalSettings> _journalSettings;
    private readonly CommandAppTester _app;

    public InitCommandTests()
    {
        _console = new TestConsole();
        _fileSystem = new TestFileSystem();
        _initService = new TestInitJournalService();

        _journalSettings = Options.Create(
            new JournalSettings
            {
                AppName = "md-journal",
                JournalConfigFileName = ".journalrc",
                DefaultJournalName = "MyJournal",
                TableOfContentsFileName = "1a-TableOfContents",
            }
        );

        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(_console);
        services.AddSingleton<IFileSystem>(_fileSystem);
        services.AddSingleton<IInitJournalService>(_initService);
        services.AddSingleton(_journalSettings);
        services.AddSingleton<InitCommand>();

        var registrar = new TypeRegistrar();
        foreach (var service in services)
        {
            if (service.ImplementationInstance != null)
                registrar.RegisterInstance(service.ServiceType, service.ImplementationInstance);
            else if (service.ImplementationType != null)
                registrar.Register(service.ServiceType, service.ImplementationType);
        }

        _app = new CommandAppTester(registrar);
        _app.Configure(config =>
        {
            config.SetApplicationName(_journalSettings.Value.AppName);
            config
                .AddCommand<InitCommand>("init")
                .WithDescription(
                    "Initialises an existing directory as an mdjournal-managed journal."
                );
        });
    }

    [Fact]
    public void Should_Initialize_Journal_With_Explicit_Name()
    {
        // Given
        var dir = "/existing/notes";
        _fileSystem.CreateDirectory(dir);

        // When
        var result = _app.Run(["init", "MyNotes", "--path", dir]);

        // Then
        result.ExitCode.ShouldBe(0);
        result.Output.ShouldContain("MyNotes");
        _initService.Calls.ShouldContain(c => c.journalName == "MyNotes" && c.directory == dir);
    }

    [Fact]
    public void Should_Initialize_Journal_Using_Directory_Name_As_Default()
    {
        // Given — use a path whose last segment is the expected name
        var dir = "notes-dir";
        _fileSystem.CreateDirectory(dir);

        // When — no [name] argument supplied
        var result = _app.Run(["init", "--path", dir]);

        // Then
        result.ExitCode.ShouldBe(0);
        _initService.Calls.ShouldHaveSingleItem();
        _initService.Calls[0].journalName.ShouldBe("notes-dir");
    }

    [Fact]
    public void Should_Return_Error_When_Directory_Does_Not_Exist()
    {
        // When
        var result = _app.Run(["init", "--path", "/nonexistent"]);

        // Then
        result.ExitCode.ShouldBe(1);
        result.Output.ShouldContain("Error");
        _initService.Calls.ShouldBeEmpty();
    }

    [Fact]
    public void Should_Return_Error_When_Journal_Already_Initialized()
    {
        // Given
        var dir = "/existing/journal";
        _fileSystem.CreateDirectory(dir);
        _fileSystem.CreateFile(dir, ".journalrc", "{}");

        // When
        var result = _app.Run(["init", "--path", dir]);

        // Then
        result.ExitCode.ShouldBe(1);
        result.Output.ShouldContain("Error");
        result.Output.ShouldContain("already a managed journal");
        _initService.Calls.ShouldBeEmpty();
    }

    [Fact]
    public void Should_Return_Error_When_Toc_File_Already_Exists()
    {
        // Given
        var dir = "/existing/notes";
        _fileSystem.CreateDirectory(dir);
        _initService.ExceptionToThrow = new TocFileAlreadyExistsException(
            dir,
            "1a-TableOfContents.md"
        );

        // When
        var result = _app.Run(["init", "--path", dir]);

        // Then
        result.ExitCode.ShouldBe(1);
        result.Output.ShouldContain("Error");
    }

    [Fact]
    public void Should_Pass_Custom_TOC_Name_To_Service()
    {
        // Given
        var dir = "/existing/notes";
        _fileSystem.CreateDirectory(dir);

        // When
        var result = _app.Run(["init", "--path", dir, "--toc", "my-toc"]);

        // Then
        result.ExitCode.ShouldBe(0);
        _initService.Calls.ShouldHaveSingleItem();
        _initService.Calls[0].tocName.ShouldBe("my-toc");
    }

    [Fact]
    public void Should_Pass_Null_TOC_Name_When_Not_Specified()
    {
        // Given
        var dir = "/existing/notes";
        _fileSystem.CreateDirectory(dir);

        // When
        var result = _app.Run(["init", "--path", dir]);

        // Then
        result.ExitCode.ShouldBe(0);
        _initService.Calls.ShouldHaveSingleItem();
        _initService.Calls[0].tocName.ShouldBeNull();
    }

    [Fact]
    public void Should_Validate_Journal_Name_For_Invalid_Characters()
    {
        // When
        var result = _app.Run(["init", "Invalid/Name"]);

        // Then
        result.ExitCode.ShouldNotBe(0);
        result.Output.ShouldContain("invalid characters");
    }

    [Fact]
    public void Should_Validate_Empty_Journal_Name()
    {
        // When
        var result = _app.Run(["init", ""]);

        // Then
        result.ExitCode.ShouldNotBe(0);
        result.Output.ShouldContain("cannot be empty");
    }

    [Fact]
    public void Should_Initialize_With_Custom_Path()
    {
        // Given
        var customPath = "/my/notes/folder";
        _fileSystem.CreateDirectory(customPath);

        // When
        var result = _app.Run(["init", "MyJournal", "-p", customPath]);

        // Then
        result.ExitCode.ShouldBe(0);
        _initService.Calls.ShouldHaveSingleItem();
        _initService.Calls[0].directory.ShouldBe(customPath);
    }

    [Fact]
    public void Should_Return_Error_On_Unexpected_Exception()
    {
        // Given
        var dir = "/existing/notes";
        _fileSystem.CreateDirectory(dir);
        _initService.ExceptionToThrow = new InvalidOperationException("Disk full");

        // When
        var result = _app.Run(["init", "--path", dir]);

        // Then
        result.ExitCode.ShouldBe(1);
        result.Output.ShouldContain("Error");
        result.Output.ShouldContain("unexpected");
    }

    [Fact]
    public void Execute_PathContainingBrackets_DoesNotThrowMarkupException_WhenDirMissing()
    {
        // Paths like "/repos/my[project]/journal" would cause Spectre MarkupException before fix.
        // The command should emit a plain error message, not crash.
        var result = _app.Run(["init", "--path", "/nonexistent/my[project]/journal"]);

        result.ExitCode.ShouldBe(1);
        result.Output.ShouldContain("Error");
        result.Output.ShouldNotContain("MarkupException");
        result.Output.ShouldContain("does not exist");
    }

    [Fact]
    public void Execute_PathContainingBrackets_DoesNotThrowMarkupException_WhenAlreadyManaged()
    {
        // Init on an already-managed journal should display the path safely.
        var dir = "/test/my[journal]";
        _fileSystem.CreateDirectory(dir);
        _fileSystem.CreateFile(dir, ".journalrc", "{}");

        var result = _app.Run(["init", "--path", dir]);

        result.ExitCode.ShouldBe(1);
        result.Output.ShouldContain("Error");
        result.Output.ShouldNotContain("MarkupException");
    }

    /// <summary>
    /// Test double for <see cref="IInitJournalService"/> that records calls and supports
    /// configurable exception injection.
    /// </summary>
    private sealed class TestInitJournalService : IInitJournalService
    {
        public List<(string directory, string journalName, string? tocName)> Calls { get; } = [];
        public Exception? ExceptionToThrow { get; set; }

        public void Initialize(
            string journalDirectory,
            string journalName,
            string? tableOfContentsName
        )
        {
            if (ExceptionToThrow is not null)
                throw ExceptionToThrow;

            Calls.Add((journalDirectory, journalName, tableOfContentsName));
        }
    }
}
