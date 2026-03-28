using markdown_journal_cli.Infrastructure.Configuration.Models;

namespace markdown_journal_cli.Infrastructure.Tracking.Models;

/// <summary>
/// Aggregates all dry-run preview data for a single <c>update journal --dry-run</c> invocation.
/// Each section is null when that section was not requested (flag-scoping).
/// </summary>
public class UpdateDryRunReport
{
    public ChangeDetectionResult? TrackingChanges { get; init; }
    public JournalConfigSyncResult? ConfigChanges { get; init; }
    public TocDiffResult? TocPreview { get; init; }
    public TocRenameDryRunResult? RenamePreview { get; init; }

    public bool HasAnyChanges =>
        (TrackingChanges?.HasChanges ?? false)
        || (ConfigChanges?.HasChanges ?? false)
        || (TocPreview?.HasChanges ?? false)
        || (RenamePreview?.IsRename ?? false)
        || (RenamePreview?.FilesWithBacklinks.Count > 0);
}

/// <summary>
/// Holds the current and preview TOC content for line-level diffing at render time.
/// </summary>
public class TocDiffResult
{
    public string CurrentContent { get; init; } = string.Empty;
    public string PreviewContent { get; init; } = string.Empty;

    public bool HasChanges => CurrentContent != PreviewContent;
}

/// <summary>
/// Describes a pending <c>--rename-toc</c> operation without applying it.
/// </summary>
public class TocRenameDryRunResult
{
    public string CurrentName { get; init; } = string.Empty;
    public string NewName { get; init; } = string.Empty;
    public IReadOnlyList<string> FilesWithBacklinks { get; init; } = [];

    public bool IsRename =>
        !string.Equals(CurrentName, NewName, StringComparison.OrdinalIgnoreCase);
}
