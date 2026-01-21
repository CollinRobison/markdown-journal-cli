using System.Text;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.Configuration.Models;
using markdown_journal_cli.Infrastructure.FileSystem;
using Microsoft.Extensions.Options;

namespace markdown_journal_cli.JournalTemplates;

/// <summary>
/// Generates and updates the table of contents based on journal configuration.
/// </summary>
public class TableOfContentsGenerator(
    IFileSystem fileSystem,
    IJournalConfiguration journalConfiguration,
    IOptions<JournalSettings> journalSettings
    ) : ITableOfContentsGenerator
{
    private readonly IFileSystem _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    private readonly IJournalConfiguration _journalConfiguration =
            journalConfiguration ?? throw new ArgumentNullException(nameof(journalConfiguration));
    private readonly JournalSettings _journalSettings = journalSettings.Value;

    /// <inheritdoc />
    public void UpdateTableOfContents(
        string journalDirectory,
        DateTime? createdDate = null,
        DateTime? lastEditedDate = null
    )
    {
        if (string.IsNullOrWhiteSpace(journalDirectory))
        {
            throw new ArgumentException(
                "Journal directory cannot be null or whitespace.",
                nameof(journalDirectory)
            );
        }

        var config = _journalConfiguration.Read(journalDirectory) ?? throw new InvalidOperationException(
                $"Could not read journal configuration from {journalDirectory}"
            );

        // If dates aren't provided, try to preserve existing dates from the current TOC
        var tocFilePath = Path.Combine(journalDirectory, $"{_journalSettings.TableOfContentsFileName}.md");
        if (_fileSystem.FileExists(tocFilePath))
        {
            var existingContent = _fileSystem.GetFileContent(tocFilePath);
            var (existingCreated, existingEdited) = MarkdownMetadataParser.ParseDates(existingContent);
            
            // Use existing dates if new ones aren't provided
            createdDate ??= existingCreated;
            lastEditedDate ??= existingEdited;
        }

        var tocContent = GenerateTableOfContents(config, createdDate, lastEditedDate);

        _fileSystem.UpdateFile(journalDirectory, $"{_journalSettings.TableOfContentsFileName}.md", tocContent);
    }

    private string GenerateTableOfContents(
        JournalConfig config,
        DateTime? createdDate,
        DateTime? lastEditedDate
    )
    {
        var sb = new StringBuilder();

        // Add dates if provided
        if (createdDate.HasValue)
        {
            sb.AppendLine($"Created: {createdDate.Value:M/d/yyyy}");
        }

        if (lastEditedDate.HasValue)
        {
            sb.AppendLine($"Last Edited: {lastEditedDate.Value:MM/dd/yyyy}");
        }

        // Add blank line after dates if any were added
        if (createdDate.HasValue || lastEditedDate.HasValue)
        {
            sb.AppendLine();
        }

        // Add title
        sb.AppendLine($"# {_journalSettings.TableOfContentsTitle}");

        // Add root entries
        if (config.TableOfContents.RootEntries != null && config.TableOfContents.RootEntries.Length > 0)
        {
            foreach (var entry in config.TableOfContents.RootEntries)
            {
                sb.AppendLine($"- [{entry.Name}]({entry.File})");
            }
        }

        // Add topics
        if (config.TableOfContents.Structure?.Topics != null && config.TableOfContents.Structure.Topics.Length > 0)
        {
            foreach (var topic in config.TableOfContents.Structure.Topics)
            {
                GenerateTopicSection(sb, topic, 0); // Start with no indentation
            }
        }

        return sb.ToString();
    }

    private void GenerateTopicSection(StringBuilder sb, Topic topic, int indentLevel)
    {
        // Top-level topics (indentLevel == 0) get headings
        // Subtopics (indentLevel > 0) are rendered as indented list items
        
        if (indentLevel == 0)
        {
            // Top-level topic: use heading with optional title-casing
            var displayName = _journalSettings.CapitalizeTopicHeadings 
                ? ToTitleCase(topic.Name) 
                : topic.Name;
            
            // Edge case: if topic has exactly one entry and the entry name matches the topic name,
            // make the topic heading a link
            if (topic.Entries != null && topic.Entries.Length == 1 && 
                string.Equals(topic.Name, topic.Entries[0].Name, StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine($"## [{displayName}]({topic.Entries[0].File})");
            }
            else
            {
                // Normal case: plain heading with entries and subtopics listed below
                sb.AppendLine($"## {displayName}");
            }
        }
        else
        {
            // Subtopic: render as indented list item with optional title-casing
            var indent = new string(' ', indentLevel * 2);
            var displayName = _journalSettings.CapitalizeTopicHeadings 
                ? ToTitleCase(topic.Name) 
                : topic.Name;
            sb.AppendLine($"{indent}- {displayName}");
        }

        // Add entries (only if not the edge case with single matching entry at top level)
        if (!(indentLevel == 0 && topic.Entries != null && topic.Entries.Length == 1 && 
              string.Equals(topic.Name, topic.Entries[0].Name, StringComparison.OrdinalIgnoreCase)))
        {
            if (topic.Entries != null && topic.Entries.Length > 0)
            {
                // Entries are indented: 2 more spaces than the current level for subtopics, or 2 spaces for top-level
                var entryIndent = new string(' ', (indentLevel + 1) * 2);
                foreach (var entry in topic.Entries)
                {
                    sb.AppendLine($"{entryIndent}- [{entry.Name}]({entry.File})");
                }
            }
        }

        // Add subtopics recursively with increased indentation
        if (topic.Subtopics != null && topic.Subtopics.Length > 0)
        {
            foreach (var subtopic in topic.Subtopics)
            {
                GenerateTopicSection(sb, subtopic, indentLevel + 1);
            }
        }
    }

    private static string ToTitleCase(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        var words = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 0)
            {
                // Only capitalize first letter, preserve the rest as-is
                words[i] = char.ToUpper(words[i][0]) + words[i][1..];
            }
        }

        return string.Join(' ', words);
    }
}
