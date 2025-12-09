using System;
using System.ComponentModel;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.JournalTemplates;
using Microsoft.Extensions.Options;
using Spectre.Console;
using Spectre.Console.Cli;

namespace markdown_journal_cli.Commands.Add;

[Description("Creates a new journal entry")]
public sealed class AddEntry(
    IAnsiConsole console, 
    IFileSystem fileSystem,
    IOptions<JournalSettings> journalSettings
) : Command<AddEntrySettings>
{
    private readonly IAnsiConsole _console =
        console ?? throw new ArgumentNullException(nameof(console));
    private readonly IFileSystem _fileSystem =
        fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

    private readonly JournalSettings _journalSettings = journalSettings.Value;

    public override int Execute(CommandContext context, AddEntrySettings settings)
    {   
        //add tests
        //verify a journal exists in directory by checking if journalrc exist - (make this a helper function and maybe custom exception)
        //format entry name and subheading with - in place of spaces. - (make this into helper function)
        //generate file name by merging heading - subheading - file name. (make this a helper function)
        //check if file exists
        //update journalrc - (make this a helper function)
        //update table of contents based on journalrc - (make this a helper function)
        throw new NotImplementedException();
    }

}
