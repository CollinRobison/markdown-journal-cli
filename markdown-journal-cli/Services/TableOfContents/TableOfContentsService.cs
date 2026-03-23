using System.Text;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.Configuration.Models;
using markdown_journal_cli.Infrastructure.FileSystem;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace markdown_journal_cli.Services;

/// <summary>
/// Generates and updates the table of contents based on journal configuration.
/// </summary>
public class TableOfContentsService(
    IFileSystem fileSystem,
    IJournalConfiguration journalConfiguration,
    IOptions<JournalSettings> journalSettings,
    ILogger<TableOfContentsService> logger
) : ITableOfContentsService
{
    private readonly IFileSystem _fileSystem =
        fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    private readonly IJournalConfiguration _journalConfiguration =
        journalConfiguration ?? throw new ArgumentNullException(nameof(journalConfiguration));
    private readonly ILogger<TableOfContentsService> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));
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

        _logger.LogDebug(
            "Updating table of contents for journal at '{JournalDirectory}'",
            journalDirectory
        );

        var config =
            _journalConfiguration.Read(journalDirectory)
            ?? throw new InvalidOperationException(
                $"Could not read journal configuration from {journalDirectory}"
            );

        var tocFile = config.TableOfContents.File;
        var tocFilePath = _fileSystem.CombinePaths(journalDirectory, tocFile);
        if (_fileSystem.FileExists(tocFilePath))
        {
            var existingContent = _fileSystem.GetFileContent(tocFilePath);
            var (existingCreated, existingEdited) = MarkdownMetadataParser.ParseDates(
                existingContent
            );

            // Use existing dates if new ones aren't provided
            if (createdDate == null && existingCreated != null)
            {
                _logger.LogDebug("Preserving existing created date from TOC");
                createdDate = existingCreated;
            }
            lastEditedDate ??= existingEdited;
        }

        var tocContent = GenerateTableOfContents(config, createdDate, lastEditedDate);

        _fileSystem.UpdateFile(journalDirectory, tocFile, tocContent);
        _logger.LogDebug("Table of contents updated at '{TocFilePath}'", tocFilePath);
    }

    /// <inheritdoc />
    public string PreviewTableOfContents(string journalDirectory)
    {
        if (string.IsNullOrWhiteSpace(journalDirectory))
        {
            throw new ArgumentException(
                "Journal directory cannot be null or whitespace.",
                nameof(journalDirectory)
            );
        }

        var config =
            _journalConfiguration.Read(journalDirectory)
            ?? throw new InvalidOperationException(
                $"Could not read journal configuration from {journalDirectory}"
            );

        var tocFile = config.TableOfContents.File;
        var tocFilePath = _fileSystem.CombinePaths(journalDirectory, tocFile);

        DateTime? createdDate = null;
        DateTime? lastEditedDate = null;

        if (_fileSystem.FileExists(tocFilePath))
        {
            var existingContent = _fileSystem.GetFileContent(tocFilePath);
            var (existingCreated, existingEdited) = MarkdownMetadataParser.ParseDates(
                existingContent
            );
            createdDate = existingCreated;
            lastEditedDate = existingEdited;
        }

        _logger.LogDebug(
            "Previewing table of contents for journal at '{JournalDirectory}' (no writes)",
            journalDirectory
        );

        return GenerateTableOfContents(config, createdDate, lastEditedDate);
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
            sb.AppendLine($"Created: {createdDate.Value:MM/dd/yyyy}");
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

        // Get ignore files list and add the TOC file itself to prevent it from appearing in its own contents
        var tocFile = config.TableOfContents.File;
        var ignoreFiles = config.TableOfContents.IgnoreFiles ?? Array.Empty<string>();
        var ignoreFilesWithToc = ignoreFiles.Append(tocFile).ToArray();

        // Add root entries (filter out ignored files)
        if (
            config.TableOfContents.RootEntries != null
            && config.TableOfContents.RootEntries.Length > 0
        )
        {
            foreach (var entry in config.TableOfContents.RootEntries)
            {
                if (!IsFileIgnored(entry.File, ignoreFilesWithToc))
                {
                    sb.AppendLine($"- [{entry.Name}]({entry.File})");
                }
            }
        }

        // Add topics
        if (
            config.TableOfContents.Structure?.Topics != null
            && config.TableOfContents.Structure.Topics.Length > 0
        )
        {
            foreach (var topic in config.TableOfContents.Structure.Topics)
            {
                GenerateTopicSection(sb, topic, 0, ignoreFilesWithToc); // Start with no indentation
            }
        }

        return sb.ToString();
    }

    private void GenerateTopicSection(
        StringBuilder sb,
        Topic topic,
        int indentLevel,
        string[] ignoreFiles
    )
    {
        // Filter out ignored entries first
        var visibleEntries =
            topic.Entries?.Where(e => !IsFileIgnored(e.File, ignoreFiles)).ToArray()
            ?? Array.Empty<Entries>();

        // Get all subtopics (don't pre-filter them - let each subtopic decide whether to render itself)
        var subtopics = topic.Subtopics ?? Array.Empty<Topic>();

        // For top-level topics, skip entirely if no visible entries and no subtopics
        // (This filters out topics where the only entry was the TOC file)
        // But for nested subtopics, always render them to show the hierarchy
        if (indentLevel == 0 && visibleEntries.Length == 0 && subtopics.Length == 0)
        {
            return;
        }

        // Top-level topics (indentLevel == 0) get headings
        // Subtopics (indentLevel > 0) are rendered as indented list items

        if (indentLevel == 0)
        {
            // Top-level topic: use heading with optional title-casing
            var displayName = _journalSettings.CapitalizeTopicHeadings
                ? ToTitleCase(topic.Name)
                : topic.Name;

            // Edge case: if topic has exactly one visible entry and the entry name matches the topic name,
            // make the topic heading a link
            if (
                visibleEntries.Length == 1
                && string.Equals(
                    topic.Name,
                    visibleEntries[0].Name,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                sb.AppendLine($"## [{displayName}]({visibleEntries[0].File})");
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

        // Identify parent-child relationships between entries and subtopics
        var parentMatches = new Dictionary<Entries, Topic>();
        var processedSubtopics = new HashSet<Topic>();

        if (visibleEntries.Length > 0 && subtopics.Length > 0)
        {
            foreach (var entry in visibleEntries)
            {
                var entryPathWithoutExt = _fileSystem.GetFileNameWithoutExtension(entry.File);
                if (entryPathWithoutExt == null)
                    continue;

                foreach (var subtopic in subtopics)
                {
                    // Check if entry name matches subtopic name AND entry path is prefix of subtopic files
                    if (
                        string.Equals(entry.Name, subtopic.Name, StringComparison.OrdinalIgnoreCase)
                        && IsSubtopicChildOfEntry(entryPathWithoutExt, subtopic, ignoreFiles)
                    )
                    {
                        parentMatches[entry] = subtopic;
                        processedSubtopics.Add(subtopic);
                        break; // Each entry can only match one subtopic
                    }
                }
            }
        }

        // Check if this is the edge case: top-level topic with single matching entry
        var isTopLevelMatchingEntry =
            indentLevel == 0
            && visibleEntries.Length == 1
            && string.Equals(
                topic.Name,
                visibleEntries[0].Name,
                StringComparison.OrdinalIgnoreCase
            );

        var entryIndent = new string(' ', (indentLevel + 1) * 2);

        // Render entries (skip if edge case since it's already in the heading)
        if (!isTopLevelMatchingEntry)
        {
            foreach (var entry in visibleEntries)
            {
                if (parentMatches.TryGetValue(entry, out var childSubtopic))
                {
                    // This entry has matching child subtopic - render as parent with children
                    sb.AppendLine($"{entryIndent}- [{entry.Name}]({entry.File})");
                    // Render the subtopic's content nested under this entry
                    RenderSubtopicContent(sb, childSubtopic, indentLevel + 2, ignoreFiles);
                }
                else
                {
                    // Regular entry without children
                    sb.AppendLine($"{entryIndent}- [{entry.Name}]({entry.File})");
                }
            }
        }

        // Always render subtopics that weren't merged with parent entries
        if (subtopics.Length > 0)
        {
            foreach (var subtopic in subtopics)
            {
                if (!processedSubtopics.Contains(subtopic))
                {
                    GenerateTopicSection(sb, subtopic, indentLevel + 1, ignoreFiles);
                }
            }
        }
    }

    /// <summary>
    /// Renders the content of a subtopic (entries and nested subtopics) without rendering the subtopic heading itself.
    /// Used when a subtopic is merged with a parent entry.
    /// </summary>
    private void RenderSubtopicContent(
        StringBuilder sb,
        Topic subtopic,
        int indentLevel,
        string[] ignoreFiles
    )
    {
        // Filter out ignored entries
        var visibleEntries =
            subtopic.Entries?.Where(e => !IsFileIgnored(e.File, ignoreFiles)).ToArray()
            ?? Array.Empty<Entries>();

        // Identify parent-child relationships at this level too
        var parentMatches = new Dictionary<Entries, Topic>();
        var processedSubtopics = new HashSet<Topic>();

        if (
            visibleEntries.Length > 0
            && subtopic.Subtopics != null
            && subtopic.Subtopics.Length > 0
        )
        {
            foreach (var entry in visibleEntries)
            {
                var entryPathWithoutExt = _fileSystem.GetFileNameWithoutExtension(entry.File);
                if (entryPathWithoutExt == null)
                    continue;

                foreach (var nestedSubtopic in subtopic.Subtopics)
                {
                    if (
                        string.Equals(
                            entry.Name,
                            nestedSubtopic.Name,
                            StringComparison.OrdinalIgnoreCase
                        )
                        && IsSubtopicChildOfEntry(entryPathWithoutExt, nestedSubtopic, ignoreFiles)
                    )
                    {
                        parentMatches[entry] = nestedSubtopic;
                        processedSubtopics.Add(nestedSubtopic);
                        break;
                    }
                }
            }
        }

        var entryIndent = new string(' ', indentLevel * 2);

        // Render entries
        foreach (var entry in visibleEntries)
        {
            if (parentMatches.TryGetValue(entry, out var childSubtopic))
            {
                // This entry has matching child subtopic
                sb.AppendLine($"{entryIndent}- [{entry.Name}]({entry.File})");
                RenderSubtopicContent(sb, childSubtopic, indentLevel + 1, ignoreFiles);
            }
            else
            {
                // Regular entry
                sb.AppendLine($"{entryIndent}- [{entry.Name}]({entry.File})");
            }
        }

        // Render unmatched subtopics
        if (subtopic.Subtopics != null)
        {
            foreach (var nestedSubtopic in subtopic.Subtopics)
            {
                if (!processedSubtopics.Contains(nestedSubtopic))
                {
                    GenerateTopicSection(sb, nestedSubtopic, indentLevel, ignoreFiles);
                }
            }
        }
    }

    /// <summary>
    /// Checks if a topic has any visible content (non-ignored entries or subtopics with visible content).
    /// </summary>
    private static bool HasVisibleContent(Topic topic, string[] ignoreFiles)
    {
        // Check if topic has any visible entries
        if (topic.Entries != null && topic.Entries.Length > 0)
        {
            if (topic.Entries.Where(entry => !IsFileIgnored(entry.File, ignoreFiles)).Any())
            {
                return true;
            }
        }

        // Check if topic has any subtopics with visible content (recursive)
        if (topic.Subtopics != null && topic.Subtopics.Length > 0)
        {
            if (topic.Subtopics.Where(subtopic => HasVisibleContent(subtopic, ignoreFiles)).Any())
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if a subtopic is a child of an entry based on file path prefix matching.
    /// </summary>
    private bool IsSubtopicChildOfEntry(
        string entryPathWithoutExt,
        Topic subtopic,
        string[] ignoreFiles
    )
    {
        // Check if all visible files in the subtopic (and nested subtopics) start with the entry path
        return HasFilesWithPrefix(subtopic, entryPathWithoutExt, ignoreFiles, _fileSystem);
    }

    /// <summary>
    /// Recursively checks if a topic has any visible files that start with the given prefix.
    /// </summary>
    private static bool HasFilesWithPrefix(Topic topic, string prefix, string[] ignoreFiles, IFileSystem fileSystem)
    {
        // Check entries
        if (topic.Entries != null)
        {
            foreach (var entry in topic.Entries)
            {
                if (!IsFileIgnored(entry.File, ignoreFiles))
                {
                    var filePath = fileSystem.GetFileNameWithoutExtension(entry.File);
                    // Check if file path starts with prefix followed by separator
                    if (filePath != null && filePath.StartsWith(prefix + "-", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
        }

        // Check subtopics recursively
        if (topic.Subtopics != null)
        {
            foreach (var subtopic in topic.Subtopics)
            {
                if (HasFilesWithPrefix(subtopic, prefix, ignoreFiles, fileSystem))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if a file is in the ignore list.
    /// </summary>
    private static bool IsFileIgnored(string file, string[] ignoreFiles)
    {
        return ignoreFiles.Any(ignored =>
            string.Equals(file, ignored, StringComparison.OrdinalIgnoreCase)
        );
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
