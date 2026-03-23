namespace markdown_journal_cli.Infrastructure.FileSystem;

/// <summary>
/// In-memory file staging and snapshot service.
/// Two use cases:
///   1. Stage generated content for preview/diff without disk I/O (dry-run).
///   2. Snapshot-before-write for transactional rollback (future wiring in JournalUpdateService).
/// </summary>
public interface IInMemoryFileBuffer
{
    /// <summary>Captures the current disk content of a file as a snapshot for potential rollback.</summary>
    void Snapshot(string absolutePath);

    /// <summary>Stores content in the staging area without writing to disk.</summary>
    void Stage(string absolutePath, string content);

    /// <summary>Returns staged content, or <c>null</c> if the path has not been staged.</summary>
    string? GetStaged(string absolutePath);

    /// <summary>Returns snapshot content, or <c>null</c> if the path has not been snapshotted.</summary>
    string? GetSnapshot(string absolutePath);

    /// <summary>Writes staged content to disk via <see cref="IFileSystem"/>.</summary>
    void Commit(string absolutePath);

    /// <summary>Restores the file on disk from the snapshot (rollback).</summary>
    void Restore(string absolutePath);

    bool HasStaged(string absolutePath);
    bool HasSnapshot(string absolutePath);

    /// <summary>Clears all staged and snapshot state.</summary>
    void Clear();
}
