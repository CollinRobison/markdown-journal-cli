using markdown_journal_cli.Exceptions;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.Configuration.Models;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.JournalTemplates;
using markdown_journal_cli.Infrastructure.Tracking;
using Microsoft.Extensions.Options;

namespace markdown_journal_cli.Services;

public class InitJournalService : IInitJournalService
{
    private readonly IFileSystem _fileSystem;
    private readonly ITemplateManager _templateManager;
    private readonly IJournalConfiguration _journalConfiguration;
    private readonly IFileTracking _fileTracking;
    private readonly JournalSettings _journalSettings;

    public InitJournalService(
        IFileSystem fileSystem,
        ITemplateManager templateManager,
        IJournalConfiguration journalConfiguration,
        IFileTracking fileTracking,
        IOptions<JournalSettings> journalSettings
    )
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _templateManager =
            templateManager ?? throw new ArgumentNullException(nameof(templateManager));
        _journalConfiguration =
            journalConfiguration ?? throw new ArgumentNullException(nameof(journalConfiguration));
        _fileTracking = fileTracking ?? throw new ArgumentNullException(nameof(fileTracking));
        _journalSettings = journalSettings.Value;
    }

    /// <inheritdoc />
    public void Initialize(string journalDirectory, string journalName, string? tableOfContentsName)
    {
        if (string.IsNullOrWhiteSpace(journalDirectory))
            throw new ArgumentException(
                "Journal directory cannot be null or whitespace.",
                nameof(journalDirectory)
            );

        if (string.IsNullOrWhiteSpace(journalName))
            throw new ArgumentException(
                "Journal name cannot be null or whitespace.",
                nameof(journalName)
            );

        var resolvedTocName = CreateTableOfContents(journalDirectory, tableOfContentsName);
        CreateJournalConfiguration(journalDirectory, journalName, resolvedTocName);
        CreateFileTrackingIndex(journalDirectory);
    }

    private string CreateTableOfContents(string journalDirectory, string? tocName)
    {
        var resolvedName = string.IsNullOrWhiteSpace(tocName)
            ? _journalSettings.TableOfContentsFileName
            : tocName;

        var filePath = _fileSystem.CombinePaths(
            journalDirectory,
            $"{resolvedName}{FileConstants.MarkdownExtension}"
        );

        if (_fileSystem.FileExists(filePath))
            throw new TocFileAlreadyExistsException(
                journalDirectory,
                $"{resolvedName}{FileConstants.MarkdownExtension}"
            );

        _fileSystem.CreateMarkdownFile(
            journalDirectory,
            resolvedName,
            _templateManager.GenerateFromTemplate("table-of-contents", null)
        );

        return resolvedName;
    }

    private void CreateJournalConfiguration(
        string journalDirectory,
        string journalName,
        string tocName
    )
    {
        JournalConfig journalrc =
            new()
            {
                JournalName = journalName,
                TableOfContents = new()
                {
                    File = $"{tocName}{FileConstants.MarkdownExtension}",
                    Structure = new() { Topics = [] },
                    RootEntries = [],
                },
            };

        _journalConfiguration.Create(journalDirectory, journalrc);
    }

    private void CreateFileTrackingIndex(string journalDirectory)
    {
        _fileTracking.LoadIndex(journalDirectory);
        _fileTracking.UpdateIndex(journalDirectory);
    }
}
