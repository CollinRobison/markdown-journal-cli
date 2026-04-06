using markdown_journal_cli.Infrastructure.Transactions.Models;

namespace markdown_journal_cli.Infrastructure.Transactions;

internal sealed class JoinedTransactionScope(IFileTransactionScope root) : IFileTransactionScope
{
    private readonly IFileTransactionScope _root =
        root ?? throw new ArgumentNullException(nameof(root));
    private bool _sealed;
    private bool _committed;

    public void Track(string absolutePath) => _root.Track(absolutePath);

    public void TrackNew(string absolutePath) => _root.TrackNew(absolutePath);

    public void TrackRename(string oldPath, string newPath) => _root.TrackRename(oldPath, newPath);

    public void TrackDelete(string absolutePath) => _root.TrackDelete(absolutePath);

    public void TrackNewDirectory(string absolutePath) => _root.TrackNewDirectory(absolutePath);

    public void Commit()
    {
        _committed = true;
        _sealed = true;
    }

    public RollbackResult Rollback()
    {
        _sealed = true;
        return _root.Rollback();
    }

    /// <summary>
    /// Returns <c>true</c> if this joined scope committed. Note that committing a joined scope
    /// is a no-op to the root transaction — only <see cref="IFileTransactionScope.Commit"/> on
    /// the root actually commits. <see cref="IsRolledBack"/> delegates to the root, so the two
    /// properties reflect different scopes by design.
    /// </summary>
    public bool IsCommitted => _committed;

    public bool IsRolledBack => _root.IsRolledBack;

    public void Dispose()
    {
        if (!_sealed)
            Rollback();
    }
}
