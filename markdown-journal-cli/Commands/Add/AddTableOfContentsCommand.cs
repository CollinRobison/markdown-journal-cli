using System;
using System.ComponentModel;
using markdown_journal_cli.Commands;
using markdown_journal_cli.Exceptions;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.Configuration.Models;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.Transactions;
using markdown_journal_cli.Services;
using Microsoft.Extensions.Options;
using Spectre.Console;
using Spectre.Console.Cli;

namespace markdown_journal_cli.Commands.Add;

[Description("Creates a Table of Contents for a journal if one does not already exist")]
public sealed class AddTableOfContents(
    IAnsiConsole console,
    IFileSystem fileSystem,
    IJournalConfiguration journalConfiguration,
    ITableOfContentsService tableOfContentsGenerator,
    IOptions<JournalSettings> journalSettings,
    IFileTransactionCoordinator txCoordinator,
    IRollbackReporter rollbackReporter
) : JournalCommand<AddTableOfContentsSettings>
{
    private readonly IAnsiConsole _console =
        console ?? throw new ArgumentNullException(nameof(console));
    private readonly IFileSystem _fileSystem =
        fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

    private readonly IJournalConfiguration _journalConfiguration =
        journalConfiguration ?? throw new ArgumentNullException(nameof(journalConfiguration));

    private readonly ITableOfContentsService _tableOfContentsGenerator =
        tableOfContentsGenerator
        ?? throw new ArgumentNullException(nameof(tableOfContentsGenerator));
    private readonly JournalSettings _journalSettings = journalSettings.Value;
    private readonly IFileTransactionCoordinator _txCoordinator =
        txCoordinator ?? throw new ArgumentNullException(nameof(txCoordinator));
    private readonly IRollbackReporter _rollbackReporter =
        rollbackReporter ?? throw new ArgumentNullException(nameof(rollbackReporter));

    protected override int ExecuteCore(CommandContext context, AddTableOfContentsSettings settings)
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
                throw new InvalidOperationException(
                    $"Failed to read journal configuration from {settings.FilePath}"
                );
            }

            var tocName = settings.TableOfContentsName ?? _journalSettings.TableOfContentsFileName;
            var tocFile = $"{tocName}{FileConstants.MarkdownExtension}";
            var tocPath = Path.Combine(settings.FilePath, tocFile);

            if (_fileSystem.FileExists(tocPath))
            {
                _console.MarkupLine(
                    $"[yellow]Warning:[/] Table of Contents file '{tocFile.EscapeMarkup()}' already exists at '{settings.FilePath.EscapeMarkup()}'"
                );
                return 1;
            }

            using var tx = _txCoordinator.Begin();
            try
            {
                var journalrcPath = _fileSystem.CombinePaths(
                    settings.FilePath,
                    _journalSettings.JournalConfigFileName
                );
                var tocAbsPath = _fileSystem.CombinePaths(settings.FilePath, tocFile);

                if (configCurrent.TableOfContents.File != tocFile)
                {
                    tx.Track(journalrcPath);
                    _journalConfiguration.Update(
                        settings.FilePath,
                        config =>
                        {
                            config.TableOfContents.File = tocFile;
                        }
                    );
                }

                tx.TrackNew(tocAbsPath);
                _tableOfContentsGenerator.UpdateTableOfContents(
                    settings.FilePath,
                    createdDate: DateTime.Now,
                    lastEditedDate: DateTime.Now
                );

                tx.Commit();

                _console.MarkupLine(
                    $"[green]Success:[/] Created Table of Contents file '{tocFile.EscapeMarkup()}' at '{settings.FilePath.EscapeMarkup()}'"
                );
                return 0;
            }
            catch (Exception ex)
            {
                throw _rollbackReporter.RollbackAndBuildException(
                    tx,
                    _txCoordinator,
                    "add table of contents",
                    settings.FilePath,
                    ex
                );
            }
        }
        catch (JournalrcNotFoundException ex)
        {
            _console.MarkupLine($"[red]Error:[/] {ex.Message.EscapeMarkup()}");
            return 1;
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
