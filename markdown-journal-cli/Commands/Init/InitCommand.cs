using System.ComponentModel;
using markdown_journal_cli.Exceptions;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Services;
using Microsoft.Extensions.Options;
using Spectre.Console;
using Spectre.Console.Cli;

namespace markdown_journal_cli.Commands.Init;

[Description("Initializes an existing directory as an mdjournal managed journal")]
public sealed class InitCommand(
    IAnsiConsole console,
    IFileSystem fileSystem,
    IInitJournalService initJournalService,
    IOptions<JournalSettings> journalSettings
) : Command<InitSettings>
{
    private readonly IAnsiConsole _console =
        console ?? throw new ArgumentNullException(nameof(console));
    private readonly IFileSystem _fileSystem =
        fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    private readonly IInitJournalService _initJournalService =
        initJournalService ?? throw new ArgumentNullException(nameof(initJournalService));
    private readonly JournalSettings _journalSettings = journalSettings.Value;

    private static string? NullIfEmpty(string? s) => string.IsNullOrEmpty(s) ? null : s;

    public override int Execute(CommandContext context, InitSettings settings)
    {
        var filePath = settings.FilePath ?? ".";
        var resolvedPath = _fileSystem.GetFullPath(filePath);
        var journalName =
            settings.JournalName
            ?? NullIfEmpty(_fileSystem.GetFileName(resolvedPath))
            ?? _journalSettings.DefaultJournalName;

        if (!_fileSystem.DirectoryExists(filePath))
        {
            _console.MarkupLine(
                $"[red]Error:[/] Directory '[blue]{filePath}[/]' does not exist."
            );
            return 1;
        }

        var journalrcPath = _fileSystem.CombinePaths(
            filePath,
            _journalSettings.JournalConfigFileName
        );
        if (_fileSystem.FileExists(journalrcPath))
        {
            _console.MarkupLine(
                $"[red]Error:[/] '[blue]{filePath}[/]' is already a managed journal."
            );
            return 1;
        }

        try
        {
            _initJournalService.Initialize(filePath, journalName, settings.TableOfContentsName);
            _console.MarkupLine(
                $"[green]Success:[/] Journal [yellow]{journalName}[/] initialised at [blue]{filePath}[/]"
            );
            return 0;
        }
        catch (TocFileAlreadyExistsException ex)
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
