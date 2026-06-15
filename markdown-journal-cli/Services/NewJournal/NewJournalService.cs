using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.Configuration.Models;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.JournalTemplates;
using markdown_journal_cli.Infrastructure.Tracking;
using markdown_journal_cli.Infrastructure.Transactions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace markdown_journal_cli.Services;

/// <summary>
/// Default implementation of IJournalInitializer that creates a new journal with standard files and configuration.
/// </summary>
public sealed class NewJournalService(
    IFileSystem fileSystem,
    ITemplateManager templateManager,
    IJournalConfiguration journalConfiguration,
    IFileTracking fileTracking,
    IOptions<JournalSettings> journalSettings,
    IFileTransactionCoordinator txCoordinator,
    IRollbackReporter rollbackReporter,
    ILogger<NewJournalService> logger,
    IJournalTocStructureRepository tocStructureRepository
) : INewJournalService
{
    private readonly IFileSystem _fileSystem =
        fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    private readonly ITemplateManager _templateManager =
        templateManager ?? throw new ArgumentNullException(nameof(templateManager));
    private readonly IJournalConfiguration _journalConfiguration =
        journalConfiguration ?? throw new ArgumentNullException(nameof(journalConfiguration));
    private readonly IFileTracking _fileTracking =
        fileTracking ?? throw new ArgumentNullException(nameof(fileTracking));
    private readonly ILogger<NewJournalService> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly JournalSettings _journalSettings = journalSettings.Value;
    private readonly IFileTransactionCoordinator _txCoordinator =
        txCoordinator ?? throw new ArgumentNullException(nameof(txCoordinator));
    private readonly IRollbackReporter _rollbackReporter =
        rollbackReporter ?? throw new ArgumentNullException(nameof(rollbackReporter));
    private readonly IJournalTocStructureRepository _tocStructureRepository =
        tocStructureRepository ?? throw new ArgumentNullException(nameof(tocStructureRepository));

    /// <inheritdoc />
    public void Initialize(string journalDirectory, string journalName)
    {
        if (string.IsNullOrWhiteSpace(journalDirectory))
        {
            throw new ArgumentException(
                "Journal directory cannot be null or whitespace.",
                nameof(journalDirectory)
            );
        }

        if (string.IsNullOrWhiteSpace(journalName))
        {
            throw new ArgumentException(
                "Journal name cannot be null or whitespace.",
                nameof(journalName)
            );
        }

        _logger.LogDebug(
            "Initializing new journal '{JournalName}' at '{JournalDirectory}'",
            journalName,
            journalDirectory
        );

        var directoryAlreadyExisted = _fileSystem.DirectoryExists(journalDirectory);
        var metadataDir = _fileSystem.CombinePaths(
            journalDirectory,
            _journalSettings.MetadataDirName
        );

        using var tx = _txCoordinator.Begin();
        try
        {
            var tocAbsPath = _fileSystem.CombinePaths(
                journalDirectory,
                $"{_journalSettings.TableOfContentsFileName}{FileConstants.MarkdownExtension}"
            );
            var introAbsPath = _fileSystem.CombinePaths(
                journalDirectory,
                $"{_journalSettings.IntroductionFileName}{FileConstants.MarkdownExtension}"
            );
            var templateAbsPath = _fileSystem.CombinePaths(
                journalDirectory,
                $"{_journalSettings.JournalEntryTemplateFileName}{FileConstants.MarkdownExtension}"
            );
            var allJournalsAbsPath = _fileSystem.CombinePaths(
                journalDirectory,
                $"{_journalSettings.AllJournalsFileName}{FileConstants.MarkdownExtension}"
            );
            var journalrcAbsPath = _fileSystem.CombinePaths(
                journalDirectory,
                _journalSettings.JournalConfigFileName
            );
            var trackingIndexAbsPath = _fileSystem.CombinePaths(
                metadataDir,
                _journalSettings.TrackingFileName
            );
            var tocStructureAbsPath = _fileSystem.CombinePaths(
                metadataDir,
                _journalSettings.TocStructureFileName
            );

            if (!directoryAlreadyExisted)
                tx.TrackNewDirectory(journalDirectory);

            tx.TrackNew(tocAbsPath);
            tx.TrackNew(introAbsPath);
            tx.TrackNew(templateAbsPath);
            tx.TrackNew(allJournalsAbsPath);
            tx.TrackNew(journalrcAbsPath);
            tx.TrackNewDirectory(metadataDir);
            tx.TrackNew(trackingIndexAbsPath);
            tx.TrackNew(tocStructureAbsPath);

            _fileSystem.CreateDirectory(journalDirectory);
            _fileSystem.CreateDirectory(metadataDir);

            CreateTableOfContents(journalDirectory);
            CreateIntroduction(journalDirectory);
            CreateJournalEntryTemplate(journalDirectory);
            CreateAllMyJournals(journalDirectory);
            CreateJournalConfiguration(journalDirectory, journalName);
            CreateFileTrackingIndex(journalDirectory);

            tx.Commit();

            _logger.LogDebug(
                "Journal '{JournalName}' initialized successfully at '{JournalDirectory}'",
                journalName,
                journalDirectory
            );
        }
        catch (Exception ex)
        {
            throw _rollbackReporter.RollbackAndBuildException(
                tx,
                _txCoordinator,
                "create new journal",
                journalDirectory,
                ex
            );
        }
    }

    private void CreateTableOfContents(string journalDirectory)
    {
        _fileSystem.CreateMarkdownFile(
            journalDirectory,
            _journalSettings.TableOfContentsFileName,
            _templateManager.GenerateFromTemplate("table-of-contents", null)
        );
    }

    private void CreateIntroduction(string journalDirectory)
    {
        var introParams = new Dictionary<string, object>
        {
            ["title"] = _journalSettings.IntroductionTitle,
            ["body"] = "Add an introduction to your new journal here.",
            ["addSourceBlock"] = false,
        };

        _fileSystem.CreateMarkdownFile(
            journalDirectory,
            _journalSettings.IntroductionFileName,
            _templateManager.GenerateFromTemplate("journal-entry", introParams)
        );
    }

    private void CreateJournalEntryTemplate(string journalDirectory)
    {
        _fileSystem.CreateMarkdownFile(
            journalDirectory,
            _journalSettings.JournalEntryTemplateFileName,
            _templateManager.GenerateFromTemplate("journal-entry", null)
        );
    }

    private void CreateAllMyJournals(string journalDirectory)
    {
        var allMyJournalsParams = new Dictionary<string, object>
        {
            ["title"] = "Journals List",
            ["body"] =
                @"- [example journal 1](link-to-journal)
- [example journal 2](link-to-journal)
- [example journal 3](link-to-journal)",
            ["addSourceBlock"] = false,
        };

        _fileSystem.CreateMarkdownFile(
            journalDirectory,
            _journalSettings.AllJournalsFileName,
            _templateManager.GenerateFromTemplate("journal-entry", allMyJournalsParams)
        );
    }

    private void CreateJournalConfiguration(string journalDirectory, string journalName)
    {
        JournalConfig journalrc = new() { JournalName = journalName, TableOfContents = new(), TrackingIndex = new() };

        _journalConfiguration.Create(journalDirectory, journalrc);

        // Seed initial root entries into .journaltoc
        var metadataDir = Path.Combine(journalDirectory, _journalSettings.MetadataDirName);
        var initialTocStructure = new JournalTocStructure
        {
            Structure = new() { Topics = [] },
            RootEntries =
            [
                new()
                {
                    Name = _journalSettings.IntroductionTitle,
                    File =
                        $"{_journalSettings.IntroductionFileName}{FileConstants.MarkdownExtension}",
                },
                new()
                {
                    Name = _journalSettings.JournalEntryTemplateTitle,
                    File =
                        $"{_journalSettings.JournalEntryTemplateFileName}{FileConstants.MarkdownExtension}",
                },
                new()
                {
                    Name = _journalSettings.AllJournalsTitle,
                    File =
                        $"{_journalSettings.AllJournalsFileName}{FileConstants.MarkdownExtension}",
                },
            ],
        };
        _tocStructureRepository.Save(initialTocStructure, metadataDir);
    }

    private void CreateFileTrackingIndex(string journalDirectory)
    {
        _fileTracking.LoadIndex(journalDirectory);
        _fileTracking.UpdateIndex(journalDirectory);
    }
}
