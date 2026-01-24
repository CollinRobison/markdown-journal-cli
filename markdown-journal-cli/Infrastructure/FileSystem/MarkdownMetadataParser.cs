namespace markdown_journal_cli.Infrastructure.FileSystem;

/// <summary>
/// Provides utilities for parsing metadata from markdown files.
/// </summary>
public static class MarkdownMetadataParser
{
    /// <summary>
    /// Parses "Created:" and "Last Edited:" dates from markdown content.
    /// Looks for dates in the first few lines before any heading.
    /// </summary>
    /// <param name="content">The markdown file content to parse.</param>
    /// <returns>A tuple containing the created date and last edited date, if found.</returns>
    public static (DateTime? createdDate, DateTime? lastEditedDate) ParseDates(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return (null, null);
        }

        DateTime? createdDate = null;
        DateTime? lastEditedDate = null;

        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var line in lines.Take(6)) // Only check first few lines
        {
            var trimmedLine = line.Trim();
            
            if (trimmedLine.StartsWith("Created:", StringComparison.OrdinalIgnoreCase))
            {
                var dateStr = trimmedLine.Substring("Created:".Length).Trim();
                if (DateTime.TryParse(dateStr, out var parsed))
                {
                    createdDate = parsed;
                }
            }
            else if (trimmedLine.StartsWith("Last Edited:", StringComparison.OrdinalIgnoreCase))
            {
                var dateStr = trimmedLine.Substring("Last Edited:".Length).Trim();
                if (DateTime.TryParse(dateStr, out var parsed))
                {
                    lastEditedDate = parsed;
                }
            }
            else if (trimmedLine.StartsWith("#"))
            {
                // Stop if we hit the title heading
                break;
            }
        }

        return (createdDate, lastEditedDate);
    }
}
