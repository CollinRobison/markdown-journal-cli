using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using System.IO;

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
        // Also implement testing. 
        // need to create markdown files somehow
        //eventually implement a template 
        string journalDirectory = Path.Combine(settings.FilePath ?? ".", settings.JournalName);
        if (Directory.Exists(journalDirectory))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] A journal with the name [yellow]{settings.JournalName}[/] already exists at the specified path.");
            return 1;
        }

        try
        {
            Directory.CreateDirectory(journalDirectory);
            AnsiConsole.MarkupLine($"[green]Success:[/] Journal [yellow]{settings.JournalName}[/] created at [blue]{journalDirectory}[/]");
        }
        catch (IOException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Could not create journal at the specified path. {ex.Message}");
            return 1;
        }
        catch (UnauthorizedAccessException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Access denied. {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] An unexpected error occurred. {ex.Message}");
            return 1;
        }
        return 0;
    }
}
          