using System;
using System.Text.Json;
using System.Text.RegularExpressions;
using markdown_journal_cli.Infrastructure.Configuration.Models;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.Tracking;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace markdown_journal_cli.Infrastructure.Configuration;

/// <summary>
/// Comparer for natural (alphanumeric) sorting of strings.
/// Treats consecutive digits as numbers for proper numerical ordering.
/// </summary>
internal class NaturalStringComparer : IComparer<string>
{
    public int Compare(string? x, string? y)
    {
        if (x == y)
            return 0;
        if (x == null)
            return -1;
        if (y == null)
            return 1;

        int ix = 0,
            iy = 0;

        while (ix < x.Length && iy < y.Length)
        {
            // Check if both are digits
            if (char.IsDigit(x[ix]) && char.IsDigit(y[iy]))
            {
                // Extract numbers
                var numX = ExtractNumber(x, ref ix);
                var numY = ExtractNumber(y, ref iy);

                var numCompare = numX.CompareTo(numY);
                if (numCompare != 0)
                    return numCompare;
            }
            else
            {
                // Compare characters case-insensitively
                var charCompare = char.ToLowerInvariant(x[ix])
                    .CompareTo(char.ToLowerInvariant(y[iy]));
                if (charCompare != 0)
                    return charCompare;

                ix++;
                iy++;
            }
        }

        // If one string is a prefix of the other, shorter comes first
        return x.Length.CompareTo(y.Length);
    }

    private static long ExtractNumber(string str, ref int index)
    {
        int start = index;
        while (index < str.Length && char.IsDigit(str[index]))
        {
            index++;
        }

        return long.Parse(str.Substring(start, index - start));
    }
}

