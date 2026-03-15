namespace markdown_journal_cli.Infrastructure.FileSystem;

using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

/// <summary>
/// Rewrites inline markdown link references by matching the final path segment of the URL.
/// Only inline links ([text](url)) are handled; reference-style links are out of scope.
/// </summary>
public class MarkdownLinkRewriter(IFileSystem fileSystem, ILogger<MarkdownLinkRewriter> logger) : IMarkdownLinkRewriter
{
    private readonly IFileSystem _fileSystem =
        fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    private readonly ILogger<MarkdownLinkRewriter> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc/>
    public string RewriteLinks(string content, string oldFileName, string newFileName)
    {
        if (string.IsNullOrEmpty(content))
            return content;

        return BuildLinkPattern(oldFileName).Replace(
            content,
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
                return pattern.IsMatch(content);
            })
            .ToList();
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> ReplaceLinksInDirectory(
        string directory,
        string oldFileName,
        string newFileName,
        IReadOnlyCollection<string>? excludeFiles = null
    )
    {
        var modified = new List<string>();

        foreach (var relativePath in _fileSystem.GetMarkdownFiles(directory))
        {
            if (excludeFiles?.Contains(relativePath, StringComparer.OrdinalIgnoreCase) == true)
                continue;

            var absolutePath = _fileSystem.CombinePaths(directory, relativePath);
            var content = _fileSystem.GetFileContent(absolutePath);
            var updated = RewriteLinks(content, oldFileName, newFileName);

            if (string.Equals(content, updated, StringComparison.Ordinal))
                continue;

            var fileDir = _fileSystem.GetDirectoryName(absolutePath) ?? directory;
            var fileName2 = _fileSystem.GetFileName(absolutePath);
            if (fileName2 != null)
                _fileSystem.UpdateFile(fileDir, fileName2, updated);

            _logger.LogDebug("Rewrote links in '{RelativePath}': {OldFileName} → {NewFileName}", relativePath, oldFileName, newFileName);
            modified.Add(relativePath);
        }

        return modified;
    }

    // (?<text>[^\]]*) — link text inside [ ]
    // (?<prefix>(?:[^)]*/)?) — optional path prefix ending with /
    // Regex.Escape(fileName) — literal filename anchored as the final URL segment
    // RegexOptions.Compiled — JIT-compiles the pattern on first use; worthwhile since
    // the same pattern is applied across every .md file in the journal directory.
    private static Regex BuildLinkPattern(string fileName) =>
        new($@"\[(?<text>[^\]]*)\]\((?<prefix>(?:[^)]*/)?)(?<file>{Regex.Escape(fileName)})\)",
            RegexOptions.Compiled);
}
