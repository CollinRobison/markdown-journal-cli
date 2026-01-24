using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.Configuration.Models;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.Tracking;
using Microsoft.Extensions.Options;

namespace markdown_journal_cli.JournalTemplates;

/// <summary>
/// Default implementation of IJournalInitializer that creates a new journal with standard files and configuration.
/// </summary>
public class JournalInitializer : IJournalInitializer
{
    private readonly IFileSystem _fileSystem;
    private readonly ITemplateManager _templateManager;
    private readonly IJournalConfiguration _journalConfiguration;
    private readonly IFileTracking _fileTracking;
    private readonly JournalSettings _journalSettings;

    /// <summary>
    /// Initializes a new instance of the JournalInitializer class.
    /// </summary>
    /// <param name="fileSystem">The file system service for creating directories and files.</param>
    /// <param name="templateManager">The template manager for generating content from templates.</param>
    /// <param name="journalConfiguration">The configuration service for creating journalrc files.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public JournalInitializer(
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

        // Create the journal directory
        _fileSystem.CreateDirectory(journalDirectory);

        // Create default files
        CreateTableOfContents(journalDirectory);
        CreateIntroduction(journalDirectory);
        CreateJournalEntryTemplate(journalDirectory);
        CreateAllMyJournals(journalDirectory);

        // Create journal configuration
        CreateJournalConfiguration(journalDirectory, journalName);

        // create file tracking 
        CreateFileTrackingIndex(journalDirectory);

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
        Entries[] rootConfig =
        [
            new()
            {
                Name = _journalSettings.IntroductionTitle,
                File = $"{_journalSettings.IntroductionFileName}.md",
            },
            new()
            {
                Name = _journalSettings.JournalEntryTemplateTitle,
                File = $"{_journalSettings.JournalEntryTemplateFileName}.md",
            },
            new()
            {
                Name = _journalSettings.AllJournalsTitle,
                File = $"{_journalSettings.AllJournalsFileName}.md",
            },
        ];

        JournalConfig journalrc = new()
        {
            JournalName = journalName,
            TableOfContents = new()
            {
                Structure = new() { Topics = [] },
                RootEntries = rootConfig,
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
