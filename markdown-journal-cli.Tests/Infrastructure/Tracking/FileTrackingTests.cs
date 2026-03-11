using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Tests.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.Tracking;
using markdown_journal_cli.Infrastructure.Tracking.Models;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace markdown_journal_cli.Tests.Infrastructure.Tracking;

/// <summary>
/// Unit tests for the <see cref="FileTracking"/> class, covering change detection,
/// index management, and file tracking operations.
/// Uses TestFileSystem for in-memory testing.
/// </summary>
public class FileTrackingTests : IDisposable
{
    private readonly TestFileSystem _fileSystem;
    private readonly TestHashService _hashService;
    private readonly FileTracking _fileTracking;
    private readonly string _testPath;
    private readonly string _indexFileName;

    public FileTrackingTests()
    {
        _fileSystem = new TestFileSystem();
        _hashService = new TestHashService();
        _testPath = "/test/journal";

        var journalSettings = new JournalSettings { AppName = "testapp" };
        var options = Options.Create(journalSettings);
        _indexFileName = $".{journalSettings.AppName}";

        _fileTracking = new FileTracking(_fileSystem, options, _hashService);

        // Setup test directory
        _fileSystem.CreateDirectory(_testPath);
    }

    public void Dispose()
    {
        _fileSystem.Reset();
        _hashService.Reset();
    }

    #region LoadIndex Tests

    [Fact]
    public void LoadIndex_Should_Return_Empty_Index_When_File_Does_Not_Exist()
    {
        // When
        var index = _fileTracking.LoadIndex(_testPath);

        // Then
        index.ShouldNotBeNull();
        index.Files.ShouldBeEmpty();
    }

    [Fact]
    public void LoadIndex_Should_Return_Populated_Index_When_File_Exists()
    {
        // Given
        var expectedIndex = new JournalIndex
        {
            Files = new Dictionary<string, FileState>
            {
                ["note1.md"] = new FileState
                {
                    FilePath = "note1.md",
                    Hash = "hash1",
                    LastChecked = DateTime.UtcNow,
                },
            },
        };

        _fileTracking.SaveIndex(expectedIndex, _testPath);

        // When
        var index = _fileTracking.LoadIndex(_testPath);

        // Then
        index.Files.Count.ShouldBe(1);
        index.Files.ContainsKey("note1.md").ShouldBeTrue();
        index.Files["note1.md"].Hash.ShouldBe("hash1");
    }

    [Fact]
    public void LoadIndex_Should_Handle_Malformed_Json_Gracefully()
    {
        // Given
        _fileSystem.CreateFile(_testPath, _indexFileName, "invalid json");

        // When / Then
        Should.Throw<System.Text.Json.JsonException>(() => _fileTracking.LoadIndex(_testPath));
    }

    #endregion

    #region SaveIndex Tests

    [Fact]
    public void SaveIndex_Should_Create_Index_File()
    {
        // Given
        var index = new JournalIndex
        {
            Files = new Dictionary<string, FileState>
            {
                ["test.md"] = new FileState
                {
                    FilePath = "test.md",
                    Hash = "abc123",
                    LastChecked = DateTime.UtcNow,
                },
            },
        };

        // When
        _fileTracking.SaveIndex(index, _testPath);

        // Then
        var indexPath = Path.Combine(_testPath, _indexFileName);
        _fileSystem.FileExists(indexPath).ShouldBeTrue();

        var savedContent = _fileSystem.GetFileContent(indexPath);
        savedContent.ShouldNotBeNull();
        savedContent.ShouldContain("test.md");
        savedContent.ShouldContain("abc123");
    }

    [Fact]
    public void SaveIndex_Should_Use_Indented_Json_Format()
    {
        // Given
        var index = new JournalIndex
        {
            Files = new Dictionary<string, FileState>
            {
                ["test.md"] = new FileState
                {
                    FilePath = "test.md",
                    Hash = "hash",
                    LastChecked = DateTime.UtcNow,
                },
            },
        };

        // When
        _fileTracking.SaveIndex(index, _testPath);

        // Then
        var indexPath = Path.Combine(_testPath, _indexFileName);
        var content = _fileSystem.GetFileContent(indexPath);
        content.ShouldContain("  "); // Should have indentation
    }

