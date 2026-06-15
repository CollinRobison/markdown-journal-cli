using System;
using System.Text.Json;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.Configuration.Models;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.Tracking.Models;
using Microsoft.Extensions.Options;

namespace markdown_journal_cli.Infrastructure.Tracking;

public class FileTracking : IFileTracking
{
    private readonly IFileSystem _fileSystem;
    private readonly IJournalConfiguration? _journalConfiguration;
    private readonly JsonSerializerOptions opts = new() { WriteIndented = true };
    private readonly JournalSettings _journalSettings;
    private readonly IHashService _hashService;

    public FileTracking(
        IFileSystem fileSystem,
        IOptions<JournalSettings> journalSettings,
        IHashService hashService
    )
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        ArgumentNullException.ThrowIfNull(journalSettings);
        _journalSettings = journalSettings.Value;
        _hashService = hashService ?? throw new ArgumentNullException(nameof(hashService));
    }

    public FileTracking(
        IFileSystem fileSystem,
        IJournalConfiguration journalConfiguration,
        IOptions<JournalSettings> journalSettings,
        IHashService hashService
    )
        : this(fileSystem, journalSettings, hashService)
    {
        _journalConfiguration =
            journalConfiguration ?? throw new ArgumentNullException(nameof(journalConfiguration));
    }

    private string GetIndexPath(string journalDir)
    {
        return Path.Combine(
            journalDir,
            _journalSettings.MetadataDirName,
            _journalSettings.TrackingFileName
        );
    }

    public JournalIndex LoadIndex(string path)
    {
        string indexPath = GetIndexPath(path);
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
        var metadataDir = Path.Combine(path, _journalSettings.MetadataDirName);
        _fileSystem.UpdateFile(metadataDir, _journalSettings.TrackingFileName, json);
    }

    private static string Normalize(string value) =>
    value.Trim().Replace('\\', '/').TrimStart('.');

    /// <summary>
    /// Determines whether a relative path matches any entry in the no-track list.
    /// </summary>
    /// <param name="relativePath">The relative path to check against the no-track list.</param>
    /// <param name="noTrackList">The collection of patterns to match against.</param>
    /// <returns>True if the path matches any no-track pattern; otherwise, false.</returns>
    private bool IsNoTrackMatch(string relativePath, IEnumerable<string> noTrackList)
    {
        var normalizedRelative = Normalize(relativePath);
        var fileName = _fileSystem.GetFileName(normalizedRelative);

        foreach (var rawEntry in noTrackList)
        {
            if (string.IsNullOrWhiteSpace(rawEntry))
            {
                continue;
            }

            var entry = Normalize(rawEntry);

            // Exact relative path match: "notes/todo.md"
            if (string.Equals(normalizedRelative, entry, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // File-name-only match: "todo.md"
            if (!entry.Contains('/') &&
                string.Equals(fileName, entry, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Directory match: "notes" or "notes/" excludes everything under it
            if (normalizedRelative.StartsWith(entry.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }


    /// <summary>
    /// Get all markdown files in the journal directory (excluding metadata directory).
    /// </summary>
    /// <param name="path">the journal directory path.</param>
    /// <returns>all markdown files in the journal directory (excluding metadata directory).</returns>
    private JournalConfig? ReadConfig(string path)
    {
        var journalrcPath = Path.Combine(path, _journalSettings.JournalConfigFileName);
        if (!_fileSystem.FileExists(journalrcPath))
        {
            return null;
        }

        var json = _fileSystem.GetFileContent(journalrcPath);
        return JsonSerializer.Deserialize<JournalConfig>(json);
    }

    private HashSet<string> GetCurrentMarkdownFiles(string path)
    {
        var config = _journalConfiguration?.Read(path) ?? ReadConfig(path);
        var noTrackList = config?.TrackingIndex.NoTrack ?? [];

        var metadataDir =
            Path.Combine(path, _journalSettings.MetadataDirName) + Path.DirectorySeparatorChar;

        return
        [
            .. _fileSystem
                .GetFiles(path, $"*{FileConstants.MarkdownExtension}", SearchOption.AllDirectories)
                .Where(f => !f.StartsWith(metadataDir, StringComparison.OrdinalIgnoreCase))
                .Select(f => Path.GetRelativePath(path, f))
                .Where(relativePath => !IsNoTrackMatch(relativePath, noTrackList))
        ];
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
                LastChecked = DateTime.UtcNow,
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
            var fullPath = Path.Combine(path, relativeFilePath);
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
                LastChecked = DateTime.UtcNow,
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
                LastChecked = DateTime.UtcNow,
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

    public void RenameFileInIndex(
        string path,
        string oldRelativeFilePath,
        string newRelativeFilePath
    )
    {
        RemoveFileFromIndex(path, oldRelativeFilePath);
        UpdateFileInIndex(path, newRelativeFilePath);
    }
}
