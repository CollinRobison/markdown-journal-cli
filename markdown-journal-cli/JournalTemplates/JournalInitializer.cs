using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.Configuration.Objects;
using markdown_journal_cli.Infrastructure.FileSystem;

namespace markdown_journal_cli.JournalTemplates;

/// <summary>
/// Default implementation of IJournalInitializer that creates a new journal with standard files and configuration.
/// </summary>
public class JournalInitializer : IJournalInitializer
{
    private readonly IFileSystem _fileSystem;
    private readonly ITemplateManager _templateManager;
    private readonly IJournalConfiguration _journalConfiguration;

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
        IJournalConfiguration journalConfiguration)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _templateManager = templateManager ?? throw new ArgumentNullException(nameof(templateManager));
        _journalConfiguration = journalConfiguration ?? throw new ArgumentNullException(nameof(journalConfiguration));
    }

    /// <inheritdoc />
    public void Initialize(string journalDirectory, string journalName)
    {
        if (string.IsNullOrWhiteSpace(journalDirectory))
        {
            throw new ArgumentException("Journal directory cannot be null or whitespace.", nameof(journalDirectory));
        }

        if (string.IsNullOrWhiteSpace(journalName))
        {
            throw new ArgumentException("Journal name cannot be null or whitespace.", nameof(journalName));
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
    }

    private void CreateTableOfContents(string journalDirectory)
    {
        _fileSystem.CreateMarkdownFile(
            journalDirectory,
            "1a-TableOfContents",
            _templateManager.GenerateFromTemplate("table-of-contents", null)
        );
    }

    private void CreateIntroduction(string journalDirectory)
    {
        var introParams = new Dictionary<string, object>
        {
            ["title"] = "Introduction",
            ["body"] = "Add an introduction to your new journal here.",
            ["addSourceBlock"] = false,
        };

        _fileSystem.CreateMarkdownFile(
            journalDirectory,
            "1b-Intro",
            _templateManager.GenerateFromTemplate("journal-entry", introParams)
        );
    }

    private void CreateJournalEntryTemplate(string journalDirectory)
    {
        _fileSystem.CreateMarkdownFile(
            journalDirectory,
            "1c-Journal-Entry-Template",
            _templateManager.GenerateFromTemplate("journal-entry", null)
        );
    }

    private void CreateAllMyJournals(string journalDirectory)
    {
        var allMyJournalsParams = new Dictionary<string, object>
        {
            ["title"] = "Journals List",
            ["body"] = @"- [example journal 1](link-to-journal)
- [example journal 2](link-to-journal)
- [example journal 3](link-to-journal)",
            ["addSourceBlock"] = false,
        };

        _fileSystem.CreateMarkdownFile(
            journalDirectory,
            "1h-All-My-Journals",
            _templateManager.GenerateFromTemplate("journal-entry", allMyJournalsParams)
        );
    }

    private void CreateJournalConfiguration(string journalDirectory, string journalName)
    {
        RootEntries[] rootConfig =
        [
            new() { Name = "Introduction", File = "1b-Intro.md" },
            new() { Name = "Journal Entry Template", File = "1c-Journal-Entry-Template.md" },
            new() { Name = "All My Journals", File = "1h-All-My-Journals.md" }
        ];

        JournalConfig journalrc = new()
        {
            JournalName = journalName,
            TableOfContents = new()
            {
                Structure = new()
                {
                    Topics = []
                },
                RootEntries = rootConfig,
                IndexCache = new()
                {
                    UpdatedAt = DateTime.Now,
                    Topics = [],
                    RootEntries = rootConfig
                }
            }
        };

        _journalConfiguration.Create(journalDirectory, journalrc);
    }
}