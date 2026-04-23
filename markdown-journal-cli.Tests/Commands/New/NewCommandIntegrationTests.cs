using markdown_journal_cli.Commands.New;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.DependencyInjection;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.JournalTemplates;
using markdown_journal_cli.Infrastructure.Tracking;
using markdown_journal_cli.Infrastructure.Transactions;
using markdown_journal_cli.Services;
using markdown_journal_cli.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using Xunit;

namespace markdown_journal_cli.Tests.Commands.New;

/// <summary>
/// Integration tests for the new command using real services and file operations.
/// The full CLI pipeline is exercised against a real temp directory with no mocked dependencies.
/// </summary>
[Trait("Category", "Integration")]
public class NewCommandIntegrationTests : JournalIntegrationTestBase
{
    private readonly CommandAppTester _app;

    public NewCommandIntegrationTests() : base("_setup")
    {
        // JournalRoot exists (side-effect of base creating JournalRoot/_setup).
        // Tests will use JournalRoot as the parent dir and create fresh journals inside it.
        var hashService = new HashService();
        var fileTracking = new FileTracking(FileSystem, JournalSettings, hashService);
        var tocStructureRepository = new JournalTocStructureRepository(FileSystem, JournalSettings);
        var journalConfiguration = new JournalConfiguration(
            FileSystem,
            JournalSettings,
            NullLogger<JournalConfiguration>.Instance,
            fileTracking,
            tocStructureRepository
        );
        var templateManager = new TemplateManager(JournalSettings);
        var buffer = new InMemoryFileBuffer(FileSystem);
        var deletionStrategy = new InMemoryDeletionRollbackStrategy();
        var coordinator = new FileTransactionCoordinator(
            FileSystem, buffer, deletionStrategy, NullLoggerFactory.Instance
        );
        var console = new TestConsole();
        var rollbackReporter = new RollbackReporter(console, NullLogger<RollbackReporter>.Instance);

        var newJournalService = new NewJournalService(
            FileSystem,
            templateManager,
            journalConfiguration,
            fileTracking,
            JournalSettings,
            coordinator,
            rollbackReporter,
            NullLogger<NewJournalService>.Instance,
            tocStructureRepository
        );

        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(console);
        services.AddSingleton<IFileSystem>(FileSystem);
        services.AddSingleton<INewJournalService>(newJournalService);
        services.AddSingleton(JournalSettings);

        var registrar = new TypeRegistrar();
        foreach (var sd in services)
        {
            if (sd.ImplementationInstance != null)
                registrar.RegisterInstance(sd.ServiceType, sd.ImplementationInstance);
            else if (sd.ImplementationType != null)
                registrar.Register(sd.ServiceType, sd.ImplementationType);
        }

        _app = new CommandAppTester(registrar);
        _app.Configure(config =>
        {
            config.SetApplicationName("mdjournal");
            config.AddCommand<NewCommand>("new").WithDescription("Creates a new markdown journal.");
        });
    }

    [Fact]
    public void Execute_Should_CreateJournalSubdirectoryAndFiles_When_ValidNameAndPath()
    {
        // Act
        var result = _app.Run(["new", "FreshJournal", "--path", JournalRoot]);

        // Assert — command succeeded
        result.ExitCode.ShouldBe(0);
        result.Output.ShouldContain("FreshJournal");

        // Assert — journal directory and core files were created on disk
        var journalDir = Path.Combine(JournalRoot, "FreshJournal");
        Directory.Exists(journalDir).ShouldBeTrue();
        File.Exists(Path.Combine(journalDir, ".journalrc")).ShouldBeTrue();
        File.Exists(Path.Combine(journalDir, $".{JournalSettings.Value.AppName}")).ShouldBeTrue();
        File.Exists(Path.Combine(journalDir, "1a-TableOfContents.md")).ShouldBeTrue();
    }

    [Fact]
    public void Execute_Should_ReturnExitCode1_When_JournalAlreadyExists()
    {
        // Arrange — create the journal once
        _app.Run(["new", "ExistingJournal", "--path", JournalRoot]);

        // Act — try to create it again
        var result = _app.Run(["new", "ExistingJournal", "--path", JournalRoot]);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Output.ShouldContain("Error");
    }

    [Fact]
    public void Execute_Should_ReturnExitCode1_When_JournalNameContainsSpaces()
    {
        // Act
        var result = _app.Run(["new", "Invalid Name", "--path", JournalRoot]);

        // Assert
        result.ExitCode.ShouldNotBe(0);
    }
}
