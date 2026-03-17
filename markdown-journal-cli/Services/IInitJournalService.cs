namespace markdown_journal_cli.Services;

public interface IInitJournalService
{
    /// <summary>
    /// Initialises an existing directory as an mdjournal-managed journal.
    /// Creates a Table of Contents file, a .journalrc configuration, and a file-tracking index
    /// pre-populated with all existing markdown files. Does not create template files.
    /// </summary>
    /// <param name="journalDirectory">Path to the existing directory to initialise.</param>
    /// <param name="journalName">Display name for the journal.</param>
    /// <param name="tableOfContentsName">
    /// Optional name for the TOC file (without extension).
    /// Defaults to <see cref="JournalSettings.TableOfContentsFileName"/> when <c>null</c>.
    /// </param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="journalDirectory"/> or <paramref name="journalName"/> is null or whitespace.</exception>
    /// <exception cref="Exceptions.TocFileAlreadyExistsException">Thrown when the resolved TOC filename conflicts with an existing file.</exception>
    void Initialize(string journalDirectory, string journalName, string? tableOfContentsName);
}
