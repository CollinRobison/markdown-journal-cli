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
}
