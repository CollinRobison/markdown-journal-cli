using System;
using System.Text.Json;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.Tracking.Models;
using Microsoft.Extensions.Options;

namespace markdown_journal_cli.Infrastructure.Tracking;

public class FileTracking(IFileSystem fileSystem, IOptions<JournalSettings> journalSettings, IHashService hashService) : IFileTracking
{
    private readonly IFileSystem _fileSystem = fileSystem;
    private readonly JsonSerializerOptions opts = new() { WriteIndented = true };
    private readonly JournalSettings _journalSettings = journalSettings.Value;
    private readonly string _indexFileName = $".{journalSettings.Value.AppName}";
    private readonly IHashService _hashService = hashService;

    public JournalIndex LoadIndex(string path)
    {
        string indexPath = $"{path}/{_indexFileName}";
        if (!_fileSystem.FileExists(indexPath))
        {
            return new JournalIndex();
        }
        var json = _fileSystem.GetFileContent(indexPath);
        return JsonSerializer.Deserialize<JournalIndex>(json) ?? new JournalIndex();
    }
    public void SaveIndex(JournalIndex index, string path)
    {
        var json = JsonSerializer.Serialize(index, opts);
        _fileSystem.UpdateFile(path, _indexFileName, json);
    }

    /// <summary>
    /// Get all markdown files in the journal directory (excluding metadata directory).
    /// </summary>
    /// <param name="path">the journal directory path.</param>
    /// <returns>all markdown files in the journal directory (excluding metadata directory).</returns>
    private HashSet<string> GetCurrentMarkdownFiles(string path)
    {
        return [.. _fileSystem.GetFiles(path, "*.md", SearchOption.AllDirectories)
            .Where(f => !f.Contains(_indexFileName))
            .Select(f => Path.GetRelativePath(path, f))];
    }

    public ChangeDetectionResult DetectChanges(string path)
    {
        var result = new ChangeDetectionResult();
        var oldIndex = LoadIndex(path);
        var newIndex = new JournalIndex();

        // Get current files on disk
        var currentFiles = GetCurrentMarkdownFiles(path);

        // Check all files that exist on disk
        foreach (var relativeFilePath in currentFiles)
        {
            var fullPath = Path.Combine(path, relativeFilePath);
            var currentHash = _hashService.ComputeFileHash(fullPath);

            // Add to new index
            newIndex.Files[relativeFilePath] = new FileState
            {
                FilePath = relativeFilePath,
                Hash = currentHash,
                LastChecked = DateTime.UtcNow
            };

            // Determine if added or modified
            if (!oldIndex.Files.TryGetValue(relativeFilePath, out FileState? value))
            {
                // New file
                result.AddedFiles.Add(relativeFilePath);
            }
            else if (value.Hash != currentHash)
            {
                // Modified file
                result.ModifiedFiles.Add(relativeFilePath);
            }
        }

        // Check for deleted files (in old index but not on disk)
        foreach (var trackedFile in oldIndex.Files.Keys)
        {
            if (!currentFiles.Contains(trackedFile))
            {
                result.DeletedFiles.Add(trackedFile);
            }
        }

        // Save the updated index
        SaveIndex(newIndex, path);

        return result;
    }

    public ChangeDetectionResult DetectChangesWithoutUpdate(string path)
    {
        var result = new ChangeDetectionResult();
        var oldIndex = LoadIndex(path);
        var currentFiles = GetCurrentMarkdownFiles(path);

        foreach (var relativeFilePath in currentFiles)
        {
            var fullPath = Path. Combine(path, relativeFilePath);
            var currentHash = _hashService.ComputeFileHash(fullPath);

            if (!oldIndex.Files.TryGetValue(relativeFilePath, out FileState? value))
            {
                result.AddedFiles.Add(relativeFilePath);
            }
            else if (value.Hash != currentHash)
            {
                result.ModifiedFiles.Add(relativeFilePath);
            }
        }

        foreach (var trackedFile in oldIndex.Files.Keys)
        {
            if (!currentFiles.Contains(trackedFile))
            {
                result.DeletedFiles.Add(trackedFile);
            }
        }

        return result;
    }

    public void UpdateIndex(string path)
    {
        var newIndex = new JournalIndex();
        var currentFiles = GetCurrentMarkdownFiles(path);

        foreach (var relativeFilePath in currentFiles)
        {
            var fullPath = Path.Combine(path, relativeFilePath);
            var currentHash = _hashService.ComputeFileHash(fullPath);

            newIndex.Files[relativeFilePath] = new FileState
            {
                FilePath = relativeFilePath,
                Hash = currentHash,
                LastChecked = DateTime.UtcNow
            };
        }

        SaveIndex(newIndex, path);
    }

    public void UpdateFileInIndex(string path, string relativeFilePath)
    {
        var index = LoadIndex(path);
        var fullPath = Path.Combine(path, relativeFilePath);

        if (_fileSystem.FileExists(fullPath))
        {
            var hash = _hashService.ComputeFileHash(fullPath);
            index.Files[relativeFilePath] = new FileState
            {
                FilePath = relativeFilePath,
                Hash = hash,
                LastChecked = DateTime.UtcNow
            };
        }
        else
        {
            // File doesn't exist, remove from index
            index.Files.Remove(relativeFilePath);
        }

        SaveIndex(index, path);
    }

    public void RemoveFileFromIndex(string path, string relativeFilePath)
    {
        var index = LoadIndex(path);
        index.Files.Remove(relativeFilePath);
        SaveIndex(index, path);
    }

    
}
