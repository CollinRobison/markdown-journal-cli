using markdown_journal_cli.Infrastructure;

namespace markdown_journal_cli.Tests.Infrastructure;

public class TestFileSystem : IFileSystem
{
    private readonly Dictionary<string, bool> _directories = new();

    public bool DirectoryExists(string path) => _directories.ContainsKey(path);
    
    public void CreateDirectory(string path)
    {
        if (!_directories.ContainsKey(path))
        {
            _directories[path] = true;
        }
    }
    
    public string CombinePaths(params string[] paths) => Path.Combine(paths);

    public void Reset() => _directories.Clear();
}
