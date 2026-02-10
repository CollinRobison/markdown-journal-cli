using markdown_journal_cli.Infrastructure.Configuration.Models;

namespace markdown_journal_cli.Infrastructure.Configuration;

/// <summary>
/// Result of journal configuration generation operation.
/// </summary>
public class JournalConfigGenerationResult
{
    public required JournalConfig Config { get; init; }
    public required string Source { get; init; } // "toc", "tracking", or "directory"
    public int FileCount { get; init; }
}

/// <summary>
/// Generates journal configuration from various sources (TOC file, tracking index, or directory scan).
/// </summary>
public interface IJournalConfigGenerator
{
    /// <summary>
    /// Generates a journal configuration from a table of contents file if it exists.
    /// </summary>
    /// <param name="directory">The journal directory.</param>
    /// <param name="tocFileName">The table of contents filename (without extension).</param>
    /// <param name="journalName">Optional journal name. If not provided, uses directory name.</param>
    /// <returns>Configuration result if TOC file exists and is valid, null otherwise.</returns>
    JournalConfigGenerationResult? GenerateFromTableOfContents(string directory, string tocFileName, string? journalName = null);

    /// <summary>
    /// Generates a journal configuration from the file tracking index.
    /// </summary>
    /// <param name="directory">The journal directory.</param>
    /// <param name="tocFileName">The table of contents filename to exclude from entries.</param>
    /// <param name="journalName">Optional journal name. If not provided, uses directory name.</param>
    /// <returns>Configuration result if tracking file exists, null otherwise.</returns>
    JournalConfigGenerationResult? GenerateFromTrackingIndex(string directory, string tocFileName, string? journalName = null);

    /// <summary>
    /// Generates a journal configuration by scanning all markdown files in the directory.
    /// </summary>
    /// <param name="directory">The journal directory.</param>
    /// <param name="tocFileName">The table of contents filename to exclude from entries.</param>
    /// <param name="journalName">Optional journal name. If not provided, uses directory name.</param>
    /// <returns>Configuration result with all discovered markdown files.</returns>
    JournalConfigGenerationResult GenerateFromDirectory(string directory, string tocFileName, string? journalName = null);
}
