namespace markdown_journal_cli.Infrastructure;

public class FileSystem : IFileSystem
{
    public bool DirectoryExists(string path) => Directory.Exists(path);

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public string CombinePaths(params string[] paths) => Path.Combine(paths);

    public void CreateMarkdownFile(string path, string fileName, string body)
    {
        var fullFileName = fileName.Contains(".md") ? fileName : fileName + ".md";
        string filePath = Path.Combine(path, fullFileName);
        File.WriteAllText(filePath, body);
        Console.WriteLine($"Markdown file {fullFileName} created at: {filePath}");
    }
}
