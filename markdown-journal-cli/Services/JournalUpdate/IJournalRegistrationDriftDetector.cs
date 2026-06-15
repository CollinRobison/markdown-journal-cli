using markdown_journal_cli.Infrastructure.Configuration.Models;

namespace markdown_journal_cli.Services;

/// <summary>
/// Detects drift between the tracking index and the journal's registered file state.
/// </summary>
public interface IJournalRegistrationDriftDetector
{
    /// <summary>
    /// Compares tracked markdown files against the persisted TOC structure and ignore list.
    /// Returns files to add and remove so callers can synchronize journal registration state.
    /// </summary>
    /// <param name="journalPath">The root path of the journal.</param>
    /// <returns>A drift result describing files to add and remove.</returns>
    JournalRegistrationDriftResult DetectDrift(string journalPath);
}
