using System.ComponentModel;
using markdown_journal_cli.Exceptions;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Services;
using Microsoft.Extensions.Options;
using Spectre.Console;
using Spectre.Console.Cli;

namespace markdown_journal_cli.Commands.New;

[Description("Creates a new markdown journal")]
public sealed class NewCommand(
    IAnsiConsole console,
    IFileSystem fileSystem,
    INewJournalService journalInitializer,
    IOptions<JournalSettings> journalSettings
) : Command<NewCommand.Settings>
{
    private readonly IAnsiConsole _console =
        console ?? throw new ArgumentNullException(nameof(console));
    private readonly IFileSystem _fileSystem =
        fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

    private readonly INewJournalService _journalInitializer =
        journalInitializer ?? throw new ArgumentNullException(nameof(journalInitializer));

    private readonly JournalSettings _journalSettings = journalSettings.Value;

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[name]")]
        [Description(
            "The name of the journal to create. If not specified, a default name will be used."
        )]
        public string? JournalName { get; set; }

        [CommandOption("-p|--path <filePath>")]
        [Description(
            "Specify the path where the journal will be created. If not specified, it will be created in the current directory."
        )]
        [DefaultValue(".")]
        public string? FilePath { get; set; }

        public override ValidationResult Validate()
        {
            // Reject empty or whitespace-only journal names
            if (JournalName != null && string.IsNullOrWhiteSpace(JournalName))
            {
                return ValidationResult.Error("Journal name cannot be empty or whitespace");
            }

            // Validate characters if name is provided
            if (
                !string.IsNullOrWhiteSpace(JournalName)
                && JournalName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            )
            {
                return ValidationResult.Error("Journal name contains invalid characters");
            }
            if (!string.IsNullOrWhiteSpace(JournalName) && JournalName.Contains(' '))
            {
                return ValidationResult.Error("Journal name cannot contain spaces");
            }

            return ValidationResult.Success();
        }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var journalName = settings.JournalName ?? _journalSettings.DefaultJournalName;
        try
        {
            string journalDirectory = _fileSystem.CombinePaths(
                settings.FilePath ?? ".",
                journalName
            );

            if (_fileSystem.DirectoryExists(journalDirectory))
            {
                throw new JournalAlreadyExistsException(journalName, journalDirectory);
            }

            _journalInitializer.Initialize(journalDirectory, journalName);

            _console.MarkupLine(
                $"[green]Success:[/] Journal [yellow]{journalName}[/] created at [blue]{journalDirectory}[/]"
            );

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