    #endregion

    #region DetectChanges Tests

    [Fact]
    public void DetectChanges_Should_Detect_Added_Files()
    {
        // Given - Create new markdown files on disk
        CreateRealMarkdownFile("new-note.md", "Content of new note");
        _hashService.SetHash("/test/journal/new-note.md", "hash-new");

        // When
        var result = _fileTracking.DetectChanges(_testPath);

        // Then
        result.AddedFiles.Count.ShouldBe(1);
        result.AddedFiles.ShouldContain("new-note.md");
        result.ModifiedFiles.ShouldBeEmpty();
        result.DeletedFiles.ShouldBeEmpty();
        result.HasChanges.ShouldBeTrue();
    }

    [Fact]
    public void DetectChanges_Should_Detect_Modified_Files()
    {
        // Given - Create initial index with tracked file
        var oldIndex = new JournalIndex
        {
            Files = new Dictionary<string, FileState>
            {
                ["existing.md"] = new FileState
                {
                    FilePath = "existing.md",
                    Hash = "old-hash",
                    LastChecked = DateTime.UtcNow.AddDays(-1),
                },
            },
        };
        _fileTracking.SaveIndex(oldIndex, _testPath);

        // Create the file on disk with different content/hash
        CreateRealMarkdownFile("existing.md", "Modified content");
        _hashService.SetHash("/test/journal/existing.md", "new-hash");

        // When
        var result = _fileTracking.DetectChanges(_testPath);

        // Then
        result.AddedFiles.ShouldBeEmpty();
        result.ModifiedFiles.Count.ShouldBe(1);
        result.ModifiedFiles.ShouldContain("existing.md");
        result.DeletedFiles.ShouldBeEmpty();
        result.HasChanges.ShouldBeTrue();
    }

    [Fact]
    public void DetectChanges_Should_Detect_Deleted_Files()
    {
        // Given - Index has tracked file, but file doesn't exist on disk
        var oldIndex = new JournalIndex
        {
            Files = new Dictionary<string, FileState>
            {
                ["deleted.md"] = new FileState
                {
                    FilePath = "deleted.md",
                    Hash = "some-hash",
                    LastChecked = DateTime.UtcNow.AddDays(-1),
                },
            },
        };
        _fileTracking.SaveIndex(oldIndex, _testPath);

        // When (no files on disk)
        var result = _fileTracking.DetectChanges(_testPath);

        // Then
        result.AddedFiles.ShouldBeEmpty();
        result.ModifiedFiles.ShouldBeEmpty();
        result.DeletedFiles.Count.ShouldBe(1);
        result.DeletedFiles.ShouldContain("deleted.md");
        result.HasChanges.ShouldBeTrue();
    }

    [Fact]
    public void DetectChanges_Should_Detect_Multiple_Change_Types()
    {
        // Given - Index with some tracked files
        var oldIndex = new JournalIndex
        {
            Files = new Dictionary<string, FileState>
            {
                ["modified.md"] = new FileState
                {
                    FilePath = "modified.md",
                    Hash = "old-hash",
                    LastChecked = DateTime.UtcNow.AddDays(-1),
                },
                ["deleted.md"] = new FileState
                {
                    FilePath = "deleted.md",
                    Hash = "deleted-hash",
                    LastChecked = DateTime.UtcNow.AddDays(-1),
                },
                ["unchanged.md"] = new FileState
                {
                    FilePath = "unchanged.md",
                    Hash = "same-hash",
                    LastChecked = DateTime.UtcNow.AddDays(-1),
                },
            },
        };
        _fileTracking.SaveIndex(oldIndex, _testPath);

        // Create files on disk
        CreateRealMarkdownFile("new.md", "New file");
        _hashService.SetHash("/test/journal/new.md", "new-hash");

        CreateRealMarkdownFile("modified.md", "Modified content");
        _hashService.SetHash("/test/journal/modified.md", "new-modified-hash");

        CreateRealMarkdownFile("unchanged.md", "Same content");
        _hashService.SetHash("/test/journal/unchanged.md", "same-hash");
        // deleted.md is not created, so it's detected as deleted

        // When
        var result = _fileTracking.DetectChanges(_testPath);

        // Then
        result.AddedFiles.Count.ShouldBe(1);
        result.AddedFiles.ShouldContain("new.md");

        result.ModifiedFiles.Count.ShouldBe(1);
        result.ModifiedFiles.ShouldContain("modified.md");

        result.DeletedFiles.Count.ShouldBe(1);
        result.DeletedFiles.ShouldContain("deleted.md");

        result.HasChanges.ShouldBeTrue();
    }

