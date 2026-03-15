using System;

namespace markdown_journal_cli.Services;

/// <summary>
/// Provides operations for updating journal entries including renaming, relocating,
/// and managing display names and ignore status.
/// </summary>
public interface IJournalFileUpdateService
{
    /// <summary>
    /// Updates a journal entry with new name, title, location, or ignore status.
    /// Handles all orchestration logic for determining what changed and performing the appropriate operations.
    /// </summary>
    /// <param name="directory">The directory containing the journal.</param>
    /// <param name="currentFileName">The current filename (with or without .md extension).</param>
    /// <param name="newEntryName">Optional new entry name (will update filename).</param>
    /// <param name="newEntryTitle">Optional new display title for TOC.</param>
    /// <param name="newHeadings">Optional new heading location in TOC hierarchy.</param>
    /// <param name="ignoreFile">If true, add file to ignore list.</param>
    /// <param name="unignoreFile">If true, remove file from ignore list.</param>
    void UpdateEntry(
        string directory,
        string currentFileName,
        string? newEntryName = null,
        string? newEntryTitle = null,
        string? newHeadings = null,
        bool ignoreFile = false,
        bool unignoreFile = false
    );

    /// <summary>
    /// Renames a journal entry file and updates all references in the configuration.
    /// </summary>
    /// <param name="directory">The directory containing the journal.</param>
    /// <param name="oldFile">The current filename.</param>
    /// <param name="newFile">The new filename.</param>
    /// <exception cref="FileNotFoundException">Thrown when the old file does not exist.</exception>
    void RenameEntry(string directory, string oldFile, string newFile);

    /// <summary>
    /// Updates an entry's location in the topic hierarchy.
    /// </summary>
    /// <param name="directory">The directory containing the journal.</param>
    /// <param name="fileName">The filename of the entry to relocate.</param>
    /// <param name="newTopicPath">The new topic path (empty array for root level).</param>
    /// <param name="displayName">The display name to use for the entry at its new location.</param>
    void UpdateEntryLocation(string directory, string fileName, string[] newTopicPath, string displayName);

    /// <summary>
    /// Updates an entry's display name in the table of contents.
    /// </summary>
    /// <param name="directory">The directory containing the journal.</param>
    /// <param name="fileName">The filename of the entry to update.</param>
    /// <param name="newDisplayName">The new display name.</param>
    void UpdateEntryDisplayName(string directory, string fileName, string newDisplayName);

    /// <summary>
    /// Sets the ignore status of an entry (whether it appears in the table of contents).
    /// </summary>
    /// <param name="directory">The directory containing the journal.</param>
    /// <param name="fileName">The filename of the entry.</param>
    /// <param name="ignored">True to ignore the file, false to unignore it.</param>
    void SetIgnoreStatus(string directory, string fileName, bool ignored);
}
