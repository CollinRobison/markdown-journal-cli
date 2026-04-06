using System;
using markdown_journal_cli.Exceptions;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.Tracking;
using markdown_journal_cli.Infrastructure.Transactions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace markdown_journal_cli.Services;

public sealed class JournalFileUpdateService(
    IFileSystem fileSystem,
    IJournalConfiguration journalConfiguration,
    IEntryFormatterService entryFormatter,
    ITableOfContentsService tableOfContentsService,
    IOptions<JournalSettings> journalSettings,
    ILogger<JournalFileUpdateService> logger,
    IFileTracking fileTracking,
    IMarkdownLinkRewriter markdownLinkRewriter,
    IFileTransactionCoordinator txCoordinator,
    IRollbackReporter rollbackReporter
) : IJournalFileUpdateService
{
    private readonly IFileSystem _fileSystem =
        fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    private readonly IJournalConfiguration _journalConfiguration =
        journalConfiguration ?? throw new ArgumentNullException(nameof(journalConfiguration));
    private readonly IEntryFormatterService _entryFormatter =
        entryFormatter ?? throw new ArgumentNullException(nameof(entryFormatter));
    private readonly ITableOfContentsService _tableOfContentsService =
        tableOfContentsService ?? throw new ArgumentNullException(nameof(tableOfContentsService));
    private readonly JournalSettings _journalSettings = journalSettings.Value;
    private readonly ILogger<JournalFileUpdateService> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IFileTracking _fileTracking =
        fileTracking ?? throw new ArgumentNullException(nameof(fileTracking));
    private readonly IMarkdownLinkRewriter _markdownLinkRewriter =
        markdownLinkRewriter ?? throw new ArgumentNullException(nameof(markdownLinkRewriter));
    private readonly IFileTransactionCoordinator _txCoordinator =
        txCoordinator ?? throw new ArgumentNullException(nameof(txCoordinator));
    private readonly IRollbackReporter _rollbackReporter =
        rollbackReporter ?? throw new ArgumentNullException(nameof(rollbackReporter));

    public void UpdateEntry(
        string directory,
        string currentFileName,
        string? newEntryName = null,
        string? newEntryTitle = null,
        string? newHeadings = null,
        bool ignoreFile = false,
        bool unignoreFile = false,
        bool updateBacklinks = true
    )
    {
        _logger.LogDebug(
            "Updating entry '{FileName}' in '{Directory}'",
            currentFileName,
            directory
        );

        // 1. Validate request and normalize file name
        var currentFile = ValidateAndNormalizeFile(directory, currentFileName);

        // 2. Find the current entry and its location
        var (currentEntry, currentTopicPath) = _journalConfiguration.FindEntry(
            directory,
            currentFile
        );
        var currentDisplayName =
            currentEntry?.Name ?? _fileSystem.GetFileNameWithoutExtension(currentFile) ?? "";

        // 3. Determine target values
        var targetFile = DetermineTargetFileName(newEntryName, newHeadings, currentFile);
        var targetDisplayName = DetermineTargetDisplayName(
            newEntryTitle,
            newEntryName,
            currentFile,
            currentDisplayName
        );
        var targetTopicPath = DetermineTargetTopicPath(newHeadings, targetFile, currentTopicPath);

        // 4. Analyze what changes are needed
        var (isRenaming, isChangingHeadings, isChangingDisplayName) = AnalyzeChanges(
            currentFile,
            targetFile,
            currentTopicPath,
            targetTopicPath,
            currentDisplayName,
            targetDisplayName
        );

        // 5. Validate target file doesn't exist (before any operations)
        if (isRenaming)
        {
            var targetFilePath = _fileSystem.CombinePaths(directory, targetFile);
            if (_fileSystem.FileExists(targetFilePath))
            {
                throw new InvalidOperationException(
                    $"Cannot rename '{currentFile}' to '{targetFile}': target file already exists in '{directory}'."
                );
            }
        }

        using var tx = _txCoordinator.Begin();
        try
        {
            var journalrcPath = _fileSystem.CombinePaths(
                directory,
                _journalSettings.JournalConfigFileName
            );
            if (_fileSystem.FileExists(journalrcPath))
                tx.Track(journalrcPath);

            var tocFile =
                $"{_journalSettings.TableOfContentsFileName}{FileConstants.MarkdownExtension}";
            var tocAbsPath = _fileSystem.CombinePaths(directory, tocFile);
            if (_fileSystem.FileExists(tocAbsPath))
                tx.Track(tocAbsPath);
            else
                tx.TrackNew(tocAbsPath);

            if (isRenaming)
            {
                var currentAbsPath = _fileSystem.CombinePaths(directory, currentFile);
                var targetAbsPath = _fileSystem.CombinePaths(directory, targetFile);
                tx.TrackRename(currentAbsPath, targetAbsPath);
            }

            if (isRenaming && updateBacklinks)
            {
                var backlinks =
                    _markdownLinkRewriter.FindFilesWithLinkTo(directory, currentFile) ?? [];
                foreach (var relative in backlinks)
                    tx.Track(_fileSystem.CombinePaths(directory, relative));
            }

            // 6. Apply the changes
            ApplyFileRename(directory, currentFile, targetFile, isRenaming);

            if (isRenaming && updateBacklinks)
            {
                var backlinkTocFile =
                    _journalSettings.TableOfContentsFileName + FileConstants.MarkdownExtension;
                _markdownLinkRewriter.ReplaceLinksInDirectory(
                    directory,
                    currentFile,
                    targetFile,
                    excludeFiles: [targetFile, backlinkTocFile]
                );
            }

            // Skip config updates if we're ignoring - they'll be removed anyway
            if (!ignoreFile)
            {
                ApplyConfigUpdates(
                    directory,
                    isRenaming ? targetFile : currentFile,
                    targetTopicPath,
                    targetDisplayName,
                    isChangingHeadings,
                    isChangingDisplayName
                );
            }

            ApplyIgnoreStatusChange(directory, targetFile, ignoreFile, unignoreFile);

            // 7. Regenerate table of contents
            _tableOfContentsService.UpdateTableOfContents(directory, lastEditedDate: DateTime.Now);

            tx.Commit();

            _logger.LogDebug(
                "Successfully updated entry '{CurrentFile}' to '{TargetFile}'",
                currentFile,
                targetFile
            );
        }
        catch (Exception ex)
        {
            throw _rollbackReporter.RollbackAndBuildException(
                tx,
                _txCoordinator,
                "update entry",
                directory,
                ex
            );
        }
    }

    private string ValidateAndNormalizeFile(string directory, string fileName)
    {
        // Normalize filename — add .md extension if missing
        var normalizedFile = fileName.EndsWith(
            FileConstants.MarkdownExtension,
            StringComparison.OrdinalIgnoreCase
        )
            ? fileName
            : fileName + FileConstants.MarkdownExtension;

        var filePath = _fileSystem.CombinePaths(directory, normalizedFile);

        // Verify the file exists
        if (!_fileSystem.FileExists(filePath))
        {
            throw new FileNotFoundException($"File '{normalizedFile}' not found at '{directory}'.");
        }

        // Verify .journalrc exists
        var journalrcPath = _fileSystem.CombinePaths(
            directory,
            _journalSettings.JournalConfigFileName
        );
        if (!_fileSystem.FileExists(journalrcPath))
        {
            throw new JournalrcNotFoundException(directory);
        }

        return normalizedFile;
    }

    private string DetermineTargetFileName(
        string? newEntryName,
        string? newHeadings,
        string currentFile
    )
    {
        var hasNewName = !string.IsNullOrEmpty(newEntryName);
        var hasNewHeadings = !string.IsNullOrEmpty(newHeadings);

        if (!hasNewName && !hasNewHeadings)
        {
            return currentFile;
        }

        var currentFileWithoutExt =
            _fileSystem.GetFileNameWithoutExtension(currentFile) ?? currentFile;
        var currentParts = currentFileWithoutExt.Split(
            new[] { _journalSettings.HeadingSeparator },
            StringSplitOptions.RemoveEmptyEntries
        );

        // Entry name: use -n if provided, otherwise keep current last segment.
        var entryPart = hasNewName
            ? _entryFormatter.AddSpaceSeparators(newEntryName!.Trim())
            : (currentParts.Length > 0 ? currentParts[^1] : currentFileWithoutExt);

        if (string.IsNullOrEmpty(entryPart))
        {
            return currentFile;
        }

        // Heading prefix: -h always wins; fall back to current file's prefix segments.
        string[] prefixParts;
        if (hasNewHeadings)
        {
            prefixParts = newHeadings!
                .Split(
                    new[] { _journalSettings.HeadingSeparator },
                    StringSplitOptions.RemoveEmptyEntries
                )
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrEmpty(p))
                .ToArray();
        }
        else
        {
            prefixParts =
                currentParts.Length > 1
                    ? currentParts.Take(currentParts.Length - 1).ToArray()
                    : Array.Empty<string>();
        }

        var allParts = prefixParts.Append(entryPart).ToArray();
        return _entryFormatter.AddHeadingSeparators(allParts) + FileConstants.MarkdownExtension;
    }

    private string DetermineTargetDisplayName(
        string? newEntryTitle,
        string? newEntryName,
        string currentFile,
        string currentDisplayName
    )
    {
        // Explicit --title provided: always use it
        if (!string.IsNullOrEmpty(newEntryTitle))
        {
            return _entryFormatter.RemoveSpaceSeparators(newEntryTitle);
        }

        // --name provided without --title: only update if display name currently matches filename
        if (!string.IsNullOrEmpty(newEntryName))
        {
            // -n is name-only (no heading path); use the value directly.
            return ShouldUpdateDisplayNameWithFileName(currentFile, currentDisplayName)
                ? _entryFormatter.RemoveSpaceSeparators(newEntryName)
                : currentDisplayName;
        }

        // No changes to display name
        return currentDisplayName;
    }

    private bool ShouldUpdateDisplayNameWithFileName(string currentFile, string currentDisplayName)
    {
        var currentFileWithoutExt = _fileSystem.GetFileNameWithoutExtension(currentFile);

        // Handle null case (shouldn't happen in practice, but be defensive)
        if (string.IsNullOrEmpty(currentFileWithoutExt))
        {
            return false;
        }

        // Extract the entry name portion from the filename (last part after heading separators)
        var filenameParts = currentFileWithoutExt.Split(
            _journalSettings.HeadingSeparator,
            StringSplitOptions.RemoveEmptyEntries
        );
        var entryNameFromFile =
            filenameParts.Length > 0 ? filenameParts[^1] : currentFileWithoutExt;

        // Convert underscores to spaces to get the expected display name
        var expectedDisplayFromFile = _entryFormatter.RemoveSpaceSeparators(entryNameFromFile);

        // Check if current display name matches what we'd expect from the filename
        var shouldUpdate = string.Equals(
            currentDisplayName,
            expectedDisplayFromFile,
            StringComparison.OrdinalIgnoreCase
        );

        if (!shouldUpdate)
        {
            _logger.LogDebug(
                "Preserving custom display name '{DisplayName}' (differs from filename pattern '{Expected}')",
                currentDisplayName,
                expectedDisplayFromFile
            );
        }

        return shouldUpdate;
    }

    private string[] DetermineTargetTopicPath(
        string? newHeadings,
        string targetFile,
        string[] currentTopicPath
    )
    {
        // If explicit headings provided, use those
        if (!string.IsNullOrEmpty(newHeadings))
        {
            return _entryFormatter.SeperateSubheadingString(newHeadings);
        }

        // No explicit headings - parse from target filename
        var targetFileWithoutExt = _fileSystem.GetFileNameWithoutExtension(targetFile);
        if (string.IsNullOrEmpty(targetFileWithoutExt))
        {
            return currentTopicPath;
        }

        // Split by heading separator to extract topic path from filename
        var parts = targetFileWithoutExt.Split(
            _journalSettings.HeadingSeparator,
            StringSplitOptions.RemoveEmptyEntries
        );

        // If only one part (no heading separators), file is at root - return empty topic path
        if (parts.Length <= 1)
        {
            return Array.Empty<string>();
        }

        // Multiple parts: all except last are the topic path, last is entry name
        return parts
            .Take(parts.Length - 1)
            .Select(part => _entryFormatter.RemoveSpaceSeparators(part))
            .ToArray();
    }

    private (bool isRenaming, bool isChangingHeadings, bool isChangingDisplayName) AnalyzeChanges(
        string currentFile,
        string targetFile,
        string[] currentTopicPath,
        string[] targetTopicPath,
        string currentDisplayName,
        string targetDisplayName
    )
    {
        var isRenaming = !string.Equals(
            targetFile,
            currentFile,
            StringComparison.OrdinalIgnoreCase
        );
        var isChangingHeadings = !targetTopicPath.SequenceEqual(
            currentTopicPath,
            StringComparer.OrdinalIgnoreCase
        );
        var isChangingDisplayName = !string.Equals(
            targetDisplayName,
            currentDisplayName,
            StringComparison.OrdinalIgnoreCase
        );

        _logger.LogDebug(
            "Update analysis: Renaming={Renaming}, ChangingHeadings={ChangingHeadings}, ChangingDisplayName={ChangingDisplayName}",
            isRenaming,
            isChangingHeadings,
            isChangingDisplayName
        );

        return (isRenaming, isChangingHeadings, isChangingDisplayName);
    }

    private void ApplyFileRename(
        string directory,
        string currentFile,
        string targetFile,
        bool isRenaming
    )
    {
        if (isRenaming)
        {
            RenameEntry(directory, currentFile, targetFile);
        }
    }

    private void ApplyConfigUpdates(
        string directory,
        string fileInConfig,
        string[] targetTopicPath,
        string targetDisplayName,
        bool isChangingHeadings,
        bool isChangingDisplayName
    )
    {
        if (isChangingHeadings)
        {
            // Headings changed: remove old entry and re-add at new location
            UpdateEntryLocation(directory, fileInConfig, targetTopicPath, targetDisplayName);
        }
        else if (isChangingDisplayName)
        {
            // Only the display name changed — use the efficient path
            UpdateEntryDisplayName(directory, fileInConfig, targetDisplayName);
        }
    }

    private void ApplyIgnoreStatusChange(
        string directory,
        string targetFile,
        bool ignoreFile,
        bool unignoreFile
    )
    {
        if (ignoreFile)
        {
            SetIgnoreStatus(directory, targetFile, true);
        }
        else if (unignoreFile)
        {
            SetIgnoreStatus(directory, targetFile, false);
        }
    }

    public void RenameEntry(string directory, string oldFile, string newFile)
    {
        _logger.LogDebug(
            "Renaming entry from '{OldFile}' to '{NewFile}' in '{Directory}'",
            oldFile,
            newFile,
            directory
        );

        var oldFilePath = _fileSystem.CombinePaths(directory, oldFile);
        var newFilePath = _fileSystem.CombinePaths(directory, newFile);

        // Verify old file exists
        if (!_fileSystem.FileExists(oldFilePath))
        {
            _logger.LogWarning(
                "Cannot rename: file '{OldFile}' not found at '{Directory}'",
                oldFile,
                directory
            );
            throw new FileNotFoundException($"File '{oldFile}' not found at '{directory}'.");
        }

        // Verify new file doesn't already exist
        if (_fileSystem.FileExists(newFilePath))
        {
            _logger.LogWarning(
                "Cannot rename: target file '{NewFile}' already exists in '{Directory}'",
                newFile,
                directory
            );
            throw new InvalidOperationException(
                $"Cannot rename '{oldFile}' to '{newFile}': target file already exists in '{directory}'."
            );
        }

        // Rename the physical file
        _fileSystem.RenameFile(oldFilePath, newFilePath);
        _logger.LogDebug("Physical file renamed successfully");

        // Update all config references
        _journalConfiguration.UpdateFileReferences(directory, oldFile, newFile);
        _logger.LogDebug("Configuration references updated successfully");

        // Update the tracking index: remove old entry, add new with fresh hash
        _fileTracking.RenameFileInIndex(directory, oldFile, newFile);
        _logger.LogDebug(
            "Tracking index updated for rename from '{OldFile}' to '{NewFile}'",
            oldFile,
            newFile
        );
    }

    public void UpdateEntryLocation(
        string directory,
        string fileName,
        string[] newTopicPath,
        string displayName
    )
    {
        _logger.LogDebug(
            "Updating entry location for '{FileName}' to topic path: [{TopicPath}]",
            fileName,
            string.Join(", ", newTopicPath)
        );

        // Remove from current location
        _journalConfiguration.RemoveEntry(directory, fileName);
        _logger.LogDebug("Entry removed from old location");

        // Add to new location
        _journalConfiguration.AddEntry(
            directory,
            displayName,
            fileName,
            newTopicPath.Length > 0 ? newTopicPath : null
        );
        _logger.LogDebug("Entry added to new location");
    }

    public void UpdateEntryDisplayName(string directory, string fileName, string newDisplayName)
    {
        _logger.LogDebug(
            "Updating display name for '{FileName}' to '{NewDisplayName}'",
            fileName,
            newDisplayName
        );

        var updated = _journalConfiguration.UpdateEntryName(directory, fileName, newDisplayName);

        if (!updated)
        {
            _logger.LogWarning(
                "Entry '{FileName}' not found in configuration at '{Directory}'",
                fileName,
                directory
            );
        }
        else
        {
            _logger.LogDebug("Display name updated successfully");
        }
    }

    public void SetIgnoreStatus(string directory, string fileName, bool ignored)
    {
        _logger.LogDebug("Setting ignore status for '{FileName}' to {Ignored}", fileName, ignored);

        if (ignored)
        {
            // Remove from structure first, then add to ignore list
            _journalConfiguration.RemoveEntry(directory, fileName);
            _journalConfiguration.AddIgnoreEntry(directory, fileName);
            _logger.LogDebug("File removed from structure and added to ignore list");
        }
        else
        {
            // Remove from ignore list
            _journalConfiguration.Update(
                directory,
                config =>
                {
                    if (config.TableOfContents.IgnoreFiles is null)
                    {
                        return;
                    }

                    config.TableOfContents.IgnoreFiles = config
                        .TableOfContents.IgnoreFiles.Where(f =>
                            !string.Equals(f, fileName, StringComparison.OrdinalIgnoreCase)
                        )
                        .ToArray();
                }
            );
            _logger.LogDebug("File removed from ignore list");

            // Add back to structure
            // Get the display name from the file
            var fileNameWithoutExt = _fileSystem.GetFileNameWithoutExtension(fileName) ?? fileName;
            _journalConfiguration.AddEntry(
                directory,
                name: "", // Let AddEntry parse from filename
                file: fileName,
                topicPath: null, // Let AddEntry parse from filename
                maxDepth: null,
                sortAlphabetically: true,
                ignoreFile: false
            );
            _logger.LogDebug("File added back to structure");
        }
    }
}
