using System.Text.Json;
using markdown_journal_cli.Infrastructure.FileSystem;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace markdown_journal_cli.Tests.Infrastructure;

/// <summary>
/// Abstract base class for command integration tests.
/// Creates a unique temp directory, seeds a journal, and provides real service instances.
/// Cleans up the temp directory on Dispose() regardless of test outcome.
/// Integration tests MUST use NO mocks unless there is no real implementation available.
/// </summary>
public abstract class JournalIntegrationTestBase : IDisposable
{
    /// <summary>Root temp directory for this test class instance.</summary>
    protected readonly string JournalRoot;

    /// <summary>Journal subdirectory path (JournalRoot/JournalName).</summary>
    protected readonly string JournalPath;

    /// <summary>Real FileSystem instance backed by real disk I/O.</summary>
    protected readonly IFileSystem FileSystem;

    /// <summary>JournalSettings wired to JournalPath.</summary>
    protected readonly IOptions<JournalSettings> JournalSettings;

    private readonly string _journalName;

    /// <param name="journalName">Name of the journal subfolder (default: "TestJournal").</param>
    protected JournalIntegrationTestBase(string journalName = "TestJournal")
    {
        _journalName = journalName;
        JournalRoot = Path.Combine(Path.GetTempPath(), $"journal-{Guid.NewGuid():N}");
        JournalPath = Path.Combine(JournalRoot, journalName);
        Directory.CreateDirectory(JournalPath);

        FileSystem = new markdown_journal_cli.Infrastructure.FileSystem.FileSystem(
            NullLogger<markdown_journal_cli.Infrastructure.FileSystem.FileSystem>.Instance
        );

        JournalSettings = Options.Create(
            new JournalSettings
            {
                AppName = "md-journal",
                JournalConfigFileName = ".journalrc",
                DefaultJournalName = journalName,
                TableOfContentsFileName = "1a-TableOfContents",
                TableOfContentsTitle = "Table of Contents",
                IntroductionFileName = "1b-Intro",
                IntroductionTitle = "Introduction",
                JournalEntryTemplateFileName = "1c-Journal_Entry_Template",
                JournalEntryTemplateTitle = "Journal Entry Template",
                AllJournalsFileName = "1h-All_My_Journals",
                AllJournalsTitle = "All My Journals",
                TitleSpaceSeparator = "_",
                HeadingSeparator = "-",
                DateFormat = "MM/dd/yyyy",
            }
        );
    }

    /// <summary>
    /// Seeds the journal directory with .journalrc, .mdjournal, and 1a-TableOfContents.md.
    /// Call from subclass constructor after registering services if you need a pre-initialized journal.
    /// </summary>
    protected void InitializeJournal()
    {
        var settings = JournalSettings.Value;

        // Write .journalrc
        var journalrcPath = Path.Combine(JournalPath, settings.JournalConfigFileName);
        var journalrcContent = JsonSerializer.Serialize(
            new
            {
                journalName = _journalName,
                tableOfContents = new
                {
                    file = $"{settings.TableOfContentsFileName}.md",
                    extensions = new[] { ".md" },
                    ignoreFiles = Array.Empty<string>(),
                    structure = new { topics = Array.Empty<object>() },
                    rootEntries = Array.Empty<object>(),
                },
            },
            new JsonSerializerOptions { WriteIndented = true }
        );
        File.WriteAllText(journalrcPath, journalrcContent);

        // Write tracking index (.md-journal)
        var trackingPath = Path.Combine(JournalPath, $".{settings.AppName}");
        File.WriteAllText(trackingPath, "{}");

        // Write Table of Contents
        var tocPath = Path.Combine(JournalPath, $"{settings.TableOfContentsFileName}.md");
        var tocContent =
            $"[Back to All My Journals]({settings.AllJournalsFileName}.md)\n\n"
            + $"Created: {DateTime.Now:M/d/yyyy}\n"
            + $"Last Edited: {DateTime.Now:M/d/yyyy}\n\n"
            + $"# {settings.TableOfContentsTitle}\n\n"
            + $"## Entries\n";
        File.WriteAllText(tocPath, tocContent);
    }

    /// <summary>Deletes JournalRoot and all contents. Always runs, even if a test fails.</summary>
    public void Dispose()
    {
        if (Directory.Exists(JournalRoot))
            Directory.Delete(JournalRoot, recursive: true);

        GC.SuppressFinalize(this);
    }
}
