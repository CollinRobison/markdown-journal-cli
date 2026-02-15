using System;
using System.ComponentModel;
using markdown_journal_cli.Exceptions;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.Tracking;
using markdown_journal_cli.Infrastructure.Tracking.Models;
using markdown_journal_cli.JournalTemplates;
using Microsoft.Extensions.Options;
using Spectre.Console;
using Spectre.Console.Cli;

namespace markdown_journal_cli.Commands.Update;

[Description("Updates configuration, table of contents, and file created dates. All items are updated by default unless specific flags are provided")]
public sealed class UpdateCommand(
    IAnsiConsole console,
    IFileSystem fileSystem,
    IFileTracking fileTracking,
    IJournalConfiguration journalConfiguration,
    ITableOfContentsGenerator tableOfContentsGenerator,
    IOptions<JournalSettings> journalSettings
) : Command<UpdateJournalSettings>
{
    private readonly IAnsiConsole _console =
        console ?? throw new ArgumentNullException(nameof(console));
    private readonly IFileSystem _fileSystem =
        fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

    private readonly IFileTracking _fileTracking = 
        fileTracking ?? throw new ArgumentNullException(nameof(fileTracking));

    private readonly IJournalConfiguration _journalConfiguration =
        journalConfiguration ?? throw new ArgumentNullException(nameof(journalConfiguration));

    private readonly ITableOfContentsGenerator _tableOfContentsGenerator =
        tableOfContentsGenerator ?? throw new ArgumentNullException(nameof(tableOfContentsGenerator));

    private readonly JournalSettings _journalSettings = journalSettings.Value;

    public override int Execute(CommandContext context, UpdateJournalSettings settings)
    {
        try
        {
            var trackingFileName = $".{_journalSettings.AppName}";
            var trackingFilePath = _fileSystem.CombinePaths(settings.FilePath, trackingFileName);
            var journalrcPath = _fileSystem.CombinePaths(settings.FilePath, _journalSettings.JournalConfigFileName);

            if (!_fileSystem.FileExists(trackingFilePath))
            {
                throw new TrackingIndexNotFoundException(settings.FilePath, trackingFileName);
            }

            bool all = !settings.DateFlag && !settings.ConfigFlag && !settings.TocFlag; 

            if ((all || settings.ConfigFlag || settings.TocFlag) && !_fileSystem.FileExists(journalrcPath))
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
                UpdateLastEditedDatesAndTracking(settings.FilePath, fileResults, settings.Tracking);
            }

            if (all || settings.ConfigFlag)
            {
                UpdateJournalConfig(settings.FilePath, fileResults);
            }

            if (all || settings.TocFlag)
            {
                UpdateTableOfContents(settings.FilePath);
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

    /// <summary>
    /// Updates the "Last Edited:" date for modified files, adds new files to the tracking index,
    /// and removes deleted files from the tracking index.
    /// </summary>
    private void UpdateLastEditedDatesAndTracking(string journalPath, ChangeDetectionResult fileResults, bool trackingOnly)
    {
        // Update "Last Edited:" for modified files and re-hash
        foreach (var relativePath in fileResults.ModifiedFiles)
        {
            if (!trackingOnly)
            {
                var absolutePath = _fileSystem.CombinePaths(journalPath, relativePath);
                var content = _fileSystem.GetFileContent(absolutePath);

                var updatedContent = MarkdownMetadataParser.UpdateLastEditedDate(
                    content, DateTime.Now, _journalSettings.DateFormat);

                var directory = Path.GetDirectoryName(absolutePath) ?? journalPath;
                var fileName = Path.GetFileName(absolutePath);
                _fileSystem.UpdateFile(directory, fileName, updatedContent);                
            }
            _fileTracking.UpdateFileInIndex(journalPath, relativePath);

            _console.MarkupLine($"[green]Updated:[/] {relativePath}");
        }

        // Track newly added files
        foreach (var relativePath in fileResults.AddedFiles)
        {
            _fileTracking.UpdateFileInIndex(journalPath, relativePath);
            _console.MarkupLine($"[green]Tracked:[/] {relativePath}");
        }

        // Remove deleted files from tracking
        foreach (var relativePath in fileResults.DeletedFiles)
        {
            _fileTracking.RemoveFileFromIndex(journalPath, relativePath);
            _console.MarkupLine($"[yellow]Removed:[/] {relativePath}");
        }

        if (fileResults.ModifiedFiles.Count > 0)
            _console.MarkupLine($"[green]Updated dates for {fileResults.ModifiedFiles.Count} file(s).[/]");
        if (fileResults.AddedFiles.Count > 0)
            _console.MarkupLine($"[green]Tracked {fileResults.AddedFiles.Count} new file(s).[/]");
        if (fileResults.DeletedFiles.Count > 0)
            _console.MarkupLine($"[yellow]Removed {fileResults.DeletedFiles.Count} deleted file(s) from tracking.[/]");
    }

    /// <summary>
    /// Incrementally updates the .journalrc configuration: adds new entries, removes deleted entries.
    /// </summary>
    private void UpdateJournalConfig(string journalPath, ChangeDetectionResult fileResults)
    {
        // Get the TOC filename to exclude it from being added as an entry
        var config = _journalConfiguration.Read(journalPath);
        var tocFile = config?.TableOfContents.File;
        
        foreach (var relativePath in fileResults.AddedFiles)
        {
            // Skip the TOC file - it should never be an entry
            if (!string.IsNullOrEmpty(tocFile) && 
                string.Equals(relativePath, tocFile, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            
            _journalConfiguration.AddEntry(journalPath, string.Empty, relativePath);
            _console.MarkupLine($"[green]Config added:[/] {relativePath}");
        }

        foreach (var relativePath in fileResults.DeletedFiles)
        {
            var removed = _journalConfiguration.RemoveEntry(journalPath, relativePath);
            if (removed)
                _console.MarkupLine($"[yellow]Config removed:[/] {relativePath}");
            else
                _console.MarkupLine($"[dim]Config entry not found for deleted file:[/] {relativePath}");
        }

        if (fileResults.AddedFiles.Count > 0 || fileResults.DeletedFiles.Count > 0)
            _console.MarkupLine($"[green]Journal configuration updated.[/]");
        else
            _console.MarkupLine("[dim]No configuration changes needed.[/]");
    }

    /// <summary>
    /// Regenerates the table of contents markdown file from the current journal configuration.
    /// </summary>
    private void UpdateTableOfContents(string journalPath)
    {   
        _tableOfContentsGenerator.UpdateTableOfContents(journalPath, lastEditedDate: DateTime.Now);
        
        // Track the TOC file so it doesn't show as "added" on next run
        var config = _journalConfiguration.Read(journalPath);
        var tocFile = config?.TableOfContents.File ?? $"{_journalSettings.TableOfContentsFileName}.md";
        _fileTracking.UpdateFileInIndex(journalPath, tocFile);
        
        _console.MarkupLine($"[green]Table of contents updated.[/]");
    }
}