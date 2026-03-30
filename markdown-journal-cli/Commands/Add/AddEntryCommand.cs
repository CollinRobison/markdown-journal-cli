using System;
using System.ComponentModel;
using markdown_journal_cli.Exceptions;
using markdown_journal_cli.Services;
using markdown_journal_cli.Commands;
using markdown_journal_cli.Infrastructure.Transactions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace markdown_journal_cli.Commands.Add;

[Description("Creates a new journal entry")]
public sealed class AddEntry(IAnsiConsole console, IJournalEntryService journalEntryService)
    : JournalCommand<AddEntrySettings>
{
    private readonly IAnsiConsole _console =
        console ?? throw new ArgumentNullException(nameof(console));

    private readonly IJournalEntryService _journalEntryService =
        journalEntryService ?? throw new ArgumentNullException(nameof(journalEntryService));

    protected override int ExecuteCore(CommandContext context, AddEntrySettings settings)
    {
        try
        {
            _journalEntryService.AddEntry(
                settings.FilePath,
                settings.IgnoreFile,
                settings.EntryName,
                settings.Heading,
                settings.Subheading,
                settings.EntryTitle
            );
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
        catch (TrackingIndexNotFoundException ex)
        {
            _console.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
        catch (RollbackCompletedException) { throw; }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Error:[/] An unexpected error occurred: {ex.Message}");
            return 1;
        }
    }
}

// Future TODO: allow add entry to include all aspects of a file including body, sources, etc. from the command line.
