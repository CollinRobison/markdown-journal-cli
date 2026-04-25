using System;
using System.ComponentModel;
using markdown_journal_cli.Commands;
using markdown_journal_cli.Infrastructure.Transactions;
using markdown_journal_cli.Services.AddToc;
using Spectre.Console;
using Spectre.Console.Cli;

namespace markdown_journal_cli.Commands.Add;

[Description("Creates a Table of Contents for a journal if one does not already exist")]
public sealed class AddTableOfContents(
    IAnsiConsole console,
    IAddTocService addTocService,
    IRollbackReporter rollbackReporter
) : JournalCommand<AddTableOfContentsSettings>
{
    private readonly IAnsiConsole _console =
        console ?? throw new ArgumentNullException(nameof(console));
    private readonly IAddTocService _addTocService =
        addTocService ?? throw new ArgumentNullException(nameof(addTocService));
    private readonly IRollbackReporter _rollbackReporter =
        rollbackReporter ?? throw new ArgumentNullException(nameof(rollbackReporter));

    protected override int ExecuteCore(CommandContext context, AddTableOfContentsSettings settings)
    {
        if (settings.StructureOnly && settings.MdOnly)
        {
            _console.MarkupLine(
                "[red]Error:[/] --structure-only and --md-only are mutually exclusive. Specify at most one."
            );
            return 1;
        }

        try
        {
            var result = _addTocService.Execute(
                settings.FilePath,
                structureOnly: settings.StructureOnly,
                mdOnly: settings.MdOnly
            );

            switch (result)
            {
                case AddTocResult.Created:
                    _console.MarkupLine(
                        $"[green]Success:[/] Table of Contents artifacts created at '{settings.FilePath.EscapeMarkup()}'"
                    );
                    return 0;

                case AddTocResult.PartiallyCreated:
                    _console.MarkupLine(
                        $"[green]Success:[/] Table of Contents partially created at '{settings.FilePath.EscapeMarkup()}' (one artifact already existed)"
                    );
                    return 0;

                case AddTocResult.AlreadyExists:
                    _console.MarkupLine(
                        $"[yellow]Warning:[/] Table of Contents artifacts already exist at '{settings.FilePath.EscapeMarkup()}'"
                    );
                    return 1;

                default:
                    _console.MarkupLine("[red]Error:[/] Unexpected result from add toc operation.");
                    return 1;
            }
        }
        catch (RollbackCompletedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _console.MarkupLine(
                $"[red]Error:[/] An unexpected error occurred: {ex.Message.EscapeMarkup()}"
            );
            return 1;
        }
    }
}
