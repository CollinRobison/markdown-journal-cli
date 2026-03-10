using System;
using System.ComponentModel;
using markdown_journal_cli.Exceptions;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.FileSystem;
using Microsoft.Extensions.Options;
using Spectre.Console;
using Spectre.Console.Cli;

namespace markdown_journal_cli.Commands.Add;

[Description("Creates a new journal configuration file if one does not already exist")]
public class AddJournalrc(
    IAnsiConsole console,
    IFileSystem fileSystem,
    IJournalConfigGenerator configGenerator,
    IOptions<JournalSettings> journalSettings
) : Command<AddJournalrcSettings>
{
    private readonly IAnsiConsole _console =
        console ?? throw new ArgumentNullException(nameof(console));
    private readonly IFileSystem _fileSystem =
        fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    private readonly IJournalConfigGenerator _configGenerator =
        configGenerator ?? throw new ArgumentNullException(nameof(configGenerator));
    private readonly JournalSettings _journalSettings = journalSettings.Value;

    public override int Execute(CommandContext context, AddJournalrcSettings settings)
    {
        try
        {
            var directory = settings.FilePath;
            var journalrcPath = Path.Combine(directory, _journalSettings.JournalConfigFileName);

            // Check if .journalrc already exists
            if (_fileSystem.FileExists(journalrcPath))
            {
                _console.MarkupLine(
                    $"[yellow]Journal configuration already exists at {journalrcPath}[/]"
                );
                return 1;
            }

            // Determine TOC filename (from flag or default)
            var tocFileName =
                settings.TableOfContentsFile ?? _journalSettings.TableOfContentsFileName;

            // Determine journal name (from flag or directory name)
            var journalName = settings.JournalName ?? GetJournalName(directory);

            // Try to generate config from TOC file
            var result = _configGenerator.GenerateFromTableOfContents(
                directory,
                tocFileName,
                journalName
            );

            if (result != null)
            {
                _console.MarkupLine(
                    $"[green]✓[/] Created journal configuration with {result.FileCount} entries from table of contents"
                );
                return 0;
            }

            // Try to generate config from tracking index
            result = _configGenerator.GenerateFromTrackingIndex(
                directory,
                tocFileName,
                journalName
            );

            if (result != null)
            {
                _console.MarkupLine(
                    $"[green]✓[/] Created journal configuration with {result.FileCount} entries from tracking index"
                );
                return 0;
            }

            // Fallback: generate config from directory scan
            _console.MarkupLine(
                $"[yellow]No table of contents or tracking index found. Scanning directory...[/]"
            );
            result = _configGenerator.GenerateFromDirectory(directory, tocFileName, journalName);
            _console.MarkupLine(
                $"[green]✓[/] Created journal configuration with {result.FileCount} entries from directory scan"
            );
            return 0;
        }
        catch (ArgumentException ex)
        {
            _console.MarkupLine($"[red]Error: {ex.Message}[/]");
            return 1;
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]An unexpected error occurred: {ex.Message}[/]");
            return 1;
        }
    }

    private string GetJournalName(string directory)
    {
        // Convert to absolute path if relative
        var absolutePath = Path.IsPathRooted(directory) ? directory : Path.GetFullPath(directory);

        // Get the directory name from the absolute path
        var dirName = Path.GetFileName(absolutePath);

        return string.IsNullOrEmpty(dirName) ? "MyJournal" : dirName;
    }
}
