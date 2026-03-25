using System;
using markdown_journal_cli.Exceptions;
using Microsoft.Extensions.Logging;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.Configuration.Models;
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

    public UpdateDryRunReport BuildDryRunReport(
        string journalPath,
        ChangeDetectionResult? trackingChanges,
        JournalConfigSyncResult? configChanges,
        bool includeToc,
        string? renameTocTarget
    )
    {
        // When both tracking and config are in scope, recompute config drift from the projected
        // tracking state (committed + added - deleted) so Config and TOC sections reflect
        // pending tracking changes rather than the stale committed index.
        var effectiveConfigChanges = configChanges;
        if (trackingChanges is not null && configChanges is not null)
        {
            effectiveConfigChanges = ComputeProjectedConfigDrift(journalPath, trackingChanges);
            _logger.LogDebug(
                "Projected config drift: {Add} to add, {Remove} to remove",
                effectiveConfigChanges.FilesToAdd.Count,
                effectiveConfigChanges.FilesToRemove.Count
            );
        }

        TocDiffResult? tocPreview = null;
        TocRenameDryRunResult? renamePreview = null;

        if (includeToc)
        {
            var config = _journalConfiguration.Read(journalPath);
            var tocFile =
                config?.TableOfContents.File
                ?? $"{_journalSettings.TableOfContentsFileName}{FileConstants.MarkdownExtension}";
            var tocFilePath = _fileSystem.CombinePaths(journalPath, tocFile);

            var currentContent = _fileSystem.FileExists(tocFilePath)
                ? _fileSystem.GetFileContent(tocFilePath)
                : string.Empty;

            string previewContent;
            if (config is not null && effectiveConfigChanges is not null)
            {
                // Apply the effective config drift to an in-memory config clone so the TOC
                // preview reflects what the TOC would look like after config sync is applied.
                var projectedConfig = ProjectConfig(config, effectiveConfigChanges);
                previewContent = _tableOfContentsService.PreviewTableOfContents(
                    journalPath,
                    projectedConfig
                );
            }
            else
            {
                previewContent = _tableOfContentsService.PreviewTableOfContents(journalPath);
            }

            tocPreview = new TocDiffResult
            {
                CurrentContent = currentContent,
                PreviewContent = previewContent,
            };

            _logger.LogDebug(
                "TOC preview computed: hasChanges={HasChanges}",
                tocPreview.HasChanges
            );
        }

        if (renameTocTarget is not null)
        {
            var config = _journalConfiguration.Read(journalPath);
            var currentTocFile =
                config?.TableOfContents.File
                ?? $"{_journalSettings.TableOfContentsFileName}{FileConstants.MarkdownExtension}";
            var newTocFile = renameTocTarget + FileConstants.MarkdownExtension;

            var filesWithBacklinks = _markdownLinkRewriter.FindFilesWithLinkTo(
                journalPath,
                currentTocFile
            );

            renamePreview = new TocRenameDryRunResult
            {
                CurrentName = currentTocFile,
                NewName = newTocFile,
                FilesWithBacklinks = filesWithBacklinks,
            };

            _logger.LogDebug(
                "Rename preview: {Current} → {New}, {Count} backlink file(s)",
                currentTocFile,
                newTocFile,
                filesWithBacklinks.Count
            );
        }

        return new UpdateDryRunReport
        {
            TrackingChanges = trackingChanges,
            ConfigChanges = effectiveConfigChanges,
            TocPreview = tocPreview,
            RenamePreview = renamePreview,
        };
    }

    /// <summary>
    /// Computes what the config drift would look like after applying the pending tracking changes,
    /// without touching disk. Mirrors the logic in <see cref="JournalConfiguration.DetectConfigChanges"/>
    /// but operates against a projected tracking set instead of the committed index.
    /// </summary>
    private JournalConfigSyncResult ComputeProjectedConfigDrift(
        string journalPath,
        ChangeDetectionResult pendingTracking
    )
    {
        var config = _journalConfiguration.Read(journalPath);
        if (config is null)
            return new JournalConfigSyncResult();

        // Project: committed tracking index + pending adds - pending deletes
        var projectedFiles = new HashSet<string>(
            _fileTracking.LoadIndex(journalPath).Files.Keys,
            StringComparer.OrdinalIgnoreCase
        );
        projectedFiles.UnionWith(pendingTracking.AddedFiles);
        projectedFiles.ExceptWith(pendingTracking.DeletedFiles);

        // All files currently registered in .journalrc
        var configFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in config.TableOfContents.RootEntries)
            configFiles.Add(entry.File);
        CollectConfigEntryFiles(config.TableOfContents.Structure.Topics, configFiles);
        configFiles.UnionWith(config.TableOfContents.IgnoreFiles ?? []);

        var tocFile = config.TableOfContents.File;

        var filesToAdd = projectedFiles
            .Where(f =>
                !configFiles.Contains(f)
                && !string.Equals(f, tocFile, StringComparison.OrdinalIgnoreCase)
            )
            .ToList();

        var filesToRemove = configFiles.Where(f => !projectedFiles.Contains(f)).ToList();

        return new JournalConfigSyncResult { FilesToAdd = filesToAdd, FilesToRemove = filesToRemove };
    }

    private static void CollectConfigEntryFiles(IEnumerable<Topic> topics, HashSet<string> fileSet)
    {
        foreach (var topic in topics)
        {
            foreach (var entry in topic.Entries)
                fileSet.Add(entry.File);
            if (topic.Subtopics is not null)
                CollectConfigEntryFiles(topic.Subtopics, fileSet);
        }
    }

    /// <summary>
    /// Returns an in-memory clone of <paramref name="original"/> with the given config drift applied:
    /// files in <see cref="JournalConfigSyncResult.FilesToAdd"/> are appended as root entries and
    /// files in <see cref="JournalConfigSyncResult.FilesToRemove"/> are stripped from root entries
    /// and all topic entries. No disk I/O occurs.
    /// </summary>
    private static JournalConfig ProjectConfig(
        JournalConfig original,
        JournalConfigSyncResult configSync
    )
    {
        var tocFile = original.TableOfContents.File;

        // Remove deleted files from root entries
        var rootEntries = original.TableOfContents.RootEntries
            .Where(e =>
                !configSync.FilesToRemove.Any(f =>
                    string.Equals(f, e.File, StringComparison.OrdinalIgnoreCase)
                )
            )
            .ToList();

        // Append newly tracked files as root entries (skip already present; skip the TOC file)
        foreach (var file in configSync.FilesToAdd)
        {
            var alreadyPresent = rootEntries.Any(e =>
                string.Equals(e.File, file, StringComparison.OrdinalIgnoreCase)
            );
            if (!alreadyPresent && !string.Equals(file, tocFile, StringComparison.OrdinalIgnoreCase))
                rootEntries.Add(new Entries { Name = Path.GetFileNameWithoutExtension(file), File = file });
        }

        // Remove deleted files from topic structure recursively
        var topics = ProjectTopics(original.TableOfContents.Structure.Topics, configSync.FilesToRemove);

        return new JournalConfig
        {
            JournalName = original.JournalName,
            TableOfContents = new TableOfContents
            {
                File = original.TableOfContents.File,
                Extensions = original.TableOfContents.Extensions,
                IgnoreFiles = original.TableOfContents.IgnoreFiles,
                Structure = new Structure { Topics = topics },
                RootEntries = rootEntries.ToArray(),
            },
        };
    }

    private static Topic[] ProjectTopics(Topic[] topics, IReadOnlyList<string> filesToRemove)
        => topics.Select(t => ProjectTopic(t, filesToRemove)).ToArray();

    private static Topic ProjectTopic(Topic topic, IReadOnlyList<string> filesToRemove)
    {
        var filteredEntries = topic.Entries
            .Where(e =>
                !filesToRemove.Any(f =>
                    string.Equals(f, e.File, StringComparison.OrdinalIgnoreCase)
                )
            )
            .ToArray();

        var filteredSubtopics = topic.Subtopics is not null
            ? ProjectTopics(topic.Subtopics, filesToRemove)
            : null;

        return new Topic
        {
            Name = topic.Name,
            Entries = filteredEntries,
            Subtopics = filteredSubtopics,
        };
    }

    public void UpdateJournalConfig(string journalPath, JournalConfigSyncResult syncResult)
    {
        foreach (var relativePath in syncResult.FilesToAdd)
        {
            _journalConfiguration.AddEntry(journalPath, string.Empty, relativePath);
            _console.MarkupLine($"[dim]  + {relativePath}[/]");
            _logger.LogDebug("Config entry added: {RelativePath}", relativePath);
        }

        foreach (var relativePath in syncResult.FilesToRemove)
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

        if (syncResult.HasChanges)
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
