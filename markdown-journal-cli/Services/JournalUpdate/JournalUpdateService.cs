using System;
using markdown_journal_cli.Exceptions;
using Microsoft.Extensions.Logging;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.Tracking;
using markdown_journal_cli.Infrastructure.Tracking.Models;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace markdown_journal_cli.Services;

public class JournalUpdateService(
    IAnsiConsole console,
    IFileSystem fileSystem,
    IJournalConfiguration journalConfiguration,
    IFileTracking fileTracking,
    ITableOfContentsService tableOfContentsService,
    IOptions<JournalSettings> journalSettings,
    IMarkdownLinkRewriter markdownLinkRewriter,
    ILogger<JournalUpdateService> logger
) : IJournalUpdateService
{
    private readonly IAnsiConsole _console =
        console ?? throw new ArgumentNullException(nameof(console));
    private readonly IFileSystem _fileSystem =
        fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    private readonly IJournalConfiguration _journalConfiguration =
        journalConfiguration ?? throw new ArgumentNullException(nameof(journalConfiguration));
    private readonly IFileTracking _fileTracking =
        fileTracking ?? throw new ArgumentNullException(nameof(fileTracking));
    private readonly ITableOfContentsService _tableOfContentsService =
        tableOfContentsService ?? throw new ArgumentNullException(nameof(tableOfContentsService));
    private readonly IMarkdownLinkRewriter _markdownLinkRewriter =
        markdownLinkRewriter ?? throw new ArgumentNullException(nameof(markdownLinkRewriter));
    private readonly ILogger<JournalUpdateService> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly JournalSettings _journalSettings = journalSettings.Value;

    public void UpdateJournalConfig(string journalPath, ChangeDetectionResult fileResults)
    {
        // Get the TOC filename to exclude it from being added as an entry
        var config = _journalConfiguration.Read(journalPath);
        var tocFile = config?.TableOfContents.File;

        foreach (var relativePath in fileResults.AddedFiles)
        {
            // Skip the TOC file - it should never be an entry
            if (
                !string.IsNullOrEmpty(tocFile)
                && string.Equals(relativePath, tocFile, StringComparison.OrdinalIgnoreCase)
            )
            {
                continue;
            }

            _journalConfiguration.AddEntry(journalPath, string.Empty, relativePath);
            _console.MarkupLine($"[dim]  + {relativePath}[/]");
            _logger.LogDebug("Config entry added: {RelativePath}", relativePath);
        }

        foreach (var relativePath in fileResults.DeletedFiles)
        {
            var removed = _journalConfiguration.RemoveEntry(journalPath, relativePath);
            if (removed)
            {
                _console.MarkupLine($"[dim]  - {relativePath}[/]");
                _logger.LogDebug("Config entry removed: {RelativePath}", relativePath);
            }
            else
                _console.MarkupLine($"[yellow]Warning:[/] config entry not found for deleted file: {relativePath}");
        }

        if (fileResults.AddedFiles.Count > 0 || fileResults.DeletedFiles.Count > 0)
            _console.MarkupLine($"[green]Journal configuration updated.[/]");
        else
            _console.MarkupLine("[dim]No configuration changes needed.[/]");
    }

    public void UpdateTableOfContents(string journalPath)
    {
        _tableOfContentsService.UpdateTableOfContents(journalPath, lastEditedDate: DateTime.Now);

        // Track the TOC file so it doesn't show as "added" on next run
        var config = _journalConfiguration.Read(journalPath);
        var tocFile =
            config?.TableOfContents.File
            ?? $"{_journalSettings.TableOfContentsFileName}{FileConstants.MarkdownExtension}";
        _fileTracking.UpdateFileInIndex(journalPath, tocFile);

        _console.MarkupLine($"[green]Table of contents updated.[/]");
    }

    public void UpdateLastEditedDatesAndTracking(
        string journalPath,
        ChangeDetectionResult fileResults,
        bool trackingOnly
    )
    {
        // Update "Last Edited:" for modified files and re-hash
        foreach (var relativePath in fileResults.ModifiedFiles)
        {
            if (!trackingOnly)
            {
                var absolutePath = _fileSystem.CombinePaths(journalPath, relativePath);
                var content = _fileSystem.GetFileContent(absolutePath);

                var updatedContent = MarkdownMetadataParser.UpdateLastEditedDate(
                    content,
                    DateTime.Now,
                    _journalSettings.DateFormat
                );

                var directory = _fileSystem.GetDirectoryName(absolutePath) ?? journalPath;
                var fileName = _fileSystem.GetFileName(absolutePath);
                if (fileName != null)
                {
                    _fileSystem.UpdateFile(directory, fileName, updatedContent);
                }
            }
            _fileTracking.UpdateFileInIndex(journalPath, relativePath);
            _console.MarkupLine($"[dim]  Updated: {relativePath}[/]");
            _logger.LogDebug("Updated Last Edited date: {RelativePath}", relativePath);
        }

        // Track newly added files
        foreach (var relativePath in fileResults.AddedFiles)
        {
            _fileTracking.UpdateFileInIndex(journalPath, relativePath);
            _console.MarkupLine($"[dim]  Tracked: {relativePath}[/]");
            _logger.LogDebug("Tracked new file: {RelativePath}", relativePath);
        }

        // Remove deleted files from tracking
        foreach (var relativePath in fileResults.DeletedFiles)
        {
            _fileTracking.RemoveFileFromIndex(journalPath, relativePath);
            _console.MarkupLine($"[dim]  Removed: {relativePath}[/]");
            _logger.LogDebug("Removed from tracking: {RelativePath}", relativePath);
        }

        if (fileResults.ModifiedFiles.Count > 0)
            _console.MarkupLine(
                $"[green]Updated dates for {fileResults.ModifiedFiles.Count} file(s).[/]"
            );
        if (fileResults.AddedFiles.Count > 0)
            _console.MarkupLine($"[green]Tracked {fileResults.AddedFiles.Count} new file(s).[/]");
        if (fileResults.DeletedFiles.Count > 0)
            _console.MarkupLine(
                $"[yellow]Removed {fileResults.DeletedFiles.Count} deleted file(s) from tracking.[/]"
            );
    }

    public void RenameToc(string journalPath, string newTocName)
    {
        var config = _journalConfiguration.Read(journalPath)
            ?? throw new JournalrcNotFoundException(journalPath);

        var currentTocFile = config.TableOfContents.File;
        var newTocFile = newTocName + FileConstants.MarkdownExtension;
        var isAlreadyNamed = string.Equals(
            currentTocFile,
            newTocFile,
            StringComparison.OrdinalIgnoreCase
        );

        if (!isAlreadyNamed)
        {
            var newTocAbsPath = _fileSystem.CombinePaths(journalPath, newTocFile);
            if (_fileSystem.FileExists(newTocAbsPath))
                throw new TocRenameConflictException(journalPath, newTocFile);

            var currentTocAbsPath = _fileSystem.CombinePaths(journalPath, currentTocFile);
            _fileSystem.RenameFile(currentTocAbsPath, newTocAbsPath);
            _console.MarkupLine($"Renamed TOC: {currentTocFile} → {newTocFile}");

            _journalConfiguration.Update(
                journalPath,
                cfg => cfg.TableOfContents.File = newTocFile
            );
            _console.MarkupLine($"[green]Updated .journalrc table-of-contents filename to '{newTocFile}'.[/]");
            _fileTracking.RenameFileInIndex(journalPath, currentTocFile, newTocFile);
        }

        // Rewrite links in all other markdown files that reference the old TOC filename
        var modifiedFiles = _markdownLinkRewriter.ReplaceLinksInDirectory(
            journalPath,
            currentTocFile,
            newTocFile,
            excludeFiles: [currentTocFile, newTocFile]
        );

        if (modifiedFiles.Count == 0)
        {
            _console.MarkupLine("No link references needed updating.");
            return;
        }

        foreach (var relativePath in modifiedFiles)
        {
            var absolutePath = _fileSystem.CombinePaths(journalPath, relativePath);
            var content = _fileSystem.GetFileContent(absolutePath);

            var stamped = MarkdownMetadataParser.UpdateLastEditedDate(
                content,
                DateTime.Now,
                _journalSettings.DateFormat
            );

            var directory = _fileSystem.GetDirectoryName(absolutePath) ?? journalPath;
            var fileName = _fileSystem.GetFileName(absolutePath);
            if (fileName != null)
                _fileSystem.UpdateFile(directory, fileName, stamped);

            _fileTracking.UpdateFileInIndex(journalPath, relativePath);
            _console.MarkupLine($"[dim]  Rewrote links: {relativePath}[/]");
            _logger.LogDebug("Rewrote TOC links in: {RelativePath}", relativePath);
        }

        _console.MarkupLine(
            $"[green]Last Edited updated for {modifiedFiles.Count} file(s).[/]"
        );
    }
}
