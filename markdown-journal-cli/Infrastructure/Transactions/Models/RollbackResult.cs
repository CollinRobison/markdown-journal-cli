namespace markdown_journal_cli.Infrastructure.Transactions.Models;

/// <summary>
/// The outcome of a rollback operation, describing which entries were successfully
/// restored and which could not be reversed.
/// </summary>
/// <param name="Restored">Entries that were successfully rolled back.</param>
/// <param name="Failed">Entries that could not be rolled back, paired with the exception that caused the failure.</param>
public record RollbackResult(
    IReadOnlyList<RollbackEntry> Restored,
    IReadOnlyList<RollbackFailure> Failed
)
{
    /// <summary>
    /// <see langword="true"/> when every tracked entry was successfully restored;
    /// <see langword="false"/> when one or more entries could not be reversed.
    /// </summary>
    public bool IsFullyRestored => Failed.Count == 0;
}
