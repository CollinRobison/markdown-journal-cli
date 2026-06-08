using System.Text.Json.Serialization;

namespace markdown_journal_cli.Infrastructure.Configuration.Models;

public class JournalConfig
{
    [JsonPropertyName("journalName")]
    public string JournalName { get; set; } = "MyJournal";

    /// <summary>
    /// Handles the configuration of the Journal's table of contents.
    /// </summary>
    [JsonPropertyName("tableOfContents")]
    public required TableOfContents TableOfContents { get; set; }

    /// <summary>
    /// Handles the configuration of the Journal's tracking index.
    /// </summary>
    [JsonPropertyName("trackingIndex")]
    public required TrackingIndex TrackingIndex { get; set; }
}
