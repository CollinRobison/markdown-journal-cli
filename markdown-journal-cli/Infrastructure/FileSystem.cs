namespace markdown_journal_cli.Infrastructure;

public class FileSystem : IFileSystem
{
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);
    public string CombinePaths(params string[] paths) => Path.Combine(paths);
}
