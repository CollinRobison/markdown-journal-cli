using markdown_journal_cli.Infrastructure.FileSystem;

namespace markdown_journal_cli.Infrastructure.Transactions;

/// <summary>
/// Strategy that captures and restores the content of files that will be deleted during a transaction.
/// The default implementation (<see cref="InMemoryDeletionRollbackStrategy"/>) holds content in memory;
/// alternative implementations could use a temporary directory for very large files.
/// </summary>
public interface IDeletionRollbackStrategy
{
    /// <summary>
    /// Captures the content of a file before it is deleted.
    /// Call this <em>before</em> the deletion.
    /// </summary>
    /// <param name="absolutePath">Absolute path to the file that will be deleted.</param>
    /// <param name="content">Current content of the file.</param>
    void Capture(string absolutePath, string content);

    /// <summary>
    /// Restores a previously captured file by re-creating it on disk.
    /// Called during rollback.
    /// </summary>
    /// <param name="fileSystem">File system abstraction used to create the file.</param>
    /// <param name="absolutePath">Absolute path where the file should be re-created.</param>
    void Restore(IFileSystem fileSystem, string absolutePath);

    /// <summary>
    /// Releases the captured snapshot for a path, freeing any associated resources.
    /// Called on <see cref="IFileTransactionScope.Commit"/>.
    /// </summary>
    /// <param name="absolutePath">Absolute path whose snapshot should be discarded.</param>
    void Release(string absolutePath);
}
