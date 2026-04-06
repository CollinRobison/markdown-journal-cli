namespace markdown_journal_cli.Infrastructure.Transactions.Models;

/// <summary>
/// Represents a single file operation tracked by an
/// <see cref="markdown_journal_cli.Infrastructure.Transactions.IFileTransactionScope"/>
/// that can be reversed during rollback.
/// </summary>
/// <param name="AbsolutePath">
/// Absolute path to the file at the time of tracking.
/// For <see cref="RollbackEntryKind.Rename"/> operations this is the original (pre-rename) path.
/// </param>
/// <param name="Kind">The kind of operation that was tracked.</param>
/// <param name="NewPath">
/// For <see cref="RollbackEntryKind.Rename"/> only: the path the file was renamed <em>to</em>.
/// <see langword="null"/> for all other kinds.
/// </param>
public record RollbackEntry(string AbsolutePath, RollbackEntryKind Kind, string? NewPath = null);
