namespace markdown_journal_cli.Services.RemoveEntry;

/// <summary>
/// Orchestrates the full removal of a journal entry: deletes the file, removes it from
/// .journalrc, removes it from the tracking index, regenerates the TOC, and optionally
/// strips dead inline-link references across the journal.
/// </summary>
public interface IRemoveEntryService
{
    /// <summary>
    /// Removes a journal entry and returns the relative paths of any files whose dead links
    /// were stripped (populated only when <paramref name="cleanRefs"/> is <c>true</c>).
    /// </summary>
    /// <param name="journalPath">The journal directory.</param>
    /// <param name="fileName">The entry filename (with or without .md extension).</param>
    /// <param name="cleanRefs">
    /// When <c>true</c>, scans all other entries and strips inline links that pointed to the
    /// removed file, then re-hashes those files in the tracking index.
    /// </param>
    /// <returns>
    /// A read-only list of relative file paths that were modified by the dead-link cleanup.
    /// Empty when <paramref name="cleanRefs"/> is <c>false</c> or no links were found.
    /// </returns>
    IReadOnlyList<string> RemoveEntry(string journalPath, string fileName, bool cleanRefs);
}
