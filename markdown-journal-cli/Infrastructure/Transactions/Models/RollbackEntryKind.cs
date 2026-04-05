namespace markdown_journal_cli.Infrastructure.Transactions.Models;

/// <summary>
/// Identifies the kind of file operation that was tracked and can be reversed by a
/// <see cref="markdown_journal_cli.Infrastructure.Transactions.IFileTransactionScope"/>.
/// </summary>
public enum RollbackEntryKind
{
    /// <summary>An existing file was modified. Rollback restores the original content.</summary>
    Modify,

    /// <summary>A new file was created. Rollback deletes it.</summary>
    New,

    /// <summary>
    /// A file was renamed from <c>AbsolutePath</c> to <c>NewPath</c>.
    /// Rollback renames it back to the original path.
    /// </summary>
    Rename,

    /// <summary>A file was deleted. Rollback re-creates it from the snapshotted content.</summary>
    Delete,

    /// <summary>
    /// A directory was created. Rollback deletes it after all file entries inside are reversed.
    /// </summary>
    NewDirectory,
}
