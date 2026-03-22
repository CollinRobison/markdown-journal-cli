namespace markdown_journal_cli.Infrastructure.Configuration.Models;

/// <summary>
/// Represents the difference between what is recorded in the tracking index
/// and what is registered in the journal configuration (.journalrc).
/// Used to drive incremental updates to the journal config independently of
/// hash-based file change detection.
/// </summary>
public class JournalConfigSyncResult
{
    /// <summary>
    /// Files present in the tracking index but absent from .journalrc.
    /// These should be added to the journal configuration.
    /// </summary>
    public IReadOnlyList<string> FilesToAdd { get; init; } = [];

    /// <summary>
    /// Files registered in .journalrc but absent from the tracking index.
    /// These should be removed from the journal configuration.
    /// </summary>
    public IReadOnlyList<string> FilesToRemove { get; init; } = [];

    public bool HasChanges => FilesToAdd.Count > 0 || FilesToRemove.Count > 0;
}
