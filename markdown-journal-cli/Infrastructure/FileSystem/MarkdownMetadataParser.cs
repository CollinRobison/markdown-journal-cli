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

    /// <summary>
    /// Updates the "Last Edited:" date line in markdown content.
    /// If the line exists in the metadata header (first few lines before any heading), it is replaced.
    /// If no "Last Edited:" line is found, one is inserted after the "Created:" line,
    /// or at the top of the file if neither exists.
    /// </summary>
    /// <param name="content">The markdown file content.</param>
    /// <param name="date">The date to set as the last edited date.</param>
    /// <param name="dateFormat">The date format string (e.g. "MM/dd/yyyy"). Defaults to "MM/dd/yyyy".</param>
    /// <returns>The updated markdown content with the new last edited date.</returns>
    public static string UpdateLastEditedDate(
        string content,
        DateTime date,
        string dateFormat = "MM/dd/yyyy"
    )
    {
        if (string.IsNullOrEmpty(content))
        {
            return $"Last Edited: {date.ToString(dateFormat)}\n";
        }

        // Detect the newline style used in the content (CRLF or LF)
        var newline = content.Contains("\r\n") ? "\r\n" : "\n";

        var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var dateString = $"Last Edited: {date.ToString(dateFormat)}";
        int createdLineIndex = -1;

        // Search the metadata header (first 6 non-empty lines, stop at heading)
        int nonEmptyCount = 0;
        for (int i = 0; i < lines.Length && nonEmptyCount < 6; i++)
        {
            var trimmed = lines[i].Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;
            nonEmptyCount++;

            if (trimmed.StartsWith('#'))
                break;

            if (trimmed.StartsWith("Last Edited:", StringComparison.OrdinalIgnoreCase))
            {
                lines[i] = dateString;
                return string.Join(newline, lines);
            }

            if (trimmed.StartsWith("Created:", StringComparison.OrdinalIgnoreCase))
            {
                createdLineIndex = i;
            }
        }

        // No existing "Last Edited:" line found — insert one
        var result = new List<string>(lines);
        int insertAt = createdLineIndex >= 0 ? createdLineIndex + 1 : 0;
        result.Insert(insertAt, dateString);
        return string.Join(newline, result);
    }
}
