namespace markdown_journal_cli.Infrastructure;

/// <summary>
/// Provides an abstraction layer for file system operations, enabling testability and cross-platform compatibility.
/// </summary>
public interface IFileSystem
{
    /// <summary>
    /// Determines whether the specified directory exists.
    /// </summary>
    /// <param name="path">The directory path to check.</param>
    /// <returns>true if the directory exists; otherwise, false.</returns>
    bool DirectoryExists(string path);

    /// <summary>
    /// Creates all directories and subdirectories in the specified path unless they already exist.
    /// </summary>
    /// <param name="path">The directory path to create.</param>
    /// <exception cref="ArgumentException">Thrown when path contains invalid characters.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the caller does not have the required permission.</exception>
    void CreateDirectory(string path);

    /// <summary>
    /// Combines multiple path segments into a single path.
    /// </summary>
    /// <param name="paths">An array of path segments to combine.</param>
    /// <returns>The combined path.</returns>
    /// <exception cref="ArgumentException">Thrown when one of the paths contains invalid characters.</exception>
    string CombinePaths(params string[] paths);
}
