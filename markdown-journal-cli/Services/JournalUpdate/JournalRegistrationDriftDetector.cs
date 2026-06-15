using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.Configuration.Models;
using markdown_journal_cli.Infrastructure.Tracking;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace markdown_journal_cli.Services;

/// <summary>
/// Computes registration drift between tracked files and the journal's persisted registration state.
/// </summary>
public sealed class JournalRegistrationDriftDetector(
    IJournalConfiguration journalConfiguration,
    IFileTracking fileTracking,
    IJournalTocStructureRepository tocStructureRepository,
    IOptions<JournalSettings> journalSettings,
    ILogger<JournalRegistrationDriftDetector> logger
) : IJournalRegistrationDriftDetector
{
    private readonly IJournalConfiguration _journalConfiguration =
        journalConfiguration ?? throw new ArgumentNullException(nameof(journalConfiguration));
    private readonly IFileTracking _fileTracking =
        fileTracking ?? throw new ArgumentNullException(nameof(fileTracking));
    private readonly IJournalTocStructureRepository _tocStructureRepository =
        tocStructureRepository ?? throw new ArgumentNullException(nameof(tocStructureRepository));
    private readonly JournalSettings _journalSettings = journalSettings.Value;
    private readonly ILogger<JournalRegistrationDriftDetector> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public JournalRegistrationDriftResult DetectDrift(string journalPath)
    {
        var config = _journalConfiguration.Read(journalPath);
        if (config is null)
            return new JournalRegistrationDriftResult();

        var metadataDir = Path.Combine(journalPath, _journalSettings.MetadataDirName);
        var tocStructure = _tocStructureRepository.Load(metadataDir);

        var trackedFiles = new HashSet<string>(
            _fileTracking.LoadIndex(journalPath).Files.Keys,
            StringComparer.OrdinalIgnoreCase
        );

        var registeredFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in tocStructure.RootEntries)
            registeredFiles.Add(entry.File);
        CollectTopicEntryFiles(tocStructure.Structure.Topics, registeredFiles);
        registeredFiles.UnionWith(config.TableOfContents.IgnoreFiles ?? []);

        _logger.LogDebug(
            "Detecting journal registration drift: {TrackedCount} tracked files, {RegisteredCount} registered files",
            trackedFiles.Count,
            registeredFiles.Count
        );

        var tocFile = config.TableOfContents.File;
        var ignoreFiles = new HashSet<string>(
            config.TableOfContents.IgnoreFiles ?? [],
            StringComparer.OrdinalIgnoreCase
        );

        var filesToAdd = trackedFiles
            .Where(f =>
                !registeredFiles.Contains(f)
                && !string.Equals(f, tocFile, StringComparison.OrdinalIgnoreCase)
            )
            .ToList();

        var filesToRemove = registeredFiles
            .Where(f => !trackedFiles.Contains(f) && !ignoreFiles.Contains(f))
            .ToList();

        return new JournalRegistrationDriftResult
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
}
