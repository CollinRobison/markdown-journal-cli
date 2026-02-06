using System.Text.RegularExpressions;
using markdown_journal_cli.Infrastructure.Configuration.Models;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.Tracking;
using markdown_journal_cli.Services;
using Microsoft.Extensions.Options;

namespace markdown_journal_cli.Infrastructure.Configuration;

/// <summary>
/// Generates journal configuration from various sources (TOC file, tracking index, or directory scan).
/// </summary>
public class JournalConfigGenerator(
    IFileSystem fileSystem,
    ITableOfContentsMarkdownParser tocParser,
    IFileTracking fileTracking,
    IEntryFormatterService entryFormatter,
    IJournalConfiguration journalConfiguration,
    IOptions<JournalSettings> journalSettings
) : IJournalConfigGenerator
{
    private readonly IFileSystem _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    private readonly ITableOfContentsMarkdownParser _tocParser = tocParser ?? throw new ArgumentNullException(nameof(tocParser));
    private readonly IFileTracking _fileTracking = fileTracking ?? throw new ArgumentNullException(nameof(fileTracking));
    private readonly IEntryFormatterService _entryFormatter = entryFormatter ?? throw new ArgumentNullException(nameof(entryFormatter));
    private readonly IJournalConfiguration _journalConfiguration = journalConfiguration ?? throw new ArgumentNullException(nameof(journalConfiguration));
    private readonly JournalSettings _journalSettings = journalSettings.Value;

    /// <inheritdoc />
    public JournalConfigGenerationResult? GenerateFromTableOfContents(string directory, string tocFileName, string? journalName = null)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("Directory cannot be null or whitespace.", nameof(directory));
        }

        if (string.IsNullOrWhiteSpace(tocFileName))
        {
            throw new ArgumentException("TOC file name cannot be null or whitespace.", nameof(tocFileName));
        }

        var tocFilePath = Path.Combine(directory, $"{tocFileName}.md");
        
        if (!_fileSystem.FileExists(tocFilePath))
        {
            return null;
        }

        var tocContent = _fileSystem.GetFileContent(tocFilePath);
        var entries = _tocParser.ParseTableOfContents(tocContent);

        if (entries.Length == 0)
        {
            return null;
        }

        // Create initial empty config
        var config = new JournalConfig
        {
            JournalName = journalName ?? GetJournalNameFromDirectory(directory),
            TableOfContents = new TableOfContents
            {
                File = $"{tocFileName}.md",
                Extensions = [".md"],
                IgnoreFiles = null,
                Structure = new Structure
                {
                    Topics = Array.Empty<Topic>()
                },
                RootEntries = Array.Empty<Entries>()
            }
        };

        // Delete any existing config and save temp config so we can use AddEntry
        var configPath = Path.Combine(directory, _journalSettings.JournalConfigFileName);
        if (_fileSystem.FileExists(configPath))
        {
            _journalConfiguration.Delete(directory);
        }
        _journalConfiguration.Create(directory, config);

        // Add each file using AddEntry which handles root vs topic logic and name extraction
        foreach (var entry in entries)
        {
            // AddEntry automatically skips TOC files, so no need to check here
            // Use the name from TOC, let AddEntry determine structure from filename
            _journalConfiguration.AddEntry(directory, entry.Name, entry.File, topicPath: null);
        }

        // Read back the populated config
        config = _journalConfiguration.Read(directory)!;

        return new JournalConfigGenerationResult
        {
            Config = config,
            Source = "toc",
            FileCount = config.TableOfContents.RootEntries.Length + CountTopicEntries(config.TableOfContents.Structure.Topics)
        };
    }

    /// <inheritdoc />
    public JournalConfigGenerationResult? GenerateFromTrackingIndex(string directory, string tocFileName, string? journalName = null)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("Directory cannot be null or whitespace.", nameof(directory));
        }

        if (string.IsNullOrWhiteSpace(tocFileName))
        {
            throw new ArgumentException("TOC file name cannot be null or whitespace.", nameof(tocFileName));
        }

        var trackingFilePath = Path.Combine(directory, $".{_journalSettings.AppName}");
        
        if (!_fileSystem.FileExists(trackingFilePath))
        {
            return null;
        }

        var index = _fileTracking.LoadIndex(directory);
        
        if (index.Files.Count == 0)
        {
            return null;
        }

        // Get all markdown files from tracking index
        var markdownFiles = index.Files.Keys
            .Where(f => f.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Create initial empty config
        var config = new JournalConfig
        {
            JournalName = journalName ?? GetJournalNameFromDirectory(directory),
            TableOfContents = new TableOfContents
            {
                File = $"{tocFileName}.md",
                Extensions = [".md"],
                IgnoreFiles = null,
                Structure = new Structure
                {
                    Topics = Array.Empty<Topic>()
                },
                RootEntries = Array.Empty<Entries>()
            }
        };

        // Delete any existing config and save temp config so we can use AddEntry
        var configPath = Path.Combine(directory, _journalSettings.JournalConfigFileName);
        if (_fileSystem.FileExists(configPath))
        {
            _journalConfiguration.Delete(directory);
        }
        _journalConfiguration.Create(directory, config);

        // Add each file using AddEntry which handles root vs topic logic and name extraction
        foreach (var file in markdownFiles)
        {
            // AddEntry automatically skips TOC files, so no need to check here
            // Pass null for both name and topicPath - AddEntry will extract everything from the filename
            _journalConfiguration.AddEntry(directory, null!, file, topicPath: null);
        }

        // Read back the populated config
        config = _journalConfiguration.Read(directory)!;

        return new JournalConfigGenerationResult
        {
            Config = config,
            Source = "tracking",
            FileCount = config.TableOfContents.RootEntries.Length + CountTopicEntries(config.TableOfContents.Structure.Topics)
        };
    }

    /// <inheritdoc />
    public JournalConfigGenerationResult GenerateFromDirectory(string directory, string tocFileName, string? journalName = null)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("Directory cannot be null or whitespace.", nameof(directory));
        }

        if (string.IsNullOrWhiteSpace(tocFileName))
        {
            throw new ArgumentException("TOC file name cannot be null o r whitespace.", nameof(tocFileName));
        }

        // Get all markdown files in directory
        var markdownFiles = _fileSystem.GetFiles(directory, "*.md", SearchOption.TopDirectoryOnly)
            .Select(f => Path.GetFileName(f))
            .Where(f => !string.IsNullOrEmpty(f))
            .Cast<string>()
            .ToList();

        // Create initial empty config
        var config = new JournalConfig
        {
            JournalName = journalName ?? GetJournalNameFromDirectory(directory),
            TableOfContents = new TableOfContents
            {
                File = $"{tocFileName}.md",
                Extensions = [".md"],
                IgnoreFiles = null,
                Structure = new Structure
                {
                    Topics = Array.Empty<Topic>()
                },
                RootEntries = Array.Empty<Entries>()
            }
        };

        // Delete any existing config and save temp config so we can use AddEntry
        var configPath = Path.Combine(directory, _journalSettings.JournalConfigFileName);
        if (_fileSystem.FileExists(configPath))
        {
            _journalConfiguration.Delete(directory);
        }
        _journalConfiguration.Create(directory, config);

        // Add each file using AddEntry which handles root vs topic logic and name extraction
        foreach (var file in markdownFiles)
        {
            // AddEntry automatically skips TOC files, so no need to check here
            // Pass null for both name and topicPath - AddEntry will extract everything from the filename
            _journalConfiguration.AddEntry(directory, null!, file, topicPath: null);
        }

        // Read back the populated config
        config = _journalConfiguration.Read(directory)!;

        return new JournalConfigGenerationResult
        {
            Config = config,
            Source = "directory",
            FileCount = config.TableOfContents.RootEntries.Length + CountTopicEntries(config.TableOfContents.Structure.Topics)
        };
    }

    /// <summary>
    /// Recursively counts all entries in topics and their subtopics.
    /// </summary>
    private int CountTopicEntries(Topic[] topics)
    {
        var count = 0;
        foreach (var topic in topics)
        {
            count += topic.Entries.Length;
            if (topic.Subtopics != null)
            {
                count += CountTopicEntries(topic.Subtopics);
            }
        }
        return count;
    }

    private string GetJournalNameFromDirectory(string directory)
    {
        // Convert to absolute path if relative
        var absolutePath = Path.IsPathRooted(directory) 
            ? directory 
            : Path.GetFullPath(directory);
        
        // Get the directory name from the absolute path
        var dirName = Path.GetFileName(absolutePath);
        
        return string.IsNullOrEmpty(dirName) ? "MyJournal" : dirName;
    }
}
