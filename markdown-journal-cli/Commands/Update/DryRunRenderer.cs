using System;
using System.Collections.Generic;
using System.Linq;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.Configuration.Models;
using markdown_journal_cli.Infrastructure.Tracking.Models;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace markdown_journal_cli.Commands.Update;

/// <summary>
/// Renders a <see cref="UpdateDryRunReport"/> to the terminal using Spectre.Console.
/// All output is read-only — no file writes occur here.
/// </summary>
public sealed class DryRunRenderer(
    IAnsiConsole console,
    IJournalConfiguration journalConfiguration,
    IOptions<JournalSettings> journalSettings
) : IDryRunRenderer
{
    private readonly IAnsiConsole _console =
        console ?? throw new ArgumentNullException(nameof(console));
    private readonly IJournalConfiguration _journalConfiguration =
        journalConfiguration ?? throw new ArgumentNullException(nameof(journalConfiguration));
    private readonly JournalSettings _journalSettings = journalSettings.Value;

    public void Render(UpdateDryRunReport report, string journalPath)
    {
        if (report.TrackingChanges is { HasChanges: true })
            RenderTrackingTable(report.TrackingChanges);

        if (report.ConfigChanges is { HasChanges: true })
            RenderConfigTable(report.ConfigChanges);

        if (report.TocPreview is not null)
            RenderTocPreview(report.TocPreview, journalPath);

        if (report.RenamePreview is not null)
            RenderRenamePreview(report.RenamePreview);
    }

    private void RenderTrackingTable(ChangeDetectionResult changes)
    {
        var table = new Table()
            .Title("[bold]Tracking Changes[/]")
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey);

        table.AddColumn(new TableColumn("[bold]File[/]"));
        table.AddColumn(new TableColumn("[bold]Change[/]").Width(16));

        foreach (var f in changes.AddedFiles)
            table.AddRow(f.EscapeMarkup(), "[green]✚ added[/]");
        foreach (var f in changes.ModifiedFiles)
            table.AddRow(f.EscapeMarkup(), "[yellow]~ modified[/]");
        foreach (var f in changes.DeletedFiles)
            table.AddRow(f.EscapeMarkup(), "[red]✖ removed[/]");

        _console.Write(table);

        var total =
            changes.AddedFiles.Count + changes.ModifiedFiles.Count + changes.DeletedFiles.Count;
        _console.MarkupLine($"[dim]{total} tracked file change(s) detected.[/]");
    }

    private void RenderConfigTable(JournalConfigSyncResult configChanges)
    {
        var table = new Table()
            .Title("[bold]Config Changes (.journalrc)[/]")
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey);

        table.AddColumn(new TableColumn("[bold]File[/]"));
        table.AddColumn(new TableColumn("[bold]Change[/]").Width(18));

        foreach (var f in configChanges.FilesToAdd)
            table.AddRow(f.EscapeMarkup(), "[green]✚ will be added[/]");
        foreach (var f in configChanges.FilesToRemove)
            table.AddRow(f.EscapeMarkup(), "[red]✖ will be removed[/]");

        _console.Write(table);
    }

    private void RenderTocPreview(TocDiffResult diff, string journalPath)
    {
        var config = _journalConfiguration.Read(journalPath);
        var tocFileName =
            config?.TableOfContents.File
            ?? $"{_journalSettings.TableOfContentsFileName}.md";

        if (!diff.HasChanges)
        {
            _console.MarkupLine("[dim]Table of contents: no changes.[/]");
            return;
        }

        var diffLines = TextDiffer.ComputeDiff(diff.CurrentContent, diff.PreviewContent);
        var added = diffLines.Count(l => l.Type == DiffLineType.Added);
        var removed = diffLines.Count(l => l.Type == DiffLineType.Removed);

        var diffMarkup = new System.Text.StringBuilder();
        diffMarkup.AppendLine($"[dim]--- current ({tocFileName.EscapeMarkup()})[/]");
        diffMarkup.AppendLine("[dim]+++ generated[/]");

        foreach (var line in diffLines)
        {
            var escaped = line.Content.EscapeMarkup();
            diffMarkup.AppendLine(
                line.Type switch
                {
                    DiffLineType.Added => $"[green]+ {escaped}[/]",
                    DiffLineType.Removed => $"[red]- {escaped}[/]",
                    _ => $"[dim]  {escaped}[/]",
                }
            );
        }

        var panel = new Panel(new Markup(diffMarkup.ToString().TrimEnd()))
        {
            Header = new PanelHeader("[bold] Table of Contents Preview [/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Grey),
        };

        _console.Write(panel);
        _console.MarkupLine($"[dim]{added} line(s) added · {removed} line(s) removed[/]");
    }

    private void RenderRenamePreview(TocRenameDryRunResult rename)
    {
        var renameText = rename.IsRename
            ? $"[yellow]{rename.CurrentName.EscapeMarkup()}[/]  →  [green]{rename.NewName.EscapeMarkup()}[/]"
            : $"[dim]{rename.CurrentName.EscapeMarkup()} (already named correctly)[/]";

        var renamePanel = new Panel(new Markup(renameText))
        {
            Header = new PanelHeader("[bold] TOC Rename Preview [/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Grey),
        };
        _console.Write(renamePanel);

        if (rename.FilesWithBacklinks.Count == 0)
        {
            _console.MarkupLine("[dim]No backlink references found.[/]");
            return;
        }

        var table = new Table()
            .Title("[bold]Files With Backlinks to Update[/]")
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey);

        table.AddColumn(new TableColumn("[bold]File[/]"));
        table.AddColumn(new TableColumn("[bold]Change[/]").Width(16));

        foreach (var f in rename.FilesWithBacklinks)
            table.AddRow(f.EscapeMarkup(), "[yellow]~ backlinks[/]");

        _console.Write(table);
    }
}
