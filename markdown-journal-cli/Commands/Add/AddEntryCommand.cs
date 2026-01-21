using System;
using System.ComponentModel;
using markdown_journal_cli.Exceptions;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.Tracking;
using markdown_journal_cli.JournalTemplates;
using markdown_journal_cli.Services;
using Microsoft.Extensions.Options;
using Spectre.Console;
using Spectre.Console.Cli;

namespace markdown_journal_cli.Commands.Add;

[Description("Creates a new journal entry")]
public sealed class AddEntry(
    IAnsiConsole console,
    IFileSystem fileSystem,
    ITemplateManager templateManager,
    IEntryFormatterService entryFormatter,
    IJournalConfiguration journalConfiguration,
    IFileTracking fileTracking,
    ITableOfContentsGenerator tableOfContentsGenerator,
    IOptions<JournalSettings> journalSettings
) : Command<AddEntrySettings>
{
    private readonly IAnsiConsole _console =
        console ?? throw new ArgumentNullException(nameof(console));
    private readonly IFileSystem _fileSystem =
        fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

    private readonly ITemplateManager _templateManager = 
        templateManager ?? throw new ArgumentNullException(nameof(templateManager));

    private readonly IEntryFormatterService _entryFormatter =
        entryFormatter ?? throw new ArgumentNullException(nameof(entryFormatter));

    private readonly IJournalConfiguration _journalConfiguration = 
        journalConfiguration ?? throw new ArgumentNullException(nameof(journalConfiguration));

    private readonly IFileTracking _fileTracking =
        fileTracking ?? throw new ArgumentNullException(nameof(fileTracking));

    private readonly ITableOfContentsGenerator _tableOfContentsGenerator = 
        tableOfContentsGenerator ?? throw new ArgumentNullException(nameof(tableOfContentsGenerator));

    private readonly JournalSettings _journalSettings = journalSettings.Value;

    public override int Execute(CommandContext context, AddEntrySettings settings)
    {
        var journalrc = $"{settings.FilePath}/{_journalSettings.JournalConfigFileName}";
        var trackingFileName = $".{_journalSettings.AppName}";
        var trackingFilePath = $"{settings.FilePath}/{trackingFileName}";
        try
        {
            //add tests
            //verify a journal exists in directory by checking if journalrc and tracking index file exist - maybe make this a middleware
            if (!_fileSystem.FileExists(journalrc))
            {
                throw new JournalrcNotFoundException(settings.FilePath);
            }
            if (!_fileSystem.FileExists(trackingFilePath))
            {
                throw new TrackingIndexNotFoundException(settings.FilePath, trackingFileName);
            }
            //format entry name and subheading with - in place of spaces. - (make this into helper function)

            // title for use in table of contents and entry file
            var entryTitle = _entryFormatter.RemoveSpaceSeperators(
                settings.EntryTitle ?? settings.EntryName
            );
            // title for use in the file name
            var entryNameFormatted = _entryFormatter.AddSpaceSeperators(settings.EntryName);

            // heading for use in file name
            var headingFormatted =
                settings.Heading != null
                    ? entryFormatter.AddSpaceSeperators(settings.Heading)
                    : null;

            var fileName = new[] { headingFormatted, settings.Subheading, entryNameFormatted }
                .Where(x => x != null)
                .Cast<string>()
                .ToArray();

            //generate file name by merging heading - subheading - file name. (make this a helper function)
            var fileNameFormatted = $"{entryFormatter.AddHeadingSeperators(fileName)}.md";
            var entryFilePath = $"{settings.FilePath}/{fileNameFormatted}";
            //check if file exists
            if (_fileSystem.FileExists(entryFilePath))
            {
                throw new JournalEntryAlreadyExistsException(entryFilePath, entryFilePath);
            }

            //create file - make sure to use entryTitle if exists if not use formatted entryName
            var entryParams = new Dictionary<string, object>
            {
                ["title"] = entryTitle, 
                ["addSourceBlock"] = true
            };

            _fileSystem.CreateMarkdownFile(
                settings.FilePath,
                fileNameFormatted,
                _templateManager.GenerateFromTemplate("journal-entry", entryParams)
            );
            //update journalrc 
            string[] headings = (settings.Heading != null 
                    ? [entryFormatter.RemoveSpaceSeperators(settings.Heading)] 
                    : Array.Empty<string>())
                .Concat(settings.Subheading != null 
                    ? entryFormatter.SeperateSubheadingString(settings.Subheading) 
                    : [])
                .Where(h => !string.IsNullOrEmpty(h))
                .ToArray();
            
            _journalConfiguration.AddEntry(settings.FilePath, entryTitle, fileNameFormatted, headings.Length > 0 ? headings : null);
            //add file to file tracking index
            _fileTracking.UpdateFileInIndex(settings.FilePath, fileNameFormatted);
            //update table of contents based on journalrc
            _tableOfContentsGenerator.UpdateTableOfContents(settings.FilePath, lastEditedDate: DateTime.Now);
            return 0;
        }
        catch (JournalrcNotFoundException ex)
        {
            _console.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
        catch (JournalEntryAlreadyExistsException ex)
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
}

// Future TODO: allow add entry to include all aspects of a file including body, sources, etc. from the command line. 
