using markdown_journal_cli.Exceptions;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.Configuration.Models;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.Tracking;
using markdown_journal_cli.Infrastructure.Transactions;
using Microsoft.Extensions.Options;

namespace markdown_journal_cli.Services;

public sealed class InitJournalService(
    IFileSystem fileSystem,
    IJournalConfigGenerator configGenerator,
    IFileTracking fileTracking,
    ITableOfContentsService tableOfContentsService,
    IOptions<JournalSettings> journalSettings,
    IFileTransactionCoordinator txCoordinator,
    IRollbackReporter rollbackReporter
) : IInitJournalService
{
    private readonly IFileSystem _fileSystem =
        fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    private readonly IJournalConfigGenerator _configGenerator =
        configGenerator ?? throw new ArgumentNullException(nameof(configGenerator));
    private readonly IFileTracking _fileTracking =
        fileTracking ?? throw new ArgumentNullException(nameof(fileTracking));
    private readonly ITableOfContentsService _tableOfContentsService =
        tableOfContentsService ?? throw new ArgumentNullException(nameof(tableOfContentsService));
    private readonly JournalSettings _journalSettings = journalSettings.Value;
    private readonly IFileTransactionCoordinator _txCoordinator =
        txCoordinator ?? throw new ArgumentNullException(nameof(txCoordinator));
    private readonly IRollbackReporter _rollbackReporter =
        rollbackReporter ?? throw new ArgumentNullException(nameof(rollbackReporter));

    /// <inheritdoc />
    public void Initialize(string journalDirectory, string journalName, string? tableOfContentsName)
    {
        if (string.IsNullOrWhiteSpace(journalDirectory))
            throw new ArgumentException(
                "Journal directory cannot be null or whitespace.",
                nameof(journalDirectory)
            );

        if (string.IsNullOrWhiteSpace(journalName))
            throw new ArgumentException(
                "Journal name cannot be null or whitespace.",
                nameof(journalName)
            );

        var resolvedTocName = string.IsNullOrWhiteSpace(tableOfContentsName)
            ? _journalSettings.TableOfContentsFileName
            : tableOfContentsName;

        var tocFile = $"{resolvedTocName}{FileConstants.MarkdownExtension}";
        var tocPath = _fileSystem.CombinePaths(journalDirectory, tocFile);

        if (_fileSystem.FileExists(tocPath))
            throw new TocFileAlreadyExistsException(journalDirectory, tocFile);

        using var tx = _txCoordinator.Begin();
        try
        {
            var trackingPath = _fileSystem.CombinePaths(
                journalDirectory,
                $".{_journalSettings.AppName}"
            );
            var journalrcPath = _fileSystem.CombinePaths(
                journalDirectory,
                _journalSettings.JournalConfigFileName
            );

            tx.TrackNew(trackingPath);
            tx.TrackNew(journalrcPath);
            tx.TrackNew(tocPath);

            // 1. Create file tracking index
            _fileTracking.LoadIndex(journalDirectory);
            _fileTracking.UpdateIndex(journalDirectory);

            // 2. Create journal configuration
            _configGenerator.GenerateFromTrackingIndex(
                journalDirectory,
                resolvedTocName,
                journalName
            );

            // 3. Create table of contents (reads from configuration)
            _tableOfContentsService.UpdateTableOfContents(
                journalDirectory,
                createdDate: DateTime.Now,
                lastEditedDate: DateTime.Now
            );

            // 4. Add TOC to file tracking after creation
            _fileTracking.UpdateIndex(journalDirectory);

            tx.Commit();
        }
        catch (Exception ex)
        {
            throw _rollbackReporter.RollbackAndBuildException(
                tx,
                _txCoordinator,
                "initialize journal",
                journalDirectory,
                ex
            );
        }
    }
}
