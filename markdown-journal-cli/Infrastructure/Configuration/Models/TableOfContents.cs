using System;
using System.Text.Json.Serialization;

namespace markdown_journal_cli.Infrastructure.Configuration.Models;

public class TableOfContents
{
    [JsonPropertyName("file")]
    public string File { get; set; } = $"1a-TableOfContents{FileConstants.MarkdownExtension}";

    [JsonPropertyName("extensions")]
    public string[] Extensions { get; set; } = [FileConstants.MarkdownExtension];

    [JsonPropertyName("ignoreFiles")]
    public string[]? IgnoreFiles { get; set; }

    [JsonPropertyName("structure")]
    public required Structure Structure { get; set; }

    [JsonPropertyName("rootEntries")]
    public required Entries[] RootEntries { get; set; }

}

public class Topic
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("Entries")]
    public required Entries[] Entries { get; set; }

    [JsonPropertyName("subtopics")]
    public Topic[]? Subtopics { get; set; }
}

public class Structure
{
    [JsonPropertyName("topics")]
    public required Topic[] Topics { get; set; }
}
public class Entries
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("file")]
    public required string File { get; set; }
}
