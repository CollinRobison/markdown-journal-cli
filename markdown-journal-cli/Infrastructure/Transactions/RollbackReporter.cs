using markdown_journal_cli.Infrastructure.Transactions.Models;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace markdown_journal_cli.Infrastructure.Transactions;

/// <summary>
/// Writes rollback progress and results to the terminal using Spectre.Console markup.
/// Reports a red error line and a yellow "Rolling back changes..." notice before rollback,
/// then renders a summary table afterwards.
/// </summary>
public sealed class RollbackReporter(IAnsiConsole console, ILogger<RollbackReporter> logger)
    : IRollbackReporter
{
    private readonly IAnsiConsole _console =
        console ?? throw new ArgumentNullException(nameof(console));
    private readonly ILogger<RollbackReporter> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    public void ReportRollbackStarting(string operationDescription, Exception cause)
    {
        _console.MarkupLine(
            $"[red]Error:[/] Failed to {operationDescription}: {cause.Message.EscapeMarkup()}"
        );
        _console.MarkupLine("[yellow]Rolling back changes...[/]");
    }

    public void ReportRollbackComplete(RollbackResult result, string journalRoot)
    {
        if (result.Restored.Count == 0 && result.Failed.Count == 0)
        {
            _console.MarkupLine("[yellow]No changes to roll back.[/]");
            return;
        }

        var table = new Table()
            .Title("[bold]Rollback Summary[/]")
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Yellow)
            .AddColumn(new TableColumn("[bold]Action[/]").Centered())
            .AddColumn(new TableColumn("[bold]File[/]"));

        foreach (var entry in result.Restored)
        {
            var rel = Path.GetRelativePath(journalRoot, entry.AbsolutePath).EscapeMarkup();
            var relNew = entry.NewPath is not null
                ? Path.GetRelativePath(journalRoot, entry.NewPath).EscapeMarkup()
                : string.Empty;

            var (action, detail) = entry.Kind switch
            {
                RollbackEntryKind.Modify => ("Restored", rel),
                RollbackEntryKind.Delete => ("Restored", rel),
                RollbackEntryKind.New => ("Deleted", $"{rel} [dim](never committed)[/]"),
                RollbackEntryKind.NewDirectory => (
                    "Dir Deleted",
                    $"{rel} [dim](empty dir removed)[/]"
                ),
                RollbackEntryKind.Rename => ("Renamed back", $"{relNew} [dim]-> {rel}[/]"),
                _ => ("Restored", rel),
            };

            table.AddRow($"[yellow]{action}[/]", detail);
        }

        _console.Write(table);

        if (result.IsFullyRestored)
        {
            _console.MarkupLine(
                "[green]All changes have been rolled back. Your journal is unchanged.[/]"
            );
            _console.MarkupLine("[dim]To retry, fix the error above and run the command again.[/]");
        }
        else
        {
            foreach (var failure in result.Failed)
            {
                var rel = Path.GetRelativePath(journalRoot, failure.Entry.AbsolutePath)
                    .EscapeMarkup();
                _console.MarkupLine($"[red]  x  {rel}: {failure.Error.Message.EscapeMarkup()}[/]");
            }

            _console.MarkupLine(
                "[red]WARNING: Some files could not be restored. Manual inspection recommended.[/]"
            );
        }

        _logger.LogWarning(
            "Rollback complete. Restored={RestoredCount}, Failed={FailedCount}",
            result.Restored.Count,
            result.Failed.Count
        );
    }
}
