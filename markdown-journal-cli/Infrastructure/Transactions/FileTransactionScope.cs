using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.Transactions.Models;
using Microsoft.Extensions.Logging;

namespace markdown_journal_cli.Infrastructure.Transactions;

internal sealed class FileTransactionScope(
    IFileSystem fileSystem,
    IInMemoryFileBuffer buffer,
    IDeletionRollbackStrategy deletionStrategy,
    ILogger<FileTransactionScope> logger,
    Action onDisposed
) : IFileTransactionScope
{
    private readonly IFileSystem _fileSystem =
        fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    private readonly IInMemoryFileBuffer _buffer =
        buffer ?? throw new ArgumentNullException(nameof(buffer));
    private readonly IDeletionRollbackStrategy _deletionStrategy =
        deletionStrategy ?? throw new ArgumentNullException(nameof(deletionStrategy));
    private readonly ILogger<FileTransactionScope> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly Action _onDisposed =
        onDisposed ?? throw new ArgumentNullException(nameof(onDisposed));

    private readonly List<RollbackEntry> _entries = [];
    private readonly Dictionary<string, string> _modifySnapshots = new(
        StringComparer.OrdinalIgnoreCase
    );
    private bool _committed;
    private bool _rolledBack;
    private bool _disposed;

    public bool IsCommitted => _committed;
    public bool IsRolledBack => _rolledBack;

    public void Track(string absolutePath)
    {
        EnsureMutable();
        _logger.LogTrace("Tracking modify path {AbsolutePath}", absolutePath);

        if (!_modifySnapshots.ContainsKey(absolutePath))
            _modifySnapshots[absolutePath] = _fileSystem.GetFileContent(absolutePath);

        _entries.Add(new RollbackEntry(absolutePath, RollbackEntryKind.Modify));
    }

    public void TrackNew(string absolutePath)
    {
        EnsureMutable();
        _logger.LogTrace("Tracking new file {AbsolutePath}", absolutePath);
        _entries.Add(new RollbackEntry(absolutePath, RollbackEntryKind.New));
    }

    public void TrackRename(string oldPath, string newPath)
    {
        EnsureMutable();
        _logger.LogTrace("Tracking rename {OldPath} -> {NewPath}", oldPath, newPath);
        _entries.Add(new RollbackEntry(oldPath, RollbackEntryKind.Rename, newPath));
    }

    public void TrackDelete(string absolutePath)
    {
        EnsureMutable();
        _logger.LogTrace("Tracking delete {AbsolutePath}", absolutePath);
        var content = _fileSystem.GetFileContent(absolutePath);
        _deletionStrategy.Capture(absolutePath, content);
        _entries.Add(new RollbackEntry(absolutePath, RollbackEntryKind.Delete));
    }

    public void TrackNewDirectory(string absolutePath)
    {
        EnsureMutable();
        _logger.LogTrace("Tracking new directory {AbsolutePath}", absolutePath);
        _entries.Add(new RollbackEntry(absolutePath, RollbackEntryKind.NewDirectory));
    }

    public void Commit()
    {
        EnsureMutable();
        _committed = true;

        foreach (var entry in _entries.Where(e => e.Kind == RollbackEntryKind.Delete))
            _deletionStrategy.Release(entry.AbsolutePath);

        _logger.LogDebug("Transaction committed with {EntryCount} entries", _entries.Count);
        _entries.Clear();
        _modifySnapshots.Clear();
        _buffer.Clear();
        _onDisposed();
    }

    public RollbackResult Rollback()
    {
        if (_committed || _rolledBack)
        {
            _logger.LogDebug("Rollback called on already finalized transaction scope; no-op");
            return new RollbackResult([], []);
        }

        _rolledBack = true;
        var restored = new List<RollbackEntry>();
        var failed = new List<RollbackFailure>();

        _logger.LogWarning("Rollback started for {EntryCount} entries", _entries.Count);

        for (var i = _entries.Count - 1; i >= 0; i--)
        {
            var entry = _entries[i];
            try
            {
                switch (entry.Kind)
                {
                    case RollbackEntryKind.Modify:
                    {
                        var directory =
                            _fileSystem.GetDirectoryName(entry.AbsolutePath)
                            ?? throw new InvalidOperationException(
                                $"Cannot determine directory for '{entry.AbsolutePath}'."
                            );
                        var fileName =
                            _fileSystem.GetFileName(entry.AbsolutePath)
                            ?? throw new InvalidOperationException(
                                $"Cannot determine file name for '{entry.AbsolutePath}'."
                            );
                        _fileSystem.UpdateFile(
                            directory,
                            fileName,
                            _modifySnapshots[entry.AbsolutePath]
                        );
                        break;
                    }
                    case RollbackEntryKind.New:
                        if (_fileSystem.FileExists(entry.AbsolutePath))
                            _fileSystem.DeleteFile(entry.AbsolutePath);
                        break;
                    case RollbackEntryKind.Rename:
                        if (entry.NewPath is not null && _fileSystem.FileExists(entry.NewPath))
                            _fileSystem.RenameFile(entry.NewPath, entry.AbsolutePath);
                        break;
                    case RollbackEntryKind.Delete:
                        _deletionStrategy.Restore(_fileSystem, entry.AbsolutePath);
                        break;
                    case RollbackEntryKind.NewDirectory:
                        if (_fileSystem.DirectoryExists(entry.AbsolutePath))
                            _fileSystem.DeleteDirectory(entry.AbsolutePath);
                        break;
                }

                restored.Add(entry);
                _logger.LogInformation(
                    "Rolled back {Kind} for {AbsolutePath}",
                    entry.Kind,
                    entry.AbsolutePath
                );
            }
            catch (Exception ex)
            {
                failed.Add(new RollbackFailure(entry, ex));
                _logger.LogError(
                    ex,
                    "Rollback failed for {Kind} at {AbsolutePath}",
                    entry.Kind,
                    entry.AbsolutePath
                );
            }
        }

        _entries.Clear();
        _modifySnapshots.Clear();
        _buffer.Clear();
        _onDisposed();

        _logger.LogWarning(
            "Rollback complete. Restored={RestoredCount}, Failed={FailedCount}",
            restored.Count,
            failed.Count
        );

        return new RollbackResult(restored, failed);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (!_committed && !_rolledBack)
            Rollback();
    }

    private void EnsureMutable()
    {
        if (_committed)
            throw new InvalidOperationException("Transaction has already been committed.");
        if (_rolledBack)
            throw new InvalidOperationException("Transaction has already been rolled back.");
    }
}
