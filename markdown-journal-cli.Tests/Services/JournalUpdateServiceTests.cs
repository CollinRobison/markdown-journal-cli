using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.Configuration.Models;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.Tracking;
using markdown_journal_cli.Infrastructure.Tracking.Models;
using markdown_journal_cli.Services;
using markdown_journal_cli.Tests.Infrastructure.FileSystem;
using markdown_journal_cli.Tests.Infrastructure.Tracking;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Spectre.Console.Testing;

namespace markdown_journal_cli.Tests.Services;

/// <summary>
/// Unit tests for <see cref="JournalUpdateService"/> covering journal configuration updates,
/// last-edited date tracking, and table of contents management.
/// Uses concrete infrastructure implementations with in-memory test doubles.
/// </summary>
public class JournalUpdateServiceTests
{
    private readonly TestConsole _console;
    private readonly TestFileSystem _fileSystem;
    private readonly TestHashService _hashService;
    private readonly FileTracking _fileTracking;
    private readonly JournalConfiguration _journalConfiguration;
    private readonly TableOfContentsService _tableOfContentsService;
    private readonly IOptions<JournalSettings> _journalSettings;
    private readonly JournalUpdateService _service;
    private readonly string _testPath;

    public JournalUpdateServiceTests()
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

        _tableOfContentsService = new TableOfContentsService(
            _fileSystem,
            _journalConfiguration,
            _journalSettings,
            NullLogger<TableOfContentsService>.Instance
        );

        _service = new JournalUpdateService(
            _console,
            _fileSystem,
            _journalConfiguration,
            _fileTracking,
            _tableOfContentsService,
            _journalSettings
        );

