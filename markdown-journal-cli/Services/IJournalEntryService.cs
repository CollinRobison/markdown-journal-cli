using System;

namespace markdown_journal_cli.Services;

/// <summary>
/// Defines operations for creating and managing journal entries.
/// </summary>
/// <remarks>
/// Implementations are responsible for creating the markdown file, updating journal configuration,
/// maintaining the tracking index, and updating the table of contents as needed.
/// </remarks>
public interface IJournalEntryService
{
    /// <summary>
    /// Adds a new journal entry file to the specified journal directory and updates configuration and indexes.
    /// </summary>
    /// <param name="filePath">Path to the journal directory where the entry should be created.</param>
    /// <param name="ignoreFile">If true, the entry is excluded from generated indices like the table of contents.</param>
    /// <param name="entryName">Short name used to construct the entry file name.</param>
    /// <param name="heading">Optional heading to group the entry under; used when building the file name.</param>
    /// <param name="subheading">Optional subheading to group the entry under; used when building the file name.</param>
    /// <param name="entryTitle">Optional display title to place inside the entry; falls back to entryName if null.</param>
    void AddEntry(
        string filePath,
        bool ignoreFile,
        string entryName,
        string? heading,
        string? subheading,
        string? entryTitle
    );
}
