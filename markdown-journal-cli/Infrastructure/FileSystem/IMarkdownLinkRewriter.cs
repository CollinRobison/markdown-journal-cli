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

    /// <summary>
    /// Scans every markdown file under <paramref name="directory"/>, rewrites all inline links
    /// whose final path segment matches <paramref name="oldFileName"/> to use
    /// <paramref name="newFileName"/> instead, and writes the file back if it changed.
    /// Files listed in <paramref name="excludeFiles"/> are skipped.
    /// Returns the relative paths of every file that was actually modified.
    /// </summary>
    IReadOnlyList<string> ReplaceLinksInDirectory(
        string directory,
        string oldFileName,
        string newFileName,
        IReadOnlyCollection<string>? excludeFiles = null
    );

    /// <summary>
    /// Scans every markdown file under <paramref name="directory"/>, strips all inline markdown
    /// links whose final path segment matches <paramref name="fileName"/> — replacing
    /// [text](file.md) with just the link text — and writes the file back if it changed.
    /// Files listed in <paramref name="excludeFiles"/> are skipped.
    /// Returns the relative paths of every file that was modified.
    /// </summary>
    IReadOnlyList<string> StripLinksInDirectory(
        string directory,
        string fileName,
        IReadOnlyCollection<string>? excludeFiles = null
    );
}
