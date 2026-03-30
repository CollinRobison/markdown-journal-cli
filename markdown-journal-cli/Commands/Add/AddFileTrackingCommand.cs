using System;
using System.ComponentModel;
using markdown_journal_cli.Exceptions;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.Tracking;
using markdown_journal_cli.Commands;
using markdown_journal_cli.Infrastructure.Transactions;
using Microsoft.Extensions.Options;
using Spectre.Console;
using Spectre.Console.Cli;

namespace markdown_journal_cli.Commands.Add;

[Description("Creates a new file tracking index file")]
public sealed class AddFileTracking(
    IAnsiConsole console,
    IFileSystem fileSystem,
    IFileTracking fileTracking,
    IOptions<JournalSettings> journalSettings,
    IFileTransactionCoordinator txCoordinator,
    IRollbackReporter rollbackReporter
) : JournalCommand<AddFileTrackingSettings>
{
    private readonly IAnsiConsole _console =
        console ?? throw new ArgumentNullException(nameof(console));
    private readonly IFileSystem _fileSystem =
        fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

    private readonly IFileTracking _fileTracking =
        fileTracking ?? throw new ArgumentNullException(nameof(fileTracking));

    private readonly JournalSettings _journalSettings = journalSettings.Value;
    private readonly IFileTransactionCoordinator _txCoordinator =
        txCoordinator ?? throw new ArgumentNullException(nameof(txCoordinator));
    private readonly IRollbackReporter _rollbackReporter =
        rollbackReporter ?? throw new ArgumentNullException(nameof(rollbackReporter));

    protected override int ExecuteCore(CommandContext context, AddFileTrackingSettings settings)
    {
        var journalrc = Path.Combine(settings.FilePath, _journalSettings.JournalConfigFileName);
        var trackingFile = $".{_journalSettings.AppName}";
        var trackingFilePath = Path.Combine(settings.FilePath, trackingFile);

        try
        {
            // Verify a journal exists in directory by checking if journalrc exists
            if (!_fileSystem.FileExists(journalrc) && !settings.IgnoreJournalConfig)
            {
                throw new JournalrcNotFoundException(settings.FilePath);
            }

            // Check if tracking file already exists
            if (_fileSystem.FileExists(trackingFilePath))
            {
                _console.MarkupLine(
                    $"[yellow]Warning:[/] Tracking file '{trackingFile}' already exists at '{settings.FilePath}'"
                );
                return 0;
            }

            using var tx = _txCoordinator.Begin();
            try
            {
                tx.TrackNew(trackingFilePath);

                // Create file tracking file with all md files in directory
                _fileTracking.LoadIndex(settings.FilePath);
                _fileTracking.UpdateIndex(settings.FilePath);

                tx.Commit();

                _console.MarkupLine(
                    $"[green]Success:[/] Created tracking file '{trackingFile}' at '{settings.FilePath}'"
                );
                return 0;
            }
            catch (Exception ex)
            {
                throw _rollbackReporter.RollbackAndBuildException(tx, _txCoordinator, "add file tracking", settings.FilePath, ex);
            }
        }
        catch (JournalrcNotFoundException ex)
        {
            _console.MarkupLine(
                $"[red]Error:[/] {ex.Message} this file is needed to be considered a journal."
            );
            return 1;
        }
        catch (RollbackCompletedException) { throw; }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Error:[/] An unexpected error occurred: {ex.Message}");
            return 1;
        }
    }
}
