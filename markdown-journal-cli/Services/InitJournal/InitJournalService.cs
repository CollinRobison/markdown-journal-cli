using markdown_journal_cli.Exceptions;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.Configuration.Models;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.Tracking;
using Microsoft.Extensions.Options;

namespace markdown_journal_cli.Services;

public class InitJournalService : IInitJournalService
{
    private readonly IFileSystem _fileSystem;
    private readonly IJournalConfigGenerator _configGenerator;
    private readonly IFileTracking _fileTracking;
    private readonly ITableOfContentsService _tableOfContentsService;
    private readonly JournalSettings _journalSettings;

    public InitJournalService(
        IFileSystem fileSystem,
        IJournalConfiguration journalConfiguration,
        IJournalConfigGenerator configGenerator,
        IFileTracking fileTracking,
        ITableOfContentsService tableOfContentsService,
        IOptions<JournalSettings> journalSettings
    )
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _configGenerator = configGenerator ?? throw new ArgumentNullException(nameof(configGenerator));
        _fileTracking = fileTracking ?? throw new ArgumentNullException(nameof(fileTracking));
        _tableOfContentsService =
            tableOfContentsService
            ?? throw new ArgumentNullException(nameof(tableOfContentsService));
        _journalSettings = journalSettings.Value;
    }

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

        // 1. Create file tracking index
        _fileTracking.LoadIndex(journalDirectory);
        _fileTracking.UpdateIndex(journalDirectory);
        
        // 2. Create journal configuration

        _configGenerator.GenerateFromTrackingIndex(journalDirectory, resolvedTocName, journalName);

        // 3. Create table of contents (reads from configuration)
        _tableOfContentsService.UpdateTableOfContents(
            journalDirectory,
            createdDate: DateTime.Now,
            lastEditedDate: DateTime.Now);

        //4. add toc to file tracking after creation

        _fileTracking.UpdateIndex(journalDirectory);
    }
}
