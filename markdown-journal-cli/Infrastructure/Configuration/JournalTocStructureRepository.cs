using System.Text.Json;
using markdown_journal_cli.Infrastructure.Configuration.Models;
using markdown_journal_cli.Infrastructure.FileSystem;
using Microsoft.Extensions.Options;

namespace markdown_journal_cli.Infrastructure.Configuration;

/// <summary>
/// Reads and writes <see cref="JournalTocStructure"/> as JSON in
/// <c>.journaltoc</c> inside the journal's <c>.mdjournal/</c> metadata directory.
/// </summary>
public class JournalTocStructureRepository(
    IFileSystem fileSystem,
    IOptions<JournalSettings> journalSettings
) : IJournalTocStructureRepository
{
    private readonly IFileSystem _fileSystem = fileSystem;
    private readonly JsonSerializerOptions _opts = new() { WriteIndented = true };
    private readonly string _tocStructureFileName = journalSettings.Value.TocStructureFileName;

    /// <inheritdoc />
    public JournalTocStructure Load(string metadataDir)
    {
        var filePath = Path.Combine(metadataDir, _tocStructureFileName);
        if (!_fileSystem.FileExists(filePath))
        {
            return JournalTocStructure.Empty();
        }

        var json = _fileSystem.GetFileContent(filePath);
        return JsonSerializer.Deserialize<JournalTocStructure>(json) ?? JournalTocStructure.Empty();
    }

    /// <inheritdoc />
    public void Save(JournalTocStructure structure, string metadataDir)
    {
        var json = JsonSerializer.Serialize(structure, _opts);
        _fileSystem.UpdateFile(metadataDir, _tocStructureFileName, json);
    }
}
