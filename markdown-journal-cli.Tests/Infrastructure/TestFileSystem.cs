using markdown_journal_cli.Infrastructure;

namespace markdown_journal_cli.Tests.Infrastructure;

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
    /// Determines whether the specified directory exists in the in-memory file system.
    /// </summary>
    /// <param name="path">The directory path to check.</param>
    /// <returns><c>true</c> if the directory exists; otherwise, <c>false</c>.</returns>
    public bool DirectoryExists(string path) => _directories.ContainsKey(path);
    
    /// <summary>
    /// Creates a directory entry in the in-memory file system.
    /// If the directory already exists in the in-memory store, this method does nothing.
    /// </summary>
    /// <param name="path">The directory path to create.</param>
    public void CreateDirectory(string path)
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
    public void Reset() => _directories.Clear();

    public void CreateMarkdownFile(string path, string fileName, string body)
    {
        if (!_directories.ContainsKey(path))
        {
            _directories[path] = true;
        }
    }
}
