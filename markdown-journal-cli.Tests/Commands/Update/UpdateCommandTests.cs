using markdown_journal_cli.Commands.Update;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.Tracking;
using markdown_journal_cli.Infrastructure.Tracking.Models;
using markdown_journal_cli.Services;
using markdown_journal_cli.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Spectre.Console.Cli;
using Spectre.Console.Testing;

namespace markdown_journal_cli.Tests.Commands.Update;

public class UpdateCommandTests
{
    private readonly TestConsole _console;
    private readonly TestFileSystem _fileSystem;
    private readonly TestHashService _hashService;
    private readonly FileTracking _fileTracking;
    private readonly JournalConfiguration _journalConfiguration;
    private readonly TableOfContentsService _tableOfContentsGenerator;
    private readonly IOptions<JournalSettings> _journalSettings;
    private readonly string _testPath;

    public UpdateCommandTests()
    {
        _console = new TestConsole();
        _fileSystem = new TestFileSystem();
        _hashService = new TestHashService();
        _testPath = "/test/journal";

        _journalSettings = Options.Create(
            new JournalSettings
            {
                AppName = "testapp",
                JournalConfigFileName = ".journalrc",
                TableOfContentsFileName = "1a-TableOfContents",
                TableOfContentsTitle = "Table of Contents",
                DateFormat = "MM/dd/yyyy",
            }
        );

        _fileTracking = new FileTracking(_fileSystem, _journalSettings, _hashService);

        _journalConfiguration = new JournalConfiguration(
            _fileSystem,
            _journalSettings,
            NullLogger<JournalConfiguration>.Instance
        );

        _tableOfContentsGenerator = new TableOfContentsService(
            _fileSystem,
            _journalConfiguration,
            _journalSettings,
            NullLogger<TableOfContentsService>.Instance
        );

        _fileSystem.CreateDirectory(_testPath);
        SetupJournalConfig();
    }

    private UpdateCommand CreateCommand()
    {
        return new UpdateCommand(
            _console,
            _fileSystem,
            _fileTracking,
            _journalConfiguration,
            _tableOfContentsGenerator,
            _journalSettings
        );
    }

    private static CommandContext CreateCommandContext()
    {
        return new CommandContext([], Mock.Of<IRemainingArguments>(), "update", null);
    }

    #region No Changes

    [Fact]
    public void Execute_ReturnsZero_AndPrintsUpToDate_WhenNoChanges()
    {
        // Arrange — create a file and build the index so there are no diffs
        var filePath = Path.Combine(_testPath, "note.md");
        _fileSystem.CreateFile(
            _testPath,
            "note.md",
            "Created: 01/01/2024\nLast Edited: 01/01/2024\n\n# Note"
        );
        _hashService.SetHash(filePath, "hash-a");
        _fileTracking.UpdateIndex(_testPath);

        var command = CreateCommand();
        var settings = new UpdateJournalSettings { FilePath = _testPath };

        // Act
        var result = command.Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(0);
        _console.Output.ShouldContain("up to date");
    }

    #endregion

    #region Date Updates

    [Fact]
    public void Execute_UpdatesLastEditedDate_ForModifiedFiles()
    {
        // Arrange — create file and index with hash-a, then change hash to hash-b
        var filePath = Path.Combine(_testPath, "note.md");
        _fileSystem.CreateFile(
            _testPath,
            "note.md",
            "Created: 01/01/2024\nLast Edited: 01/01/2024\n\n# Note"
        );
        _hashService.SetHash(filePath, "hash-a");
        _fileTracking.UpdateIndex(_testPath);

        // Simulate modification by changing the hash
        _hashService.SetHash(filePath, "hash-b");

        var command = CreateCommand();
        var settings = new UpdateJournalSettings { FilePath = _testPath };

        // Act
        var result = command.Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(0);
        var updatedContent = _fileSystem.GetFileContent(filePath);
        updatedContent.ShouldNotBeNull();
        updatedContent.ShouldContain("Last Edited:");
        updatedContent.ShouldNotContain("Last Edited: 01/01/2024");
        updatedContent.ShouldContain("Created: 01/01/2024");
        _console.Output.ShouldContain("Updated");
    }

    [Fact]
    public void Execute_UpdatesLastEditedDate_WhenDateFlagIsSet()
    {
        // Arrange
        var filePath = Path.Combine(_testPath, "note.md");
        _fileSystem.CreateFile(
            _testPath,
            "note.md",
            "Created: 01/01/2024\nLast Edited: 01/01/2024\n\n# Note"
        );
        _hashService.SetHash(filePath, "hash-a");
        _fileTracking.UpdateIndex(_testPath);
        _hashService.SetHash(filePath, "hash-b");

        var command = CreateCommand();
        var settings = new UpdateJournalSettings { FilePath = _testPath, DateFlag = true };

        // Act
        var result = command.Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(0);
        var updatedContent = _fileSystem.GetFileContent(filePath);
        updatedContent.ShouldNotBeNull();
        updatedContent.ShouldNotContain("Last Edited: 01/01/2024");
    }

