using markdown_journal_cli.Commands.Add;
using markdown_journal_cli.Commands.New;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.DependencyInjection;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.JournalTemplates;
using Microsoft.Extensions.Configuration;
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
        var host = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory
        });
        
        // Configure options
        host.Services.AddOptions<JournalSettings>()
            .BindConfiguration(JournalSettings.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Register Spectre.Console services
        host.Services.AddSingleton<IAnsiConsole>(AnsiConsole.Console);

        // Register services
        host.Services.AddSingleton<IFileSystem, FileSystem>();
        host.Services.AddSingleton<ITemplateManager, TemplateManager>();
        host.Services.AddSingleton<IJournalConfiguration, JournalConfiguration>();
        host.Services.AddSingleton<IJournalInitializer, JournalInitializer>();
        
        // Register commands
        host.Services.AddSingleton<NewCommand>();
        host.Services.AddSingleton<AddEntry>();
        host.Services.AddSingleton<AddJournalrc>();
        host.Services.AddSingleton<AddTableOfContents>();

        // Build the host and get the service provider
        var builtHost = host.Build();
        
        // Get settings
        var settings = builtHost.Services.GetRequiredService<IOptions<JournalSettings>>().Value;
        
        // Set up dependency injection
        var registrar = new TypeRegistrar(builtHost.Services);

        var app = new CommandApp(registrar);
        app.Configure(config =>
        {
            config.SetApplicationName(settings.AppName);
            config.ValidateExamples();
            config.AddExample("new", "TestJournal", "--path", "Source/Repos");

            // New
            config.AddCommand<NewCommand>("new");
            
            config.AddBranch<AddSettings>("add", add =>
            {
                add.SetDescription("Creates a new specified file to an existing journal.");
                add.AddCommand<AddEntry>("entry");
                add.AddCommand<AddJournalrc>("config");
                add.AddCommand<AddTableOfContents>("toc");
            });
            // Add
            // config.AddBranch<AddSettings>("add", add =>
            // {
            //     add.SetDescription("Add a package or reference to a .NET project");
            //     add.AddCommand<AddPackageCommand>("package");
            //     add.AddCommand<AddReferenceCommand>("reference");
            // });
        });

        return app.Run(args);
    }
}
