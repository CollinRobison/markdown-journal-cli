namespace markdown_journal_cli.Services;

/// <summary>
/// Generates and updates the table of contents based on journal configuration.
/// </summary>
public interface ITableOfContentsService
{
    /// <summary>
    /// Updates the table of contents file based on the current journal configuration.
    /// </summary>
    /// <param name="journalDirectory">The directory containing the journal.</param>
    /// <param name="createdDate">Optional creation date to display at the top of the TOC.</param>
    /// <param name="lastEditedDate">Optional last edited date to display at the top of the TOC.</param>
    void UpdateTableOfContents(string journalDirectory, DateTime? createdDate = null, DateTime? lastEditedDate = null);
}
