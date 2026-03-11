using System;
using markdown_journal_cli.Infrastructure.Tracking.Models;

namespace markdown_journal_cli.Services;

public interface IJournalUpdateService
{
    /// <summary>
    /// Updates the "Last Edited:" date for modified files, adds new files to the tracking index,
    /// and removes deleted files from the tracking index.
    /// </summary>
    public void UpdateLastEditedDatesAndTracking( string journalPath,
        ChangeDetectionResult fileResults,
        bool trackingOnly
    ); 

    /// <summary>
    /// Incrementally updates the .journalrc configuration: adds new entries, removes deleted entries.
    /// </summary>
    public void UpdateJournalConfig(string journalPath, ChangeDetectionResult fileResults);

    /// <summary>
    /// Regenerates the table of contents markdown file from the current journal configuration.
    /// </summary>
    public void UpdateTableOfContents(string journalPath);

}
