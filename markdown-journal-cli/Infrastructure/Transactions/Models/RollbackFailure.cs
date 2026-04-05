namespace markdown_journal_cli.Infrastructure.Transactions.Models;

/// <summary>
/// Associates a <see cref="RollbackEntry"/> with the exception that prevented it from being rolled back.
/// </summary>
/// <param name="Entry">The entry that could not be reversed.</param>
/// <param name="Error">The exception thrown during the rollback attempt.</param>
public record RollbackFailure(RollbackEntry Entry, Exception Error);
