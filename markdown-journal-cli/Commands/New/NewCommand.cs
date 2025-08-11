using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using markdown_journal_cli.Infrastructure;
using markdown_journal_cli.Exceptions;

namespace markdown_journal_cli.Commands.New;

[Description("Creates a new markdown journal")]
public sealed class NewCommand : Command<NewCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly IFileSystem _fileSystem;

    public NewCommand(IAnsiConsole console, IFileSystem fileSystem)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

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

        public override ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(JournalName))
            {
                return ValidationResult.Error("Journal name cannot be empty");
            }

            if (JournalName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                return ValidationResult.Error("Journal name contains invalid characters");
            }

            return ValidationResult.Success();
        }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        try
        {
            string journalDirectory = _fileSystem.CombinePaths(settings.FilePath ?? ".", settings.JournalName);
            
            if (_fileSystem.DirectoryExists(journalDirectory))
            {
                throw new JournalAlreadyExistsException(settings.JournalName, journalDirectory);
            }

            _fileSystem.CreateDirectory(journalDirectory);
            _console.MarkupLine($"[green]Success:[/] Journal [yellow]{settings.JournalName}[/] created at [blue]{journalDirectory}[/]");
            
            return 0;
        }
        catch (JournalAlreadyExistsException ex)
        {
            _console.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Error:[/] An unexpected error occurred: {ex.Message}");
            return 1;
        }
    }
}