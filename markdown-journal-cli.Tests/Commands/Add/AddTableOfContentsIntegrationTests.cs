using System.Text.Json;
using markdown_journal_cli.Commands.Add;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.DependencyInjection;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.Tracking;
using markdown_journal_cli.Infrastructure.Transactions;
using markdown_journal_cli.Services;
using markdown_journal_cli.Services.AddToc;
using markdown_journal_cli.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using Xunit;

namespace markdown_journal_cli.Tests.Commands.Add;

/// <summary>
/// Integration tests for the <c>add toc</c> command (User Story 4).
/// Uses real services and disk I/O. No mocks.
/// </summary>
[Trait("Category", "Integration")]
public class AddTableOfContentsIntegrationTests : JournalIntegrationTestBase
{
    private readonly CommandAppTester _app;
    private readonly string _metadataDir;
    private readonly string _tocStructurePath;
    private readonly string _tocMdPath;

    public AddTableOfContentsIntegrationTests()
        : base("TocJournal")
    {
        _metadataDir = Path.Combine(JournalPath, JournalSettings.Value.MetadataDirName);
        _tocStructurePath = Path.Combine(_metadataDir, JournalSettings.Value.TocStructureFileName);
        _tocMdPath = Path.Combine(
            JournalPath,
            $"{JournalSettings.Value.TableOfContentsFileName}.md"
        );

        // Seed the journal WITHOUT the two TOC artifacts so tests can exercise creation
        SeedJournalWithoutTocArtifacts();

        var hashService = new HashService();
        var fileTracking = new FileTracking(FileSystem, JournalSettings, hashService);
        var tocStructureRepository = new JournalTocStructureRepository(FileSystem, JournalSettings);
        var journalConfiguration = new JournalConfiguration(
            FileSystem,
            JournalSettings,
            NullLogger<JournalConfiguration>.Instance,
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
            FileSystem,
            buffer,
            deletionStrategy,
            NullLoggerFactory.Instance
        );
        var console = new TestConsole();
        var rollbackReporter = new RollbackReporter(console, NullLogger<RollbackReporter>.Instance);

        var addTocService = new AddTocService(
            FileSystem,
            journalConfiguration,
            tocStructureRepository,
            tocService,
            fileTracking,
            coordinator,
            rollbackReporter,
            JournalSettings
        );

        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(console);
        services.AddSingleton<IFileSystem>(FileSystem);
        services.AddSingleton<IAddTocService>(addTocService);
        services.AddSingleton<IRollbackReporter>(rollbackReporter);
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
            config.AddBranch<AddSettings>(
                "add",
                add =>
                {
                    add.AddCommand<AddTableOfContents>("toc");
                }
            );
        });
    }

    private void SeedJournalWithoutTocArtifacts()
    {
        var settings = JournalSettings.Value;

        var journalrcContent = JsonSerializer.Serialize(
            new
            {
                journalName = "TocJournal",
                tableOfContents = new
                {
                    file = $"{settings.TableOfContentsFileName}.md",
                    extensions = new[] { ".md" },
                    ignoreFiles = Array.Empty<string>(),
                },
            },
            new JsonSerializerOptions { WriteIndented = true }
        );
        File.WriteAllText(
            Path.Combine(JournalPath, settings.JournalConfigFileName),
            journalrcContent
        );

        Directory.CreateDirectory(_metadataDir);
        File.WriteAllText(Path.Combine(_metadataDir, settings.TrackingFileName), "{}");
        // Intentionally NOT writing .journaltoc or the markdown TOC file
    }

    private void RemoveTocArtifacts()
    {
        if (File.Exists(_tocStructurePath))
            File.Delete(_tocStructurePath);
        if (File.Exists(_tocMdPath))
            File.Delete(_tocMdPath);
    }

    [Fact]
    public void AddToc_NoFlags_CreatesBothArtifacts()
    {
        // Arrange — neither artifact exists
        File.Exists(_tocStructurePath).ShouldBeFalse();
        File.Exists(_tocMdPath).ShouldBeFalse();

        // Act
        var result = _app.Run(["add", "--path", JournalPath, "toc"]);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.Output.ShouldContain("Success");
        File.Exists(_tocStructurePath).ShouldBeTrue(".journaltoc should have been created");
        File.Exists(_tocMdPath).ShouldBeTrue("Markdown TOC file should have been created");
    }

    [Fact]
    public void AddToc_StructureOnly_CreatesOnlyJournalToc()
    {
        // Arrange — neither artifact exists
        RemoveTocArtifacts();

        // Act
        var result = _app.Run(["add", "--path", JournalPath, "toc", "--structure-only"]);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.Output.ShouldContain("Success");
        File.Exists(_tocStructurePath).ShouldBeTrue(".journaltoc should have been created");
        File.Exists(_tocMdPath)
            .ShouldBeFalse("Markdown TOC file should NOT have been created with --structure-only");
    }

    [Fact]
    public void AddToc_MdOnly_CreatesOnlyMarkdownToc()
    {
        // Arrange — neither artifact exists
        RemoveTocArtifacts();

        // Act
        var result = _app.Run(["add", "--path", JournalPath, "toc", "--md-only"]);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.Output.ShouldContain("Success");
        File.Exists(_tocMdPath).ShouldBeTrue("Markdown TOC file should have been created");
        File.Exists(_tocStructurePath)
            .ShouldBeFalse(".journaltoc should NOT have been created with --md-only");
    }

    [Fact]
    public void AddToc_BothAlreadyExist_ReturnsExitCode1WithWarning()
    {
        // Arrange — pre-create both artifacts
        File.WriteAllText(_tocStructurePath, """{"Structure":{"Topics":[]},"RootEntries":[]}""");
        File.WriteAllText(_tocMdPath, "# Table of Contents\n");

        // Act
        var result = _app.Run(["add", "--path", JournalPath, "toc"]);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Output.ShouldContain("Warning");
    }

    [Fact]
    public void AddToc_MutuallyExclusiveFlags_ReturnsExitCode1WithError()
    {
        // Act
        var result = _app.Run(
            ["add", "--path", JournalPath, "toc", "--structure-only", "--md-only"]
        );

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Output.ShouldContain("Error");
    }

    [Fact]
    public void AddToc_NoFlags_FailsGracefully_WhenJournalrcMissing()
    {
        // Arrange — remove .journalrc
        var journalrcPath = Path.Combine(JournalPath, JournalSettings.Value.JournalConfigFileName);
        if (File.Exists(journalrcPath))
            File.Delete(journalrcPath);

        // Act
        var result = _app.Run(["add", "--path", JournalPath, "toc"]);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Output.ShouldContain("Error");
    }
}
