using markdown_journal_cli.Infrastructure.Transactions.Models;

namespace markdown_journal_cli.Infrastructure.Transactions;

/// <summary>
/// Represents an active file transaction that tracks write operations and can reverse them on failure.
/// Obtain an instance via <see cref="IFileTransactionCoordinator.Begin"/> or
/// <see cref="IFileTransactionCoordinator.BeginOrJoin"/>.
/// Automatically rolls back on <see cref="IDisposable.Dispose"/> if <see cref="Commit"/> was not called.
/// </summary>
public interface IFileTransactionScope : IDisposable
{
    /// <summary>
    /// Snapshots an existing file that will be modified.
    /// Call this <em>before</em> performing the write.
    /// If the path was already tracked, this is a no-op — the first snapshot is kept (first-write-wins).
    /// </summary>
    /// <param name="absolutePath">Absolute path to the file.</param>
    void Track(string absolutePath);

    /// <summary>
    /// Records that a new file will be created at this path.
    /// Call this <em>before</em> creating the file.
    /// Rollback will delete the file if it exists at that path.
    /// </summary>
    /// <param name="absolutePath">Absolute path where the new file will be created.</param>
    void TrackNew(string absolutePath);

    /// <summary>
    /// Records a pending file rename.
    /// Call this <em>before</em> performing the rename.
    /// Rollback will rename the file at <paramref name="newPath"/> back to <paramref name="oldPath"/>.
    /// </summary>
    /// <param name="oldPath">The current (pre-rename) absolute path.</param>
    /// <param name="newPath">The destination (post-rename) absolute path.</param>
    void TrackRename(string oldPath, string newPath);

    /// <summary>
    /// Snapshots the content of a file that will be deleted.
    /// Call this <em>before</em> deleting the file.
    /// Rollback will re-create the file from the captured snapshot.
    /// </summary>
    /// <param name="absolutePath">Absolute path to the file that will be deleted.</param>
    void TrackDelete(string absolutePath);

    /// <summary>
    /// Records that a new directory will be created.
    /// Call this <em>before</em> creating the directory, and <em>before</em> any <see cref="TrackNew"/>
    /// calls for files inside it so that reverse-order rollback deletes the directory last.
    /// Rollback will call <c>DeleteDirectory</c> on the path.
    /// </summary>
    /// <param name="absolutePath">Absolute path of the directory that will be created.</param>
    void TrackNewDirectory(string absolutePath);

    /// <summary>
    /// Marks the transaction as successfully completed.
    /// Clears all tracked entries and snapshot state.
    /// After committing, <see cref="IDisposable.Dispose"/> is a no-op.
    /// </summary>
    void Commit();

    /// <summary>
    /// Reverses all tracked changes in reverse registration order.
    /// Returns a <see cref="RollbackResult"/> describing what was and was not restored.
    /// Idempotent — subsequent calls return an empty result.
    /// </summary>
    RollbackResult Rollback();

    /// <summary>Whether <see cref="Commit"/> has been called on this scope.</summary>
    bool IsCommitted { get; }

    /// <summary>Whether <see cref="Rollback"/> has already been executed on this scope.</summary>
    bool IsRolledBack { get; }
}
