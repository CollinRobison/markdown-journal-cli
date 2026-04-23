using markdown_journal_cli.Infrastructure.Configuration.Models;

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

    /// <summary>
    /// Generates and returns the TOC markdown content using a caller-supplied projected config,
    /// without writing to disk and without reading .journalrc from disk.
    /// Preserves existing Created/Last Edited dates from the current TOC file on disk (if present).
    /// Used by the dry-run path to preview the TOC after applying in-memory config drift.
    /// </summary>
    /// <param name="journalDirectory">The directory containing the journal.</param>
    /// <param name="projectedConfig">The in-memory config to use for TOC generation.</param>
    /// <returns>The generated TOC markdown content as a string.</returns>
    string PreviewTableOfContents(string journalDirectory, JournalConfig projectedConfig);

    /// <summary>
    /// Generates and returns the TOC markdown content using caller-supplied projected config and
    /// TOC structure, without writing to disk.
    /// Used by the dry-run path when both the config and the structural entries are projected.
    /// </summary>
    /// <param name="journalDirectory">The directory containing the journal.</param>
    /// <param name="projectedConfig">The in-memory config to use for TOC generation.</param>
    /// <param name="projectedTocStructure">The in-memory TOC structure to use.</param>
    /// <returns>The generated TOC markdown content as a string.</returns>
    string PreviewTableOfContents(
        string journalDirectory,
        JournalConfig projectedConfig,
        JournalTocStructure projectedTocStructure
    );
}
