using markdown_journal_cli.Infrastructure.Configuration.Models;

namespace markdown_journal_cli.Infrastructure.Configuration;

/// <summary>
/// Reads and writes the <see cref="JournalTocStructure"/> stored in
/// <c>.journaltoc</c> inside the journal's metadata directory.
/// </summary>
public interface IJournalTocStructureRepository
{
    /// <summary>
    /// Loads the TOC structure from <c>.journaltoc</c> inside <paramref name="metadataDir"/>.
    /// Returns <see cref="JournalTocStructure.Empty()"/> when the file is absent.
    /// </summary>
    /// <param name="metadataDir">Absolute path to the <c>.mdjournal/</c> metadata directory.</param>
    JournalTocStructure Load(string metadataDir);

    /// <summary>
    /// Saves <paramref name="structure"/> as <c>.journaltoc</c> inside <paramref name="metadataDir"/>.
    /// </summary>
    /// <param name="structure">The TOC structure to persist.</param>
    /// <param name="metadataDir">Absolute path to the <c>.mdjournal/</c> metadata directory.</param>
    void Save(JournalTocStructure structure, string metadataDir);
}
