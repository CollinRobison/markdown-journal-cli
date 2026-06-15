using System;
using markdown_journal_cli.Infrastructure.Tracking.Models;

namespace markdown_journal_cli.Services;

public interface IJournalUpdateService
{
    /// <summary>
    /// Updates the "Last Edited:" date for modified files, adds new files to the tracking index,
    /// and removes deleted files from the tracking index.
    /// </summary>
    public void UpdateLastEditedDatesAndTracking(
        string journalPath,
        ChangeDetectionResult fileResults,
        bool trackingOnly
    );

    /// <summary>
    /// Incrementally updates the .journalrc configuration using a pre-computed config sync result:
    /// adds new entries and removes deleted entries.
    /// </summary>
    public void UpdateJournalConfig(string journalPath, JournalRegistrationDriftResult syncResult);

    /// <summary>
    /// Regenerates the table of contents markdown file from the current journal configuration.
    /// </summary>
    public void UpdateTableOfContents(string journalPath);

    /// <summary>
    /// Renames the journal's table-of-contents file to <paramref name="newTocName"/>,
    /// updates the .journalrc configuration, and rewrites all markdown inline link references
    /// to the old TOC filename in other journal files, updating Last Edited dates and
    /// the tracking index for each modified file.
    /// If the TOC is already named correctly, only the link-reference check is performed.
    /// </summary>
    /// <param name="journalPath">The root path of the journal.</param>
    /// <param name="newTocName">The desired stem of the new TOC file (no extension).</param>
    /// <exception cref="markdown_journal_cli.Exceptions.TocRenameConflictException">
    /// Thrown when a file named <paramref name="newTocName"/>.md already exists and is not the current TOC.
    /// </exception>
    public void RenameToc(string journalPath, string newTocName);

    /// <summary>
    /// Builds a dry-run preview report without performing any writes.
    /// Sections are populated only for the flags requested (null means "not requested").
    /// </summary>
    /// <param name="journalPath">The root path of the journal.</param>
    /// <param name="trackingChanges">Pre-detected file tracking changes, or null to skip.</param>
    /// <param name="configChanges">Pre-detected registration drift, or null to skip.</param>
    /// <param name="includeToc">When true, generates a TOC preview and diff.</param>
    /// <param name="renameTocTarget">When non-null, lists files whose backlinks would be updated.</param>
    /// <returns>A <see cref="UpdateDryRunReport"/> describing all pending changes.</returns>
    public UpdateDryRunReport BuildDryRunReport(
        string journalPath,
        ChangeDetectionResult? trackingChanges,
        JournalRegistrationDriftResult? configChanges,
        bool includeToc,
        string? renameTocTarget
    );
}