public class JournalConfiguration(
    IFileSystem fileSystem,
    IOptions<JournalSettings> journalSettings,
    ILogger<JournalConfiguration> logger,
    IFileTracking fileTracking,
    IJournalTocStructureRepository tocStructureRepository
) : IJournalConfiguration
{
    private readonly IFileSystem _fileSystem = fileSystem;
    private readonly ILogger<JournalConfiguration> _logger = logger;
    private readonly JsonSerializerOptions opts = new() { WriteIndented = true };
    private readonly JournalSettings _journalSettings = journalSettings.Value;
    private readonly IFileTracking _fileTracking = fileTracking;
    private readonly IJournalTocStructureRepository _tocStructureRepository = tocStructureRepository;

    private string GetMetadataDir(string directory)
    {
        var (_, actualDirectory) = GetJournalrcPaths(directory);
        return Path.Combine(actualDirectory, _journalSettings.MetadataDirName);
    }

    public void Create(string directory, JournalConfig config)
    {
        var (journalrcPath, actualDirectory) = GetJournalrcPaths(directory);
        var journalConfName = _journalSettings.JournalConfigFileName;

        if (!_fileSystem.FileExists(journalrcPath))
        {
            string jsonString = JsonSerializer.Serialize(config, opts);
            _fileSystem.CreateFile(actualDirectory, journalConfName, jsonString);
        }
        else
        {
            _logger.LogWarning(
                "{JournalConfName} already exists at {JournalrcPath}",
                journalConfName,
                journalrcPath
            );
        }
    }

    public void Delete(string directory)
    {
        var (journalrcPath, _) = GetJournalrcPaths(directory);
        var journalConfName = _journalSettings.JournalConfigFileName;

        if (_fileSystem.FileExists(journalrcPath))
        {
            _fileSystem.DeleteFile(journalrcPath);
        }
        else
        {
            _logger.LogDebug(
                "{JournalConfName} doesn't exist at {JournalrcPath}",
                journalConfName,
                journalrcPath
            );
        }
    }

    public void Update(string directory, Action<JournalConfig> config)
    {
        var (journalrcPath, actualDirectory) = GetJournalrcPaths(directory);
        var journalConfName = _journalSettings.JournalConfigFileName;

        if (!_fileSystem.FileExists(journalrcPath))
        {
            _logger.LogDebug(
                "{JournalConfName} doesn't exist at {JournalrcPath}",
                journalConfName,
                journalrcPath
            );
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
            _logger.LogWarning(
                "Failed to parse {JournalConfName} at {JournalrcPath}",
                journalConfName,
                journalrcPath
            );
            return;
        }

        if (existingConfig == null)
        {
            _logger.LogWarning(
                "Failed to parse {JournalConfName} at {JournalrcPath}",
                journalConfName,
                journalrcPath
            );
            return;
        }

        // Capture old TOC file before applying changes
        var oldTocFile = existingConfig.TableOfContents?.File;

        // Only modify the properties specified by the user
        config(existingConfig);

        // If TOC file changed, remove the new TOC file from entries (in case it was already there)
        var newTocFile = existingConfig.TableOfContents?.File;
        if (
            !string.IsNullOrEmpty(newTocFile)
            && !string.Equals(oldTocFile, newTocFile, StringComparison.OrdinalIgnoreCase)
        )
        {
            RemoveEntryFromTocStructure(directory, newTocFile);
        }

        // Save with all values (existing + updated)
        string updatedJsonString = JsonSerializer.Serialize(existingConfig, opts);
        _fileSystem.UpdateFile(actualDirectory, journalConfName, updatedJsonString);
    }

    public JournalConfig? Read(string directory)
    {
        var (journalrcPath, _) = GetJournalrcPaths(directory);
        var journalConfName = _journalSettings.JournalConfigFileName;

        if (!_fileSystem.FileExists(journalrcPath))
        {
            _logger.LogDebug(
                "{JournalConfName} doesn't exist at {JournalrcPath}",
                journalConfName,
                journalrcPath
            );
            return null;
        }

        var json = _fileSystem.GetFileContent(journalrcPath);
        try
        {
            return JsonSerializer.Deserialize<JournalConfig>(json);
        }
        catch (JsonException)
        {
            _logger.LogWarning(
                "Failed to parse {JournalConfName} at {JournalrcPath}",
                journalConfName,
                journalrcPath
            );
            return null;
        }
    }

    public void AddIgnoreEntry(string directory, string file)
    {
        Update(
            directory,
            config =>
            {
                if (IgnoreFileEntryExists(config.TableOfContents.IgnoreFiles ?? [], file))
                {
                    _logger.LogDebug("File '{File}' already exists in the Ignore File List", file);
                    return;
                }

                var ignoreEntries = (config.TableOfContents.IgnoreFiles ?? []).ToList();
                ignoreEntries.Add(file);
                config.TableOfContents.IgnoreFiles = ignoreEntries.ToArray();
            }
        );
    }

    public void AddRootEntry(string directory, string name, string file)
    {
        var metadataDir = GetMetadataDir(directory);
        var tocStructure = _tocStructureRepository.Load(metadataDir);

        if (RootEntryExists(tocStructure.RootEntries, file))
        {
            _logger.LogDebug(
                "Root entry '{File}' already exists in journal configuration",
                file
            );
            return;
        }

        var existingEntries = tocStructure.RootEntries.ToList();
        existingEntries.Add(new Entries { Name = name, File = file });
        var naturalComparer = new NaturalStringComparer();
        existingEntries.Sort((a, b) => naturalComparer.Compare(a.File, b.File));
        tocStructure.RootEntries = existingEntries.ToArray();
        _tocStructureRepository.Save(tocStructure, metadataDir);
    }

    public void AddTopicEntry(
        string directory,
        string[] topicPath,
        string entryName,
        string file,
        int? maxDepth = null,
        bool sortAlphabetically = true
    )
    {
        if (topicPath == null || topicPath.Length == 0)
        {
            _logger.LogWarning("Topic path cannot be empty");
            return;
        }

        if (maxDepth.HasValue && topicPath.Length > maxDepth.Value)
        {
            _logger.LogWarning(
                "Topic path depth ({TopicPathLength}) exceeds maximum allowed depth ({MaxDepth})",
                topicPath.Length,
                maxDepth.Value
            );
            return;
        }

        var metadataDir = GetMetadataDir(directory);
        var tocStructure = _tocStructureRepository.Load(metadataDir);

        var topics = tocStructure.Structure.Topics.ToList();
        AddOrUpdateTopicHierarchy(topics, topicPath, entryName, file, 0, sortAlphabetically);
        tocStructure.Structure.Topics = topics.ToArray();
        _tocStructureRepository.Save(tocStructure, metadataDir);
    }

    public void AddEntry(
        string directory,
        string name,
        string file,
        string[]? topicPath = null,
        int? maxDepth = null,
        bool sortAlphabetically = true,
        bool ignoreFile = false
    )
    {
        // Check if file is TOC file - skip it to avoid circular references
        var config = Read(directory);
        if (
            config != null
            && !string.IsNullOrEmpty(config.TableOfContents.File)
            && string.Equals(file, config.TableOfContents.File, StringComparison.OrdinalIgnoreCase)
        )
        {
            _logger.LogDebug("Skipping TOC file '{File}' from being added as entry", file);
            return;
        }

        // If ignoring file, only add to ignore list and skip structure
        if (ignoreFile)
        {
            AddIgnoreEntry(directory, file);
            _logger.LogDebug(
                "File '{File}' added to ignore list only (not added to structure)",
                file
            );
            return;
        }

        // Extract filename without path and extension
        var fileName = Path.GetFileNameWithoutExtension(file);
        if (IsRootEntry(fileName))
        {
            // File matches root entry pattern (1a-9z), add as root entry
            // Extract name from filename if not provided
            var effectiveName = string.IsNullOrEmpty(name)
                ? ExtractEntryNameFromFilename(fileName, isRootEntry: true)
                : name;
            AddRootEntry(directory, effectiveName, file);
        }
        else
        {
            // File doesn't match root entry pattern, add as topic entry
            // If no topic path provided, parse it from the filename
            var effectiveTopicPath = topicPath;
            if (effectiveTopicPath == null || effectiveTopicPath.Length == 0)
            {
                effectiveTopicPath = ParseTopicPathFromFilename(fileName);
            }

            // Extract name from filename if not provided
            var effectiveName = string.IsNullOrEmpty(name)
                ? ExtractEntryNameFromFilename(fileName, isRootEntry: false)
                : name;

            AddTopicEntry(
                directory,
                effectiveTopicPath,
                effectiveName,
                file,
                maxDepth,
                sortAlphabetically
            );
        }
    }

    public bool UpdateEntryName(string directory, string file, string newEntryName)
    {
        var metadataDir = GetMetadataDir(directory);
        var tocStructure = _tocStructureRepository.Load(metadataDir);

        bool updated = false;

        // Search in root entries
        foreach (var entry in tocStructure.RootEntries)
        {
            if (string.Equals(entry.File, file, StringComparison.OrdinalIgnoreCase))
            {
                entry.Name = newEntryName;
                updated = true;
                break;
            }
        }

        // If not found in root entries, search in topics
        if (!updated)
        {
            updated = UpdateEntryNameInTopics(tocStructure.Structure.Topics, file, newEntryName);
        }

        if (updated)
        {
            _tocStructureRepository.Save(tocStructure, metadataDir);
        }
        else
        {
            _logger.LogDebug("Entry with file '{File}' not found in journal configuration", file);
        }

        return updated;
    }

    public bool RemoveEntry(string directory, string file)
    {
        var metadataDir = GetMetadataDir(directory);
        var tocStructure = _tocStructureRepository.Load(metadataDir);

        bool removed = false;

        // Try removing from root entries
        var rootEntries = tocStructure.RootEntries.ToList();
        var originalCount = rootEntries.Count;
        rootEntries.RemoveAll(e => string.Equals(e.File, file, StringComparison.OrdinalIgnoreCase));
        if (rootEntries.Count < originalCount)
        {
            tocStructure.RootEntries = rootEntries.ToArray();
            removed = true;
        }

        // If not found in root entries, search in topics
        if (!removed)
        {
            var topics = tocStructure.Structure.Topics.ToList();
            removed = RemoveEntryFromTopics(topics, file);
            if (removed)
            {
                tocStructure.Structure.Topics = topics.ToArray();
            }
        }

        if (removed)
        {
            _tocStructureRepository.Save(tocStructure, metadataDir);
        }
        else
        {
            _logger.LogDebug("Entry with file '{File}' not found in journal configuration", file);
        }

        return removed;
    }

    public void RegenerateStructure(string directory, IEnumerable<string> files)
    {
        var metadataDir = GetMetadataDir(directory);
        var tocStructure = JournalTocStructure.Empty();
        _tocStructureRepository.Save(tocStructure, metadataDir);

        // Re-add all files — AddEntry handles root vs topic classification
        foreach (var file in files)
        {
            AddEntry(directory, string.Empty, file);
        }
    }

    public (Entries? entry, string[] topicPath) FindEntry(string directory, string fileName)
    {
        var metadataDir = GetMetadataDir(directory);
        var tocStructure = _tocStructureRepository.Load(metadataDir);

        // Search in root entries first
        foreach (var entry in tocStructure.RootEntries)
        {
            if (string.Equals(entry.File, fileName, StringComparison.OrdinalIgnoreCase))
            {
                return (entry, []);
            }
        }

        // Search in topics recursively
        return FindInTopics(tocStructure.Structure.Topics, fileName, []);
    }

    public void UpdateFileReferences(string directory, string oldFile, string newFile)
    {
        var metadataDir = GetMetadataDir(directory);
        var tocStructure = _tocStructureRepository.Load(metadataDir);

        // Update root entries
        foreach (var entry in tocStructure.RootEntries)
        {
            if (string.Equals(entry.File, oldFile, StringComparison.OrdinalIgnoreCase))
            {
                entry.File = newFile;
            }
        }

        // Update topic entries recursively
        UpdateFileInTopics(tocStructure.Structure.Topics, oldFile, newFile);

        _tocStructureRepository.Save(tocStructure, metadataDir);

        // Update ignore list in .journalrc
        Update(
            directory,
            config =>
            {
                if (config.TableOfContents.IgnoreFiles is { Length: > 0 })
                {
                    config.TableOfContents.IgnoreFiles = config
                        .TableOfContents.IgnoreFiles.Select(f =>
                            string.Equals(f, oldFile, StringComparison.OrdinalIgnoreCase)
                                ? newFile
                                : f
                        )
                        .ToArray();
                }
            }
        );
    }

    public JournalConfigSyncResult DetectConfigChanges(string journalPath)
    {
        var config = Read(journalPath);
        if (config is null)
            return new JournalConfigSyncResult();

        var metadataDir = GetMetadataDir(journalPath);
        var tocStructure = _tocStructureRepository.Load(metadataDir);

        var trackedFiles = new HashSet<string>(
            _fileTracking.LoadIndex(journalPath).Files.Keys,
            StringComparer.OrdinalIgnoreCase
        );

        var configFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in tocStructure.RootEntries)
            configFiles.Add(entry.File);
        CollectTopicEntryFiles(tocStructure.Structure.Topics, configFiles);
        configFiles.UnionWith(config.TableOfContents.IgnoreFiles ?? []);

        _logger.LogDebug(
            "Detecting config changes: {TrackedCount} tracked files, {ConfigCount} config entries",
            trackedFiles.Count,
            configFiles.Count
        );

        var tocFile = config.TableOfContents.File;

        var filesToAdd = trackedFiles
            .Where(f =>
                !configFiles.Contains(f)
                && !string.Equals(f, tocFile, StringComparison.OrdinalIgnoreCase)
            )
            .ToList();

        var filesToRemove = configFiles.Where(f => !trackedFiles.Contains(f)).ToList();

        return new JournalConfigSyncResult
        {
            FilesToAdd = filesToAdd,
            FilesToRemove = filesToRemove,
        };
    }

    private static void CollectTopicEntryFiles(IEnumerable<Topic> topics, HashSet<string> fileSet)
    {
        foreach (var topic in topics)
        {
            foreach (var entry in topic.Entries)
                fileSet.Add(entry.File);
            if (topic.Subtopics is not null)
                CollectTopicEntryFiles(topic.Subtopics, fileSet);
        }
    }

    #region Private Helper Methods

    /// <summary>
    /// Resolves the full path to the journalrc file and its containing directory.
    /// Handles both directory paths and full file paths as input.
    /// </summary>
    private (string filePath, string directory) GetJournalrcPaths(string directoryOrFilePath)
    {
        var journalConfName = _journalSettings.JournalConfigFileName;

        // Check if input already ends with the config filename
        var isFilePath = Path.GetFileName(directoryOrFilePath)
            .Equals(journalConfName, StringComparison.OrdinalIgnoreCase);

        var filePath = isFilePath
            ? directoryOrFilePath
            : Path.Combine(directoryOrFilePath, journalConfName);

        var directory = isFilePath
            ? Path.GetDirectoryName(directoryOrFilePath) ?? directoryOrFilePath
            : directoryOrFilePath;

        return (filePath, directory);
    }

    /// <summary>
    /// checks if root entry already exists in the .journalrc
    /// </summary>
    private static bool RootEntryExists(Entries[] rootEntries, string file)
    {
        return rootEntries.Any(entry =>
            string.Equals(entry.File, file, StringComparison.OrdinalIgnoreCase)
        );
    }

    /// <summary>
    /// Determines if a filename matches the root entry pattern (1a-9z).
    /// Checks if the filename starts with the pattern followed by the heading separator or end of string.
    /// Examples: 1a, 2b, 5h, 9z, 3z-test_file, 1a-Introduction
    /// NOT valid: 3zebra, 3z_ebra (underscore is title separator, not heading separator)
    /// </summary>
    private bool IsRootEntry(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return false;
        }

        // Pattern matches: starts with single digit 1-9 followed by single lowercase letter a-z
        // Followed by either end of string or the heading separator from settings
        var escapedSeparator = Regex.Escape(_journalSettings.HeadingSeparator);
        var pattern = $@"^[1-9][a-z](?:{escapedSeparator}|$)";
        return Regex.IsMatch(fileName, pattern, RegexOptions.IgnoreCase)
            || fileName.ToLower().Equals("readme");
    }

    /// <summary>
    /// Removes a file entry from the TOC structure (root entries + topics). Used when the TOC
    /// filename changes so that the new TOC file is not also listed as a content entry.
    /// </summary>
    private void RemoveEntryFromTocStructure(string directory, string file)
    {
        var metadataDir = GetMetadataDir(directory);
        var tocStructure = _tocStructureRepository.Load(metadataDir);

        var rootEntries = tocStructure.RootEntries?.ToList() ?? new List<Entries>();
        rootEntries.RemoveAll(e => string.Equals(e.File, file, StringComparison.OrdinalIgnoreCase));
        tocStructure.RootEntries = rootEntries.ToArray();

        if (tocStructure.Structure?.Topics != null)
        {
            var topics = tocStructure.Structure.Topics.ToList();
            RemoveEntryFromTopics(topics, file);
            tocStructure.Structure.Topics = topics.ToArray();
        }

        _tocStructureRepository.Save(tocStructure, metadataDir);
    }

    /// <summary>
    /// checks if entry already exists in the ignoreFiles of .journalrc
    /// </summary>
    private static bool IgnoreFileEntryExists(string[] ignoreEntries, string file)
    {
        return ignoreEntries.Any(entry =>
            string.Equals(entry, file, StringComparison.OrdinalIgnoreCase)
        );
    }

    /// <summary>
    /// Parses a topic path from a filename by splitting on the heading separator
    /// and converting title separators to spaces for display.
    /// Examples:
    /// - "new_entry" → ["New Entry"]
    /// - "Learning-Rust_Programming" → ["Learning", "Rust Programming"]
    /// </summary>
    /// <summary>
    /// Extracts entry name from filename. For root entries (1a-9z pattern), extracts the part after the pattern.
    /// For topic entries, extracts the last part after splitting by heading separator.
    /// </summary>
    private string ExtractEntryNameFromFilename(string fileName, bool isRootEntry)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return "Untitled";
        }

        if (isRootEntry)
        {
            // For root entries like "1b-Intro", extract "Intro" after the pattern
            // Pattern is: digit + letter + separator (e.g., "1b-")
            var match = Regex.Match(
                fileName,
                @"^[0-9][a-zA-Z]" + Regex.Escape(_journalSettings.HeadingSeparator) + "(.+)$"
            );
            if (match.Success && match.Groups.Count > 1)
            {
                var name = match.Groups[1].Value;
                // Replace both separators with spaces
                return name.Replace(_journalSettings.TitleSpaceSeparator, " ")
                    .Replace(_journalSettings.HeadingSeparator, " ")
                    .Trim();
            }
            // Fallback if pattern doesn't match
            return fileName
                .Replace(_journalSettings.TitleSpaceSeparator, " ")
                .Replace(_journalSettings.HeadingSeparator, " ")
                .Trim();
        }
        else
        {
            // For topic entries, get the last part after splitting by heading separator
            var parts = fileName.Split(
                _journalSettings.HeadingSeparator,
                StringSplitOptions.RemoveEmptyEntries
            );
            if (parts.Length > 0)
            {
                var lastName = parts[^1];
                return lastName.Replace(_journalSettings.TitleSpaceSeparator, " ").Trim();
            }
            return fileName.Replace(_journalSettings.TitleSpaceSeparator, " ").Trim();
        }
    }

    private string[] ParseTopicPathFromFilename(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return new[] { "General" };
        }

        // Split by heading separator to get topic hierarchy
        var parts = fileName.Split(
            _journalSettings.HeadingSeparator,
            StringSplitOptions.RemoveEmptyEntries
        );

        if (parts.Length == 0)
        {
            return new[] { "General" };
        }

        // If only one part, treat it as a single topic
        if (parts.Length == 1)
        {
            var singleTopic = parts[0].Replace(_journalSettings.TitleSpaceSeparator, " ").Trim();
            return string.IsNullOrEmpty(singleTopic) ? new[] { "General" } : new[] { singleTopic };
        }

        // Multiple parts: all except the last are topics, last is entry name
        // Convert each topic part: replace title separators with spaces for display
        return parts
            .Take(parts.Length - 1) // Exclude the last part (entry name)
            .Select(part => part.Replace(_journalSettings.TitleSpaceSeparator, " ").Trim())
            .Where(part => !string.IsNullOrEmpty(part))
            .ToArray();
    }

    private static bool UpdateEntryNameInTopics(Topic[] topics, string file, string newEntryName)
    {
        foreach (var topic in topics)
        {
            // Search in this topic's entries
            foreach (var entry in topic.Entries)
            {
                if (string.Equals(entry.File, file, StringComparison.OrdinalIgnoreCase))
                {
                    entry.Name = newEntryName;
                    return true;
                }
            }

            // Recursively search in subtopics
            if (topic.Subtopics != null)
            {
                if (UpdateEntryNameInTopics(topic.Subtopics, file, newEntryName))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void AddOrUpdateTopicHierarchy(
        List<Topic> topics,
        string[] topicPath,
        string entryName,
        string file,
        int currentDepth,
        bool sortAlphabetically
    )
    {
        if (currentDepth >= topicPath.Length)
            return;

        var currentTopicName = topicPath[currentDepth];
        var isLastLevel = currentDepth == topicPath.Length - 1;

        // Find existing topic (case-insensitive)
        var existingTopic = topics.FirstOrDefault(t =>
            string.Equals(t.Name, currentTopicName, StringComparison.OrdinalIgnoreCase)
        );

        if (existingTopic == null)
        {
            // Create new topic
            var newTopic = new Topic
            {
                Name = currentTopicName,
                Entries = Array.Empty<Entries>(),
                Subtopics = currentDepth < topicPath.Length - 1 ? Array.Empty<Topic>() : null,
            };

            topics.Add(newTopic);

            // Sort if requested - use natural sort
            if (sortAlphabetically)
            {
                var naturalComparer = new NaturalStringComparer();
                topics.Sort((a, b) => naturalComparer.Compare(a.Name, b.Name));
            }

            existingTopic = newTopic;
        }

        // If we're at the last level of the path, add the file entry to this topic
        if (isLastLevel)
        {
            // Check if file entry already exists
            if (
                !existingTopic.Entries.Any(e =>
                    string.Equals(e.File, file, StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                var entries = existingTopic.Entries.ToList();
                entries.Add(new Entries { Name = entryName, File = file });

                // Sort entries by file name using natural sort
                if (sortAlphabetically)
                {
                    var naturalComparer = new NaturalStringComparer();
                    entries.Sort((a, b) => naturalComparer.Compare(a.File, b.File));
                }

                existingTopic.Entries = entries.ToArray();
            }
            else
            {
                // Entry already exists - static method cannot log
            }
        }
        // Otherwise, process subtopics
        else
        {
            // Ensure subtopics array exists
            if (existingTopic.Subtopics == null)
            {
                existingTopic.Subtopics = Array.Empty<Topic>();
            }

            var subtopics = existingTopic.Subtopics.ToList();
            AddOrUpdateTopicHierarchy(
                subtopics,
                topicPath,
                entryName,
                file,
                currentDepth + 1,
                sortAlphabetically
            );
            existingTopic.Subtopics = subtopics.ToArray();
        }
    }

    /// <summary>
    /// Recursively removes an entry by filename from the topic hierarchy.
    /// Cleans up empty topics after removal.
    /// </summary>
    private static bool RemoveEntryFromTopics(List<Topic> topics, string file)
    {
        for (int i = topics.Count - 1; i >= 0; i--)
        {
            var topic = topics[i];

            // Try removing from this topic's entries
            var entries = topic.Entries.ToList();
            var originalCount = entries.Count;
            entries.RemoveAll(e => string.Equals(e.File, file, StringComparison.OrdinalIgnoreCase));
            if (entries.Count < originalCount)
            {
                topic.Entries = entries.ToArray();

                // Clean up empty topic (no entries and no subtopics)
                if (
                    topic.Entries.Length == 0
                    && (topic.Subtopics == null || topic.Subtopics.Length == 0)
                )
                {
                    topics.RemoveAt(i);
                }

                return true;
            }

            // Recursively search subtopics
            if (topic.Subtopics != null)
            {
                var subtopics = topic.Subtopics.ToList();
                if (RemoveEntryFromTopics(subtopics, file))
                {
                    topic.Subtopics = subtopics.ToArray();

                    // Clean up topic if it's now empty
                    if (topic.Entries.Length == 0 && topic.Subtopics.Length == 0)
                    {
                        topics.RemoveAt(i);
                    }

                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Recursively searches topics for the given file name, returning the matching entry and the
    /// accumulated topic path.
    /// </summary>
    private static (Entries? entry, string[] path) FindInTopics(
        Topic[] topics,
        string fileName,
        List<string> currentPath
    )
    {
        foreach (var topic in topics)
        {
            var pathWithTopic = new List<string>(currentPath) { topic.Name };

            foreach (var entry in topic.Entries)
            {
                if (string.Equals(entry.File, fileName, StringComparison.OrdinalIgnoreCase))
                {
                    return (entry, pathWithTopic.ToArray());
                }
            }

            if (topic.Subtopics is { Length: > 0 })
            {
                var (found, path) = FindInTopics(topic.Subtopics, fileName, pathWithTopic);
                if (found is not null)
                {
                    return (found, path);
                }
            }
        }

        return (null, []);
    }

    /// <summary>
    /// Recursively updates the file reference in all topic and subtopic entries.
    /// </summary>
    private static void UpdateFileInTopics(Topic[] topics, string oldFile, string newFile)
    {
        foreach (var topic in topics)
        {
            foreach (var entry in topic.Entries)
            {
                if (string.Equals(entry.File, oldFile, StringComparison.OrdinalIgnoreCase))
                {
                    entry.File = newFile;
                }
            }

            if (topic.Subtopics is { Length: > 0 })
            {
                UpdateFileInTopics(topic.Subtopics, oldFile, newFile);
            }
        }
    }

    #endregion
}
