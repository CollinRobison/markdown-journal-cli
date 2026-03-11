using System.Text.RegularExpressions;
using markdown_journal_cli.Infrastructure.Configuration.Models;

namespace markdown_journal_cli.Infrastructure.Configuration;

/// <summary>
/// Parses markdown table of contents files to extract all entry links.
/// Structure is derived by the caller using filename patterns.
/// </summary>
public class TableOfContentsMarkdownParser : ITableOfContentsMarkdownParser
{
    // Matches markdown links: [text](file.md)
    private static readonly Regex LinkPattern = new(
        @"\[([^\]]+)\]\(([^)]+)\)",
        RegexOptions.Compiled
    );

    /// <inheritdoc />
    public Entries[] ParseTableOfContents(string tocContent)
    {
        if (string.IsNullOrWhiteSpace(tocContent))
        {
            return Array.Empty<Entries>();
        }

        // Extract all markdown links from the TOC
        var matches = LinkPattern.Matches(tocContent);
        var entries = new List<Entries>();

        foreach (Match match in matches)
        {
            var name = match.Groups[1].Value.Trim();
            var file = match.Groups[2].Value.Trim();

            // Only include .md files
            if (file.EndsWith(FileConstants.MarkdownExtension, StringComparison.OrdinalIgnoreCase))
            {
                entries.Add(new Entries { Name = name, File = file });
            }
        }

        return entries.ToArray();
    }
}
