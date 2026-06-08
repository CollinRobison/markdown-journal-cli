using markdown_journal_cli.Infrastructure.Validation;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;
using JournalFileSystem = markdown_journal_cli.Infrastructure.FileSystem.FileSystem;

namespace markdown_journal_cli.Tests.Infrastructure.Validation;

/// <summary>
/// Unit tests for <see cref="JournalValidator"/> using the real file system
/// with a temporary directory.
/// </summary>
public class JournalValidatorTests : IDisposable
{
    private readonly JournalValidator _sut;
    private readonly string _tempDirectory;
    private const string MetadataDirName = ".mdjournal";
    private const string TrackingFileName = ".journalindex";
    private const string TocFileName = ".journaltoc";

    public JournalValidatorTests()
    {
        var fileSystem = new JournalFileSystem(NullLogger<JournalFileSystem>.Instance);
        var settings = Options.Create(
            new markdown_journal_cli.JournalSettings
            {
                MetadataDirName = MetadataDirName,
                TrackingFileName = TrackingFileName,
                TocStructureFileName = TocFileName,
            }
        );
        _sut = new JournalValidator(fileSystem, settings);
        _tempDirectory = Path.Combine(
            Path.GetTempPath(),
            $"JournalValidatorTests_{Guid.NewGuid()}"
        );
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, recursive: true);
    }

    [Fact]
    public void ValidateMetadataDirectory_Should_Return_Empty_List_For_Valid_Layout()
    {
        // Given — complete metadata directory with both required files
        var metadataDir = Path.Combine(_tempDirectory, MetadataDirName);
        Directory.CreateDirectory(metadataDir);
        File.WriteAllText(Path.Combine(metadataDir, TrackingFileName), "{}");
        File.WriteAllText(Path.Combine(metadataDir, TocFileName), "{}");

        // When
        var missing = _sut.ValidateMetadataDirectory(_tempDirectory);

        // Then
        missing.ShouldBeEmpty();
    }

    [Fact]
    public void ValidateMetadataDirectory_Should_Report_Missing_Metadata_Directory()
    {
        // Given — no .mdjournal/ directory at all

        // When
        var missing = _sut.ValidateMetadataDirectory(_tempDirectory);

        // Then
        missing.ShouldHaveSingleItem();
        missing[0].ShouldBe(MetadataDirName);
    }

    [Fact]
    public void ValidateMetadataDirectory_Should_Report_Missing_JournalIndex()
    {
        // Given — metadata directory exists but .journalindex is absent
        var metadataDir = Path.Combine(_tempDirectory, MetadataDirName);
        Directory.CreateDirectory(metadataDir);
        File.WriteAllText(Path.Combine(metadataDir, TocFileName), "{}");

        // When
        var missing = _sut.ValidateMetadataDirectory(_tempDirectory);

        // Then
        missing.ShouldHaveSingleItem();
        missing[0].ShouldBe(Path.Combine(MetadataDirName, TrackingFileName));
    }

    [Fact]
    public void ValidateMetadataDirectory_Should_Report_Missing_JournalToc()
    {
        // Given — metadata directory exists but .journaltoc is absent
        var metadataDir = Path.Combine(_tempDirectory, MetadataDirName);
        Directory.CreateDirectory(metadataDir);
        File.WriteAllText(Path.Combine(metadataDir, TrackingFileName), "{}");

        // When
        var missing = _sut.ValidateMetadataDirectory(_tempDirectory);

        // Then
        missing.ShouldHaveSingleItem();
        missing[0].ShouldBe(Path.Combine(MetadataDirName, TocFileName));
    }

    [Fact]
    public void ValidateMetadataDirectory_Should_Report_Both_Missing_When_Directory_Exists_But_Files_Absent()
    {
        // Given — metadata directory exists but both required files are absent
        var metadataDir = Path.Combine(_tempDirectory, MetadataDirName);
        Directory.CreateDirectory(metadataDir);

        // When
        var missing = _sut.ValidateMetadataDirectory(_tempDirectory);

        // Then
        missing.Count.ShouldBe(2);
        missing.ShouldContain(Path.Combine(MetadataDirName, TrackingFileName));
        missing.ShouldContain(Path.Combine(MetadataDirName, TocFileName));
    }
}
