using markdown_journal_cli.Infrastructure.Configuration.Models;
using markdown_journal_cli.Infrastructure.Tracking.Models;

namespace markdown_journal_cli.Commands.Update;

/// <summary>
/// Renders a dry-run report to the terminal without performing any file writes.
/// </summary>
public interface IDryRunRenderer
{
    /// <summary>
    /// Renders all sections of the dry-run report that contain changes.
    /// </summary>
    void Render(UpdateDryRunReport report, string journalPath);
}
