using System;
using System.Text.Json;
using markdown_journal_cli.Infrastructure.Configuration.Models;
using markdown_journal_cli.Infrastructure.FileSystem;
using Microsoft.Extensions.Options;

namespace markdown_journal_cli.Infrastructure.Configuration;

public class JournalConfiguration(IFileSystem fileSystem, IOptions<JournalSettings> journalSettings)
    : IJournalConfiguration
{
    private readonly IFileSystem _fileSystem = fileSystem;
    private readonly JsonSerializerOptions opts = new() { WriteIndented = true };
    private readonly JournalSettings _journalSettings = journalSettings.Value;

    public void Create(string directory, JournalConfig config)
    {
        var journalConfName = _journalSettings.JournalConfigFileName;
        var journalrcPath = directory.Contains(journalConfName)
            ? directory
            : Path.Combine(directory, journalConfName);
        var actualDirectory = directory.Contains(journalConfName)
            ? Path.GetDirectoryName(directory) ?? directory
            : directory;

        if (!_fileSystem.FileExists(journalrcPath))
        {
            var journalrc = config;
            string jsonString = JsonSerializer.Serialize(journalrc, opts);
            _fileSystem.CreateFile(actualDirectory, journalConfName, jsonString);
        }
        else
        {
            Console.WriteLine($"{journalConfName} already exists at {journalrcPath}");
        }
    }

    public void Delete(string directory)
    {
        var journalConfName = _journalSettings.JournalConfigFileName;
        var journalrcPath = directory.Contains(journalConfName)
            ? directory
            : Path.Combine(directory, journalConfName);
        if (_fileSystem.FileExists(journalrcPath))
        {
            _fileSystem.DeleteFile(journalrcPath);
        }
        else
        {
            Console.WriteLine($"{journalConfName} doesn't exist at {journalrcPath}");
        }
    }

    public void Update(string directory, Action<JournalConfig> config)
    {
        var journalConfName = _journalSettings.JournalConfigFileName;
        var journalrcPath = directory.Contains(journalConfName)
            ? directory
            : Path.Combine(directory, journalConfName);
        if (!_fileSystem.FileExists(journalrcPath))
        {
            Console.WriteLine($"{journalConfName} doesn't exist at {journalrcPath}");
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
            Console.WriteLine($"Failed to parse {journalConfName} at {journalrcPath}");
            return;
        }

        if (existingConfig == null)
        {
            Console.WriteLine($"Failed to parse {journalConfName} at {journalrcPath}");
            return;
        }

        // Only modify the properties specified by the user
        config(existingConfig);

        // Save with all values (existing + updated)
        string updatedJsonString = JsonSerializer.Serialize(existingConfig, opts);
        var actualDirectory = directory.Contains(journalConfName)
            ? Path.GetDirectoryName(directory) ?? directory
            : directory;
        _fileSystem.UpdateFile(actualDirectory, journalConfName, updatedJsonString);
    }
}
