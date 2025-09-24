using System;
using System.Text.Json;
using markdown_journal_cli.Infrastructure.Configuration.Objects;
using markdown_journal_cli.Infrastructure.FileSystem;

namespace markdown_journal_cli.Infrastructure.Configuration;

public class JournalConfiguration(IFileSystem fileSystem) : IJournalConfiguration
{
    private readonly IFileSystem _fileSystem = fileSystem;
    private JsonSerializerOptions opts = new() { WriteIndented = true };

    public void Create(string directory, JournalConfig config)
    {
        var journalrcPath = directory.Contains(".journalrc")
            ? directory
            : Path.Combine(directory, ".journalrc");
        var actualDirectory = directory.Contains(".journalrc")
            ? Path.GetDirectoryName(directory) ?? directory
            : directory;

        if (!_fileSystem.FileExists(journalrcPath))
        {
            var journalrc = config;
            string jsonString = JsonSerializer.Serialize(journalrc, opts);
            _fileSystem.CreateFile(actualDirectory, ".journalrc", jsonString);
        }
        else
        {
            Console.WriteLine($".journalrc already exists at {journalrcPath}");
        }
    }

    public void Delete(string directory)
    {
        var journalrcPath = directory.Contains(".journalrc")
            ? directory
            : Path.Combine(directory, ".journalrc");
        if (_fileSystem.FileExists(journalrcPath))
        {
            _fileSystem.DeleteFile(journalrcPath);
        }
        else
        {
            Console.WriteLine($".journalrc doesn't exist at {journalrcPath}");
        }
    }

    public void Update(string directory, Action<JournalConfig> config)
    {
        var journalrcPath = directory.Contains(".journalrc")
            ? directory
            : Path.Combine(directory, ".journalrc");
        if (!_fileSystem.FileExists(journalrcPath))
        {
            Console.WriteLine($".journalrc doesn't exist at {journalrcPath}");
            return;
        }

        // Read existing configuration - preserves all existing values
        var existingJson = _fileSystem.GetFileContent(journalrcPath);
        JournalConfig? existingConfig;

        try
        {
            existingConfig = JsonSerializer.Deserialize<JournalConfig>(existingJson);
        }
        catch (JsonException)
        {
            Console.WriteLine($"Failed to parse .journalrc at {journalrcPath}");
            return;
        }

        if (existingConfig == null)
        {
            Console.WriteLine($"Failed to parse .journalrc at {journalrcPath}");
            return;
        }

        // Only modify the properties specified by the user
        config(existingConfig);

        // Save with all values (existing + updated)
        string updatedJsonString = JsonSerializer.Serialize(existingConfig, opts);
        var actualDirectory = directory.Contains(".journalrc")
            ? Path.GetDirectoryName(directory) ?? directory
            : directory;
        _fileSystem.UpdateFile(actualDirectory, ".journalrc", updatedJsonString);
    }
}
