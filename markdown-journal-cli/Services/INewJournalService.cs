namespace markdown_journal_cli.Services;

/// <summary>
/// Provides functionality to initialize a new journal with default files and configuration.
/// </summary>
public interface INewJournalService
{
    /// <summary>
    /// Initializes a new journal by creating the directory structure, default files, and configuration.
    /// </summary>
    /// <param name="journalDirectory">The directory path where the journal will be created.</param>
    /// <param name="journalName">The name of the journal to create.</param>
    /// <exception cref="ArgumentNullException">Thrown when journalDirectory or journalName is null.</exception>
    /// <exception cref="ArgumentException">Thrown when journalDirectory or journalName is empty or whitespace.</exception>
    void Initialize(string journalDirectory, string journalName);
}
