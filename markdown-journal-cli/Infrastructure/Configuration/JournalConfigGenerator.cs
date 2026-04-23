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
    IOptions<JournalSettings> journalSettings,
    IJournalTocStructureRepository tocStructureRepository
) : IJournalConfigGenerator
{
    private readonly IFileSystem _fileSystem =
        fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    private readonly ITableOfContentsMarkdownParser _tocParser =
        tocParser ?? throw new ArgumentNullException(nameof(tocParser));
    private readonly IFileTracking _fileTracking =
        fileTracking ?? throw new ArgumentNullException(nameof(fileTracking));
    private readonly IEntryFormatterService _entryFormatter =
        entryFormatter ?? throw new ArgumentNullException(nameof(entryFormatter));
    private readonly IJournalConfiguration _journalConfiguration =
        journalConfiguration ?? throw new ArgumentNullException(nameof(journalConfiguration));
    private readonly JournalSettings _journalSettings = journalSettings.Value;
    private readonly IJournalTocStructureRepository _tocStructureRepository =
        tocStructureRepository ?? throw new ArgumentNullException(nameof(tocStructureRepository));

    private string GetMetadataDir(string directory) =>
        Path.Combine(directory, _journalSettings.MetadataDirName);

    /// <inheritdoc />
    public JournalConfigGenerationResult? GenerateFromTableOfContents(
        string directory,
        string tocFileName,
        string? journalName = null
    )
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("Directory cannot be null or whitespace.", nameof(directory));

        if (string.IsNullOrWhiteSpace(tocFileName))
            throw new ArgumentException("TOC file name cannot be null or whitespace.", nameof(tocFileName));

        var tocFilePath = Path.Combine(directory, $"{tocFileName}{FileConstants.MarkdownExtension}");

        if (!_fileSystem.FileExists(tocFilePath))
            return null;

        var tocContent = _fileSystem.GetFileContent(tocFilePath);
        var entries = _tocParser.ParseTableOfContents(tocContent);

        if (entries.Length == 0)
            return null;

        var config = new JournalConfig
        {
            JournalName = journalName ?? GetJournalNameFromDirectory(directory),
            TableOfContents = new TableOfContents
            {
                File = $"{tocFileName}{FileConstants.MarkdownExtension}",
                Extensions = [FileConstants.MarkdownExtension],
                IgnoreFiles = null,
            },
        };

        var configPath = Path.Combine(directory, _journalSettings.JournalConfigFileName);
        if (_fileSystem.FileExists(configPath))
            _journalConfiguration.Delete(directory);
        _journalConfiguration.Create(directory, config);

        var metadataDir = GetMetadataDir(directory);
        _tocStructureRepository.Save(JournalTocStructure.Empty(), metadataDir);

        foreach (var entry in entries)
            _journalConfiguration.AddEntry(directory, entry.Name, entry.File, topicPath: null);

        config = _journalConfiguration.Read(directory)!;
        var tocStructure = _tocStructureRepository.Load(metadataDir);

        return new JournalConfigGenerationResult
        {
            Config = config,
            Source = "toc",
            FileCount = tocStructure.RootEntries.Length + CountTopicEntries(tocStructure.Structure.Topics),
        };
    }

    /// <inheritdoc />
    public JournalConfigGenerationResult? GenerateFromTrackingIndex(
        string directory,
        string tocFileName,
        string? journalName = null
    )
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("Directory cannot be null or whitespace.", nameof(directory));

        if (string.IsNullOrWhiteSpace(tocFileName))
            throw new ArgumentException("TOC file name cannot be null or whitespace.", nameof(tocFileName));

        var metadataDir = GetMetadataDir(directory);
        var trackingFilePath = Path.Combine(metadataDir, _journalSettings.TrackingFileName);

        if (!_fileSystem.FileExists(trackingFilePath))
            return null;

        var index = _fileTracking.LoadIndex(directory);

        var markdownFiles = index
            .Files.Keys.Where(f => f.EndsWith(FileConstants.MarkdownExtension, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var config = new JournalConfig
        {
            JournalName = journalName ?? GetJournalNameFromDirectory(directory),
            TableOfContents = new TableOfContents
            {
                File = $"{tocFileName}{FileConstants.MarkdownExtension}",
                Extensions = [FileConstants.MarkdownExtension],
                IgnoreFiles = null,
            },
        };

        var configPath = Path.Combine(directory, _journalSettings.JournalConfigFileName);
        if (_fileSystem.FileExists(configPath))
            _journalConfiguration.Delete(directory);
        _journalConfiguration.Create(directory, config);

        _tocStructureRepository.Save(JournalTocStructure.Empty(), metadataDir);

        foreach (var file in markdownFiles)
            _journalConfiguration.AddEntry(directory, string.Empty, file, topicPath: null);

        config = _journalConfiguration.Read(directory)!;
        var tocStructure = _tocStructureRepository.Load(metadataDir);

        return new JournalConfigGenerationResult
        {
            Config = config,
            Source = "tracking",
            FileCount = tocStructure.RootEntries.Length + CountTopicEntries(tocStructure.Structure.Topics),
        };
    }

    /// <inheritdoc />
    public JournalConfigGenerationResult GenerateFromDirectory(
        string directory,
        string tocFileName,
        string? journalName = null
    )
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("Directory cannot be null or whitespace.", nameof(directory));

        if (string.IsNullOrWhiteSpace(tocFileName))
            throw new ArgumentException("TOC file name cannot be null or whitespace.", nameof(tocFileName));

        var markdownFiles = _fileSystem
            .GetFiles(directory, $"*{FileConstants.MarkdownExtension}", SearchOption.TopDirectoryOnly)
            .Select(f => Path.GetFileName(f))
            .Where(f => !string.IsNullOrEmpty(f))
            .Cast<string>()
            .ToList();

        var config = new JournalConfig
        {
            JournalName = journalName ?? GetJournalNameFromDirectory(directory),
            TableOfContents = new TableOfContents
            {
                File = $"{tocFileName}{FileConstants.MarkdownExtension}",
                Extensions = [FileConstants.MarkdownExtension],
                IgnoreFiles = null,
            },
        };

        var configPath = Path.Combine(directory, _journalSettings.JournalConfigFileName);
        if (_fileSystem.FileExists(configPath))
            _journalConfiguration.Delete(directory);
        _journalConfiguration.Create(directory, config);

        var metadataDir = GetMetadataDir(directory);
        _tocStructureRepository.Save(JournalTocStructure.Empty(), metadataDir);

        foreach (var file in markdownFiles)
            _journalConfiguration.AddEntry(directory, null!, file, topicPath: null);

        config = _journalConfiguration.Read(directory)!;
        var tocStructure = _tocStructureRepository.Load(metadataDir);

        return new JournalConfigGenerationResult
        {
            Config = config,
            Source = "directory",
            FileCount = tocStructure.RootEntries.Length + CountTopicEntries(tocStructure.Structure.Topics),
        };
    }

    /// <summary>Recursively counts all entries in topics and their subtopics.</summary>
    private int CountTopicEntries(Topic[] topics)
    {
        var count = 0;
        foreach (var topic in topics)
        {
            count += topic.Entries.Length;
            if (topic.Subtopics != null)
                count += CountTopicEntries(topic.Subtopics);
        }
        return count;
    }

    private string GetJournalNameFromDirectory(string directory)
    {
        var absolutePath = Path.IsPathRooted(directory) ? directory : Path.GetFullPath(directory);
        var dirName = Path.GetFileName(absolutePath);
        return string.IsNullOrEmpty(dirName) ? "MyJournal" : dirName;
    }
}
