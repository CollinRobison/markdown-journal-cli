using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.Configuration.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using System.Text.Json;
using Xunit;
using JournalFileSystem = markdown_journal_cli.Infrastructure.FileSystem.FileSystem;

namespace markdown_journal_cli.Tests.Infrastructure.Configuration;

/// <summary>
/// Unit tests for <see cref="JournalTocStructureRepository"/> using the real file system
/// with a temporary directory — mirrors FileSystemTests pattern.
/// </summary>
public class JournalTocStructureRepositoryTests : IDisposable
{
    private readonly JournalTocStructureRepository _sut;
    private readonly JournalFileSystem _fileSystem;
    private readonly string _tempDirectory;
    private const string TocFileName = ".journaltoc";

    public JournalTocStructureRepositoryTests()
    {
        _fileSystem = new JournalFileSystem(NullLogger<JournalFileSystem>.Instance);
        var settings = Options.Create(new markdown_journal_cli.JournalSettings
        {
            TocStructureFileName = TocFileName,
        });
        _sut = new JournalTocStructureRepository(_fileSystem, settings);
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"TocStructureRepoTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, recursive: true);
    }

    [Fact]
    public void Load_Should_Return_Empty_Structure_When_File_Absent()
    {
        // Given — no .journaltoc file in _tempDirectory

        // When
        var result = _sut.Load(_tempDirectory);

        // Then
        result.ShouldNotBeNull();
        result.Structure.ShouldNotBeNull();
        result.Structure.Topics.ShouldBeEmpty();
        result.RootEntries.ShouldBeEmpty();
    }

    [Fact]
    public void Load_Should_Read_Existing_File_Correctly()
    {
        // Given
        var json = """
            {
              "structure": { "topics": [] },
              "rootEntries": [
                {
                  "name": "Alpha",
                  "file": "Alpha.md",
                  "ignore": false,
                  "extensions": null,
                  "subheading": null
                }
              ]
            }
            """;
        File.WriteAllText(Path.Combine(_tempDirectory, TocFileName), json);

        // When
        var result = _sut.Load(_tempDirectory);

        // Then
        result.RootEntries.ShouldHaveSingleItem();
        result.RootEntries[0].Name.ShouldBe("Alpha");
        result.RootEntries[0].File.ShouldBe("Alpha.md");
    }

    [Fact]
    public void Save_RoundTrip_Should_Produce_Identical_Deserialized_Output()
    {
        // Given
        var original = new JournalTocStructure
        {
            Structure = new Structure { Topics = [] },
            RootEntries =
            [
                new Entries { Name = "Beta", File = "Beta.md" }
            ]
        };

        // When
        _sut.Save(original, _tempDirectory);
        var loaded = _sut.Load(_tempDirectory);

        // Then
        loaded.RootEntries.ShouldHaveSingleItem();
        loaded.RootEntries[0].Name.ShouldBe("Beta");
        loaded.RootEntries[0].File.ShouldBe("Beta.md");
        loaded.Structure.Topics.ShouldBeEmpty();
    }
}
