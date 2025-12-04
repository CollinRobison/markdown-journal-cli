using System;
using System.Text.Json.Serialization;

namespace markdown_journal_cli.Infrastructure.Configuration.Objects;

public class JournalConfig
{
    [JsonPropertyName("journalName")]
    public string JournalName { get; set; } = "MyJournal";

    [JsonPropertyName("tableOfContents")]
    public required TableOfContents TableOfContents { get; set; }
}
