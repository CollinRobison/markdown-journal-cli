using System;
using System.ComponentModel;
using markdown_journal_cli.Exceptions;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.Configuration.Models;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.JournalTemplates;
using Microsoft.Extensions.Options;
using Spectre.Console;
using Spectre.Console.Cli;

namespace markdown_journal_cli.Commands.Add;

[Description("Creates or updates a Table of Contents for a journal")]
public class AddTableOfContents(
    IAnsiConsole console,
    IFileSystem fileSystem,
    IJournalConfiguration journalConfiguration,
    ITableOfContentsGenerator tableOfContentsGenerator,
    IOptions<JournalSettings> journalSettings
) : Command<AddTableOfContentsSettings>
{
    private readonly IAnsiConsole _console =
        console ?? throw new ArgumentNullException(nameof(console));
    private readonly IFileSystem _fileSystem =
        fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

    private readonly IJournalConfiguration _journalConfiguration =
        journalConfiguration ?? throw new ArgumentNullException(nameof(journalConfiguration));
    
    private readonly ITableOfContentsGenerator _tableOfContentsGenerator =
        tableOfContentsGenerator ?? throw new ArgumentNullException(nameof(tableOfContentsGenerator));
    private readonly JournalSettings _journalSettings = journalSettings.Value;

    public override int Execute(CommandContext context, AddTableOfContentsSettings settings)
    {
        var journalrc = Path.Combine(settings.FilePath, _journalSettings.JournalConfigFileName);
        
        try
        {
            if (!_fileSystem.FileExists(journalrc))
            {
                throw new JournalrcNotFoundException(settings.FilePath);
            }

            var configCurrent = _journalConfiguration.Read(settings.FilePath);
            if (configCurrent == null)
            {
                throw new InvalidOperationException($"Failed to read journal configuration from {settings.FilePath}");
            }

            var tocName = settings.TableOfContentsName ?? _journalSettings.TableOfContentsFileName;
            var tocFile = $"{tocName}.md";
            var tocPath = Path.Combine(settings.FilePath, tocFile);

            if (_fileSystem.FileExists(tocPath))
            {
                _console.MarkupLine($"[yellow]Warning:[/] Table of Contents file '{tocFile}' already exists at '{settings.FilePath}'");
                return 0;
            }

            if (configCurrent.TableOfContents.File != tocFile)
            {
                _journalConfiguration.Update(settings.FilePath, config =>
                {
                    config.TableOfContents.File = tocFile;
                });
            }

            _tableOfContentsGenerator.UpdateTableOfContents(settings.FilePath);

            _console.MarkupLine($"[green]Success:[/] Created Table of Contents file '{tocFile}' at '{settings.FilePath}'");
            return 0;
        }
        catch (JournalrcNotFoundException ex)
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
