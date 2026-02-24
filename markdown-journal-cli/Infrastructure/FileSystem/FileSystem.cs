namespace markdown_journal_cli.Infrastructure.FileSystem;

using Microsoft.Extensions.Logging;

public class FileSystem : IFileSystem
{
    private readonly ILogger<FileSystem> _logger;

    public FileSystem(ILogger<FileSystem> logger)
    {
        _logger = logger;
    }

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public bool FileExists(string path) => File.Exists(path);

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public string CombinePaths(params string[] paths) => Path.Combine(paths);

    public void CreateMarkdownFile(string path, string fileName, string body)
    {
        var fullFileName = fileName.Contains(FileConstants.MarkdownExtension) ? fileName : fileName + FileConstants.MarkdownExtension;
        string filePath = Path.Combine(path, fullFileName);
        if (FileExists(filePath))
        {
            throw new InvalidOperationException($"File already exists: {filePath}");
        }
        File.WriteAllText(filePath, body);
        _logger.LogDebug("Markdown file {FileName} created at: {FilePath}", fullFileName, filePath);
    }

    public void CreateFile(string path, string fileName, string body)
    {
        string filePath = Path.Combine(path, fileName);
        if (FileExists(filePath))
        {
            throw new InvalidOperationException($"File already exists: {filePath}");
        }
        File.WriteAllText(filePath, body);
        _logger.LogDebug("File {FileName} created at: {FilePath}", fileName, filePath);
    }

    public void UpdateFile(string path, string fileName, string body)
    {
        string filePath = Path.Combine(path, fileName);
        File.WriteAllText(filePath, body);
        _logger.LogDebug("File {FileName} updated at: {FilePath}", fileName, filePath);
    }

    public void DeleteFile(string filePath)
    {
        if (FileExists(filePath))
        {
            File.Delete(filePath);
            _logger.LogDebug("File deleted at {FilePath}", filePath);
        }
        else
        {
            _logger.LogDebug("File doesn't exist at {FilePath}", filePath);
        }
    }

    public string GetFileContent(string filePath)
    {
        if (!FileExists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }
        return File.ReadAllText(filePath);
    }

    public string[] GetFiles(string path, string searchPattern, SearchOption searchOption)
    {
        return Directory.GetFiles(path, searchPattern, searchOption);
    }
}
