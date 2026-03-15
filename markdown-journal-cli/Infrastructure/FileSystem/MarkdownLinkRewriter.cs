namespace markdown_journal_cli.Infrastructure.FileSystem;

using System.Text.RegularExpressions;

/// <summary>
/// Rewrites inline markdown link references by matching the final path segment of the URL.
/// Only inline links ([text](url)) are handled; reference-style links are out of scope.
/// </summary>
public class MarkdownLinkRewriter(IFileSystem fileSystem) : IMarkdownLinkRewriter
{
    private readonly IFileSystem _fileSystem =
        fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

    /// <inheritdoc/>
    public string RewriteLinks(string content, string oldFileName, string newFileName)
    {
        if (string.IsNullOrEmpty(content))
            return content;

        // Matches [text](optional/path/oldFileName) — captures the optional leading path separately
        // so it can be re-emitted unchanged. The filename must be the final segment before ')'.
        var pattern = BuildLinkPattern(oldFileName);
        return Regex.Replace(
            content,
            pattern,
            m => $"[{m.Groups["text"].Value}]({m.Groups["prefix"].Value}{newFileName})"
        );
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> FindFilesWithLinkTo(string directory, string fileName)
    {
        var pattern = BuildLinkPattern(fileName);
        return _fileSystem
            .GetMarkdownFiles(directory)
            .Where(relativePath =>
            {
                var absolutePath = _fileSystem.CombinePaths(directory, relativePath);
                var content = _fileSystem.GetFileContent(absolutePath);
                return Regex.IsMatch(content, pattern);
            })
            .ToList();
    }

    private static string BuildLinkPattern(string fileName) =>
        // (?<text>[^\]]*) — link text inside [ ]
        // (?<prefix>(?:[^)]*/)?) — optional path prefix ending with /
        // Regex.Escape(fileName) — literal filename
        $@"\[(?<text>[^\]]*)\]\((?<prefix>(?:[^)]*/)?)(?<file>{Regex.Escape(fileName)})\)";
}