    [Fact]
    public void Execute_DoesNotUpdateDates_WhenOnlyConfigFlagIsSet()
    {
        // Arrange
        var filePath = Path.Combine(_testPath, "note.md");
        _fileSystem.CreateFile(
            _testPath,
            "note.md",
            "Created: 01/01/2024\nLast Edited: 01/01/2024\n\n# Note"
        );
        _hashService.SetHash(filePath, "hash-a");
        _fileTracking.UpdateIndex(_testPath);
        _hashService.SetHash(filePath, "hash-b");

        var command = CreateCommand();
        var settings = new UpdateJournalSettings { FilePath = _testPath, ConfigFlag = true };

        // Act
        var result = command.Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(0);
        var updatedContent = _fileSystem.GetFileContent(filePath);
        updatedContent.ShouldNotBeNull();
        // Date should NOT have been updated since only --config was set
        updatedContent.ShouldContain("Last Edited: 01/01/2024");
    }

    [Fact]
    public void Execute_DoesNotUpdateDates_WhenOnlyTocFlagIsSet()
    {
        // Arrange
        var filePath = Path.Combine(_testPath, "note.md");
        _fileSystem.CreateFile(
            _testPath,
            "note.md",
            "Created: 01/01/2024\nLast Edited: 01/01/2024\n\n# Note"
        );
        _hashService.SetHash(filePath, "hash-a");
        _fileTracking.UpdateIndex(_testPath);
        _hashService.SetHash(filePath, "hash-b");

        var command = CreateCommand();
        var settings = new UpdateJournalSettings { FilePath = _testPath, TocFlag = true };

        // Act
        var result = command.Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(0);
        var updatedContent = _fileSystem.GetFileContent(filePath);
        updatedContent.ShouldNotBeNull();
        updatedContent.ShouldContain("Last Edited: 01/01/2024");
    }

    #endregion

    #region Tracking Index Only