        _fileSystem.CreateDirectory(_testPath);
        SetupJournalConfig();
    }

    private void SetupJournalConfig()
    {
        var config = new JournalConfig
        {
            JournalName = "Test Journal",
            TableOfContents = new TableOfContents
            {
                File = "1a-TableOfContents.md",
                Extensions = [".md"],
                Structure = new Structure { Topics = [] },
                RootEntries = [],
            },
        };
        _journalConfiguration.Create(_testPath, config);
    }

    #region Constructor Guards

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenConsoleIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new JournalUpdateService(
                null!,
                Mock.Of<IFileSystem>(),
                Mock.Of<IJournalConfiguration>(),
                Mock.Of<IFileTracking>(),
                Mock.Of<ITableOfContentsService>(),
                _journalSettings
            )
        );
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenFileSystemIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new JournalUpdateService(
                _console,
                null!,
                Mock.Of<IJournalConfiguration>(),
                Mock.Of<IFileTracking>(),
                Mock.Of<ITableOfContentsService>(),
                _journalSettings
            )
        );
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenJournalConfigurationIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new JournalUpdateService(
                _console,
                Mock.Of<IFileSystem>(),
                null!,
                Mock.Of<IFileTracking>(),
                Mock.Of<ITableOfContentsService>(),
                _journalSettings
            )
        );
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenFileTrackingIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new JournalUpdateService(
                _console,
                Mock.Of<IFileSystem>(),
                Mock.Of<IJournalConfiguration>(),
                null!,
                Mock.Of<ITableOfContentsService>(),
                _journalSettings
            )
        );
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenTableOfContentsServiceIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new JournalUpdateService(
                _console,
                Mock.Of<IFileSystem>(),
                Mock.Of<IJournalConfiguration>(),
                Mock.Of<IFileTracking>(),
                null!,
                _journalSettings
            )
        );
    }

    #endregion

    #region UpdateJournalConfig

    [Fact]
    public void UpdateJournalConfig_AddsNewFileToConfig_WhenFileIsAdded()
    {
        // Arrange
        var fileResults = new ChangeDetectionResult { AddedFiles = ["2a-SomeNote.md"] };

        // Act
        _service.UpdateJournalConfig(_testPath, fileResults);

        // Assert
        var config = _journalConfiguration.Read(_testPath);
        config.ShouldNotBeNull();
        config.TableOfContents.RootEntries.ShouldContain(e => e.File == "2a-SomeNote.md");
    }

    [Fact]
    public void UpdateJournalConfig_AddsMultipleFilesToConfig_WhenMultipleFilesAdded()
    {
        // Arrange
        var fileResults = new ChangeDetectionResult
        {
            AddedFiles = ["2a-NoteOne.md", "3b-NoteTwo.md"],
        };

        // Act
        _service.UpdateJournalConfig(_testPath, fileResults);

        // Assert
        var config = _journalConfiguration.Read(_testPath);
        config.ShouldNotBeNull();
        config.TableOfContents.RootEntries.ShouldContain(e => e.File == "2a-NoteOne.md");
        config.TableOfContents.RootEntries.ShouldContain(e => e.File == "3b-NoteTwo.md");
    }

    [Fact]
    public void UpdateJournalConfig_RemovesDeletedFileFromConfig_WhenFileIsDeleted()
    {
        // Arrange
        _journalConfiguration.AddEntry(_testPath, string.Empty, "2a-SomeNote.md");
        var fileResults = new ChangeDetectionResult { DeletedFiles = ["2a-SomeNote.md"] };

        // Act
        _service.UpdateJournalConfig(_testPath, fileResults);

        // Assert
        var config = _journalConfiguration.Read(_testPath);
        config.ShouldNotBeNull();
        config.TableOfContents.RootEntries.ShouldNotContain(e => e.File == "2a-SomeNote.md");
    }

    [Fact]
    public void UpdateJournalConfig_PrintsConfigEntryNotFound_WhenDeletedFileNotInConfig()
    {
        // Arrange
        var fileResults = new ChangeDetectionResult { DeletedFiles = ["nonexistent-file.md"] };

        // Act
        _service.UpdateJournalConfig(_testPath, fileResults);

        // Assert
        _console.Output.ShouldContain("Config entry not found for deleted file");
    }

    [Fact]
    public void UpdateJournalConfig_SkipsTocFile_WhenTocFileIsAdded()
    {
        // Arrange
        var fileResults = new ChangeDetectionResult { AddedFiles = ["1a-TableOfContents.md"] };

        // Act
        _service.UpdateJournalConfig(_testPath, fileResults);

        // Assert — the TOC file must never appear as a config entry
        var config = _journalConfiguration.Read(_testPath);
        config.ShouldNotBeNull();
        config.TableOfContents.RootEntries.ShouldBeEmpty();
    }

    [Fact]
    public void UpdateJournalConfig_IsCaseInsensitive_WhenMatchingTocFile()
    {
        // Arrange — pass the TOC filename with different casing
        var fileResults = new ChangeDetectionResult { AddedFiles = ["1A-TABLEOFCONTENTS.MD"] };

        // Act
        _service.UpdateJournalConfig(_testPath, fileResults);

        // Assert — the differently-cased TOC filename must still be skipped
        var config = _journalConfiguration.Read(_testPath);
        config.ShouldNotBeNull();
        config.TableOfContents.RootEntries.ShouldBeEmpty();
    }

    [Fact]
    public void UpdateJournalConfig_PrintsNoChangesNeeded_WhenNeitherAddedNorDeleted()
    {
        // Arrange
        var fileResults = new ChangeDetectionResult();

        // Act
        _service.UpdateJournalConfig(_testPath, fileResults);

        // Assert
        _console.Output.ShouldContain("No configuration changes needed");
    }

    [Fact]
    public void UpdateJournalConfig_PrintsConfigUpdated_WhenChangesExist()
    {
        // Arrange
        var fileResults = new ChangeDetectionResult { AddedFiles = ["2a-SomeNote.md"] };

        // Act
        _service.UpdateJournalConfig(_testPath, fileResults);

        // Assert
        _console.Output.ShouldContain("Journal configuration updated");
    }

    [Fact]
    public void UpdateJournalConfig_HandlesAddedAndDeletedTogether()
    {
        // Arrange — pre-populate config with the file that will be deleted
        _journalConfiguration.AddEntry(_testPath, string.Empty, "2a-OldNote.md");
        var fileResults = new ChangeDetectionResult
        {
            AddedFiles = ["3b-NewNote.md"],
            DeletedFiles = ["2a-OldNote.md"],
        };

        // Act
        _service.UpdateJournalConfig(_testPath, fileResults);

        // Assert
        var config = _journalConfiguration.Read(_testPath);
        config.ShouldNotBeNull();
        config.TableOfContents.RootEntries.ShouldContain(e => e.File == "3b-NewNote.md");
        config.TableOfContents.RootEntries.ShouldNotContain(e => e.File == "2a-OldNote.md");
    }

    #endregion

    #region UpdateLastEditedDatesAndTracking

    [Fact]
    public void UpdateLastEditedDatesAndTracking_UpdatesLastEditedDate_ForModifiedFile()
    {
        // Arrange
        const string relativePath = "note.md";
        var absolutePath = Path.Combine(_testPath, relativePath);
        _fileSystem.CreateFile(
            _testPath,
            relativePath,
            "Created: 01/01/2025\n# My Note\n\nContent here."
        );
        _hashService.SetHash(absolutePath, "hash-a");
        _fileTracking.UpdateIndex(_testPath);
        _hashService.SetHash(absolutePath, "hash-b");

        var fileResults = new ChangeDetectionResult { ModifiedFiles = [relativePath] };

        // Act
        _service.UpdateLastEditedDatesAndTracking(_testPath, fileResults, trackingOnly: false);

        // Assert
        var updatedContent = _fileSystem.GetFileContent(absolutePath);
        updatedContent.ShouldNotBeNull();
        updatedContent.ShouldContain("Last Edited:");
    }

    [Fact]
    public void UpdateLastEditedDatesAndTracking_DoesNotUpdateLastEditedDate_WhenTrackingOnly()
    {
        // Arrange
        const string relativePath = "note.md";
        var absolutePath = Path.Combine(_testPath, relativePath);
        const string originalContent = "Created: 01/01/2025\n# My Note\n\nContent here.";
        _fileSystem.CreateFile(_testPath, relativePath, originalContent);
        _hashService.SetHash(absolutePath, "hash-a");
        _fileTracking.UpdateIndex(_testPath);
        _hashService.SetHash(absolutePath, "hash-b");

        var fileResults = new ChangeDetectionResult { ModifiedFiles = [relativePath] };

        // Act
        _service.UpdateLastEditedDatesAndTracking(_testPath, fileResults, trackingOnly: true);

        // Assert — content must be unchanged when trackingOnly is true
        var content = _fileSystem.GetFileContent(absolutePath);
        content.ShouldBe(originalContent);
    }

    [Fact]
    public void UpdateLastEditedDatesAndTracking_UpdatesTrackingIndex_ForModifiedFile()
    {
        // Arrange
        const string relativePath = "note.md";
        var absolutePath = Path.Combine(_testPath, relativePath);
        _fileSystem.CreateFile(_testPath, relativePath, "# My Note\n\nContent here.");
        _hashService.SetHash(absolutePath, "hash-a");
        _fileTracking.UpdateIndex(_testPath);
        _hashService.SetHash(absolutePath, "hash-b");

        var fileResults = new ChangeDetectionResult { ModifiedFiles = [relativePath] };

        // Act
        _service.UpdateLastEditedDatesAndTracking(_testPath, fileResults, trackingOnly: false);

        // Assert — file must no longer appear as modified after the index is updated
        var changes = _fileTracking.DetectChangesWithoutUpdate(_testPath);
        changes.ModifiedFiles.ShouldNotContain(relativePath);
    }

    [Fact]
    public void UpdateLastEditedDatesAndTracking_UpdatesTrackingIndex_ForModifiedFile_EvenWhenTrackingOnly()
    {
        // Arrange
        const string relativePath = "note.md";
        var absolutePath = Path.Combine(_testPath, relativePath);
        _fileSystem.CreateFile(_testPath, relativePath, "# My Note\n\nContent here.");
        _hashService.SetHash(absolutePath, "hash-a");
        _fileTracking.UpdateIndex(_testPath);
        _hashService.SetHash(absolutePath, "hash-b");

        var fileResults = new ChangeDetectionResult { ModifiedFiles = [relativePath] };

        // Act
        _service.UpdateLastEditedDatesAndTracking(_testPath, fileResults, trackingOnly: true);

        // Assert — the index must be updated even when trackingOnly skips content updates
        var changes = _fileTracking.DetectChangesWithoutUpdate(_testPath);
        changes.ModifiedFiles.ShouldNotContain(relativePath);
    }

    [Fact]
    public void UpdateLastEditedDatesAndTracking_AddsNewFileToTrackingIndex_WhenFileIsAdded()
    {
        // Arrange
        const string relativePath = "new-note.md";
        _fileSystem.CreateFile(_testPath, relativePath, "# New Note\n\nContent here.");
        var fileResults = new ChangeDetectionResult { AddedFiles = [relativePath] };

        // Act
        _service.UpdateLastEditedDatesAndTracking(_testPath, fileResults, trackingOnly: false);

        // Assert
        var index = _fileTracking.LoadIndex(_testPath);
        index.Files.ContainsKey(relativePath).ShouldBeTrue();
    }

    [Fact]
    public void UpdateLastEditedDatesAndTracking_DoesNotUpdateContent_ForAddedFile()
    {
        // Arrange
        const string relativePath = "new-note.md";
        var absolutePath = Path.Combine(_testPath, relativePath);
        const string originalContent = "# New Note\n\nContent here.";
        _fileSystem.CreateFile(_testPath, relativePath, originalContent);
        var fileResults = new ChangeDetectionResult { AddedFiles = [relativePath] };

        // Act
        _service.UpdateLastEditedDatesAndTracking(_testPath, fileResults, trackingOnly: false);

        // Assert — added files must not have their content modified, only tracked
        var content = _fileSystem.GetFileContent(absolutePath);
        content.ShouldBe(originalContent);
    }

    [Fact]
    public void UpdateLastEditedDatesAndTracking_RemovesDeletedFileFromTrackingIndex()
    {
        // Arrange
        const string relativePath = "deleted-note.md";
        var absolutePath = Path.Combine(_testPath, relativePath);
        _fileSystem.CreateFile(_testPath, relativePath, "# Deleted Note");
        _fileTracking.UpdateIndex(_testPath);
        _fileSystem.DeleteFile(absolutePath);

        var fileResults = new ChangeDetectionResult { DeletedFiles = [relativePath] };

        // Act
        _service.UpdateLastEditedDatesAndTracking(_testPath, fileResults, trackingOnly: false);

        // Assert
        var index = _fileTracking.LoadIndex(_testPath);
        index.Files.ContainsKey(relativePath).ShouldBeFalse();
    }

    [Fact]
    public void UpdateLastEditedDatesAndTracking_UpdatesMultipleModifiedFiles()
    {
        // Arrange
        const string relativePath1 = "note-one.md";
        const string relativePath2 = "note-two.md";
        var absolutePath1 = Path.Combine(_testPath, relativePath1);
        var absolutePath2 = Path.Combine(_testPath, relativePath2);
        _fileSystem.CreateFile(_testPath, relativePath1, "Created: 01/01/2025\n# Note One");
        _fileSystem.CreateFile(_testPath, relativePath2, "Created: 01/01/2025\n# Note Two");
        _hashService.SetHash(absolutePath1, "hash-a");
        _hashService.SetHash(absolutePath2, "hash-a");
        _fileTracking.UpdateIndex(_testPath);
        _hashService.SetHash(absolutePath1, "hash-b");
        _hashService.SetHash(absolutePath2, "hash-b");

        var fileResults = new ChangeDetectionResult
        {
            ModifiedFiles = [relativePath1, relativePath2],
        };

        // Act
        _service.UpdateLastEditedDatesAndTracking(_testPath, fileResults, trackingOnly: false);

        // Assert
        _fileSystem.GetFileContent(absolutePath1).ShouldContain("Last Edited:");
        _fileSystem.GetFileContent(absolutePath2).ShouldContain("Last Edited:");
    }

    [Fact]
    public void UpdateLastEditedDatesAndTracking_InsertsLastEditedDate_WhenFileHasNoMetadata()
    {
        // Arrange
        const string relativePath = "bare-note.md";
        var absolutePath = Path.Combine(_testPath, relativePath);
        _fileSystem.CreateFile(
            _testPath,
            relativePath,
            "# Bare Note\n\nJust content, no metadata."
        );
        _hashService.SetHash(absolutePath, "hash-a");
        _fileTracking.UpdateIndex(_testPath);
        _hashService.SetHash(absolutePath, "hash-b");

        var fileResults = new ChangeDetectionResult { ModifiedFiles = [relativePath] };

        // Act
        _service.UpdateLastEditedDatesAndTracking(_testPath, fileResults, trackingOnly: false);

        // Assert — MarkdownMetadataParser must insert the date even when no existing metadata exists
        var updatedContent = _fileSystem.GetFileContent(absolutePath);
        updatedContent.ShouldNotBeNull();
        updatedContent.ShouldContain("Last Edited:");
    }

    [Fact]
    public void UpdateLastEditedDatesAndTracking_PrintsUpdatedSummary_ForModifiedFiles()
    {
        // Arrange
        const string relativePath = "note.md";
        var absolutePath = Path.Combine(_testPath, relativePath);
        _fileSystem.CreateFile(_testPath, relativePath, "# My Note");
        _hashService.SetHash(absolutePath, "hash-a");
        _fileTracking.UpdateIndex(_testPath);
        _hashService.SetHash(absolutePath, "hash-b");

        var fileResults = new ChangeDetectionResult { ModifiedFiles = [relativePath] };

        // Act
        _service.UpdateLastEditedDatesAndTracking(_testPath, fileResults, trackingOnly: false);

        // Assert
        _console.Output.ShouldContain("Updated dates for 1 file(s)");
    }

    [Fact]
    public void UpdateLastEditedDatesAndTracking_PrintsTrackedSummary_ForAddedFiles()
    {
        // Arrange
        const string relativePath = "new-note.md";
        _fileSystem.CreateFile(_testPath, relativePath, "# New Note");
        var fileResults = new ChangeDetectionResult { AddedFiles = [relativePath] };

        // Act
        _service.UpdateLastEditedDatesAndTracking(_testPath, fileResults, trackingOnly: false);

        // Assert
        _console.Output.ShouldContain("Tracked 1 new file(s)");
    }

    [Fact]
    public void UpdateLastEditedDatesAndTracking_PrintsRemovedSummary_ForDeletedFiles()
    {
        // Arrange
        const string relativePath = "gone-note.md";
        var absolutePath = Path.Combine(_testPath, relativePath);
        _fileSystem.CreateFile(_testPath, relativePath, "# Gone Note");
        _fileTracking.UpdateIndex(_testPath);
        _fileSystem.DeleteFile(absolutePath);

        var fileResults = new ChangeDetectionResult { DeletedFiles = [relativePath] };

        // Act
        _service.UpdateLastEditedDatesAndTracking(_testPath, fileResults, trackingOnly: false);

        // Assert
        _console.Output.ShouldContain("Removed 1 deleted file(s) from tracking");
    }

    [Fact]
    public void UpdateLastEditedDatesAndTracking_UsesConfiguredDateFormat()
    {
        // Arrange
        const string relativePath = "note.md";
        var absolutePath = Path.Combine(_testPath, relativePath);
        _fileSystem.CreateFile(_testPath, relativePath, "Created: 01/01/2025\n# My Note");
        _hashService.SetHash(absolutePath, "hash-a");
        _fileTracking.UpdateIndex(_testPath);
        _hashService.SetHash(absolutePath, "hash-b");

        var fileResults = new ChangeDetectionResult { ModifiedFiles = [relativePath] };

        // Act
        _service.UpdateLastEditedDatesAndTracking(_testPath, fileResults, trackingOnly: false);

        // Assert — date must match the MM/dd/yyyy format configured in JournalSettings
        var content = _fileSystem.GetFileContent(absolutePath);
        content.ShouldNotBeNull();
        content.ShouldMatch(@"Last Edited: \d{2}/\d{2}/\d{4}");
    }

    #endregion

    #region UpdateTableOfContents

    [Fact]
    public void UpdateTableOfContents_WritesTocFile_WhenConfigHasEntries()
    {
        // Arrange
        _journalConfiguration.AddEntry(_testPath, string.Empty, "2a-SomeNote.md");
        _fileSystem.CreateFile(_testPath, "2a-SomeNote.md", "# Some Note");

        // Act
        _service.UpdateTableOfContents(_testPath);

        // Assert
        var tocPath = Path.Combine(_testPath, "1a-TableOfContents.md");
        _fileSystem.FileExists(tocPath).ShouldBeTrue();
    }

    [Fact]
    public void UpdateTableOfContents_TracksTocFile_SoItDoesNotAppearAsAddedOnNextRun()
    {
        // Arrange — build an initial empty index before the TOC file exists
        _fileTracking.UpdateIndex(_testPath);

        // Act
        _service.UpdateTableOfContents(_testPath);

        // Assert — TOC file is now tracked; a subsequent change detection must not list it as added
        var changes = _fileTracking.DetectChangesWithoutUpdate(_testPath);
        changes.AddedFiles.ShouldNotContain("1a-TableOfContents.md");
    }

    [Fact]
    public void UpdateTableOfContents_PrintsSuccessMessage()
    {
        // Act
        _service.UpdateTableOfContents(_testPath);

        // Assert
        _console.Output.ShouldContain("Table of contents updated");
    }

    [Fact]
    public void UpdateTableOfContents_UsesFallbackTocFileName_WhenConfigFileIsNull()
    {
        // Arrange — use a mock IJournalConfiguration that returns a config with a null File value,
        // and a no-op ITableOfContentsService so the TOC write itself does not interfere.
        var mockJournalConfiguration = new Mock<IJournalConfiguration>();
        mockJournalConfiguration
            .Setup(jc => jc.Read(_testPath))
            .Returns(
                new JournalConfig
                {
                    JournalName = "Test Journal",
                    TableOfContents = new TableOfContents
                    {
                        File = null!, // force the null branch so the fallback name is used
                        Extensions = [".md"],
                        Structure = new Structure { Topics = [] },
                        RootEntries = [],
                    },
                }
            );

        var mockTocService = new Mock<ITableOfContentsService>();

        var service = new JournalUpdateService(
            _console,
            _fileSystem,
            mockJournalConfiguration.Object,
            _fileTracking,
            mockTocService.Object,
            _journalSettings
        );

        // Arrange — create the TOC file so it can be tracked (UpdateFileInIndex only tracks existing files)
        var expectedTocFile = $"1a-TableOfContents{FileConstants.MarkdownExtension}";
        _fileSystem.CreateFile(_testPath, expectedTocFile, "# Table of Contents");

        // Act
        service.UpdateTableOfContents(_testPath);

        // Assert — the fallback filename (TableOfContentsFileName + ".md") must be tracked
        var index = _fileTracking.LoadIndex(_testPath);
        index.Files.ContainsKey(expectedTocFile).ShouldBeTrue();
    }

    #endregion
}
