using System.Diagnostics;
using System.Runtime.ExceptionServices;

namespace markdown_journal_cli.Infrastructure.Transactions;

/// <summary>
/// Extension methods for <see cref="IRollbackReporter"/> that combine rollback execution
/// with reporting into a single call site.
/// </summary>
public static class RollbackReporterExtensions
{
    /// <summary>
    /// Rolls back the transaction, reports the result, and returns a
    /// <see cref="RollbackCompletedException"/> ready to be thrown by the caller — or
    /// re-throws the original exception (preserving stack trace) when
    /// <paramref name="coordinator"/> is a no-op (test / dry-run) context.
    /// </summary>
    /// <remarks>
    /// Usage: <c>throw _rollbackReporter.RollbackAndBuildException(tx, _txCoordinator, "desc", path, ex);</c>
    /// The leading <c>throw</c> lets the compiler see all code paths as non-returning,
    /// eliminating CS0161 "not all code paths return a value" errors.
    /// </remarks>
    /// <param name="reporter">The rollback reporter.</param>
    /// <param name="tx">The active transaction scope to roll back.</param>
    /// <param name="coordinator">
    /// Used to detect no-op contexts. When the coordinator is a
    /// <see cref="NoOpFileTransactionCoordinator"/>, the original exception is re-thrown
    /// with its stack trace preserved and no reporting is performed.
    /// </param>
    /// <param name="operationDescription">
    /// Short human-readable description of the failing operation (e.g. "add journal configuration").
    /// </param>
    /// <param name="journalRoot">Absolute path to the journal root for relative path display.</param>
    /// <param name="ex">The exception that triggered the rollback.</param>
    /// <returns>
    /// A <see cref="RollbackCompletedException"/> to be thrown by the caller.
    /// Never returns normally — always throws or returns a throwable exception.
    /// </returns>
    public static RollbackCompletedException RollbackAndBuildException(
        this IRollbackReporter reporter,
        IFileTransactionScope tx,
        IFileTransactionCoordinator coordinator,
        string operationDescription,
        string journalRoot,
        Exception ex
    )
    {
        if (coordinator is NoOpFileTransactionCoordinator)
        {
            ExceptionDispatchInfo.Capture(ex).Throw();
            throw new UnreachableException(); // satisfy compiler; never reached
        }

        reporter.ReportRollbackStarting(operationDescription, ex);
        var result = tx.Rollback();
        reporter.ReportRollbackComplete(result, journalRoot);
        return new RollbackCompletedException(result, ex);
    }
}
