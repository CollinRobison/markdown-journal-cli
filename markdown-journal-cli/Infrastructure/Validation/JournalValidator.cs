using markdown_journal_cli.Infrastructure.FileSystem;
using Microsoft.Extensions.Options;

namespace markdown_journal_cli.Infrastructure.Validation;

/// <summary>
/// Validates that a journal directory has the expected <c>.mdjournal/</c> metadata layout.
/// </summary>
public class JournalValidator(IFileSystem fileSystem, IOptions<JournalSettings> journalSettings)
    : IJournalValidator
{
    private readonly IFileSystem _fileSystem = fileSystem;
    private readonly string _metadataDirName = journalSettings.Value.MetadataDirName;
    private readonly string _trackingFileName = journalSettings.Value.TrackingFileName;
    private readonly string _tocStructureFileName = journalSettings.Value.TocStructureFileName;

    /// <inheritdoc />
    public IReadOnlyList<string> ValidateMetadataDirectory(string journalDir)
    {
        var missing = new List<string>();

        var metadataDir = Path.Combine(journalDir, _metadataDirName);
        if (!_fileSystem.IsDirectory(metadataDir))
        {
            missing.Add(_metadataDirName);
            return missing;
        }

        var indexPath = Path.Combine(metadataDir, _trackingFileName);
        if (!_fileSystem.FileExists(indexPath))
        {
            missing.Add(Path.Combine(_metadataDirName, _trackingFileName));
        }

        var tocPath = Path.Combine(metadataDir, _tocStructureFileName);
        if (!_fileSystem.FileExists(tocPath))
        {
            missing.Add(Path.Combine(_metadataDirName, _tocStructureFileName));
        }

        return missing;
    }
}
