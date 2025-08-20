using markdown_journal_cli.Commands.New;
using markdown_journal_cli.Infrastructure;
using markdown_journal_cli.JournalTemplates;
using Spectre.Console.Cli;

namespace markdown_journal_cli;

public static class Program
{
    public static int Main(string[] args)
    {
        // Set up dependency injection
        var registrar = new TypeRegistrar();
        registrar.Register(typeof(IFileSystem), typeof(FileSystem));
        registrar.Register(typeof(ITemplateManager), typeof(TemplateManager));
        
        var app = new CommandApp(registrar);
        app.Configure(config =>
        {
            config.SetApplicationName("md-journal");
            config.ValidateExamples();
            config.AddExample("new", "TestJournal", "--path", "Source/Repos");

            // New 
            config.AddCommand<NewCommand>("new");

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
