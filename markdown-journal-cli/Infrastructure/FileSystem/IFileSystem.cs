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
    /// Determines whether the specified file exists.
    /// </summary>
    /// <param name="path">The file path to check.</param>
    /// <returns>true if the file exists; otherwise, false.</returns>
    bool FileExists(string path);

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
    /// Creates a file with the specified name and content at the given path.
    /// Implementations should ensure the target directory exists (creating it if necessary)
    /// and write the provided <paramref name="body"/> to a file with the provided <paramref name="fileName"/>.
    /// </summary>
    /// <param name="path">The directory path where the file will be created.</param>
    /// <param name="fileName">The file name. May include an extension (for example, <c>note.md</c> or <c>note.txt</c>), or be extension-less.</param>
    /// <param name="body">The content to write into the file.</param>
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
    void CreateFile(string path, string fileName, string body);

    /// <summary>
    /// Creates a markdown file with the specified name and content at the given path.
    /// The implementation should ensure the target directory exists (creating it if necessary)
    /// and write the provided <paramref name="body"/> to a file with the provided <paramref name="fileName"/>.
    /// </summary>
    /// <param name="path">The directory path where the markdown file will be created.</param>
    /// <param name="fileName">The file name excluding extension (for example, <c>note</c> not <c>note.md</c>).</param>
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
    void CreateMarkdownFile(string path, string fileName, string body);

    /// <summary>
    /// Updates an existing file with the specified content at the given path.
    /// The implementation should write the provided <paramref name="body"/> to the file with the provided <paramref name="fileName"/>
    /// in the specified directory, overwriting the existing content.
    /// </summary>
    /// <param name="path">The directory path where the file is located.</param>
    /// <param name="fileName">The file name including extension (for example, <c>config.json</c> or <c>note.md</c>).</param>
    /// <param name="body">The content to write into the file, replacing existing content.</param>
    /// <remarks>
    /// Implementations should prefer UTF-8 encoding and completely replace the existing file content.
    /// If the file does not exist, this method should behave like CreateFile.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> or <paramref name="fileName"/> contains invalid characters.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/>, <paramref name="fileName"/>, or <paramref name="body"/> is null.</exception>
    /// <exception cref="PathTooLongException">Thrown when the specified path, file name, or both exceed the system-defined maximum length.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when the specified directory cannot be found.</exception>
    /// <exception cref="IOException">Thrown when an I/O error occurs while writing the file.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the caller does not have permission to write to the target location.</exception>
    /// <exception cref="NotSupportedException">Thrown when the path format is not supported.</exception>
    /// <exception cref="System.Security.SecurityException">Thrown when the caller does not have the required permission.</exception>
    void UpdateFile(string path, string fileName, string body);

    /// <summary>
    /// Deletes the specified file if it exists.
    /// </summary>
    /// <param name="filePath">The full path to the file to delete.</param>
    /// <remarks>
    /// If the file does not exist, this method should not throw an exception.
    /// Implementations should handle file deletion gracefully and only throw exceptions
    /// for actual errors (permissions, I/O issues, etc.).
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when <paramref name="filePath"/> contains invalid characters.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="filePath"/> is null.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when the specified directory cannot be found.</exception>
    /// <exception cref="IOException">Thrown when an I/O error occurs while deleting the file or the file is in use.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the caller does not have permission to delete the file or the file is read-only.</exception>
    /// <exception cref="NotSupportedException">Thrown when the file path format is not supported.</exception>
    /// <exception cref="PathTooLongException">Thrown when the specified path exceeds the system-defined maximum length.</exception>
    void DeleteFile(string filePath);

    /// <summary>
    /// Renames or moves a file from <paramref name="oldPath"/> to <paramref name="newPath"/>.
    /// </summary>
    /// <param name="oldPath">The current full path of the file.</param>
    /// <param name="newPath">The destination full path for the file.</param>
    /// <exception cref="FileNotFoundException">Thrown when <paramref name="oldPath"/> does not exist.</exception>
    /// <exception cref="IOException">Thrown when a file already exists at <paramref name="newPath"/> or an I/O error occurs.</exception>
    void RenameFile(string oldPath, string newPath);

    /// <summary>
    /// Reads all text content from the specified file.
    /// </summary>
    /// <param name="filePath">The full path to the file to read.</param>
    /// <returns>The content of the file as a string.</returns>
    /// <remarks>
    /// Implementations should prefer UTF-8 encoding when reading the file.
    /// The entire file content is loaded into memory, so this method is not suitable for very large files.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when <paramref name="filePath"/> contains invalid characters.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="filePath"/> is null.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the specified file cannot be found.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when the specified directory cannot be found.</exception>
    /// <exception cref="IOException">Thrown when an I/O error occurs while reading the file.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the caller does not have permission to read the file.</exception>
    /// <exception cref="NotSupportedException">Thrown when the file path format is not supported.</exception>
    /// <exception cref="PathTooLongException">Thrown when the specified path exceeds the system-defined maximum length.</exception>
    /// <exception cref="System.Security.SecurityException">Thrown when the caller does not have the required permission.</exception>
    string GetFileContent(string filePath);

    /// <summary>
    /// Deletes the specified directory.
    /// </summary>
    /// <param name="path">The directory path to delete.</param>
    void DeleteDirectory(string path);

    /// <summary>
    /// Gets the file name without the extension from the specified path.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <returns>The file name without extension, or null if the path is null.</returns>
    string? GetFileNameWithoutExtension(string? path);

    /// <summary>
    /// Gets the directory name from the specified path.
    /// </summary>
    /// <param name="path">The file or directory path.</param>
    /// <returns>The directory name, or null if the path is null or has no directory component.</returns>
    string? GetDirectoryName(string? path);

    /// <summary>
    /// Gets the file name and extension from the specified path.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <returns>The file name and extension, or null if the path is null.</returns>
    string? GetFileName(string? path);

    /// <summary>
    /// Returns the absolute path for the specified path string.
    /// </summary>
    /// <param name="path">The file or directory for which to obtain absolute path information.</param>
    /// <returns>The fully qualified location of <paramref name="path"/>.</returns>
    string GetFullPath(string path);

    /// <summary>
    /// Gets the files that match the specified search pattern in the specified directory.
    /// </summary>
    /// <param name="path">The directory path to search.</param>
    /// <param name="searchPattern">The search pattern to match against file names (e.g., "*.md").</param>
    /// <param name="searchOption">Specifies whether to search only the current directory or all subdirectories.</param>
    /// <returns>An array of file paths that match the search pattern.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> or <paramref name="searchPattern"/> contains invalid characters.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> or <paramref name="searchPattern"/> is null.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when the specified directory cannot be found.</exception>
    /// <exception cref="IOException">Thrown when an I/O error occurs.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the caller does not have permission to access the directory.</exception>
    string[] GetFiles(string path, string searchPattern, SearchOption searchOption);

    /// <summary>
    /// Returns the relative paths of all markdown files found recursively under
    /// <paramref name="directory"/>, relative to that directory.
    /// </summary>
    /// <param name="directory">The root directory to search.</param>
    /// <returns>A read-only list of relative paths (e.g. <c>notes/intro.md</c>).</returns>
    IReadOnlyList<string> GetMarkdownFiles(string directory);
}
