using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace markdown_journal_cli.Commands.New;

[Description("Creates a new markdown journal")]
public sealed class NewCommand : Command<NewCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[name]")]
        [Description("The name of the journal to create. If not specified, a default name will be used.")]
        [DefaultValue("MyJournal")]
        public required string JournalName { get; set; }

        [CommandOption("-p|--path <filePath>")]
        [Description("Specify the path where the journal will be created. If not specified, it will be created in the current directory.")]
        [DefaultValue(".")]
        public string? FilePath { get; set; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        // need to implement an actual journal creation logic here
        // will want it to be cross-platform, so using .NET APIs (Copilot said that)
        // Also implement testing. 
        AnsiConsole.MarkupLine($"Creating journal: [green]{settings.JournalName}[/]");
        return 0;
    }
}
          