    [Fact]
    public void DetectChanges_Should_Return_No_Changes_When_Nothing_Changed()
    {
        // Given - Index with tracked file
        var oldIndex = new JournalIndex
        {
            Files = new Dictionary<string, FileState>
            {
                ["unchanged.md"] = new FileState
                {
                    FilePath = "unchanged.md",
                    Hash = "same-hash",
                    LastChecked = DateTime.UtcNow.AddDays(-1),
                },
            },
        };
        _fileTracking.SaveIndex(oldIndex, _testPath);

        // File exists with same hash
        CreateRealMarkdownFile("unchanged.md", "Same content");
        _hashService.SetHash("/test/journal/unchanged.md", "same-hash");

        // When
        var result = _fileTracking.DetectChanges(_testPath);

        // Then
        result.AddedFiles.ShouldBeEmpty();
        result.ModifiedFiles.ShouldBeEmpty();
        result.DeletedFiles.ShouldBeEmpty();
        result.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void DetectChanges_Should_Update_Index_With_New_State()
    {
        // Given
        CreateRealMarkdownFile("note.md", "Content");
        _hashService.SetHash("/test/journal/note.md", "content-hash");

        // When
        _fileTracking.DetectChanges(_testPath);

        // Then
        var updatedIndex = _fileTracking.LoadIndex(_testPath);
        updatedIndex.Files.Count.ShouldBe(1);
        updatedIndex.Files.ContainsKey("note.md").ShouldBeTrue();
        updatedIndex.Files["note.md"].Hash.ShouldBe("content-hash");
    }

    [Fact]
    public void DetectChanges_Should_Exclude_Index_File_From_Tracking()
    {
        // Given - Create regular markdown file (not index file)
        CreateRealMarkdownFile("regular.md", "Regular note");
        _hashService.SetHash("/test/journal/regular.md", "regular-hash");

        // Also create the actual index file through SaveIndex to ensure it exists but shouldn't be tracked
        var index = new JournalIndex();
        _fileTracking.SaveIndex(index, _testPath);

        // When
        var result = _fileTracking.DetectChanges(_testPath);

        // Then
        result.AddedFiles.Count.ShouldBe(1);
        result.AddedFiles.ShouldContain("regular.md");
        result.AddedFiles.ShouldNotContain(f => f.Contains(_indexFileName));
    }

    #endregion

    #region DetectChangesWithoutUpdate Tests

    [Fact]
    public void DetectChangesWithoutUpdate_Should_Detect_Changes_Without_Updating_Index()
    {
        // Given - Initial state
        var oldIndex = new JournalIndex
        {
            Files = new Dictionary<string, FileState>
            {
                ["old.md"] = new FileState
                {
                    FilePath = "old.md",
                    Hash = "old-hash",
                    LastChecked = DateTime.UtcNow.AddDays(-1),
                },
            },
        };
        _fileTracking.SaveIndex(oldIndex, _testPath);

        // Create new file on disk
        CreateRealMarkdownFile("new.md", "New content");
        _hashService.SetHash("/test/journal/new.md", "new-hash");

        // When
        var result = _fileTracking.DetectChangesWithoutUpdate(_testPath);

        // Then
        result.AddedFiles.Count.ShouldBe(1);
        result.AddedFiles.ShouldContain("new.md");
        result.DeletedFiles.Count.ShouldBe(1);
        result.DeletedFiles.ShouldContain("old.md");

        // Verify index was NOT updated
        var index = _fileTracking.LoadIndex(_testPath);
        index.Files.Count.ShouldBe(1);
        index.Files.ContainsKey("old.md").ShouldBeTrue();
        index.Files.ContainsKey("new.md").ShouldBeFalse();
    }

    [Fact]
    public void DetectChangesWithoutUpdate_Should_Detect_Modified_Files()
    {
        // Given
        var oldIndex = new JournalIndex
        {
            Files = new Dictionary<string, FileState>
            {
                ["file.md"] = new FileState
                {
                    FilePath = "file.md",
                    Hash = "original-hash",
                    LastChecked = DateTime.UtcNow.AddDays(-1),
                },
            },
        };
        _fileTracking.SaveIndex(oldIndex, _testPath);

        CreateRealMarkdownFile("file.md", "Modified");
        _hashService.SetHash("/test/journal/file.md", "modified-hash");

        // When
        var result = _fileTracking.DetectChangesWithoutUpdate(_testPath);

        // Then
        result.ModifiedFiles.Count.ShouldBe(1);
        result.ModifiedFiles.ShouldContain("file.md");

        // Verify original hash still in index
        var index = _fileTracking.LoadIndex(_testPath);
        index.Files["file.md"].Hash.ShouldBe("original-hash");
    }

    #endregion

    #region UpdateIndex Tests

    [Fact]
    public void UpdateIndex_Should_Create_Index_For_All_Current_Files()
    {
        // Given
        CreateRealMarkdownFile("note1.md", "Content 1");
        CreateRealMarkdownFile("note2.md", "Content 2");
        CreateRealMarkdownFile("note3.md", "Content 3");

        _hashService.SetHash("/test/journal/note1.md", "hash1");
        _hashService.SetHash("/test/journal/note2.md", "hash2");
        _hashService.SetHash("/test/journal/note3.md", "hash3");

        // When
        _fileTracking.UpdateIndex(_testPath);

        // Then
        var index = _fileTracking.LoadIndex(_testPath);
        index.Files.Count.ShouldBe(3);
        index.Files.ContainsKey("note1.md").ShouldBeTrue();
        index.Files.ContainsKey("note2.md").ShouldBeTrue();
        index.Files.ContainsKey("note3.md").ShouldBeTrue();
        index.Files["note1.md"].Hash.ShouldBe("hash1");
        index.Files["note2.md"].Hash.ShouldBe("hash2");
        index.Files["note3.md"].Hash.ShouldBe("hash3");
    }

    [Fact]
    public void UpdateIndex_Should_Replace_Old_Index_Completely()
    {
        // Given - Old index with different files
        var oldIndex = new JournalIndex
        {
            Files = new Dictionary<string, FileState>
            {
                ["old-file.md"] = new FileState
                {
                    FilePath = "old-file.md",
                    Hash = "old-hash",
                    LastChecked = DateTime.UtcNow.AddDays(-1),
                },
            },
        };
        _fileTracking.SaveIndex(oldIndex, _testPath);

        // New files on disk
        CreateRealMarkdownFile("new-file.md", "New content");
        _hashService.SetHash("/test/journal/new-file.md", "new-hash");

        // When
        _fileTracking.UpdateIndex(_testPath);

        // Then
        var index = _fileTracking.LoadIndex(_testPath);
        index.Files.Count.ShouldBe(1);
        index.Files.ContainsKey("new-file.md").ShouldBeTrue();
        index.Files.ContainsKey("old-file.md").ShouldBeFalse();
    }

    [Fact]
    public void UpdateIndex_Should_Handle_Empty_Directory()
    {
        // Given - No markdown files

        // When
        _fileTracking.UpdateIndex(_testPath);

        // Then
        var index = _fileTracking.LoadIndex(_testPath);
        index.Files.ShouldBeEmpty();
    }

    [Fact]
    public void UpdateIndex_Should_Set_LastChecked_Timestamp()
    {
        // Given
        var beforeUpdate = DateTime.UtcNow;
        CreateRealMarkdownFile("note.md", "Content");
        _hashService.SetHash("/test/journal/note.md", "hash");

        // When
        _fileTracking.UpdateIndex(_testPath);

        // Then
        var index = _fileTracking.LoadIndex(_testPath);
        var fileState = index.Files["note.md"];
        fileState.LastChecked.ShouldBeGreaterThanOrEqualTo(beforeUpdate);
        fileState.LastChecked.ShouldBeLessThanOrEqualTo(DateTime.UtcNow.AddSeconds(1));
    }

    #endregion

    #region UpdateFileInIndex Tests

    [Fact]
    public void UpdateFileInIndex_Should_Add_New_File_To_Index()
    {
        // Given - Empty index
        CreateRealMarkdownFile("new.md", "Content");
        _hashService.SetHash("/test/journal/new.md", "new-hash");

        // When
        _fileTracking.UpdateFileInIndex(_testPath, "new.md");

        // Then
        var index = _fileTracking.LoadIndex(_testPath);
        index.Files.Count.ShouldBe(1);
        index.Files.ContainsKey("new.md").ShouldBeTrue();
        index.Files["new.md"].Hash.ShouldBe("new-hash");
    }

    [Fact]
    public void UpdateFileInIndex_Should_Update_Existing_File_Hash()
    {
        // Given - Index with existing file
        var oldIndex = new JournalIndex
        {
            Files = new Dictionary<string, FileState>
            {
                ["existing.md"] = new FileState
                {
                    FilePath = "existing.md",
                    Hash = "old-hash",
                    LastChecked = DateTime.UtcNow.AddDays(-1),
                },
            },
        };
        _fileTracking.SaveIndex(oldIndex, _testPath);

        // File updated on disk
        CreateRealMarkdownFile("existing.md", "Updated content");
        _hashService.SetHash("/test/journal/existing.md", "updated-hash");

        // When
        _fileTracking.UpdateFileInIndex(_testPath, "existing.md");

        // Then
        var index = _fileTracking.LoadIndex(_testPath);
        index.Files["existing.md"].Hash.ShouldBe("updated-hash");
    }

    [Fact]
    public void UpdateFileInIndex_Should_Remove_File_If_Not_Exists_On_Disk()
    {
        // Given - Index with file
        var oldIndex = new JournalIndex
        {
            Files = new Dictionary<string, FileState>
            {
                ["missing.md"] = new FileState
                {
                    FilePath = "missing.md",
                    Hash = "some-hash",
                    LastChecked = DateTime.UtcNow.AddDays(-1),
                },
            },
        };
        _fileTracking.SaveIndex(oldIndex, _testPath);

        // File doesn't exist on disk

        // When
        _fileTracking.UpdateFileInIndex(_testPath, "missing.md");

        // Then
        var index = _fileTracking.LoadIndex(_testPath);
        index.Files.ContainsKey("missing.md").ShouldBeFalse();
    }

    [Fact]
    public void UpdateFileInIndex_Should_Preserve_Other_Files()
    {
        // Given - Index with multiple files
        var oldIndex = new JournalIndex
        {
            Files = new Dictionary<string, FileState>
            {
                ["file1.md"] = new FileState
                {
                    FilePath = "file1.md",
                    Hash = "hash1",
                    LastChecked = DateTime.UtcNow,
                },
                ["file2.md"] = new FileState
                {
                    FilePath = "file2.md",
                    Hash = "hash2",
                    LastChecked = DateTime.UtcNow,
                },
                ["file3.md"] = new FileState
                {
                    FilePath = "file3.md",
                    Hash = "hash3",
                    LastChecked = DateTime.UtcNow,
                },
            },
        };
        _fileTracking.SaveIndex(oldIndex, _testPath);

        CreateRealMarkdownFile("file2.md", "Updated");
        _hashService.SetHash("/test/journal/file2.md", "new-hash2");

        // When
        _fileTracking.UpdateFileInIndex(_testPath, "file2.md");

        // Then
        var index = _fileTracking.LoadIndex(_testPath);
        index.Files.Count.ShouldBe(3);
        index.Files["file1.md"].Hash.ShouldBe("hash1");
        index.Files["file2.md"].Hash.ShouldBe("new-hash2");
        index.Files["file3.md"].Hash.ShouldBe("hash3");
    }

    #endregion

    #region RemoveFileFromIndex Tests

    [Fact]
    public void RemoveFileFromIndex_Should_Remove_File()
    {
        // Given
        var oldIndex = new JournalIndex
        {
            Files = new Dictionary<string, FileState>
            {
                ["to-remove.md"] = new FileState
                {
                    FilePath = "to-remove.md",
                    Hash = "hash",
                    LastChecked = DateTime.UtcNow,
                },
            },
        };
        _fileTracking.SaveIndex(oldIndex, _testPath);

        // When
        _fileTracking.RemoveFileFromIndex(_testPath, "to-remove.md");

        // Then
        var index = _fileTracking.LoadIndex(_testPath);
        index.Files.ContainsKey("to-remove.md").ShouldBeFalse();
    }

    [Fact]
    public void RemoveFileFromIndex_Should_Preserve_Other_Files()
    {
        // Given
        var oldIndex = new JournalIndex
        {
            Files = new Dictionary<string, FileState>
            {
                ["keep.md"] = new FileState
                {
                    FilePath = "keep.md",
                    Hash = "hash1",
                    LastChecked = DateTime.UtcNow,
                },
                ["remove.md"] = new FileState
                {
                    FilePath = "remove.md",
                    Hash = "hash2",
                    LastChecked = DateTime.UtcNow,
                },
            },
        };
        _fileTracking.SaveIndex(oldIndex, _testPath);

        // When
        _fileTracking.RemoveFileFromIndex(_testPath, "remove.md");

        // Then
        var index = _fileTracking.LoadIndex(_testPath);
        index.Files.Count.ShouldBe(1);
        index.Files.ContainsKey("keep.md").ShouldBeTrue();
        index.Files.ContainsKey("remove.md").ShouldBeFalse();
    }

    [Fact]
    public void RemoveFileFromIndex_Should_Handle_Nonexistent_File_Gracefully()
    {
        // Given
        var oldIndex = new JournalIndex
        {
            Files = new Dictionary<string, FileState>
            {
                ["existing.md"] = new FileState
                {
                    FilePath = "existing.md",
                    Hash = "hash",
                    LastChecked = DateTime.UtcNow,
                },
            },
        };
        _fileTracking.SaveIndex(oldIndex, _testPath);

        // When
        _fileTracking.RemoveFileFromIndex(_testPath, "nonexistent.md");

        // Then - Should not throw, index should be unchanged
        var index = _fileTracking.LoadIndex(_testPath);
        index.Files.Count.ShouldBe(1);
        index.Files.ContainsKey("existing.md").ShouldBeTrue();
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a markdown file using the TestFileSystem.
    /// </summary>
    private void CreateRealMarkdownFile(string fileName, string content)
    {
        // Use the TestFileSystem to create markdown files
        _fileSystem.CreateMarkdownFile(_testPath, fileName.Replace(".md", ""), content);
    }

    #endregion
}

/// <summary>
/// Test double for IHashService that provides deterministic hash values for testing.
/// </summary>
public class TestHashService : IHashService
{
    private readonly Dictionary<string, string> _hashes = new();

    public string ComputeFileHash(string filePath)
    {
        if (_hashes.TryGetValue(filePath, out var hash))
        {
            return hash;
        }

        // Return a default hash based on file path for deterministic testing
        return $"hash-{Path.GetFileName(filePath)}";
    }

    public void SetHash(string filePath, string hash)
    {
        _hashes[filePath] = hash;
    }

    public void Reset()
    {
        _hashes.Clear();
    }
}
