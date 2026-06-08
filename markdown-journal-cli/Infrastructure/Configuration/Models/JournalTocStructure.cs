using System.Text.Json.Serialization;

namespace markdown_journal_cli.Infrastructure.Configuration.Models;

/// <summary>
/// Represents the TOC structure stored in the .journaltoc file inside the .mdjournal/ metadata directory.
/// Contains the topic hierarchy and root entries that were previously embedded in .journalrc.
/// </summary>
public class JournalTocStructure
{
    [JsonPropertyName("structure")]
    public required Structure Structure { get; set; }

    [JsonPropertyName("rootEntries")]
    public required Entries[] RootEntries { get; set; }

    /// <summary>
    /// Creates an empty <see cref="JournalTocStructure"/> with safe defaults suitable for use
    /// when the .journaltoc file is absent.
    /// </summary>
    public static JournalTocStructure Empty() =>
        new()
        {
            Structure = new Structure { Topics = [] },
            RootEntries = [],
        };
}
