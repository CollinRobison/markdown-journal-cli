namespace markdown_journal_cli.Infrastructure.FileSystem;

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

    /// <summary>
    /// Creates a markdown file with the specified name and content at the given path.
    /// The implementation should ensure the target directory exists (creating it if necessary)
    /// and write the provided body to a file with the provided <paramref name="fileName"/>.
    /// </summary>
    /// <param name="path">The directory path where the markdown file will be created.</param>
    /// <param name="fileName">The file name excluding extension (for example, <c>note not note.md</c>).</param>
    /// <param name="body">The markdown content to write into the file.</param>
    /// <remarks>
    /// Implementations should prefer UTF-8 encoding and overwrite existing files by default,
    /// or throw an exception if overwriting is not allowed.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> or <paramref name="fileName"/> contains invalid characters.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/>, <paramref name="fileName"/>, or <paramref name="body"/> is null.</exception>
    /// <exception cref="PathTooLongException">Thrown when the specified path, file name, or both exceed the system-defined maximum length.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when the specified directory cannot be found.</exception>
    /// <exception cref="IOException">Thrown when an I/O error occurs while writing the file.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the caller does not have permission to write to the target location.</exception>
    /// <exception cref="NotSupportedException">Thrown when the path format is not supported.</exception>
    /// <exception cref="System.Security.SecurityException">Thrown when the caller does not have the required permission.</exception>
    public void CreateMarkdownFile(string path, string fileName, string body);
}
