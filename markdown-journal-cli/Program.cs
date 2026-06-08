using System.Reflection;
using markdown_journal_cli.Commands.Add;
using markdown_journal_cli.Commands.Init;
using markdown_journal_cli.Commands.New;
using markdown_journal_cli.Commands.Remove;
using markdown_journal_cli.Commands.Update;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.DependencyInjection;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.JournalTemplates;
using markdown_journal_cli.Infrastructure.Tracking;
using markdown_journal_cli.Infrastructure.Transactions;
using markdown_journal_cli.Infrastructure.Validation;
using markdown_journal_cli.Services;
using markdown_journal_cli.Services.AddToc;
using markdown_journal_cli.Services.RemoveEntry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Spectre.Console;
using Spectre.Console.Cli;

namespace markdown_journal_cli;

public static class Program
{
    public static int Main(string[] args)
    {
        // Create host with configuration from the application directory
        var host = Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings
            {
                Args = args,
                ContentRootPath = AppContext.BaseDirectory,
            }
        );

        // Configure options
        host.Services.AddOptions<JournalSettings>()
            .BindConfiguration(JournalSettings.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Register Spectre.Console services
        host.Services.AddSingleton<IAnsiConsole>(AnsiConsole.Console);

        // Register services
        host.Services.AddSingleton<IFileSystem, FileSystem>();
        host.Services.AddSingleton<IInMemoryFileBuffer, InMemoryFileBuffer>();
        host.Services.AddSingleton<ITemplateManager, TemplateManager>();
        host.Services.AddSingleton<IJournalTocStructureRepository, JournalTocStructureRepository>();
        host.Services.AddSingleton<IJournalConfiguration, JournalConfiguration>();
        host.Services.AddSingleton<IJournalValidator, JournalValidator>();
        host.Services.AddSingleton<INewJournalService, NewJournalService>();
        host.Services.AddSingleton<IInitJournalService, InitJournalService>();
        host.Services.AddSingleton<IEntryFormatterService, EntryFormatterService>();
        host.Services.AddSingleton<IHashService, HashService>();
        host.Services.AddSingleton<IFileTracking, FileTracking>();
        host.Services.AddSingleton<ITableOfContentsService, TableOfContentsService>();
        host.Services.AddSingleton<ITableOfContentsMarkdownParser, TableOfContentsMarkdownParser>();
        host.Services.AddSingleton<IJournalConfigGenerator, JournalConfigGenerator>();
        host.Services.AddSingleton<IJournalEntryService, JournalEntryService>();
        host.Services.AddSingleton<IJournalUpdateService, JournalUpdateService>();
        host.Services.AddSingleton<IJournalFileUpdateService, JournalFileUpdateService>();
        host.Services.AddSingleton<IMarkdownLinkRewriter, MarkdownLinkRewriter>();
        host.Services.AddSingleton<IRemoveEntryService, RemoveEntryService>();
        host.Services.AddSingleton<IDryRunRenderer, DryRunRenderer>();
        host.Services.AddSingleton<IAddTocService, AddTocService>();

        // Rollback infrastructure
        host.Services.AddSingleton<IDeletionRollbackStrategy, InMemoryDeletionRollbackStrategy>();
        host.Services.AddSingleton<IFileTransactionCoordinator, FileTransactionCoordinator>();
        host.Services.AddSingleton<IRollbackReporter, RollbackReporter>();

        // Register commands
        host.Services.AddSingleton<NewCommand>();
        host.Services.AddSingleton<InitCommand>();
        host.Services.AddSingleton<AddEntry>();
        host.Services.AddSingleton<AddJournalrc>();
        host.Services.AddSingleton<AddTableOfContents>();
        host.Services.AddSingleton<AddFileTracking>();
        host.Services.AddSingleton<UpdateCommand>();
        host.Services.AddSingleton<UpdateEntryCommand>();
        host.Services.AddSingleton<RemoveEntryCommand>();

        // Build the host and get the service provider
        var builtHost = host.Build();

        // Get settings
        var settings = builtHost.Services.GetRequiredService<IOptions<JournalSettings>>().Value;

        // Read version from assembly metadata (set by <Version> in .csproj)
        var version =
            Assembly
                .GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion
            ?? "0.0.0";

        // Set up dependency injection
        var registrar = new TypeRegistrar(builtHost.Services);

        var app = new CommandApp(registrar);
        app.Configure(config =>
        {
            config.SetApplicationName(settings.AppName);
            config.SetApplicationVersion(version);
            config.ValidateExamples();
            config.AddExample("new", "TestJournal", "--path", "Source/Repos");
            config.AddExample(
                "add",
                "--path",
                "Source/Repos/TestJournal",
                "entry",
                "Meeting_Notes",
                "--heading",
                "Work",
                "--subheading",
                "Team-Standup"
            );
            config.AddExample("update", "--path", "Source/Repos/TestJournal", "journal");

            // New
            config.AddCommand<NewCommand>("new");

            // Init
            config.AddCommand<InitCommand>("init");

            config.AddBranch<AddSettings>(
                "add",
                add =>
                {
                    add.SetDescription("Creates a new specified file to an existing journal.");
                    add.AddCommand<AddEntry>("entry")
                        .WithExample(
                            "add",
                            "--path",
                            "Source/Repos/TestJournal",
                            "entry",
                            "Meeting_Notes",
                            "--heading",
                            "Work",
                            "--subheading",
                            "Team-Standup"
                        );
                    add.AddCommand<AddJournalrc>("config");
                    add.AddCommand<AddTableOfContents>("toc");
                    add.AddCommand<AddFileTracking>("tracking");
                }
            );

            config.AddBranch<UpdateSettings>(
                "update",
                update =>
                {
                    update.SetDescription("Updates various aspects of an existing journal.");
                    update
                        .AddCommand<UpdateCommand>("journal")
                        .WithExample("update", "--path", "Source/Repos/TestJournal", "journal");
                    update
                        .AddCommand<UpdateEntryCommand>("entry")
                        .WithExample(
                            "update",
                            "--path",
                            "Source/Repos/TestJournal",
                            "entry",
                            "OldFileName",
                            "--name",
                            "NewFileName",
                            "--headings",
                            "Projects-Completed_Tasks"
                        );
                }
            );

            config
                .AddBranch<RemoveSettings>(
                    "remove",
                    remove =>
                    {
                        remove.SetDescription("Removes a specified file from an existing journal.");
                        remove
                            .AddCommand<RemoveEntryCommand>("entry")
                            .WithExample(
                                "remove",
                                "--path",
                                "Source/Repos/TestJournal",
                                "entry",
                                "old_notes"
                            )
                            .WithExample(
                                "remove",
                                "--path",
                                "Source/Repos/TestJournal",
                                "entry",
                                "old_notes",
                                "--force"
                            )
                            .WithExample(
                                "remove",
                                "--path",
                                "Source/Repos/TestJournal",
                                "entry",
                                "old_notes",
                                "--clean-refs"
                            );
                    }
                )
                .WithAlias("rm");
        });

        // Spectre.Console returns -1 for validation failures; normalise to 1 so callers
        // get the documented "pre-write guard failed" exit code instead of bash's 255.
        var exitCode = app.Run(args);
        return exitCode == -1 ? 1 : exitCode;
    }
}
