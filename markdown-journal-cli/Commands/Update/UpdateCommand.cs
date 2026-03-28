using System;
using System.ComponentModel;
using markdown_journal_cli.Exceptions;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.Configuration.Models;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.Tracking;
using markdown_journal_cli.Infrastructure.Tracking.Models;
using markdown_journal_cli.Services;
using Microsoft.Extensions.Logging;
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
    IOptions<JournalSettings> journalSettings,
    IJournalConfiguration journalConfiguration,
    ILogger<UpdateCommand> logger,
    IDryRunRenderer dryRunRenderer
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
    private readonly IJournalConfiguration _journalConfiguration =
        journalConfiguration ?? throw new ArgumentNullException(nameof(journalConfiguration));
    private readonly ILogger<UpdateCommand> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IDryRunRenderer _dryRunRenderer =
        dryRunRenderer ?? throw new ArgumentNullException(nameof(dryRunRenderer));
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
                throw new TrackingIndexNotFoundException(settings.FilePath, trackingFileName);

            bool all =
                !settings.DateFlag
                && !settings.ConfigFlag
                && !settings.TocFlag
                && !settings.Tracking
                && settings.RenameToc is null;

            if (
                (all || settings.ConfigFlag || settings.TocFlag || settings.RenameToc is not null)
                && !_fileSystem.FileExists(journalrcPath)
            )
                throw new JournalrcNotFoundException(settings.FilePath);

            if (settings.DryRun)
                return ExecuteDryRun(settings, all);

            // --rename-toc is not a change-detection operation — handle it first and independently
            if (settings.RenameToc is not null)
                _journalUpdateService.RenameToc(settings.FilePath, settings.RenameToc);

            // For the remaining update operations we need to detect tracked file changes
            if (all || settings.DateFlag || settings.Tracking || settings.ConfigFlag || settings.TocFlag)
            {
                var fileResults = _fileTracking.DetectChangesWithoutUpdate(settings.FilePath);

                // Pre-detect config drift to include in the early-return check
                var configDrift = (all || settings.ConfigFlag)
                    ? _journalConfiguration.DetectConfigChanges(settings.FilePath)
                    : null;

                var hasAnythingToDo = fileResults.HasChanges || (configDrift?.HasChanges ?? false);

                if (!hasAnythingToDo)
                {
                    if (settings.RenameToc is null)
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
                    // Re-detect after tracking update so same-run additions/deletions are captured
                    var configSyncResult = _journalConfiguration.DetectConfigChanges(settings.FilePath);
                    _journalUpdateService.UpdateJournalConfig(settings.FilePath, configSyncResult);
                }

                if (all || settings.TocFlag)
                    _journalUpdateService.UpdateTableOfContents(settings.FilePath);
            }

            return 0;
        }
        catch (TocRenameConflictException ex)
        {
            _console.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
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

    // -------------------------------------------------------------------------
    // Dry-run path
    // -------------------------------------------------------------------------

    private int ExecuteDryRun(UpdateJournalSettings settings, bool all)
    {
        _logger.LogDebug("Dry-run mode active, skipping all writes");

        var includeTracking = all || settings.DateFlag || settings.Tracking;
        var includeConfig = all || settings.ConfigFlag;
        var includeToc = all || settings.TocFlag;

        ChangeDetectionResult? fileResults = includeTracking
            ? _fileTracking.DetectChangesWithoutUpdate(settings.FilePath)
            : null;

        JournalConfigSyncResult? configDrift = includeConfig
            ? _journalConfiguration.DetectConfigChanges(settings.FilePath)
            : null;

        var report = _journalUpdateService.BuildDryRunReport(
            settings.FilePath,
            includeTracking ? fileResults : null,
            includeConfig ? configDrift : null,
            includeToc,
            settings.RenameToc
        );

        _logger.LogDebug(
            "Dry-run report: {TrackingCount} tracking, {ConfigCount} config, toc={TocIncluded}, rename={RenameTarget}",
            (report.TrackingChanges?.AddedFiles.Count ?? 0)
                + (report.TrackingChanges?.ModifiedFiles.Count ?? 0)
                + (report.TrackingChanges?.DeletedFiles.Count ?? 0),
            (report.ConfigChanges?.FilesToAdd.Count ?? 0)
                + (report.ConfigChanges?.FilesToRemove.Count ?? 0),
            includeToc,
            settings.RenameToc ?? "none"
        );

        if (!report.HasAnyChanges)
        {
            _console.MarkupLine(
                "[green]Everything is up to date.[/] No changes detected. [dim](--dry-run active, no writes made)[/]"
            );
            return 0;
        }

        _dryRunRenderer.Render(report, settings.FilePath);

        _console.MarkupLine(
            "[dim]No changes were applied. Re-run without --dry-run to apply.[/]"
        );
        return 0;
    }
}
