using System;
using markdown_journal_cli.Infrastructure.FileSystem;
using Microsoft.Extensions.Options;
using Spectre.Console;
using Spectre.Console.Cli;
namespace markdown_journal_cli.Commands.Add;

public class AddFileTracking  ( 
    IAnsiConsole console,
    IFileSystem fileSystem,
    IOptions<JournalSettings> journalSettings
) : Command<AddFileTrackingSettings>
{
    private readonly IAnsiConsole _console =
        console ?? throw new ArgumentNullException(nameof(console));
    private readonly IFileSystem _fileSystem =
        fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

    private readonly JournalSettings _journalSettings = journalSettings.Value;

    public override int Execute(CommandContext context, AddFileTrackingSettings settings)
    {
        // create file tracking file with all md files in directory. 
        throw new NotImplementedException();
    }
}