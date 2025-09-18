namespace markdown_journal_cli.Infrastructure.FileSystem;

public class FileSystem : IFileSystem
{
    public bool DirectoryExists(string path) => Directory.Exists(path);

    public bool FileExists(string path) => File.Exists(path);

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public string CombinePaths(params string[] paths) => Path.Combine(paths);

    public void CreateMarkdownFile(string path, string fileName, string body)
    {
        var fullFileName = fileName.Contains(".md") ? fileName : fileName + ".md";
        string filePath = Path.Combine(path, fullFileName);
        if (FileExists(filePath))
        {
            throw new InvalidOperationException($"File already exists: {filePath}");
        }
        File.WriteAllText(filePath, body);
        Console.WriteLine($"Markdown file {fullFileName} created at: {filePath}");
    }

    public void CreateFile(string path, string fileName, string body)
    {
        string filePath = Path.Combine(path, fileName);
        if (FileExists(filePath))
        {
            throw new InvalidOperationException($"File already exists: {filePath}");
        }
        File.WriteAllText(filePath, body);
        Console.WriteLine($"file {fileName} created at: {filePath}");
    }
}
