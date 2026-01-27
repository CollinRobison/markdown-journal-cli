using System;
using System.ComponentModel;
using markdown_journal_cli.Exceptions;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.Tracking;
using Microsoft.Extensions.Options;
using Spectre.Console;
using Spectre.Console.Cli;
namespace markdown_journal_cli.Commands.Add;

[Description("Creates a new file tracking index file")]
public sealed class AddFileTracking  ( 
    IAnsiConsole console,
    IFileSystem fileSystem,
    IFileTracking fileTracking,
    IOptions<JournalSettings> journalSettings
) : Command<AddFileTrackingSettings>
{
    private readonly IAnsiConsole _console =
        console ?? throw new ArgumentNullException(nameof(console));
    private readonly IFileSystem _fileSystem =
        fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

    private readonly IFileTracking _fileTracking = 
        fileTracking ?? throw new ArgumentNullException(nameof(fileTracking));

    private readonly JournalSettings _journalSettings = journalSettings.Value;

    public override int Execute(CommandContext context, AddFileTrackingSettings settings)
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
                _console.MarkupLine($"[yellow]Warning:[/] Tracking file '{trackingFile}' already exists at '{settings.FilePath}'");
                return 0;
            }

            // Create file tracking file with all md files in directory
            _fileTracking.LoadIndex(settings.FilePath);
            _fileTracking.UpdateIndex(settings.FilePath);
            
            _console.MarkupLine($"[green]Success:[/] Created tracking file '{trackingFile}' at '{settings.FilePath}'");
            return 0;
        }
        catch (JournalrcNotFoundException ex)
        {
            _console.MarkupLine($"[red]Error:[/] {ex.Message} this file is needed to be considered a journal.");
            return 1;
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Error:[/] An unexpected error occurred: {ex.Message}");
            return 1;
        }
    }
}