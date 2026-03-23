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
    void UpdateTableOfContents(
        string journalDirectory,
        DateTime? createdDate = null,
        DateTime? lastEditedDate = null
    );

    /// <summary>
    /// Generates and returns the TOC markdown content without writing to disk.
    /// Preserves existing Created/Last Edited dates from the current TOC file (if present).
    /// Useful for dry-run previews and diffing against the current file.
    /// </summary>
    /// <param name="journalDirectory">The directory containing the journal.</param>
    /// <returns>The generated TOC markdown content as a string.</returns>
    string PreviewTableOfContents(string journalDirectory);
}