    [Fact]
    public void Execute_UpdatesTrackingOnly_WithoutModifyingMetadata_WhenTrackingFlagSet()
    {
        // Arrange — create file and index with hash-a, then change hash to hash-b
        var filePath = Path.Combine(_testPath, "note.md");
        _fileSystem.CreateFile(
            _testPath,
            "note.md",
            "Created: 01/01/2024\nLast Edited: 01/01/2024\n\n# Note"
        );
        _hashService.SetHash(filePath, "hash-a");
        _fileTracking.UpdateIndex(_testPath);

        // Simulate modification by changing the hash
        _hashService.SetHash(filePath, "hash-b");

        var command = CreateCommand();
        var settings = new UpdateJournalSettings { FilePath = _testPath, Tracking = true };

        // Act
        var result = command.Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(0);
        var updatedContent = _fileSystem.GetFileContent(filePath);
        updatedContent.ShouldNotBeNull();
        // Date should NOT have been updated - tracking only
        updatedContent.ShouldContain("Last Edited: 01/01/2024");
        updatedContent.ShouldContain("Created: 01/01/2024");
        _console.Output.ShouldContain("Updated:");

        // But tracking index should be updated - verify no changes on next check
        var changeResults = _fileTracking.DetectChangesWithoutUpdate(_testPath);
        changeResults.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void Execute_TrackingFlagOverridesDateFlag_DoesNotUpdateMetadata()
    {
        // Arrange
        var filePath = Path.Combine(_testPath, "note.md");
        _fileSystem.CreateFile(
            _testPath,
            "note.md",
            "Created: 01/01/2024\nLast Edited: 01/01/2024\n\n# Note"
        );
        _hashService.SetHash(filePath, "hash-a");
        _fileTracking.UpdateIndex(_testPath);
        _hashService.SetHash(filePath, "hash-b");

        var command = CreateCommand();
        // Both flags set - tracking should override date
        var settings = new UpdateJournalSettings
        {
            FilePath = _testPath,
            DateFlag = true,
            Tracking = true,
        };

        // Act
        var result = command.Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(0);
        var updatedContent = _fileSystem.GetFileContent(filePath);
        updatedContent.ShouldNotBeNull();
        // Date should NOT be updated because tracking flag overrides
        updatedContent.ShouldContain("Last Edited: 01/01/2024");
    }

    [Fact]
    public void Execute_TrackingFlag_HandlesAddedFiles()
    {
        // Arrange — start with empty index, add a file
        _fileTracking.UpdateIndex(_testPath);

        var filePath = Path.Combine(_testPath, "new-note.md");
        _fileSystem.CreateFile(
            _testPath,
            "new-note.md",
            "Created: 01/01/2024\nLast Edited: 01/01/2024\n\n# New Note"
        );
        _hashService.SetHash(filePath, "hash-new");

        var command = CreateCommand();
        var settings = new UpdateJournalSettings { FilePath = _testPath, Tracking = true };

        // Act
        var result = command.Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(0);
        _console.Output.ShouldContain("Tracked:");

        // Verify file is now tracked
        var changeResults = _fileTracking.DetectChangesWithoutUpdate(_testPath);
        changeResults.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void Execute_TrackingFlag_HandlesDeletedFiles()
    {
        // Arrange — create file, index it, then delete it
        var filePath = Path.Combine(_testPath, "note.md");
        _fileSystem.CreateFile(
            _testPath,
            "note.md",
            "Created: 01/01/2024\nLast Edited: 01/01/2024\n\n# Note"
        );
        _hashService.SetHash(filePath, "hash-a");
        _fileTracking.UpdateIndex(_testPath);

        // Delete the file
        _fileSystem.DeleteFile(filePath);

        var command = CreateCommand();
        var settings = new UpdateJournalSettings { FilePath = _testPath, Tracking = true };

        // Act
        var result = command.Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(0);
        _console.Output.ShouldContain("Removed:");

        // Verify file is removed from tracking
        var changeResults = _fileTracking.DetectChangesWithoutUpdate(_testPath);
        changeResults.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void Execute_TrackingFlag_DoesNotUpdateConfig()
    {
        // Arrange — add a new file
        _fileTracking.UpdateIndex(_testPath);

        var filePath = Path.Combine(_testPath, "new-note.md");
        _fileSystem.CreateFile(
            _testPath,
            "new-note.md",
            "Created: 01/01/2024\nLast Edited: 01/01/2024\n\n# New Note"
        );
        _hashService.SetHash(filePath, "hash-new");

        var command = CreateCommand();
        var settings = new UpdateJournalSettings { FilePath = _testPath, Tracking = true };

        // Act
        var result = command.Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(0);
        // Config should NOT be updated when only tracking flag is set
        _console.Output.ShouldNotContain("Config added:");

        // Verify config doesn't have the new entry
        var config = _journalConfiguration.Read(_testPath);
        config.ShouldNotBeNull();
        var allEntries = config
            .TableOfContents.RootEntries.Concat(
                config.TableOfContents.Structure.Topics.SelectMany(t => t.Entries)
            )
            .ToList();
        allEntries.ShouldNotContain(e => e.File == "new-note.md");
    }

    [Fact]
    public void Execute_TrackingFlag_DoesNotUpdateToc()
    {
        // Arrange
        var filePath = Path.Combine(_testPath, "note.md");
        _fileSystem.CreateFile(
            _testPath,
            "note.md",
            "Created: 01/01/2024\nLast Edited: 01/01/2024\n\n# Note"
        );
        _hashService.SetHash(filePath, "hash-a");
        _fileTracking.UpdateIndex(_testPath);
        _hashService.SetHash(filePath, "hash-b");

        // Get initial TOC state
        var tocPath = Path.Combine(_testPath, "1a-TableOfContents.md");
        var initialTocContent = _fileSystem.FileExists(tocPath)
            ? _fileSystem.GetFileContent(tocPath)
            : null;

        var command = CreateCommand();
        var settings = new UpdateJournalSettings { FilePath = _testPath, Tracking = true };

        // Act
        var result = command.Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(0);
        _console.Output.ShouldNotContain("Table of contents updated");

        // TOC should be unchanged (or not exist if it didn't before)
        var finalTocContent = _fileSystem.FileExists(tocPath)
            ? _fileSystem.GetFileContent(tocPath)
            : null;
        finalTocContent.ShouldBe(initialTocContent);
    }

    [Fact]
    public void Execute_TrackingFlag_WorksWithMultipleFiles()
    {
        // Arrange
        var file1 = Path.Combine(_testPath, "note1.md");
        var file2 = Path.Combine(_testPath, "note2.md");
        var file3 = Path.Combine(_testPath, "note3.md");

        _fileSystem.CreateFile(
            _testPath,
            "note1.md",
            "Created: 01/01/2024\nLast Edited: 01/01/2024\n\n# Note 1"
        );
        _fileSystem.CreateFile(
            _testPath,
            "note2.md",
            "Created: 01/01/2024\nLast Edited: 01/01/2024\n\n# Note 2"
        );
        _fileSystem.CreateFile(
            _testPath,
            "note3.md",
            "Created: 01/01/2024\nLast Edited: 01/01/2024\n\n# Note 3"
        );

        _hashService.SetHash(file1, "hash-a");
        _hashService.SetHash(file2, "hash-b");
        _hashService.SetHash(file3, "hash-c");
        _fileTracking.UpdateIndex(_testPath);

        // Modify all files
        _hashService.SetHash(file1, "hash-a-modified");
        _hashService.SetHash(file2, "hash-b-modified");
        _hashService.SetHash(file3, "hash-c-modified");

        var command = CreateCommand();
        var settings = new UpdateJournalSettings { FilePath = _testPath, Tracking = true };

        // Act
        var result = command.Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(0);

        // None of the files should have updated dates
        var content1 = _fileSystem.GetFileContent(file1);
        content1.ShouldContain("Last Edited: 01/01/2024");

        var content2 = _fileSystem.GetFileContent(file2);
        content2.ShouldContain("Last Edited: 01/01/2024");

        var content3 = _fileSystem.GetFileContent(file3);
        content3.ShouldContain("Last Edited: 01/01/2024");

        // But tracking should be updated
        var changeResults = _fileTracking.DetectChangesWithoutUpdate(_testPath);
        changeResults.HasChanges.ShouldBeFalse();
    }

    #endregion

    #region Multiple Files

    [Fact]
    public void Execute_UpdatesMultipleModifiedFiles()
    {
        // Arrange
        var file1 = Path.Combine(_testPath, "note1.md");
        var file2 = Path.Combine(_testPath, "note2.md");
        var file3 = Path.Combine(_testPath, "note3.md");

        _fileSystem.CreateFile(
            _testPath,
            "note1.md",
            "Created: 01/01/2024\nLast Edited: 01/01/2024\n\n# Note 1"
        );
        _fileSystem.CreateFile(
            _testPath,
            "note2.md",
            "Created: 01/01/2024\nLast Edited: 01/01/2024\n\n# Note 2"
        );
        _fileSystem.CreateFile(
            _testPath,
            "note3.md",
            "Created: 01/01/2024\nLast Edited: 01/01/2024\n\n# Note 3"
        );

        _hashService.SetHash(file1, "hash-a");
        _hashService.SetHash(file2, "hash-b");
        _hashService.SetHash(file3, "hash-c");
        _fileTracking.UpdateIndex(_testPath);

        // Modify only files 1 and 3
        _hashService.SetHash(file1, "hash-a-modified");
        _hashService.SetHash(file3, "hash-c-modified");

        var command = CreateCommand();
        var settings = new UpdateJournalSettings { FilePath = _testPath };

        // Act
        var result = command.Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(0);

        // Files 1 and 3 should be updated
        var content1 = _fileSystem.GetFileContent(file1);
        content1.ShouldNotContain("Last Edited: 01/01/2024");

        var content3 = _fileSystem.GetFileContent(file3);
        content3.ShouldNotContain("Last Edited: 01/01/2024");

        // File 2 should be untouched
        var content2 = _fileSystem.GetFileContent(file2);
        content2.ShouldContain("Last Edited: 01/01/2024");

        _console.Output.ShouldContain("2 file(s)");
    }

    #endregion

    #region Tracking Index Updated After Date Edit

    [Fact]
    public void Execute_UpdatesTrackingIndex_AfterDateEdit()
    {
        // Arrange
        var filePath = Path.Combine(_testPath, "note.md");
        _fileSystem.CreateFile(
            _testPath,
            "note.md",
            "Created: 01/01/2024\nLast Edited: 01/01/2024\n\n# Note"
        );
        _hashService.SetHash(filePath, "hash-a");
        _fileTracking.UpdateIndex(_testPath);

        _hashService.SetHash(filePath, "hash-b");

        var command = CreateCommand();
        var settings = new UpdateJournalSettings { FilePath = _testPath };

        // Act
        command.Execute(CreateCommandContext(), settings);

        // Assert — after the update, a second run should show no changes
        var secondResult = _fileTracking.DetectChangesWithoutUpdate(_testPath);
        secondResult.HasChanges.ShouldBeFalse();
    }

    #endregion

    #region Date Format

    [Fact]
    public void Execute_UsesConfiguredDateFormat()
    {
        // Arrange
        var customSettings = Options.Create(
            new JournalSettings
            {
                AppName = "testapp",
                JournalConfigFileName = ".journalrc",
                TableOfContentsFileName = "1a-TableOfContents",
                TableOfContentsTitle = "Table of Contents",
                DateFormat = "yyyy-MM-dd",
            }
        );
        var tracking = new FileTracking(_fileSystem, customSettings, _hashService);

        var filePath = Path.Combine(_testPath, "note.md");
        _fileSystem.CreateFile(
            _testPath,
            "note.md",
            "Created: 01/01/2024\nLast Edited: 01/01/2024\n\n# Note"
        );
        _hashService.SetHash(filePath, "hash-a");
        tracking.UpdateIndex(_testPath);
        _hashService.SetHash(filePath, "hash-b");

        var customConfig = new JournalConfiguration(
            _fileSystem,
            customSettings,
            NullLogger<JournalConfiguration>.Instance
        );
        var customTocGen = new TableOfContentsService(_fileSystem, customConfig, customSettings, NullLogger<TableOfContentsService>.Instance);
        var command = new UpdateCommand(
            _console,
            _fileSystem,
            tracking,
            customConfig,
            customTocGen,
            customSettings
        );
        var settings = new UpdateJournalSettings { FilePath = _testPath };

        // Act
        command.Execute(CreateCommandContext(), settings);

        // Assert
        var updatedContent = _fileSystem.GetFileContent(filePath);
        updatedContent.ShouldNotBeNull();
        // Should use yyyy-MM-dd format
        updatedContent.ShouldContain($"Last Edited: {DateTime.Now:yyyy-MM-dd}");
    }

    #endregion

    #region Added Files (no date update)

    [Fact]
    public void Execute_DoesNotUpdateDates_ForAddedFiles_ButTracksThemInIndex()
    {
        // Arrange — start with an empty index, add a file (it's "added" not "modified")
        _fileTracking.UpdateIndex(_testPath); // empty index

        var filePath = Path.Combine(_testPath, "new-note.md");
        _fileSystem.CreateFile(
            _testPath,
            "new-note.md",
            "Created: 01/01/2024\nLast Edited: 01/01/2024\n\n# New Note"
        );
        _hashService.SetHash(filePath, "hash-new");

        var command = CreateCommand();
        var settings = new UpdateJournalSettings { FilePath = _testPath };

        // Act
        var result = command.Execute(CreateCommandContext(), settings);

        // Assert — the file is "added" but Last Edited should not be changed
        result.ShouldBe(0);
        var content = _fileSystem.GetFileContent(filePath);
        content.ShouldContain("Last Edited: 01/01/2024");

        // The file should now be tracked in the index
        var secondResult = _fileTracking.DetectChangesWithoutUpdate(_testPath);
        secondResult.AddedFiles.ShouldNotContain("new-note.md");
        _console.Output.ShouldContain("Tracked");
    }

    #endregion

    #region Deleted Files

    [Fact]
    public void Execute_RemovesDeletedFiles_FromTrackingIndex()
    {
        // Arrange — create a file, index it, then delete it
        var filePath = Path.Combine(_testPath, "doomed.md");
        _fileSystem.CreateFile(
            _testPath,
            "doomed.md",
            "Created: 01/01/2024\nLast Edited: 01/01/2024\n\n# Doomed"
        );
        _hashService.SetHash(filePath, "hash-doomed");
        _fileTracking.UpdateIndex(_testPath);

        // Delete the file from disk
        _fileSystem.DeleteFile(filePath);

        var command = CreateCommand();
        var settings = new UpdateJournalSettings { FilePath = _testPath };

        // Act
        var result = command.Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(0);
        _console.Output.ShouldContain("Removed");

        // The file should no longer be in the index
        var secondResult = _fileTracking.DetectChangesWithoutUpdate(_testPath);
        secondResult.DeletedFiles.ShouldBeEmpty();
    }

    [Fact]
    public void Execute_HandlesAllChangeTypes_Simultaneously()
    {
        // Arrange — set up modified, added, and deleted files
        var modifiedFile = Path.Combine(_testPath, "modified.md");
        var deletedFile = Path.Combine(_testPath, "deleted.md");

        _fileSystem.CreateFile(
            _testPath,
            "modified.md",
            "Created: 01/01/2024\nLast Edited: 01/01/2024\n\n# Modified"
        );
        _fileSystem.CreateFile(
            _testPath,
            "deleted.md",
            "Created: 01/01/2024\nLast Edited: 01/01/2024\n\n# Deleted"
        );
        _hashService.SetHash(modifiedFile, "hash-m");
        _hashService.SetHash(deletedFile, "hash-d");
        _fileTracking.UpdateIndex(_testPath);

        // Modify one, delete one, add one
        _hashService.SetHash(modifiedFile, "hash-m-changed");
        _fileSystem.DeleteFile(deletedFile);
        var addedFile = Path.Combine(_testPath, "added.md");
        _fileSystem.CreateFile(_testPath, "added.md", "# Added\n\nNew file");
        _hashService.SetHash(addedFile, "hash-a");

        var command = CreateCommand();
        var settings = new UpdateJournalSettings { FilePath = _testPath };

        // Act
        var result = command.Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(0);

        // Modified file should have updated date
        var modifiedContent = _fileSystem.GetFileContent(modifiedFile);
        modifiedContent.ShouldNotContain("Last Edited: 01/01/2024");

        // Added file should be unchanged in content
        var addedContent = _fileSystem.GetFileContent(addedFile);
        addedContent.ShouldContain("# Added");

        // All should be synced — no changes on second run
        var secondResult = _fileTracking.DetectChangesWithoutUpdate(_testPath);
        secondResult.HasChanges.ShouldBeFalse();

        _console.Output.ShouldContain("Updated");
        _console.Output.ShouldContain("Tracked");
        _console.Output.ShouldContain("Removed");
    }

    #endregion

    #region File Without Metadata

    [Fact]
    public void Execute_InsertsLastEditedDate_WhenFileHasNoMetadata()
    {
        // Arrange
        var filePath = Path.Combine(_testPath, "bare.md");
        _fileSystem.CreateFile(_testPath, "bare.md", "# Just a title\n\nSome content");
        _hashService.SetHash(filePath, "hash-a");
        _fileTracking.UpdateIndex(_testPath);
        _hashService.SetHash(filePath, "hash-b");

        var command = CreateCommand();
        var settings = new UpdateJournalSettings { FilePath = _testPath };

        // Act
        var result = command.Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(0);
        var updatedContent = _fileSystem.GetFileContent(filePath);
        updatedContent.ShouldNotBeNull();
        updatedContent.ShouldContain("Last Edited:");
        updatedContent.ShouldContain("# Just a title");
    }

    #endregion

    #region Config Update

    private void SetupJournalConfig()
    {
        var config = new markdown_journal_cli.Infrastructure.Configuration.Models.JournalConfig
        {
            JournalName = "Test Journal",
            TableOfContents =
                new markdown_journal_cli.Infrastructure.Configuration.Models.TableOfContents
                {
                    File = "1a-TableOfContents.md",
                    Extensions = [".md"],
                    Structure =
                        new markdown_journal_cli.Infrastructure.Configuration.Models.Structure
                        {
                            Topics = [],
                        },
                    RootEntries = [],
                },
        };
        _journalConfiguration.Create(_testPath, config);
    }

    [Fact]
    public void Execute_AddsNewFilesToConfig_WhenConfigFlagSet()
    {
        // Arrange
        SetupJournalConfig();
        _fileTracking.UpdateIndex(_testPath); // empty index

        var filePath = Path.Combine(_testPath, "Learning-Rust.md");
        _fileSystem.CreateFile(_testPath, "Learning-Rust.md", "# Rust\n\nContent");
        _hashService.SetHash(filePath, "hash-new");

        var command = CreateCommand();
        var settings = new UpdateJournalSettings { FilePath = _testPath, ConfigFlag = true };

        // Act
        var result = command.Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(0);
        var config = _journalConfiguration.Read(_testPath);
        config.ShouldNotBeNull();
        var learningTopic = config.TableOfContents.Structure.Topics.FirstOrDefault(t =>
            t.Name == "Learning"
        );
        learningTopic.ShouldNotBeNull();
        learningTopic.Entries.Any(e => e.File == "Learning-Rust.md").ShouldBeTrue();
    }

    [Fact]
    public void Execute_RemovesDeletedFilesFromConfig_WhenConfigFlagSet()
    {
        // Arrange
        SetupJournalConfig();

        var filePath = Path.Combine(_testPath, "Learning-Rust.md");
        _fileSystem.CreateFile(_testPath, "Learning-Rust.md", "# Rust\n\nContent");
        _hashService.SetHash(filePath, "hash-a");
        _fileTracking.UpdateIndex(_testPath);

        // Add the file to config so we can verify removal
        _journalConfiguration.AddEntry(_testPath, string.Empty, "Learning-Rust.md");

        // Delete the file
        _fileSystem.DeleteFile(filePath);

        var command = CreateCommand();
        var settings = new UpdateJournalSettings { FilePath = _testPath, ConfigFlag = true };

        // Act
        var result = command.Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(0);
        var config = _journalConfiguration.Read(_testPath);
        config.ShouldNotBeNull();
        config
            .TableOfContents.Structure.Topics.Any(t =>
                t.Entries.Any(e => e.File == "Learning-Rust.md")
            )
            .ShouldBeFalse();
        _console.Output.ShouldContain("Config removed");
    }

    [Fact]
    public void Execute_DoesNotUpdateConfig_WhenOnlyDateFlagSet()
    {
        // Arrange
        SetupJournalConfig();
        _fileTracking.UpdateIndex(_testPath);

        var filePath = Path.Combine(_testPath, "Learning-Rust.md");
        _fileSystem.CreateFile(_testPath, "Learning-Rust.md", "# Rust\n\nContent");
        _hashService.SetHash(filePath, "hash-new");

        var command = CreateCommand();
        var settings = new UpdateJournalSettings { FilePath = _testPath, DateFlag = true };

        // Act
        command.Execute(CreateCommandContext(), settings);

        // Assert — config should not have the new file
        var config = _journalConfiguration.Read(_testPath);
        config.ShouldNotBeNull();
        config.TableOfContents.Structure.Topics.ShouldBeEmpty();
    }

    [Fact]
    public void Execute_UpdatesConfig_WhenNoFlagsSet_AllDefaults()
    {
        // Arrange
        SetupJournalConfig();
        _fileTracking.UpdateIndex(_testPath);

        var filePath = Path.Combine(_testPath, "Learning-Go.md");
        _fileSystem.CreateFile(_testPath, "Learning-Go.md", "# Go\n\nContent");
        _hashService.SetHash(filePath, "hash-go");

        var command = CreateCommand();
        var settings = new UpdateJournalSettings { FilePath = _testPath }; // no flags = all

        // Act
        command.Execute(CreateCommandContext(), settings);

        // Assert — config should have the new file since all updates run
        var config = _journalConfiguration.Read(_testPath);
        config.ShouldNotBeNull();
        var topic = config.TableOfContents.Structure.Topics.FirstOrDefault(t =>
            t.Name == "Learning"
        );
        topic.ShouldNotBeNull();
        topic.Entries.Any(e => e.File == "Learning-Go.md").ShouldBeTrue();
    }

    #endregion

    #region Table of Contents Update

    [Fact]
    public void Execute_UpdatesTableOfContents_WhenTocFlagSet()
    {
        // Arrange
        _fileTracking.UpdateIndex(_testPath); // empty index

        var filePath = Path.Combine(_testPath, "Learning-CSharp.md");
        _fileSystem.CreateFile(_testPath, "Learning-CSharp.md", "# CSharp\n\nContent");
        _hashService.SetHash(filePath, "hash-cs");

        // Add the entry to config so it shows in TOC
        _journalConfiguration.AddEntry(_testPath, string.Empty, "Learning-CSharp.md");

        var command = CreateCommand();
        var settings = new UpdateJournalSettings { FilePath = _testPath, TocFlag = true };

        // Act
        var result = command.Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(0);
        var tocPath = Path.Combine(_testPath, "1a-TableOfContents.md");
        var tocContent = _fileSystem.GetFileContent(tocPath);
        tocContent.ShouldNotBeNull();
        tocContent.ShouldContain("# Table of Contents");
        tocContent.ShouldContain("Learning-CSharp.md");
        _console.Output.ShouldContain("Table of contents updated");
    }

    [Fact]
    public void Execute_UpdatesTableOfContents_WhenNoFlagsSet()
    {
        // Arrange
        _fileTracking.UpdateIndex(_testPath);

        var filePath = Path.Combine(_testPath, "Learning-Go.md");
        _fileSystem.CreateFile(_testPath, "Learning-Go.md", "# Go\n\nContent");
        _hashService.SetHash(filePath, "hash-go");

        _journalConfiguration.AddEntry(_testPath, string.Empty, "Learning-Go.md");

        var command = CreateCommand();
        var settings = new UpdateJournalSettings { FilePath = _testPath };

        // Act
        var result = command.Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(0);
        var tocPath = Path.Combine(_testPath, "1a-TableOfContents.md");
        var tocContent = _fileSystem.GetFileContent(tocPath);
        tocContent.ShouldNotBeNull();
        tocContent.ShouldContain("# Table of Contents");
        tocContent.ShouldContain("Learning-Go.md");
    }

    [Fact]
    public void Execute_DoesNotUpdateToc_WhenOnlyDateFlagSet()
    {
        // Arrange
        _fileTracking.UpdateIndex(_testPath);

        var filePath = Path.Combine(_testPath, "note.md");
        _fileSystem.CreateFile(
            _testPath,
            "note.md",
            "Created: 01/01/2024\nLast Edited: 01/01/2024\n\n# Note"
        );
        _hashService.SetHash(filePath, "hash-a");
        _fileTracking.UpdateIndex(_testPath);
        _hashService.SetHash(filePath, "hash-b");

        var command = CreateCommand();
        var settings = new UpdateJournalSettings { FilePath = _testPath, DateFlag = true };

        // Act
        command.Execute(CreateCommandContext(), settings);

        // Assert — TOC file should not be created
        var tocPath = Path.Combine(_testPath, "1a-TableOfContents.md");
        _fileSystem.FileExists(tocPath).ShouldBeFalse();
        _console.Output.ShouldNotContain("Table of contents updated");
    }

    [Fact]
    public void Execute_DoesNotUpdateToc_WhenOnlyConfigFlagSet()
    {
        // Arrange
        _fileTracking.UpdateIndex(_testPath);

        var filePath = Path.Combine(_testPath, "note.md");
        _fileSystem.CreateFile(
            _testPath,
            "note.md",
            "Created: 01/01/2024\nLast Edited: 01/01/2024\n\n# Note"
        );
        _hashService.SetHash(filePath, "hash-a");
        _fileTracking.UpdateIndex(_testPath);
        _hashService.SetHash(filePath, "hash-b");

        var command = CreateCommand();
        var settings = new UpdateJournalSettings { FilePath = _testPath, ConfigFlag = true };

        // Act
        command.Execute(CreateCommandContext(), settings);

        // Assert — TOC file should not be created
        var tocPath = Path.Combine(_testPath, "1a-TableOfContents.md");
        _fileSystem.FileExists(tocPath).ShouldBeFalse();
        _console.Output.ShouldNotContain("Table of contents updated");
    }

    [Fact]
    public void Execute_TocReflectsConfigChanges_WhenAllFlagsRun()
    {
        // Arrange — start with tracked index, then add a new file
        _fileTracking.UpdateIndex(_testPath);

        var filePath = Path.Combine(_testPath, "Learning-Rust.md");
        _fileSystem.CreateFile(_testPath, "Learning-Rust.md", "# Rust\n\nContent");
        _hashService.SetHash(filePath, "hash-rust");

        var command = CreateCommand();
        var settings = new UpdateJournalSettings { FilePath = _testPath }; // no flags = all

        // Act — config adds the file, then TOC regenerates from config
        var result = command.Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(0);
        var tocPath = Path.Combine(_testPath, "1a-TableOfContents.md");
        var tocContent = _fileSystem.GetFileContent(tocPath);
        tocContent.ShouldNotBeNull();
        tocContent.ShouldContain("# Table of Contents");
        tocContent.ShouldContain("Learning-Rust.md");
        tocContent.ShouldContain("Last Edited:");
    }

    [Fact]
    public void Execute_TocPreservesExistingCreatedDate()
    {
        // Arrange — create a TOC with a created date, then trigger update
        var tocPath = Path.Combine(_testPath, "1a-TableOfContents.md");
        _fileSystem.CreateFile(
            _testPath,
            "1a-TableOfContents.md",
            "Created: 06/15/2024\nLast Edited: 06/15/2024\n\n# Table of Contents\n"
        );

        _fileTracking.UpdateIndex(_testPath);

        var filePath = Path.Combine(_testPath, "note.md");
        _fileSystem.CreateFile(_testPath, "note.md", "# Note\n\nContent");
        _hashService.SetHash(filePath, "hash-a");

        var command = CreateCommand();
        var settings = new UpdateJournalSettings { FilePath = _testPath, TocFlag = true };

        // Act
        command.Execute(CreateCommandContext(), settings);

        // Assert — created date should be preserved
        var tocContent = _fileSystem.GetFileContent(tocPath);
        tocContent.ShouldNotBeNull();
        tocContent.ShouldContain("Created: 06/15/2024");
        tocContent.ShouldContain("Last Edited:");
        tocContent.ShouldNotContain("Last Edited: 06/15/2024");
    }

    #endregion

    #region Error Handling

    [Fact]
    public void Execute_ReturnsOne_WhenTrackingIndexNotFound()
    {
        // Arrange — no tracking index exists (don't call UpdateIndex)
        var filePath = Path.Combine(_testPath, "note.md");
        _fileSystem.CreateFile(_testPath, "note.md", "# Note");
        _hashService.SetHash(filePath, "hash-a");

        var command = CreateCommand();
        var settings = new UpdateJournalSettings { FilePath = _testPath };

        // Act
        var result = command.Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(1);
        _console.Output.ShouldContain("tracking file");
        _console.Output.ShouldContain("not found");
    }

    [Fact]
    public void Execute_ReturnsOne_WhenJournalrcNotFound_AllDefaults()
    {
        // Arrange — create tracking index but delete .journalrc
        _fileTracking.UpdateIndex(_testPath);

        var filePath = Path.Combine(_testPath, "note.md");
        _fileSystem.CreateFile(_testPath, "note.md", "# Note");
        _hashService.SetHash(filePath, "hash-a");

        // Remove the .journalrc created by constructor
        _fileSystem.DeleteFile(Path.Combine(_testPath, ".journalrc"));

        var command = CreateCommand();
        var settings = new UpdateJournalSettings { FilePath = _testPath }; // all defaults

        // Act
        var result = command.Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(1);
        _console.Output.ShouldContain(".journalrc");
        _console.Output.ShouldContain("not found");
    }

    [Fact]
    public void Execute_ReturnsOne_WhenJournalrcNotFound_ConfigFlagSet()
    {
        // Arrange
        _fileTracking.UpdateIndex(_testPath);

        var filePath = Path.Combine(_testPath, "note.md");
        _fileSystem.CreateFile(_testPath, "note.md", "# Note");
        _hashService.SetHash(filePath, "hash-a");

        _fileSystem.DeleteFile(Path.Combine(_testPath, ".journalrc"));

        var command = CreateCommand();
        var settings = new UpdateJournalSettings { FilePath = _testPath, ConfigFlag = true };

        // Act
        var result = command.Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(1);
        _console.Output.ShouldContain(".journalrc");
    }

    [Fact]
    public void Execute_ReturnsOne_WhenJournalrcNotFound_TocFlagSet()
    {
        // Arrange
        _fileTracking.UpdateIndex(_testPath);

        var filePath = Path.Combine(_testPath, "note.md");
        _fileSystem.CreateFile(_testPath, "note.md", "# Note");
        _hashService.SetHash(filePath, "hash-a");

        _fileSystem.DeleteFile(Path.Combine(_testPath, ".journalrc"));

        var command = CreateCommand();
        var settings = new UpdateJournalSettings { FilePath = _testPath, TocFlag = true };

        // Act
        var result = command.Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(1);
        _console.Output.ShouldContain(".journalrc");
    }

    [Fact]
    public void Execute_DoesNotRequireJournalrc_WhenOnlyDateFlagSet()
    {
        // Arrange — tracking exists but no .journalrc
        var filePath = Path.Combine(_testPath, "note.md");
        _fileSystem.CreateFile(
            _testPath,
            "note.md",
            "Created: 01/01/2024\nLast Edited: 01/01/2024\n\n# Note"
        );
        _hashService.SetHash(filePath, "hash-a");
        _fileTracking.UpdateIndex(_testPath);
        _hashService.SetHash(filePath, "hash-b");

        _fileSystem.DeleteFile(Path.Combine(_testPath, ".journalrc"));

        var command = CreateCommand();
        var settings = new UpdateJournalSettings { FilePath = _testPath, DateFlag = true };

        // Act
        var result = command.Execute(CreateCommandContext(), settings);

        // Assert — should succeed since --dates doesn't need .journalrc
        result.ShouldBe(0);
        var updatedContent = _fileSystem.GetFileContent(filePath);
        updatedContent.ShouldNotContain("Last Edited: 01/01/2024");
    }

    #endregion

    #region TOC File Exclusion Tests

    [Fact]
    public void Execute_ShouldNotAddTocFileAsEntry_WhenConfigFlagSet()
    {
        // Arrange — create config with TOC file setting
        SetupJournalConfig();
        _fileTracking.UpdateIndex(_testPath);

        // Add the TOC file as if it was just created
        var tocFilePath = Path.Combine(_testPath, "1a-TableOfContents.md");
        _fileSystem.CreateFile(_testPath, "1a-TableOfContents.md", "# Table of Contents");
        _hashService.SetHash(tocFilePath, "hash-toc");

        var command = CreateCommand();
        var settings = new UpdateJournalSettings { FilePath = _testPath, ConfigFlag = true };

        // Act
        command.Execute(CreateCommandContext(), settings);

        // Assert — TOC file should NOT be added to config
        var config = _journalConfiguration.Read(_testPath);
        config.ShouldNotBeNull();
        config.TableOfContents.RootEntries.ShouldBeEmpty();
        config.TableOfContents.Structure.Topics.ShouldBeEmpty();

        // Output should not show TOC file being added
        _console.Output.ShouldNotContain("Config added: 1a-TableOfContents.md");
    }

    [Fact]
    public void Execute_ShouldNotAddTocFileAsEntry_WhenAllFlagsDefault()
    {
        // Arrange
        SetupJournalConfig();
        _fileTracking.UpdateIndex(_testPath);

        var tocFilePath = Path.Combine(_testPath, "1a-TableOfContents.md");
        _fileSystem.CreateFile(_testPath, "1a-TableOfContents.md", "# Table of Contents");
        _hashService.SetHash(tocFilePath, "hash-toc");

        var command = CreateCommand();
        var settings = new UpdateJournalSettings { FilePath = _testPath }; // all flags

        // Act
        command.Execute(CreateCommandContext(), settings);

        // Assert
        var config = _journalConfiguration.Read(_testPath);
        config.ShouldNotBeNull();
        config.TableOfContents.RootEntries.ShouldBeEmpty();
        config.TableOfContents.Structure.Topics.ShouldBeEmpty();
    }

    [Fact]
    public void Execute_ShouldNotAddCustomTocFileAsEntry()
    {
        // Arrange — Use default TOC filename to test the exclusion logic
        SetupJournalConfig();
        _fileTracking.UpdateIndex(_testPath);

        // Manually change the TOC file in the config after creation to simulate a custom name
        _journalConfiguration.Update(
            _testPath,
            c =>
            {
                c.TableOfContents.File = "custom-toc.md";
            }
        );

        // Create the custom TOC file
        var tocFilePath = Path.Combine(_testPath, "custom-toc.md");
        _fileSystem.CreateFile(_testPath, "custom-toc.md", "# Table of Contents");
        _hashService.SetHash(tocFilePath, "hash-custom-toc");

        var command = CreateCommand();
        var settings = new UpdateJournalSettings { FilePath = _testPath };

        // Act
        command.Execute(CreateCommandContext(), settings);

        // Assert
        var updatedConfig = _journalConfiguration.Read(_testPath);
        updatedConfig.ShouldNotBeNull();
        updatedConfig.TableOfContents.RootEntries.ShouldBeEmpty();
        _console.Output.ShouldNotContain("Config added: custom-toc.md");
    }

    #endregion
}
