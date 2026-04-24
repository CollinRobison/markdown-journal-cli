using markdown_journal_cli.Commands.Init;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.DependencyInjection;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.Tracking;
using markdown_journal_cli.Infrastructure.Transactions;
using markdown_journal_cli.Services;
using markdown_journal_cli.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using Xunit;

namespace markdown_journal_cli.Tests.Commands.Init;

/// <summary>
/// Integration tests for the init command using real services and file operations.
/// The full CLI pipeline is exercised against a real temp directory with no mocked dependencies.
/// </summary>
[Trait("Category", "Integration")]
public class InitCommandIntegrationTests : JournalIntegrationTestBase
{
    // JournalPath already exists (created by base constructor) — init command initializes it
    private readonly CommandAppTester _app;

    public InitCommandIntegrationTests() : base("InitTest")
    {
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
        var entryFormatter = new EntryFormatterService(JournalSettings);
        var tocParser = new TableOfContentsMarkdownParser();
        var configGenerator = new JournalConfigGenerator(
            FileSystem,
            tocParser,
            fileTracking,
            entryFormatter,
            journalConfiguration,
            JournalSettings,
            tocStructureRepository
        );
        var tocService = new TableOfContentsService(
            FileSystem,
            journalConfiguration,
            JournalSettings,
            NullLogger<TableOfContentsService>.Instance,
            tocStructureRepository
        );
        var buffer = new InMemoryFileBuffer(FileSystem);
        var deletionStrategy = new InMemoryDeletionRollbackStrategy();
        var coordinator = new FileTransactionCoordinator(
            FileSystem, buffer, deletionStrategy, NullLoggerFactory.Instance
        );
        var console = new TestConsole();
        var rollbackReporter = new RollbackReporter(console, NullLogger<RollbackReporter>.Instance);

        var initJournalService = new InitJournalService(
            FileSystem,
            configGenerator,
            fileTracking,
            tocService,
            JournalSettings,
            coordinator,
            rollbackReporter
        );

        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(console);
        services.AddSingleton<IFileSystem>(FileSystem);
        services.AddSingleton<IInitJournalService>(initJournalService);
        services.AddSingleton(JournalSettings);
        services.AddSingleton<ILogger<InitCommand>>(NullLogger<InitCommand>.Instance);

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
            config.AddCommand<InitCommand>("init")
                .WithDescription("Initialises an existing directory as a managed journal.");
        });
    }

    [Fact]
    public void Execute_Should_CreateJournalFiles_When_DirectoryExistsAndIsUninitialized()
    {
        // Act — JournalPath already exists, init command initializes it
        var result = _app.Run(["init", "InitTest", "--path", JournalPath]);

        // Assert — command succeeded
        result.ExitCode.ShouldBe(0);
        result.Output.ShouldContain("InitTest");

        // Assert — core journal files exist on disk
        File.Exists(Path.Combine(JournalPath, ".journalrc")).ShouldBeTrue();
        File.Exists(Path.Combine(JournalPath, $".{JournalSettings.Value.AppName}")).ShouldBeTrue();
        File.Exists(Path.Combine(JournalPath, "1a-TableOfContents.md")).ShouldBeTrue();
    }

    [Fact]
    public void Execute_Should_ReturnExitCode1_When_DirectoryDoesNotExist()
    {
        // Act — pass a path that does not exist
        var nonExistent = Path.Combine(JournalRoot, "doesnotexist");
        var result = _app.Run(["init", "InitTest", "--path", nonExistent]);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Output.ShouldContain("Error");
    }

    [Fact]
    public void Execute_Should_ReturnExitCode1_When_JournalAlreadyInitialized()
    {
        // Arrange — initialize the journal first
        _app.Run(["init", "InitTest", "--path", JournalPath]);

        // Act — try to initialize again
        var result = _app.Run(["init", "InitTest", "--path", JournalPath]);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Output.ShouldContain("Error");
    }
}
