using System.Diagnostics;
using markdown_journal_cli.Infrastructure.FileSystem;

namespace markdown_journal_cli.Infrastructure.Transactions;

/// <summary>
/// Holds deleted-file snapshots in a dictionary keyed by absolute path.
/// Suitable for all normal journal operations where file sizes are expected to be small.
/// The strategy is not thread-safe; it is intended to be owned by a single
/// <c>FileTransactionScope</c> and discarded after the transaction completes.
/// </summary>
public sealed class InMemoryDeletionRollbackStrategy : IDeletionRollbackStrategy
{
    private readonly Dictionary<string, string> _snapshots = new(StringComparer.OrdinalIgnoreCase);

    public void Capture(string absolutePath, string content)
    {
        Debug.Assert(
            !_snapshots.ContainsKey(absolutePath),
            $"DeletionRollbackStrategy already has a snapshot for '{absolutePath}'. This indicates overlapping transactions, which is not supported."
        );
        _snapshots[absolutePath] = content;
    }

    public void Restore(IFileSystem fileSystem, string absolutePath)
    {
        var content = _snapshots[absolutePath];
        var directory = Path.GetDirectoryName(absolutePath)
            ?? throw new InvalidOperationException($"Cannot determine directory for '{absolutePath}'.");
        var fileName = Path.GetFileName(absolutePath);
        fileSystem.CreateFile(directory, fileName, content);
    }

    public void Release(string absolutePath) => _snapshots.Remove(absolutePath);
}
