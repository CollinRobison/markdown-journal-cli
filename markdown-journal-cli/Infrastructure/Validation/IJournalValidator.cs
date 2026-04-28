namespace markdown_journal_cli.Infrastructure.Validation;

/// <summary>
/// Validates that a journal directory contains the required metadata directory layout.
/// </summary>
public interface IJournalValidator
{
    /// <summary>
    /// Checks that the <c>.mdjournal/</c> metadata directory exists inside
    /// <paramref name="journalDir"/> and that both <c>.journalindex</c> and
    /// <c>.journaltoc</c> exist inside it.
    /// </summary>
    /// <param name="journalDir">Absolute path to the journal root directory.</param>
    /// <returns>
    /// A read-only list of the names of missing files/directories.
    /// An empty list means the layout is valid.
    /// </returns>
    IReadOnlyList<string> ValidateMetadataDirectory(string journalDir);
}
