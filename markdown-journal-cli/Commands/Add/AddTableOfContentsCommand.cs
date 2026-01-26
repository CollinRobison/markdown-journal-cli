using System;
using System.ComponentModel;
using markdown_journal_cli.Infrastructure.FileSystem;
using Microsoft.Extensions.Options;
using Spectre.Console;
using Spectre.Console.Cli;

namespace markdown_journal_cli.Commands.Add;

[Description("Creates a new Table of Contents for a journal if one does not already exist")]
public class AddTableOfContents(
    IAnsiConsole console,
    IFileSystem fileSystem,
    IOptions<JournalSettings> journalSettings
) : Command<AddTableOfContentsSettings>
{
    private readonly IAnsiConsole _console =
        console ?? throw new ArgumentNullException(nameof(console));
    private readonly IFileSystem _fileSystem =
        fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

    private readonly JournalSettings _journalSettings = journalSettings.Value;

    public override int Execute(CommandContext context, AddTableOfContentsSettings settings)
    {
        // decide whether a custom name is going to be created for the table of contents. (needs setting flag)
            // check for a journalrc and either ask to add one or error without. (add setting flag to autoapprove)
                // have a flag that allows for adding files and parsing file names for each entry name or asking to prompt for each file and ask. (needs settings flag)
        // create file tracking if doesn't exist (flag for turning this setting off.)
        throw new NotImplementedException();
    }
}
