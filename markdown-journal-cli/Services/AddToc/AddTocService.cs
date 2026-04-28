using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.Configuration.Models;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.Tracking;
using markdown_journal_cli.Infrastructure.Transactions;
using Microsoft.Extensions.Options;

namespace markdown_journal_cli.Services.AddToc;

/// <summary>
/// Creates the <c>.journaltoc</c> and/or the markdown TOC file for an existing journal
/// as a single logical operation wrapped in a <see cref="IFileTransactionScope"/>.
/// </summary>
public sealed class AddTocService(
    IFileSystem fileSystem,
    IJournalConfiguration journalConfiguration,
    IJournalTocStructureRepository tocStructureRepository,
    ITableOfContentsService tableOfContentsService,
    IFileTracking fileTracking,
    IFileTransactionCoordinator txCoordinator,
    IRollbackReporter rollbackReporter,
    IOptions<JournalSettings> journalSettings
) : IAddTocService
{
    private readonly IFileSystem _fileSystem =
        fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    private readonly IJournalConfiguration _journalConfiguration =
        journalConfiguration ?? throw new ArgumentNullException(nameof(journalConfiguration));
    private readonly IJournalTocStructureRepository _tocStructureRepository =
        tocStructureRepository ?? throw new ArgumentNullException(nameof(tocStructureRepository));
    private readonly ITableOfContentsService _tableOfContentsService =
        tableOfContentsService ?? throw new ArgumentNullException(nameof(tableOfContentsService));
    private readonly IFileTracking _fileTracking =
        fileTracking ?? throw new ArgumentNullException(nameof(fileTracking));
    private readonly IFileTransactionCoordinator _txCoordinator =
        txCoordinator ?? throw new ArgumentNullException(nameof(txCoordinator));
    private readonly IRollbackReporter _rollbackReporter =
        rollbackReporter ?? throw new ArgumentNullException(nameof(rollbackReporter));
    private readonly JournalSettings _settings = journalSettings.Value;

    /// <inheritdoc />
    public AddTocResult Execute(string journalDir, bool structureOnly = false, bool mdOnly = false, string? tocName = null)
    {
        if (string.IsNullOrWhiteSpace(journalDir))
            throw new ArgumentException(
                "Journal directory cannot be null or whitespace.",
                nameof(journalDir)
            );

        var metadataDir = _fileSystem.CombinePaths(journalDir, _settings.MetadataDirName);
        var tocStructurePath = _fileSystem.CombinePaths(metadataDir, _settings.TocStructureFileName);

        var config =
            _journalConfiguration.Read(journalDir)
            ?? throw new InvalidOperationException(
                $"Could not read journal configuration from {journalDir}"
            );

        var tocFileName = tocName is not null
            ? tocName + FileConstants.MarkdownExtension
            : config.TableOfContents.File;
        var tocMdPath = _fileSystem.CombinePaths(journalDir, tocFileName);

        bool structureExists = _fileSystem.FileExists(tocStructurePath);
        bool mdExists = _fileSystem.FileExists(tocMdPath);

        // Determine what this invocation is responsible for creating
        bool wantsStructure = structureOnly || (!structureOnly && !mdOnly);
        bool wantsMd = mdOnly || (!structureOnly && !mdOnly);

        // If every requested artifact already exists, nothing to do
        bool allExist = (!wantsStructure || structureExists) && (!wantsMd || mdExists);
        if (allExist)
            return AddTocResult.AlreadyExists;

        // Partial = at least one wanted artifact already exists (only possible when both are wanted)
        bool someExist = (wantsStructure && structureExists) || (wantsMd && mdExists);

        using var tx = _txCoordinator.BeginOrJoin();
        try
        {
            if (wantsStructure && !structureExists)
            {
                tx.TrackNew(tocStructurePath);
                _tocStructureRepository.Save(JournalTocStructure.Empty(), metadataDir);

                // Seed the structure from entries already in the tracking index so that
                // the .journaltoc and the generated markdown TOC reflect existing journal entries.
                var index = _fileTracking.LoadIndex(journalDir);
                foreach (var file in index.Files.Keys
                    .Where(f => f.EndsWith(FileConstants.MarkdownExtension, StringComparison.OrdinalIgnoreCase)))
                {
                    _journalConfiguration.AddEntry(journalDir, string.Empty, file);
                }
            }

            if (wantsMd && !mdExists)
            {
                // When a custom name was requested, update .journalrc to reflect it before
                // writing the TOC file — UpdateTableOfContents reads the filename from config.
                if (tocName is not null)
                {
                    var journalrcPath = _fileSystem.CombinePaths(journalDir, _settings.JournalConfigFileName);
                    tx.Track(journalrcPath);
                    _journalConfiguration.Update(journalDir, c => c.TableOfContents.File = tocFileName);
                }

                tx.TrackNew(tocMdPath);
                _tableOfContentsService.UpdateTableOfContents(
                    journalDir,
                    createdDate: DateTime.Now,
                    lastEditedDate: DateTime.Now
                );
            }

            tx.Commit();

            return someExist ? AddTocResult.PartiallyCreated : AddTocResult.Created;
        }
        catch (Exception ex)
        {
            throw _rollbackReporter.RollbackAndBuildException(
                tx,
                _txCoordinator,
                "add toc",
                journalDir,
                ex
            );
        }
    }
}
