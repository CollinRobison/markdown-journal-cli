using markdown_journal_cli.Infrastructure.Configuration.Models;

namespace markdown_journal_cli.Infrastructure.Configuration;

/// <summary>
/// Parses markdown table of contents files to extract all entry links.
/// </summary>
public interface ITableOfContentsMarkdownParser
{
    /// <summary>
    /// Parses the table of contents markdown file and extracts all entry links.
    /// Structure (root vs topic) is determined by the caller based on filename patterns.
    /// </summary>
    /// <param name="tocContent">The content of the table of contents file.</param>
    /// <returns>A flat array of all entries found in the TOC.</returns>
    Entries[] ParseTableOfContents(string tocContent);
}
