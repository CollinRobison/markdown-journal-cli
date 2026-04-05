using markdown_journal_cli.Infrastructure.Transactions.Models;

namespace markdown_journal_cli.Infrastructure.Transactions;

/// <summary>
/// Thrown by command-level rollback handlers after a failed write operation has been fully reversed.
/// Wraps the original exception as the <see cref="Exception.InnerException"/> and surfaces the
/// <see cref="RollbackResult"/> so callers can decide how to report partial rollback failures.
/// </summary>
public sealed class RollbackCompletedException(RollbackResult result, Exception originalCause) : Exception(originalCause.Message, originalCause)
{
    /// <summary>The outcome of the rollback that was performed before throwing.</summary>
    public RollbackResult Result { get; } = result;
}
