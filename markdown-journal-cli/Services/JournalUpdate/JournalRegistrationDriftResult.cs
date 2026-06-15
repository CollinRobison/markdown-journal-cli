namespace markdown_journal_cli.Services;

/// <summary>
/// Represents drift between the tracking index and the journal's registration state.
/// This includes TOC entries stored in <c>.journaltoc</c> and ignore entries stored
/// in <c>.journalrc</c>, and is used to update journal membership without relying on
/// file-content hash changes.
/// </summary>
public class JournalRegistrationDriftResult
{
    /// <summary>
    /// Files present in the tracking index but absent from the registered TOC
    /// structure and ignore list. The TOC file itself is excluded.
    /// </summary>
    public IReadOnlyList<string> FilesToAdd { get; init; } = [];

    /// <summary>
    /// Files present in the registered TOC structure but absent from the tracking
    /// index. Ignored files are excluded from removal.
    /// </summary>
    public IReadOnlyList<string> FilesToRemove { get; init; } = [];

    public bool HasChanges => FilesToAdd.Count > 0 || FilesToRemove.Count > 0;
}
