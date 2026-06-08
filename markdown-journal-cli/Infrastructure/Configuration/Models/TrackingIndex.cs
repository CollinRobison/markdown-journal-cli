using System.Text.Json.Serialization;

namespace markdown_journal_cli.Infrastructure.Configuration.Models;

public class TrackingIndex
{
    /// <summary>
    /// This tells the journal what files or directories shouldn't be tracked by the index 
    /// </summary>
    [JsonPropertyName("noTrack")]
    public string[]? NoTrack {get; set;}
}
