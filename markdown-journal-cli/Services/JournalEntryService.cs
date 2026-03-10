using System;
using markdown_journal_cli.Exceptions;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.JournalTemplates;
using markdown_journal_cli.Infrastructure.Tracking;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace markdown_journal_cli.Services;

public class JournalEntryService(
    IFileSystem fileSystem,
    IJournalConfiguration journalConfiguration,
    IOptions<JournalSettings> journalSettings,
    IEntryFormatterService entryFormatter,
    ITemplateManager templateManager,
    IFileTracking fileTracking,
    ITableOfContentsService tableOfContentsService,
    ILogger<JournalEntryService> logger)
    : IJournalEntryService
{
    private readonly IFileSystem _fileSystem =
        fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    private readonly IEntryFormatterService _entryFormatter =
        entryFormatter ?? throw new ArgumentNullException(nameof(entryFormatter));
    private readonly ITemplateManager _templateManager =
        templateManager ?? throw new ArgumentNullException(nameof(templateManager));
    private readonly IFileTracking _fileTracking =
        fileTracking ?? throw new ArgumentNullException(nameof(fileTracking));
    private readonly IJournalConfiguration _journalConfiguration =
        journalConfiguration ?? throw new ArgumentNullException(nameof(journalConfiguration));
    private readonly ITableOfContentsService _tableOfContentsService =
        tableOfContentsService ?? throw new ArgumentNullException(nameof(tableOfContentsService));
    private readonly ILogger<JournalEntryService> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly JournalSettings _journalSettings = journalSettings.Value;

    public void AddEntry(
        string filePath,
        bool ignoreFile,
        string entryName,
        string? heading,
        string? subheading,
        string? entryTitleUnformatted
    )
    {
        _logger.LogDebug("Adding entry '{EntryName}' to journal at '{FilePath}'", entryName, filePath);

        var journalrc = $"{filePath}/{_journalSettings.JournalConfigFileName}";
        var trackingFileName = $".{_journalSettings.AppName}";
        var trackingFilePath = $"{filePath}/{trackingFileName}";

        if (!_fileSystem.FileExists(journalrc))
        {
            _logger.LogWarning("Journal config not found at '{FilePath}'", filePath);
            throw new JournalrcNotFoundException(filePath);
        }
        if (!_fileSystem.FileExists(trackingFilePath))
        {
            _logger.LogWarning("Tracking index '{TrackingFileName}' not found at '{FilePath}'", trackingFileName, filePath);
            throw new TrackingIndexNotFoundException(filePath, trackingFileName);
        }

        // title for use in table of contents and entry file
        var entryTitle = _entryFormatter.RemoveSpaceSeparators(
            entryTitleUnformatted ?? entryName
        );

        var fileNameFormatted = FileNameFormatted(entryName, heading, subheading);

        var entryFilePath = $"{filePath}/{fileNameFormatted}";
        if (_fileSystem.FileExists(entryFilePath))
        {
            _logger.LogWarning("Entry '{FileName}' already exists at '{EntryFilePath}'", fileNameFormatted, entryFilePath);
            throw new JournalEntryAlreadyExistsException(fileNameFormatted, entryFilePath);
        }

        var entryParams = new Dictionary<string, object>
        {
            ["title"] = entryTitle,
            ["addSourceBlock"] = true,
        };

        _logger.LogDebug("Creating markdown file '{FileName}' in '{FilePath}'", fileNameFormatted, filePath);
        _fileSystem.CreateMarkdownFile(
            filePath,
            fileNameFormatted,
            _templateManager.GenerateFromTemplate("journal-entry", entryParams)
        );

        var headings = _entryFormatter.BuildHeadingArray(heading, subheading);

        _logger.LogDebug("Updating journal config with entry '{EntryTitle}'", entryTitle);
        _journalConfiguration.AddEntry(
            filePath,
            entryTitle,
            fileNameFormatted,
            headings.Length > 0 ? headings : null,
            ignoreFile: ignoreFile
        );

        _logger.LogDebug("Updating file tracking index with '{FileName}'", fileNameFormatted);
        _fileTracking.UpdateFileInIndex(filePath, fileNameFormatted);

        if (!ignoreFile)
        {
            _logger.LogDebug("Updating table of contents for journal at '{FilePath}'", filePath);
            _tableOfContentsService.UpdateTableOfContents(filePath, lastEditedDate: DateTime.Now);
        }

        _logger.LogDebug("Successfully added entry '{EntryName}' to journal at '{FilePath}'", entryName, filePath);
    }

    private string FileNameFormatted(string entryName, string? heading, string? subheading)
    {
        // title for use in the file name
        var entryNameFormatted = _entryFormatter.AddSpaceSeparators(entryName);

        // heading for use in file name
        var headingFormatted =
            heading != null ? _entryFormatter.AddSpaceSeparators(heading) : null;

        var fileName = new[] { headingFormatted, subheading, entryNameFormatted }
            .Where(x => x != null)
            .Cast<string>()
            .ToArray();

        //generate file name by merging heading - subheading - file name. (make this a helper function)
        var fileNameFormatted =
            $"{_entryFormatter.AddHeadingSeparators(fileName)}{FileConstants.MarkdownExtension}";
        return fileNameFormatted;
        
    }
}
