namespace markdown_journal_cli.Infrastructure.Transactions;

/// <summary>
/// Singleton factory that creates per-operation file transaction scopes and maintains the
/// ambient (thread-local) scope used by the <see cref="BeginOrJoin"/> pattern.
/// </summary>
public interface IFileTransactionCoordinator
{
    /// <summary>
    /// Creates a new root transaction scope and sets it as the ambient scope for this thread.
    /// Use this in command-level handlers and standalone services.
    /// Throws <see cref="InvalidOperationException"/> if a scope is already active on this thread.
    /// </summary>
    IFileTransactionScope Begin();

    /// <summary>
    /// If an active root scope exists on this thread, returns a <c>JoinedTransactionScope</c>
    /// that delegates all <c>Track*</c> and <see cref="IFileTransactionScope.Rollback"/> calls
    /// to it (with <see cref="IFileTransactionScope.Commit"/> being a no-op on the root).
    /// If no scope is active, behaves identically to <see cref="Begin"/>.
    /// Use this in services that may be called from within an outer command-level transaction.
    /// </summary>
    IFileTransactionScope BeginOrJoin();

    /// <summary>
    /// The currently active ambient scope for this thread, or <see langword="null"/> if none
    /// is in progress.
    /// </summary>
    IFileTransactionScope? Current { get; }
}
