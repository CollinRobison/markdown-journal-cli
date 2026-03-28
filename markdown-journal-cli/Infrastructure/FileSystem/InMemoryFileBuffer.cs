namespace markdown_journal_cli.Infrastructure.FileSystem;

public class InMemoryFileBuffer(IFileSystem fileSystem) : IInMemoryFileBuffer
{
    private readonly IFileSystem _fileSystem =
        fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

    private readonly Dictionary<string, string> _snapshots = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _staged = new(StringComparer.OrdinalIgnoreCase);

    public void Snapshot(string absolutePath) =>
        _snapshots[absolutePath] = _fileSystem.GetFileContent(absolutePath);

    public void Stage(string absolutePath, string content) =>
        _staged[absolutePath] = content;

    public string? GetStaged(string absolutePath) =>
        _staged.TryGetValue(absolutePath, out var content) ? content : null;

    public string? GetSnapshot(string absolutePath) =>
        _snapshots.TryGetValue(absolutePath, out var content) ? content : null;

    public void Commit(string absolutePath)
    {
        if (!_staged.TryGetValue(absolutePath, out var content))
            throw new InvalidOperationException($"No staged content found for '{absolutePath}'.");

        var directory =
            Path.GetDirectoryName(absolutePath)
            ?? throw new InvalidOperationException(
                $"Cannot determine directory for '{absolutePath}'."
            );
        _fileSystem.UpdateFile(directory, Path.GetFileName(absolutePath), content);
    }

    public void Restore(string absolutePath)
    {
        if (!_snapshots.TryGetValue(absolutePath, out var content))
            throw new InvalidOperationException($"No snapshot found for '{absolutePath}'.");

        var directory =
            Path.GetDirectoryName(absolutePath)
            ?? throw new InvalidOperationException(
                $"Cannot determine directory for '{absolutePath}'."
            );
        _fileSystem.UpdateFile(directory, Path.GetFileName(absolutePath), content);
    }

    public bool HasStaged(string absolutePath) => _staged.ContainsKey(absolutePath);

    public bool HasSnapshot(string absolutePath) => _snapshots.ContainsKey(absolutePath);

    public void Clear()
    {
        _staged.Clear();
        _snapshots.Clear();
    }
}
