using markdown_journal_cli.Exceptions;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.Tracking;
using markdown_journal_cli.Infrastructure.Transactions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace markdown_journal_cli.Services.RemoveEntry;

public sealed class RemoveEntryService(
    IFileSystem fileSystem,
    IJournalConfiguration journalConfiguration,
    IFileTracking fileTracking,
    ITableOfContentsService tableOfContentsService,
    IMarkdownLinkRewriter markdownLinkRewriter,
    IOptions<JournalSettings> journalSettings,
    IFileTransactionCoordinator txCoordinator,
    IRollbackReporter rollbackReporter,
    ILogger<RemoveEntryService> logger
) : IRemoveEntryService
{
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
    private readonly JournalSettings _journalSettings = journalSettings.Value;
    private readonly IFileTransactionCoordinator _txCoordinator =
        txCoordinator ?? throw new ArgumentNullException(nameof(txCoordinator));
    private readonly IRollbackReporter _rollbackReporter =
        rollbackReporter ?? throw new ArgumentNullException(nameof(rollbackReporter));
    private readonly ILogger<RemoveEntryService> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    public IReadOnlyList<string> RemoveEntry(string journalPath, string fileName, bool cleanRefs)
    {
        _logger.LogDebug("RemoveEntry called for '{FileName}' in '{JournalPath}'", fileName, journalPath);

        // 1. Normalise fileName — append .md if missing
        var resolvedFileName = fileName.EndsWith(FileConstants.MarkdownExtension, StringComparison.OrdinalIgnoreCase)
            ? fileName
            : $"{fileName}{FileConstants.MarkdownExtension}";

        // 2. Validate .journalrc exists
        var journalrcPath = _fileSystem.CombinePaths(journalPath, _journalSettings.JournalConfigFileName);
        if (!_fileSystem.FileExists(journalrcPath))
        {
            _logger.LogWarning("Journal config not found at '{JournalPath}'", journalPath);
            throw new JournalrcNotFoundException(journalPath);
        }

        // 3. Validate tracking index exists
        var trackingFileName = $".{_journalSettings.AppName}";
        var trackingFilePath = _fileSystem.CombinePaths(journalPath, trackingFileName);
        if (!_fileSystem.FileExists(trackingFilePath))
        {
            _logger.LogWarning("Tracking index '{TrackingFileName}' not found at '{JournalPath}'", trackingFileName, journalPath);
            throw new TrackingIndexNotFoundException(journalPath, trackingFileName);
        }

        // 4. Guard against protected files — read live TOC filename from config
        _logger.LogDebug("Checking if '{FileName}' is a protected journal file", resolvedFileName);
        var config = _journalConfiguration.Read(journalPath);
        var tocFile = config?.TableOfContents.File
            ?? $"{_journalSettings.TableOfContentsFileName}{FileConstants.MarkdownExtension}";

        var protectedFiles = new[] { _journalSettings.JournalConfigFileName, trackingFileName, tocFile };

        // Check both the raw input and the normalised name: non-.md infrastructure files
        // (e.g. .journalrc) would otherwise be missed once .md is appended.
        var targetedProtectedFile = protectedFiles.FirstOrDefault(f =>
            string.Equals(f, resolvedFileName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(f, fileName, StringComparison.OrdinalIgnoreCase)
        );
        if (targetedProtectedFile is not null)
        {
            throw new ProtectedJournalFileException(fileName);
        }

        // 5. Resolve absolute entry path; validate file exists
        var absoluteEntryPath = _fileSystem.CombinePaths(journalPath, resolvedFileName);
        _logger.LogDebug("Resolved entry path: '{AbsoluteEntryPath}'", absoluteEntryPath);
        if (!_fileSystem.FileExists(absoluteEntryPath))
        {
            throw new FileNotFoundException(
                $"Entry file '{resolvedFileName}' not found at '{journalPath}'.",
                absoluteEntryPath
            );
        }

        using var tx = _txCoordinator.Begin();
        try
        {
            var trackingAbsPath = _fileSystem.CombinePaths(journalPath, trackingFileName);
            var tocAbsPath = _fileSystem.CombinePaths(journalPath, tocFile);

            if (cleanRefs)
            {
                var backlinkFiles = _markdownLinkRewriter.FindFilesWithLinkTo(journalPath, resolvedFileName) ?? [];
                foreach (var relative in backlinkFiles)
                    tx.Track(_fileSystem.CombinePaths(journalPath, relative));
            }

            if (_fileSystem.FileExists(journalrcPath))
                tx.Track(journalrcPath);
            if (_fileSystem.FileExists(trackingAbsPath))
                tx.Track(trackingAbsPath);
            if (_fileSystem.FileExists(tocAbsPath))
                tx.Track(tocAbsPath);
            tx.TrackDelete(absoluteEntryPath);

            // 6. Delete the file
            _logger.LogDebug("Deleting file '{AbsoluteEntryPath}'", absoluteEntryPath);
            _fileSystem.DeleteFile(absoluteEntryPath);

            // 7. Remove from config
            _logger.LogDebug("Removing '{FileName}' from journal config", resolvedFileName);
            _journalConfiguration.RemoveEntry(journalPath, resolvedFileName);

            // 8. Remove from tracking index
            _logger.LogDebug("Removing '{FileName}' from tracking index", resolvedFileName);
            _fileTracking.RemoveFileFromIndex(journalPath, resolvedFileName);

            // 9. Regenerate TOC
            _logger.LogDebug("Regenerating table of contents");
            _tableOfContentsService.UpdateTableOfContents(journalPath, lastEditedDate: DateTime.Now);

            // 10. Optionally strip dead links across the journal
            if (cleanRefs)
            {
                _logger.LogDebug("Stripping dead links to '{FileName}' across journal", resolvedFileName);
                var modifiedFiles = _markdownLinkRewriter.StripLinksInDirectory(journalPath, resolvedFileName);

                foreach (var relativePath in modifiedFiles)
                {
                    _logger.LogDebug("Re-hashing '{RelativePath}' after link strip", relativePath);
                    _fileTracking.UpdateFileInIndex(journalPath, relativePath);
                }

                tx.Commit();

                _logger.LogDebug(
                    "Successfully removed entry '{FileName}' and stripped dead links in {Count} file(s)",
                    resolvedFileName,
                    modifiedFiles.Count
                );
                return modifiedFiles;
            }

            tx.Commit();
            _logger.LogDebug("Successfully removed entry '{FileName}'", resolvedFileName);
            return [];
        }
        catch (Exception ex)
        {
            throw _rollbackReporter.RollbackAndBuildException(tx, _txCoordinator, "remove entry", journalPath, ex);
        }
    }
}
