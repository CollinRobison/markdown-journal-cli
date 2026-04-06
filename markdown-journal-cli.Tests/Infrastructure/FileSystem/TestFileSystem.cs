using markdown_journal_cli.Infrastructure.FileSystem;

namespace markdown_journal_cli.Tests.Infrastructure.FileSystem;

/// <summary>
/// In-memory test double for <see cref="IFileSystem"/> used by unit tests.
/// Simulates directory creation and existence checks without touching the real file system.
/// </summary>
public class TestFileSystem : IFileSystem
{
    /// <summary>
    /// Tracks created directories in-memory. The dictionary keys are the directory paths and the
    /// values are unused (always <c>true</c>). This provides a fast lookup for existence checks.
    /// </summary>
    private readonly Dictionary<string, bool> _directories = new();

    /// <summary>
    /// Tracks created files in-memory. The dictionary keys are the full file paths and the
    /// values are the file contents.
    /// </summary>
    public readonly Dictionary<string, string> _files = new();

    public List<string> DeletedDirectories { get; } = new();

    /// <summary>
    /// Determines whether the specified directory exists in the in-memory file system.
    /// </summary>
    /// <param name="path">The directory path to check.</param>
    /// <returns><c>true</c> if the directory exists; otherwise, <c>false</c>.</returns>
    public virtual bool DirectoryExists(string path) => _directories.ContainsKey(path);

    /// <summary>
    /// Creates a directory entry in the in-memory file system.
    /// If the directory already exists in the in-memory store, this method does nothing.
    /// </summary>
    /// <param name="path">The directory path to create.</param>
    public virtual void CreateDirectory(string path)
    {
        if (!_directories.ContainsKey(path))
        {
            _directories[path] = true;
        }
    }

    /// <summary>
    /// Combines one or more path segments into a single path string using the platform
    /// <see cref="System.IO.Path.Combine"/> behavior.
    /// </summary>
    /// <param name="paths">An array of path segments to combine.</param>
    /// <returns>The combined path.</returns>
    public string CombinePaths(params string[] paths) => Path.Combine(paths);

    /// <summary>
    /// Clears the in-memory directory store. Useful for test setup/teardown between tests.
    /// </summary>
    public void Reset()
    {
        _directories.Clear();
        _files.Clear();
    }

    /// <summary>
    /// Creates a markdown file in the in-memory file system with the specified name and content.
    /// Also ensures the parent directory exists in the directory tracking.
    /// </summary>
    /// <param name="path">The directory path where the markdown file will be created.</param>
    /// <param name="fileName">The file name excluding extension (for example, <c>note</c> not <c>note.md</c>).</param>
    /// <param name="body">The markdown content to write into the file.</param>
    public virtual void CreateMarkdownFile(string path, string fileName, string body)
    {
        if (!_directories.ContainsKey(path))
        {
            _directories[path] = true;
        }

        var filePath = Path.Combine(path, $"{fileName}.md");
        _files[filePath] = body;
    }

    /// <summary>
    /// Gets the content of a file created in the test file system.
    /// </summary>
    /// <param name="filePath">The full path to the file.</param>
    /// <returns>The file content, or null if the file doesn't exist.</returns>
    public string? GetFileContent(string filePath)
    {
        return _files.TryGetValue(filePath, out var content) ? content : null;
    }

    /// <summary>
    /// Checks if a file exists in the test file system.
    /// </summary>
    /// <param name="filePath">The full path to the file.</param>
    /// <returns>True if the file exists, false otherwise.</returns>
    public virtual bool FileExists(string filePath)
    {
        return _files.ContainsKey(filePath);
    }

    /// <summary>
    /// Gets all created files for verification in tests.
    /// </summary>
    /// <returns>A dictionary of file paths and their contents.</returns>
    public IReadOnlyDictionary<string, string> GetAllFiles()
    {
        return _files.AsReadOnly();
    }

    /// <summary>
    /// Gets all created directories for verification in tests.
    /// </summary>
    /// <returns>A collection of directory paths.</returns>
    public IEnumerable<string> GetAllDirectories()
    {
        return _directories.Keys;
    }

    public virtual void CreateFile(string path, string fileName, string body)
    {
        if (!_directories.ContainsKey(path))
        {
            _directories[path] = true;
        }

        var filePath = Path.Combine(path, fileName);
        _files[filePath] = body;
    }

    public virtual void UpdateFile(string path, string fileName, string body)
    {
        var filePath = Path.Combine(path, fileName);
        _files[filePath] = body;
    }

    public virtual void DeleteFile(string filePath)
    {
        _files.Remove(filePath);
    }

    public virtual void DeleteDirectory(string path)
    {
        DeletedDirectories.Add(path);
        _directories.Remove(path);
    }

    public virtual void RenameFile(string oldPath, string newPath)
    {
        if (!_files.ContainsKey(oldPath))
            throw new FileNotFoundException($"File not found: {oldPath}");
        _files[newPath] = _files[oldPath];
        _files.Remove(oldPath);
    }

    string IFileSystem.GetFileContent(string filePath)
    {
        if (!_files.ContainsKey(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }
        return _files[filePath];
    }

    public string? GetFileNameWithoutExtension(string? path) =>
        Path.GetFileNameWithoutExtension(path);

    public string? GetDirectoryName(string? path) => Path.GetDirectoryName(path);

    public string? GetFileName(string? path) => Path.GetFileName(path);

    public string GetFullPath(string path) => Path.GetFullPath(path);

    public virtual string[] GetFiles(string path, string searchPattern, SearchOption searchOption)
    {
        var pattern = searchPattern.Replace("*", "").Replace("?", "");

        // Normalize the path (remove trailing separator)
        var normalizedPath = path.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar
        );

        var files = _files
            .Keys.Where(f =>
                f.StartsWith(normalizedPath + Path.DirectorySeparatorChar) && f.EndsWith(pattern)
            )
            .ToArray();

        if (searchOption == SearchOption.TopDirectoryOnly)
        {
            // Only include files directly in the path, not in subdirectories
            var pathPrefix = normalizedPath + Path.DirectorySeparatorChar;
            files = files
                .Where(f =>
                {
                    var relativePath = f.Substring(pathPrefix.Length);
                    return !relativePath.Contains(Path.DirectorySeparatorChar)
                        && !relativePath.Contains(Path.AltDirectorySeparatorChar);
                })
                .ToArray();
        }

        return files;
    }

    public virtual IReadOnlyList<string> GetMarkdownFiles(string directory)
    {
        var normalizedDirectory = directory.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar
        );
        var prefix = normalizedDirectory + Path.DirectorySeparatorChar;

        return GetFiles(directory, "*.md", SearchOption.AllDirectories)
            .Select(f => f.StartsWith(prefix) ? f.Substring(prefix.Length) : f)
            .ToList();
    }
}
