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
        var (journalrcPath, actualDirectory) = GetJournalrcPaths(directory);
        var journalConfName = _journalSettings.JournalConfigFileName;

        if (!_fileSystem.FileExists(journalrcPath))
        {
            string jsonString = JsonSerializer.Serialize(config, opts);
            _fileSystem.CreateFile(actualDirectory, journalConfName, jsonString);
        }
        else
        {
            Console.WriteLine($"{journalConfName} already exists at {journalrcPath}");
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
            Console.WriteLine($"{journalConfName} doesn't exist at {journalrcPath}");
        }
    }

    public void Update(string directory, Action<JournalConfig> config)
    {
        var (journalrcPath, actualDirectory) = GetJournalrcPaths(directory);
        var journalConfName = _journalSettings.JournalConfigFileName;
        
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
        _fileSystem.UpdateFile(actualDirectory, journalConfName, updatedJsonString);
    }

    public JournalConfig? Read(string directory)
    {
        var (journalrcPath, _) = GetJournalrcPaths(directory);
        var journalConfName = _journalSettings.JournalConfigFileName;

        if (!_fileSystem.FileExists(journalrcPath))
        {
            Console.WriteLine($"{journalConfName} doesn't exist at {journalrcPath}");
            return null;
        }

        var json = _fileSystem.GetFileContent(journalrcPath);
        try
        {
            return JsonSerializer.Deserialize<JournalConfig>(json);
        }
        catch (JsonException)
        {
            Console.WriteLine($"Failed to parse {journalConfName} at {journalrcPath}");
            return null;
        }
    }

    public void AddRootEntry(string directory, string name, string file)
    {
        Update(directory, config =>
        {
            // Check if entry already exists
            if (RootEntryExists(config.TableOfContents.RootEntries, file))
            {
                Console.WriteLine($"Root entry '{file}' already exists in journal configuration.");
                return;
            }

            // Add to root entries array
            var existingEntries = config.TableOfContents.RootEntries.ToList();
            existingEntries.Add(new Entries { Name = name, File = file });
            config.TableOfContents.RootEntries = existingEntries.ToArray();
        });
    }

    public void AddTopicEntry(string directory, string[] topicPath, string entryName, string file, int? maxDepth = null, bool sortAlphabetically = true)
    {
        if (topicPath == null || topicPath.Length == 0)
        {
            Console.WriteLine("Topic path cannot be empty.");
            return;
        }

        if (maxDepth.HasValue && topicPath.Length > maxDepth.Value)
        {
            Console.WriteLine($"Topic path depth ({topicPath.Length}) exceeds maximum allowed depth ({maxDepth.Value}).");
            return;
        }

        Update(directory, config =>
        {
            var topics = config.TableOfContents.Structure.Topics.ToList();
            AddOrUpdateTopicHierarchy(topics, topicPath, entryName, file, 0, sortAlphabetically);
            config.TableOfContents.Structure.Topics = topics.ToArray();
        });
    }

    public bool UpdateEntryName(string directory, string file, string newEntryName)
    {
        var config = Read(directory);
        if (config == null)
        {
            return false;
        }

        bool updated = false;

        // Search in root entries
        foreach (var entry in config.TableOfContents.RootEntries)
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
            updated = UpdateEntryNameInTopics(config.TableOfContents.Structure.Topics, file, newEntryName);
        }

        // Save the config if an update was made
        if (updated)
        {
            var actualDirectory = GetJournalrcPaths(directory).directory;
            var journalConfName = _journalSettings.JournalConfigFileName;
            
            string updatedJsonString = JsonSerializer.Serialize(config, opts);
            _fileSystem.UpdateFile(actualDirectory, journalConfName, updatedJsonString);
        }
        else
        {
            Console.WriteLine($"Entry with file '{file}' not found in journal configuration.");
        }

        return updated;
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
        var isFilePath = Path.GetFileName(directoryOrFilePath).Equals(journalConfName, StringComparison.OrdinalIgnoreCase);
        
        var filePath = isFilePath 
            ? directoryOrFilePath 
            : Path.Combine(directoryOrFilePath, journalConfName);
            
        var directory = isFilePath 
            ? Path.GetDirectoryName(directoryOrFilePath) ?? directoryOrFilePath 
            : directoryOrFilePath;
            
        return (filePath, directory);
    }

    private static bool RootEntryExists(Entries[] rootEntries, string file)
    {
        return rootEntries.Any(entry => 
            string.Equals(entry.File, file, StringComparison.OrdinalIgnoreCase));
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

    private static void AddOrUpdateTopicHierarchy(List<Topic> topics, string[] topicPath, string entryName, string file, int currentDepth, bool sortAlphabetically)
    {
        if (currentDepth >= topicPath.Length)
            return;

        var currentTopicName = topicPath[currentDepth];
        var isLastLevel = currentDepth == topicPath.Length - 1;
        
        // Find existing topic (case-insensitive)
        var existingTopic = topics.FirstOrDefault(t => 
            string.Equals(t.Name, currentTopicName, StringComparison.OrdinalIgnoreCase));

        if (existingTopic == null)
        {
            // Create new topic
            var newTopic = new Topic 
            { 
                Name = currentTopicName,
                Entries = Array.Empty<Entries>(),
                Subtopics = currentDepth < topicPath.Length - 1 ? Array.Empty<Topic>() : null
            };

            topics.Add(newTopic);

            // Sort if requested
            if (sortAlphabetically)
            {
                topics.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            }

            existingTopic = newTopic;
        }

        // If we're at the last level of the path, add the file entry to this topic
        if (isLastLevel)
        {
            // Check if file entry already exists
            if (!existingTopic.Entries.Any(e => string.Equals(e.File, file, StringComparison.OrdinalIgnoreCase)))
            {
                var entries = existingTopic.Entries.ToList();
                entries.Add(new Entries { Name = entryName, File = file });
                
                // Sort entries alphabetically by file name
                if (sortAlphabetically)
                {
                    entries.Sort((a, b) => string.Compare(a.File, b.File, StringComparison.OrdinalIgnoreCase));
                }
                
                existingTopic.Entries = entries.ToArray();
            }
            else
            {
                Console.WriteLine($"Entry '{file}' already exists in topic '{currentTopicName}'.");
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
            AddOrUpdateTopicHierarchy(subtopics, topicPath, entryName, file, currentDepth + 1, sortAlphabetically);
            existingTopic.Subtopics = subtopics.ToArray();
        }
    }

    #endregion
}
