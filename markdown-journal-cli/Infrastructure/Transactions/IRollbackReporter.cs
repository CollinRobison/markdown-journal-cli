using markdown_journal_cli.Infrastructure.Transactions.Models;

namespace markdown_journal_cli.Infrastructure.Transactions;

/// <summary>
/// Renders rollback progress and results to the terminal using Spectre.Console markup.
/// Implementations are expected to be stateless and safe to call from any service.
/// </summary>
public interface IRollbackReporter
{
    /// <summary>
    /// Called when a write operation fails and a rollback is about to begin.
    /// Renders the original error message and a "Rolling back changes..." notice.
    /// </summary>
    /// <param name="operationDescription">Short description of the operation that failed (e.g. "update entry").</param>
    /// <param name="cause">The exception that triggered the rollback.</param>
    void ReportRollbackStarting(string operationDescription, Exception cause);

    /// <summary>
    /// Called after rollback completes. Renders a summary table of restored and failed entries.
    /// </summary>
    /// <param name="result">The result returned by <see cref="IFileTransactionScope.Rollback"/>.</param>
    /// <param name="journalRoot">Absolute path to the journal root, used to compute relative display paths.</param>
    void ReportRollbackComplete(RollbackResult result, string journalRoot);
}
