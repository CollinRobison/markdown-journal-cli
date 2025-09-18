using System;
using System.Text.Json;
using markdown_journal_cli.Infrastructure.Configuration.Objects;
using markdown_journal_cli.Infrastructure.FileSystem;

namespace markdown_journal_cli.Infrastructure.Configuration;

public class JournalConfiguration(
    IFileSystem fileSystem
) : IJournalConfiguration
{
    private readonly IFileSystem _fileSystem = fileSystem;
    private JsonSerializerOptions opts = new() { WriteIndented = true };
    public void Create(string directory, JournalConfig config)
    {
        var journalrcPath = Path.Combine(directory, ".journalrc");
        if (!_fileSystem.FileExists(journalrcPath))
        {
            var journalrc = config;
            string jsonString = JsonSerializer.Serialize(journalrc, opts);
            _fileSystem.CreateFile(directory, ".journalrc", jsonString); 
        }
    }

    public void Create(string directory, Action<JournalConfig> config)
    {
        var journalrcPath = Path.Combine(directory, ".journalrc");
        if (!_fileSystem.FileExists(journalrcPath))
        {
            var journalrc = config;
            string jsonString = JsonSerializer.Serialize(journalrc, opts);
            _fileSystem.CreateFile(directory, ".journalrc", jsonString); 
        }
    }

    public void Delete(string directory)
    {
        throw new NotImplementedException();
    }

    public void EnsureConfigExists(string directory)
    {
        throw new NotImplementedException();
    }

    public void Update(string directory, Action<JournalConfig> config)
    {
        throw new NotImplementedException();
    }
}
