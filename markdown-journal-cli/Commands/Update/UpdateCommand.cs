using System;
using System.ComponentModel;
using markdown_journal_cli.Exceptions;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.Tracking;
using markdown_journal_cli.Infrastructure.Tracking.Models;
using markdown_journal_cli.Services;
using Microsoft.Extensions.Options;
using Spectre.Console;
using Spectre.Console.Cli;

namespace markdown_journal_cli.Commands.Update;

[Description(
    "Updates configuration, table of contents, and Last Edited dates metadata. All items are updated by default unless specific flags are provided"
)]
public sealed class UpdateCommand(
    IAnsiConsole console,
    IFileSystem fileSystem,
    IJournalUpdateService journalUpdateService,
    IFileTracking fileTracking,
    IOptions<JournalSettings> journalSettings
) : Command<UpdateJournalSettings>
{
    private readonly IAnsiConsole _console =
        console ?? throw new ArgumentNullException(nameof(console));
    private readonly IFileSystem _fileSystem =
        fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    private readonly IFileTracking _fileTracking =
        fileTracking ?? throw new ArgumentNullException(nameof(fileTracking));
    private readonly IJournalUpdateService _journalUpdateService =
        journalUpdateService ?? throw new ArgumentNullException(nameof(journalUpdateService));
    private readonly JournalSettings _journalSettings = journalSettings.Value;

    public override int Execute(CommandContext context, UpdateJournalSettings settings)
    {
        try
        {
            var trackingFileName = $".{_journalSettings.AppName}";
            var trackingFilePath = _fileSystem.CombinePaths(settings.FilePath, trackingFileName);
            var journalrcPath = _fileSystem.CombinePaths(
                settings.FilePath,
                _journalSettings.JournalConfigFileName
            );

            if (!_fileSystem.FileExists(trackingFilePath))
            {
                throw new TrackingIndexNotFoundException(settings.FilePath, trackingFileName);
            }

            bool all =
                !settings.DateFlag
                && !settings.ConfigFlag
                && !settings.TocFlag
                && !settings.Tracking;

            if (
                (all || settings.ConfigFlag || settings.TocFlag)
                && !_fileSystem.FileExists(journalrcPath)
            )
            {
                throw new JournalrcNotFoundException(settings.FilePath);
            }

            var fileResults = _fileTracking.DetectChangesWithoutUpdate(settings.FilePath);

            if (!fileResults.HasChanges)
            {
                _console.MarkupLine("[green]Everything is up to date.[/]");
                return 0;
            }

            if (all || settings.DateFlag || settings.Tracking)
            {
                _journalUpdateService.UpdateLastEditedDatesAndTracking(
                    settings.FilePath,
                    fileResults,
                    settings.Tracking
                );
            }

            if (all || settings.ConfigFlag)
            {
                _journalUpdateService.UpdateJournalConfig(settings.FilePath, fileResults);
            }

            if (all || settings.TocFlag)
            {
                _journalUpdateService.UpdateTableOfContents(settings.FilePath);
            }

            return 0;
        }
        catch (JournalrcNotFoundException ex)
        {
            _console.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
        catch (TrackingIndexNotFoundException ex)
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
