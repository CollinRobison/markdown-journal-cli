namespace markdown_journal_cli.Services.RemoveEntry;

/// <summary>
/// Orchestrates the full removal of a journal entry: deletes the file, removes it from
/// .journalrc, removes it from the tracking index, regenerates the TOC, and optionally
/// strips dead inline-link references across the journal.
/// </summary>
public interface IRemoveEntryService
{
    /// <summary>
    /// Validates all preconditions for removing an entry without performing any writes.
    /// Throws the same guard exceptions that <see cref="RemoveEntry"/> throws so callers
    /// can surface errors before showing a confirmation prompt.
    /// </summary>
    /// <param name="journalPath">The journal directory.</param>
    /// <param name="fileName">The entry filename (with or without .md extension).</param>
    /// <param name="cleanRefs">
    /// When <c>true</c>, the file-existence check is relaxed — a missing entry file does not
    /// throw <see cref="System.IO.FileNotFoundException"/> so that orphaned references can
    /// still be cleaned up.  All other guards (journalrc, tracking index, protected files)
    /// remain mandatory.
    /// </param>
    /// <exception cref="Exceptions.JournalrcNotFoundException">The .journalrc file is missing.</exception>
    /// <exception cref="Exceptions.TrackingIndexNotFoundException">The .mdjournal tracking index is missing.</exception>
    /// <exception cref="Exceptions.ProtectedJournalFileException">The target file is a protected journal file.</exception>
    /// <exception cref="System.IO.FileNotFoundException">
    /// The entry file does not exist and <paramref name="cleanRefs"/> is <c>false</c>.
    /// </exception>
    void ValidatePreconditions(string journalPath, string fileName, bool cleanRefs = false);

    /// <summary>
    /// Removes a journal entry and returns a <see cref="RemoveEntryResult"/> describing
    /// what was found and removed (file on disk, config entry, tracking entry, and dead links).
    /// </summary>
    /// <param name="journalPath">The journal directory.</param>
    /// <param name="fileName">The entry filename (with or without .md extension).</param>
    /// <param name="cleanRefs">
    /// When <c>true</c>, scans all other entries and strips inline links that pointed to the
    /// removed file, then re-hashes those files in the tracking index.
    /// </param>
    /// <returns>
    /// A <see cref="RemoveEntryResult"/> with flags indicating which resources were present
    /// and removed, and the relative paths of files modified by dead-link cleanup.
    /// </returns>
    RemoveEntryResult RemoveEntry(string journalPath, string fileName, bool cleanRefs);
}
