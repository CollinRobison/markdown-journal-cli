namespace markdown_journal_cli.Infrastructure.FileSystem;

/// <summary>
/// Finds and rewrites markdown inline link references to a given filename inside file content.
/// Matches standard markdown links: [text](path/to/file.md) and [text](file.md)
/// where the final path segment equals the target filename.
/// </summary>
public interface IMarkdownLinkRewriter
{
    /// <summary>
    /// Rewrites all markdown inline link references whose final path segment matches
    /// <paramref name="oldFileName"/> to use <paramref name="newFileName"/> instead,
    /// preserving any leading path segments.
    /// Returns the updated content string (unchanged if no matches found).
    /// </summary>
    string RewriteLinks(string content, string oldFileName, string newFileName);

    /// <summary>
    /// Returns the relative paths (relative to <paramref name="directory"/>) of all
    /// markdown files whose content contains a markdown inline link to <paramref name="fileName"/>.
    /// </summary>
    IReadOnlyList<string> FindFilesWithLinkTo(string directory, string fileName);
}
