using System;
using System.ComponentModel;
using markdown_journal_cli.Infrastructure.FileSystem;
using Microsoft.Extensions.Options;
using Spectre.Console;
using Spectre.Console.Cli;

namespace markdown_journal_cli.Commands.Add;

[Description("Creates a new journal configuration file if one does not already exist")]
public class AddJournalrc(
    IAnsiConsole console,
    IFileSystem fileSystem,
    IOptions<JournalSettings> journalSettings
) : Command<AddJournalrcSettings>
{
    private readonly IAnsiConsole _console =
        console ?? throw new ArgumentNullException(nameof(console));
    private readonly IFileSystem _fileSystem =
        fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

    private readonly JournalSettings _journalSettings = journalSettings.Value;

    public override int Execute(CommandContext context, AddJournalrcSettings settings)
    {
        // have a flag that allows for adding files and parsing file names for each entry name or asking to prompt for each file and ask. (needs settings flag)
            // also look for a table of contents and allow it to parse file names from that. 
        // make a table of contents automatically if one doesn't exist. (make flag for turning off)
        // create journal tracking file if doesn't exist. (flag for turning off)
        throw new NotImplementedException();
    }
}
