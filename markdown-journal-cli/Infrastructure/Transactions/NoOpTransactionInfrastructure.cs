using markdown_journal_cli.Infrastructure.Transactions.Models;

namespace markdown_journal_cli.Infrastructure.Transactions;

/// <summary>
/// No-op implementation of <see cref="IFileTransactionCoordinator"/> for tests and dry-run
/// contexts that require no real transaction tracking. All operations are silent no-ops.
/// Use the <see cref="Instance"/> singleton to avoid unnecessary allocations.
/// </summary>
public sealed class NoOpFileTransactionCoordinator : IFileTransactionCoordinator
{
    public static IFileTransactionCoordinator Instance { get; } =
        new NoOpFileTransactionCoordinator();

    public IFileTransactionScope Begin() => new NoOpFileTransactionScope();

    public IFileTransactionScope BeginOrJoin() => new NoOpFileTransactionScope();

    public IFileTransactionScope? Current => null;
}

/// <summary>
/// No-op implementation of <see cref="IFileTransactionScope"/> returned by
/// <see cref="NoOpFileTransactionCoordinator"/>. All <c>Track*</c>, <see cref="IFileTransactionScope.Commit"/>,
/// and <see cref="IFileTransactionScope.Rollback"/> calls are silent no-ops.
/// </summary>
public sealed class NoOpFileTransactionScope : IFileTransactionScope
{
    public void Track(string absolutePath) { }

    public void TrackNew(string absolutePath) { }

    public void TrackRename(string oldPath, string newPath) { }

    public void TrackDelete(string absolutePath) { }

    public void TrackNewDirectory(string absolutePath) { }

    public void Commit() { }

    public RollbackResult Rollback() => new([], []);

    public bool IsCommitted => false;

    public bool IsRolledBack => false;

    public void Dispose() { }
}

/// <summary>
/// No-op implementation of <see cref="IRollbackReporter"/> for tests and dry-run contexts
/// that should not emit any rollback output. Use <see cref="Instance"/> to avoid allocations.
/// </summary>
public sealed class NoOpRollbackReporter : IRollbackReporter
{
    public static IRollbackReporter Instance { get; } = new NoOpRollbackReporter();

    public void ReportRollbackStarting(string operationDescription, Exception cause) { }

    public void ReportRollbackComplete(RollbackResult result, string journalRoot) { }
}
