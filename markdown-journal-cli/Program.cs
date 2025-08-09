using Demo.Commands.Run;
using markdown_journal_cli.Commands.New;
using Spectre.Console.Cli;

namespace markdown_journal_cli;

public static class Program
{
    public static int Main(string[] args)
    {
        var app = new CommandApp();
        app.Configure(config =>
        {
            config.SetApplicationName("md-journal");
            config.ValidateExamples();
            config.AddExample("run", "--no-build");
            config.AddExample("new", "TestJournal");

            // Run
            config.AddCommand<RunCommand>("run");

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
