using System;
using System.ComponentModel;
using markdown_journal_cli.Exceptions;
using markdown_journal_cli.Infrastructure.FileSystem;
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
    IEntryFormatterService entryFormatter,
    IOptions<JournalSettings> journalSettings
) : Command<AddEntrySettings>
{
    private readonly IAnsiConsole _console =
        console ?? throw new ArgumentNullException(nameof(console));
    private readonly IFileSystem _fileSystem =
        fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

    private readonly IEntryFormatterService _entryFormatter =
        entryFormatter ?? throw new ArgumentNullException(nameof(entryFormatter));

    private readonly JournalSettings _journalSettings = journalSettings.Value;

    public override int Execute(CommandContext context, AddEntrySettings settings)
    {
        var journalrc = $"{settings.FilePath}/{_journalSettings.JournalConfigFileName}";
        try
        {
            //add tests
            //verify a journal exists in directory by checking if journalrc exist - maybe make this a middleware
            console.WriteLine(journalrc);
            if (!_fileSystem.FileExists(journalrc))
            {
                throw new JournalrcNotFoundException(settings.FilePath);
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
            //update journalrc - (make this a helper function make sure the helper function has an exception for 1a - 1z to not create heading and to put in right spot at top)
            //update table of contents based on journalrc - (make this a helper function)
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
