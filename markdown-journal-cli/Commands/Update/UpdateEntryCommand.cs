using System;
using System.ComponentModel;
using markdown_journal_cli.Exceptions;
using markdown_journal_cli.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace markdown_journal_cli.Commands.Update;

/// <summary>
/// Updates a journal entry's file name, TOC display title, heading location, or ignore status.
/// </summary>
[Description("Updates a journal entry's settings.")]
public sealed class UpdateEntryCommand(
    IAnsiConsole console,
    IJournalFileUpdateService fileUpdateService
) : Command<UpdateEntrySettings>
{
    private readonly IAnsiConsole _console =
        console ?? throw new ArgumentNullException(nameof(console));
    private readonly IJournalFileUpdateService _fileUpdateService =
        fileUpdateService ?? throw new ArgumentNullException(nameof(fileUpdateService));

    public override int Execute(CommandContext context, UpdateEntrySettings settings)
    {
        try
        {
            _fileUpdateService.UpdateEntry(
                settings.FilePath,
                settings.FileName,
                settings.EntryName,
                settings.EntryTitle,
                settings.Headings,
                settings.IgnoreFile,
                settings.UnignoreFile,
                !settings.NoBacklinks
            );

            _console.MarkupLine(
                $"[green]Success:[/] Entry '{settings.FileName}' updated successfully."
            );
            return 0;
        }
        catch (JournalrcNotFoundException ex)
        {
            _console.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
        catch (FileNotFoundException ex)
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
