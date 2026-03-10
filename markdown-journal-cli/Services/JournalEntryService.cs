using System;
using markdown_journal_cli.Exceptions;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.JournalTemplates;
using markdown_journal_cli.Infrastructure.Tracking;
using Microsoft.Extensions.Options;

namespace markdown_journal_cli.Services;

public class JournalEntryService(IFileSystem fileSystem, IJournalConfiguration journalConfiguration, IOptions<JournalSettings> journalSettings, 
    IEntryFormatterService entryFormatter, ITemplateManager templateManager, IFileTracking fileTracking,  ITableOfContentsService tableOfContentsService)
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
        var journalrc = $"{filePath}/{_journalSettings.JournalConfigFileName}";
        var trackingFileName = $".{_journalSettings.AppName}";
        var trackingFilePath = $"{filePath}/{trackingFileName}";

        //verify a journal exists in directory by checking if journalrc and tracking index file exist - maybe make this a middleware
        if (!_fileSystem.FileExists(journalrc))
        {
            throw new JournalrcNotFoundException(filePath);
        }
        if (!_fileSystem.FileExists(trackingFilePath))
        {
            throw new TrackingIndexNotFoundException(filePath, trackingFileName);
        }

        // title for use in table of contents and entry file
        var entryTitle = _entryFormatter.RemoveSpaceSeparators(
            entryTitleUnformatted ?? entryName
        );

        var fileNameFormatted = FileNameFormatted(entryName, heading, subheading);

        var entryFilePath = $"{filePath}/{fileNameFormatted}";
        //check if file exists
        if (_fileSystem.FileExists(entryFilePath))
        {
            throw new JournalEntryAlreadyExistsException(fileNameFormatted, entryFilePath);
        }

        //create file - make sure to use entryTitle if exists if not use formatted entryName
        var entryParams = new Dictionary<string, object>
        {
            ["title"] = entryTitle,
            ["addSourceBlock"] = true,
        };

        _fileSystem.CreateMarkdownFile(
            filePath,
            fileNameFormatted,
            _templateManager.GenerateFromTemplate("journal-entry", entryParams)
        );
        //update journalrc
        var headings = _entryFormatter.BuildHeadingArray(heading, subheading);

        _journalConfiguration.AddEntry(
            filePath,
            entryTitle,
            fileNameFormatted,
            headings.Length > 0 ? headings : null,
            ignoreFile: ignoreFile
        );
        //add file to file tracking index
        _fileTracking.UpdateFileInIndex(filePath, fileNameFormatted);
        //update table of contents based on journalrc
        if (!ignoreFile)
        {
            _tableOfContentsService.UpdateTableOfContents(filePath, lastEditedDate: DateTime.Now);
        }
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
