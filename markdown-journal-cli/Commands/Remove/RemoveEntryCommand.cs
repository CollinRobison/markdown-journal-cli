using System.ComponentModel;
using markdown_journal_cli.Commands;
using markdown_journal_cli.Exceptions;
using markdown_journal_cli.Infrastructure.Transactions;
using markdown_journal_cli.Services.RemoveEntry;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;

namespace markdown_journal_cli.Commands.Remove;

[Description(
    "Removes a journal entry file, its config entry, its tracking record, and regenerates the TOC."
)]
public sealed class RemoveEntryCommand(
    IAnsiConsole console,
    IRemoveEntryService removeEntryService,
    ILogger<RemoveEntryCommand> logger
) : JournalCommand<RemoveEntrySettings>
{
    private readonly IAnsiConsole _console =
        console ?? throw new ArgumentNullException(nameof(console));
    private readonly IRemoveEntryService _removeEntryService =
        removeEntryService ?? throw new ArgumentNullException(nameof(removeEntryService));
    private readonly ILogger<RemoveEntryCommand> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    protected override int ExecuteCore(CommandContext context, RemoveEntrySettings settings)
    {
        _logger.LogDebug(
            "RemoveEntryCommand executing for '{FileName}' in '{FilePath}'",
            settings.FileName,
            settings.FilePath
        );

        try
        {
            if (!settings.Force)
            {
                // Validate all preconditions before prompting; surfaces errors without
                // asking the user to confirm an action that was never possible.
                _removeEntryService.ValidatePreconditions(settings.FilePath, settings.FileName, settings.CleanRefs);

                var confirmed = _console.Confirm(
                    $"Are you sure you want to remove '{settings.FileName.EscapeMarkup()}'? This action cannot be undone.",
                    defaultValue: false
                );

                if (!confirmed)
                {
                    _console.MarkupLine("Removal cancelled.");
                    return 0;
                }
            }

            var result = _removeEntryService.RemoveEntry(
                settings.FilePath,
                settings.FileName,
                settings.CleanRefs
            );

            bool anythingRemoved = result.FileExistedOnDisk || result.RemovedFromConfig || result.RemovedFromTracking;

            if (anythingRemoved)
            {
                _console.MarkupLine($"[green]Removed:[/] '{settings.FileName.EscapeMarkup()}'");
                if (result.RemovedFromConfig)
                    _console.MarkupLine(
                        $"[dim]  - {settings.FileName.EscapeMarkup()} removed from config[/]"
                    );
                if (result.RemovedFromTracking)
                    _console.MarkupLine(
                        $"[dim]  - {settings.FileName.EscapeMarkup()} removed from tracking[/]"
                    );
                if (result.RemovedFromConfig)
                    _console.MarkupLine("[green]Table of contents updated.[/]");
            }

            if (settings.CleanRefs)
            {
                if (result.StrippedLinkFiles.Count > 0)
                {
                    foreach (var relativePath in result.StrippedLinkFiles)
                    {
                        _console.MarkupLine($"[dim]  Stripped links: {relativePath.EscapeMarkup()}[/]");
                    }
                    _console.MarkupLine(
                        $"[green]Cleaned dead references in {result.StrippedLinkFiles.Count} file(s).[/]"
                    );
                }
                else
                {
                    _console.MarkupLine("[dim]No dead references found.[/]");
                }
            }

            if (anythingRemoved)
            {
                _console.MarkupLine(
                    $"[green]Success:[/] Entry '{settings.FileName.EscapeMarkup()}' removed."
                );
            }
            else
            {
                _console.MarkupLine(
                    $"[dim]'{settings.FileName.EscapeMarkup()}' was not found in the journal — nothing to remove.[/]"
                );
            }

            _logger.LogDebug(
                "RemoveEntryCommand completed successfully for '{FileName}'",
                settings.FileName
            );
            return 0;
        }
        catch (JournalrcNotFoundException ex)
        {
            _console.MarkupLine($"[red]Error:[/] {ex.Message.EscapeMarkup()}");
            return 1;
        }
        catch (TrackingIndexNotFoundException ex)
        {
            _console.MarkupLine($"[red]Error:[/] {ex.Message.EscapeMarkup()}");
            return 1;
        }
        catch (ProtectedJournalFileException ex)
        {
            _console.MarkupLine($"[red]Error:[/] {ex.Message.EscapeMarkup()}");
            return 1;
        }
        catch (FileNotFoundException ex)
        {
            _console.MarkupLine($"[red]Error:[/] {ex.Message.EscapeMarkup()}");
            return 1;
        }
        catch (RollbackCompletedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _console.MarkupLine(
                $"[red]Error:[/] An unexpected error occurred: {ex.Message.EscapeMarkup()}"
            );
            return 1;
        }
    }
}
