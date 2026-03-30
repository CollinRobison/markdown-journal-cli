using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.Configuration.Models;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.Transactions;
using markdown_journal_cli.Infrastructure.Tracking;
using markdown_journal_cli.Infrastructure.Tracking.Models;
using markdown_journal_cli.Exceptions;
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
            NullLogger<JournalConfiguration>.Instance,
            _fileTracking
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
            _journalSettings,
            new MarkdownLinkRewriter(_fileSystem, NullLogger<MarkdownLinkRewriter>.Instance),
            NoOpFileTransactionCoordinator.Instance,
            NoOpRollbackReporter.Instance,
            NullLogger<JournalUpdateService>.Instance
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
                _journalSettings,
                Mock.Of<IMarkdownLinkRewriter>(),
                NoOpFileTransactionCoordinator.Instance,
                NoOpRollbackReporter.Instance,
                NullLogger<JournalUpdateService>.Instance
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
                _journalSettings,
                Mock.Of<IMarkdownLinkRewriter>(),
                NoOpFileTransactionCoordinator.Instance,
                NoOpRollbackReporter.Instance,
                NullLogger<JournalUpdateService>.Instance
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
                _journalSettings,
                Mock.Of<IMarkdownLinkRewriter>(),
                NoOpFileTransactionCoordinator.Instance,
                NoOpRollbackReporter.Instance,
                NullLogger<JournalUpdateService>.Instance
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
                _journalSettings,
                Mock.Of<IMarkdownLinkRewriter>(),
                NoOpFileTransactionCoordinator.Instance,
                NoOpRollbackReporter.Instance,
                NullLogger<JournalUpdateService>.Instance
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
                _journalSettings,
                Mock.Of<IMarkdownLinkRewriter>(),
                NoOpFileTransactionCoordinator.Instance,
                NoOpRollbackReporter.Instance,
                NullLogger<JournalUpdateService>.Instance
            )
        );
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenTxCoordinatorIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new JournalUpdateService(
                _console,
                Mock.Of<IFileSystem>(),
                Mock.Of<IJournalConfiguration>(),
                Mock.Of<IFileTracking>(),
                Mock.Of<ITableOfContentsService>(),
                _journalSettings,
                Mock.Of<IMarkdownLinkRewriter>(),
                null!,
                NoOpRollbackReporter.Instance,
                NullLogger<JournalUpdateService>.Instance
            )
        );
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenRollbackReporterIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new JournalUpdateService(
                _console,
                Mock.Of<IFileSystem>(),
                Mock.Of<IJournalConfiguration>(),
                Mock.Of<IFileTracking>(),
                Mock.Of<ITableOfContentsService>(),
                _journalSettings,
                Mock.Of<IMarkdownLinkRewriter>(),
                NoOpFileTransactionCoordinator.Instance,
                null!,
                NullLogger<JournalUpdateService>.Instance
            )
        );
    }

    #endregion

    #region UpdateJournalConfig

    [Fact]
    public void UpdateJournalConfig_AddsNewFileToConfig_WhenFileIsAdded()
    {
        // Arrange
        var syncResult = new JournalConfigSyncResult { FilesToAdd = ["2a-SomeNote.md"] };

        // Act
        _service.UpdateJournalConfig(_testPath, syncResult);

        // Assert
        var config = _journalConfiguration.Read(_testPath);
        config.ShouldNotBeNull();
        config.TableOfContents.RootEntries.ShouldContain(e => e.File == "2a-SomeNote.md");
    }

    [Fact]
    public void UpdateJournalConfig_AddsMultipleFilesToConfig_WhenMultipleFilesAdded()
    {
        // Arrange
        var syncResult = new JournalConfigSyncResult
        {
            FilesToAdd = ["2a-NoteOne.md", "3b-NoteTwo.md"],
        };

        // Act
        _service.UpdateJournalConfig(_testPath, syncResult);

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
        var syncResult = new JournalConfigSyncResult { FilesToRemove = ["2a-SomeNote.md"] };

        // Act
        _service.UpdateJournalConfig(_testPath, syncResult);

        // Assert
        var config = _journalConfiguration.Read(_testPath);
        config.ShouldNotBeNull();
        config.TableOfContents.RootEntries.ShouldNotContain(e => e.File == "2a-SomeNote.md");
    }

    [Fact]
    public void UpdateJournalConfig_PrintsConfigEntryNotFound_WhenDeletedFileNotInConfig()
    {
        // Arrange
        var syncResult = new JournalConfigSyncResult { FilesToRemove = ["nonexistent-file.md"] };

        // Act
        _service.UpdateJournalConfig(_testPath, syncResult);

        // Assert
        _console.Output.ShouldContain("Config entry not found for deleted file");
    }

    [Fact]
    public void UpdateJournalConfig_PrintsNoChangesNeeded_WhenNeitherAddedNorRemoved()
    {
        // Arrange
        var syncResult = new JournalConfigSyncResult();

        // Act
        _service.UpdateJournalConfig(_testPath, syncResult);

        // Assert
        _console.Output.ShouldContain("No configuration changes needed");
    }

    [Fact]
    public void UpdateJournalConfig_PrintsConfigUpdated_WhenChangesExist()
    {
        // Arrange
        var syncResult = new JournalConfigSyncResult { FilesToAdd = ["2a-SomeNote.md"] };

        // Act
        _service.UpdateJournalConfig(_testPath, syncResult);

        // Assert
        _console.Output.ShouldContain("Journal configuration updated");
    }

    [Fact]
    public void UpdateJournalConfig_HandlesAddedAndDeletedTogether()
    {
        // Arrange — pre-populate config with the file that will be removed
        _journalConfiguration.AddEntry(_testPath, string.Empty, "2a-OldNote.md");
        var syncResult = new JournalConfigSyncResult
        {
            FilesToAdd = ["3b-NewNote.md"],
            FilesToRemove = ["2a-OldNote.md"],
        };

        // Act
        _service.UpdateJournalConfig(_testPath, syncResult);

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
            _journalSettings,
            new MarkdownLinkRewriter(_fileSystem, NullLogger<MarkdownLinkRewriter>.Instance),
            NoOpFileTransactionCoordinator.Instance,
            NoOpRollbackReporter.Instance,
            NullLogger<JournalUpdateService>.Instance
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

    #region RenameToc

    [Fact]
    public void RenameToc_HappyPath_RenamesFile_UpdatesConfig_RewritesLinks_UpdatesTracking()
    {
        // Arrange
        const string oldTocFile = "1a-TableOfContents.md";
        const string newTocName = "MyContents";
        const string newTocFile = "MyContents.md";

        _fileSystem.CreateFile(_testPath, oldTocFile, "# Table of Contents");
        _fileTracking.UpdateFileInIndex(_testPath, oldTocFile);

        // A note that links to the old TOC
        const string noteRelPath = "notes/intro.md";
        _fileSystem.CreateDirectory(Path.Combine(_testPath, "notes"));
        _fileSystem.CreateFile(
            _testPath,
            noteRelPath,
            $"Created: 01/01/2025\n# Intro\nSee [TOC]({oldTocFile})."
        );
        _fileTracking.UpdateFileInIndex(_testPath, noteRelPath);

        // Act
        _service.RenameToc(_testPath, newTocName);

        // Assert — old TOC file gone, new one exists
        _fileSystem.FileExists(Path.Combine(_testPath, oldTocFile)).ShouldBeFalse();
        _fileSystem.FileExists(Path.Combine(_testPath, newTocFile)).ShouldBeTrue();

        // Config updated
        var config = _journalConfiguration.Read(_testPath);
        config!.TableOfContents.File.ShouldBe(newTocFile);

        // Links rewritten in the note
        var noteContent = _fileSystem.GetFileContent(Path.Combine(_testPath, noteRelPath));
        noteContent.ShouldContain($"[TOC]({newTocFile})");
        noteContent.ShouldNotContain(oldTocFile);

        // Tracking updated
        var index = _fileTracking.LoadIndex(_testPath);
        index.Files.ContainsKey(newTocFile).ShouldBeTrue();
        index.Files.ContainsKey(oldTocFile).ShouldBeFalse();
        index.Files.ContainsKey(noteRelPath).ShouldBeTrue();

        // Console output contains expected messages
        _console.Output.ShouldContain($"Renamed TOC: {oldTocFile} → {newTocFile}");
        _console.Output.ShouldContain("Last Edited updated for 1 file(s).");
    }

    [Fact]
    public void RenameToc_WhenAlreadyNamedCorrectly_SkipsRenameButRewritesStaleLinks()
    {
        // Arrange
        const string tocFile = "1a-TableOfContents.md";
        _fileSystem.CreateFile(_testPath, tocFile, "# TOC");
        _fileTracking.UpdateFileInIndex(_testPath, tocFile);

        _fileSystem.CreateFile(
            _testPath,
            "note.md",
            $"[TOC]({tocFile})"
        );
        _fileTracking.UpdateFileInIndex(_testPath, "note.md");

        // Act — pass same stem (no change in name)
        _service.RenameToc(_testPath, "1a-TableOfContents");

        // Assert — file still exists under same name
        _fileSystem.FileExists(Path.Combine(_testPath, tocFile)).ShouldBeTrue();

        // Config filename unchanged
        var config = _journalConfiguration.Read(_testPath);
        config!.TableOfContents.File.ShouldBe(tocFile);

        // Links in note.md were still checked (the link stays the same since name didn't change)
        _console.Output.ShouldNotContain("Renamed TOC:");
    }

    [Fact]
    public void RenameToc_ThrowsTocRenameConflictException_WhenTargetFileAlreadyExists()
    {
        // Arrange
        const string oldTocFile = "1a-TableOfContents.md";
        const string conflictingFile = "MyContents.md";
        _fileSystem.CreateFile(_testPath, oldTocFile, "# TOC");
        _fileSystem.CreateFile(_testPath, conflictingFile, "# Other file");

        // Act & Assert
        Should.Throw<TocRenameConflictException>(
            () => _service.RenameToc(_testPath, "MyContents")
        );
    }

    [Fact]
    public void RenameToc_WhenNoFilesReferenceTheToc_PrintsNoLinkMessage()
    {
        // Arrange
        const string oldTocFile = "1a-TableOfContents.md";
        _fileSystem.CreateFile(_testPath, oldTocFile, "# TOC");
        _fileTracking.UpdateFileInIndex(_testPath, oldTocFile);

        // A file with no link to the TOC
        _fileSystem.CreateFile(_testPath, "note.md", "No links here.");
        _fileTracking.UpdateFileInIndex(_testPath, "note.md");

        // Act
        _service.RenameToc(_testPath, "MyContents");

        // Assert
        _console.Output.ShouldContain("No link references needed updating.");
    }

    [Fact]
    public void RenameToc_WhenMultipleFilesReferToc_AllLinksRewrittenAndTrackingUpdated()
    {
        // Arrange
        const string oldTocFile = "1a-TableOfContents.md";
        const string newTocName = "MyContents";
        const string newTocFile = "MyContents.md";

        _fileSystem.CreateFile(_testPath, oldTocFile, "# TOC");
        _fileTracking.UpdateFileInIndex(_testPath, oldTocFile);

        _fileSystem.CreateFile(_testPath, "intro.md",
            $"Created: 01/01/2025\n# Intro\n[TOC]({oldTocFile})");
        _fileSystem.CreateFile(_testPath, "chapter-1.md",
            $"Created: 01/01/2025\n# Chapter 1\n[TOC]({oldTocFile})");
        _fileSystem.CreateFile(_testPath, "other.md", "No link.");
        _fileTracking.UpdateFileInIndex(_testPath, "intro.md");
        _fileTracking.UpdateFileInIndex(_testPath, "chapter-1.md");
        _fileTracking.UpdateFileInIndex(_testPath, "other.md");

        // Act
        _service.RenameToc(_testPath, newTocName);

        // Assert — both files updated
        var introContent = _fileSystem.GetFileContent(Path.Combine(_testPath, "intro.md"));
        introContent.ShouldContain($"[TOC]({newTocFile})");
        introContent.ShouldContain("Last Edited:");

        var ch1Content = _fileSystem.GetFileContent(Path.Combine(_testPath, "chapter-1.md"));
        ch1Content.ShouldContain($"[TOC]({newTocFile})");
        ch1Content.ShouldContain("Last Edited:");

        // "other.md" was not modified
        var otherContent = _fileSystem.GetFileContent(Path.Combine(_testPath, "other.md"));
        otherContent.ShouldBe("No link.");

        // Console output mentions the count of updated files
        _console.Output.ShouldContain("Last Edited updated for 2 file(s).");
    }

    #endregion

    #region BuildDryRunReport

    // ── helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a minimal .testapp tracking index and .journalrc for a fresh journal,
    /// then optionally adds extra files to each.
    /// Returns the relative path of the TOC file.
    /// </summary>
    private string SetupDryRunJournal(
        string[]? trackedFiles = null,
        string[]? configFiles = null
    )
    {
        const string tocFile = "1a-TableOfContents.md";

        // Create and track the TOC file
        _fileSystem.CreateFile(_testPath, tocFile, "# Table of Contents\n");
        _fileTracking.UpdateFileInIndex(_testPath, tocFile);

        // Add extra tracked files
        foreach (var f in trackedFiles ?? [])
        {
            _fileSystem.CreateFile(_testPath, f, $"# {f}\n");
            _fileTracking.UpdateFileInIndex(_testPath, f);
        }

        // Ensure .journalrc always exists (required for BuildDryRunReport paths that read config)
        if (!_fileSystem.FileExists(Path.Combine(_testPath, ".journalrc")))
            SetupJournalConfig();

        // Add extra config entries beyond the defaults
        foreach (var f in configFiles ?? [])
            _journalConfiguration.AddEntry(_testPath, string.Empty, f);

        return tocFile;
    }

    // ── todo 22: tracking + config + toc (implicit --dry-run all) ─────────────

    [Fact]
    public void BuildDryRunReport_AllSections_ConfigReflectsPendingTrackingAddition()
    {
        // Arrange — journal with one existing entry; a new file exists on disk but is not yet tracked
        SetupDryRunJournal(trackedFiles: ["existing.md"]);
        _journalConfiguration.AddEntry(_testPath, string.Empty, "existing.md");

        // Simulate a new file on disk that tracking will detect as "added"
        _fileSystem.CreateFile(_testPath, "new-entry.md", "# New Entry\n");
        var trackingChanges = new ChangeDetectionResult
        {
            AddedFiles = ["new-entry.md"],
        };
        var naiveConfigChanges = _journalConfiguration.DetectConfigChanges(_testPath);

        // Act
        var report = _service.BuildDryRunReport(
            _testPath,
            trackingChanges,
            naiveConfigChanges,
            includeToc: true,
            renameTocTarget: null
        );

        // Assert — projected config drift includes the pending addition
        report.ConfigChanges.ShouldNotBeNull();
        report.ConfigChanges!.FilesToAdd.ShouldContain("new-entry.md");

        // TOC preview should include the new entry
        report.TocPreview.ShouldNotBeNull();
        report.TocPreview!.PreviewContent.ShouldContain("new-entry.md");
    }

    [Fact]
    public void BuildDryRunReport_AllSections_ConfigReflectsPendingTrackingDeletion()
    {
        // Arrange — journal with a file tracked and in config; file is deleted from disk
        SetupDryRunJournal(trackedFiles: ["going-away.md"]);
        _journalConfiguration.AddEntry(_testPath, string.Empty, "going-away.md");

        var trackingChanges = new ChangeDetectionResult
        {
            DeletedFiles = ["going-away.md"],
        };
        var naiveConfigChanges = _journalConfiguration.DetectConfigChanges(_testPath);

        // Act
        var report = _service.BuildDryRunReport(
            _testPath,
            trackingChanges,
            naiveConfigChanges,
            includeToc: true,
            renameTocTarget: null
        );

        // Assert — projected config drift includes the pending removal
        report.ConfigChanges.ShouldNotBeNull();
        report.ConfigChanges!.FilesToRemove.ShouldContain("going-away.md");

        // TOC preview should NOT include the deleted entry
        report.TocPreview.ShouldNotBeNull();
        report.TocPreview!.PreviewContent.ShouldNotContain("going-away.md");
    }

    // ── todo 23: config + toc (no tracking) ──────────────────────────────────

    [Fact]
    public void BuildDryRunReport_ConfigAndTocNoTracking_TocReflectsCurrentConfigDrift()
    {
        // Arrange — file is tracked but not yet in .journalrc (config drift exists)
        SetupDryRunJournal(trackedFiles: ["unregistered.md"]);
        // deliberately do NOT add "unregistered.md" to journalrc

        var configChanges = _journalConfiguration.DetectConfigChanges(_testPath);
        configChanges.FilesToAdd.ShouldContain("unregistered.md");

        // Act — no tracking changes passed (null)
        var report = _service.BuildDryRunReport(
            _testPath,
            trackingChanges: null,
            configChanges: configChanges,
            includeToc: true,
            renameTocTarget: null
        );

        // Assert — TOC preview includes the file that config drift would add
        report.TocPreview.ShouldNotBeNull();
        report.TocPreview!.PreviewContent.ShouldContain("unregistered.md");
    }

    // ── todo 24a: tracking + toc (no config) — TOC unchanged ─────────────────

    [Fact]
    public void BuildDryRunReport_TrackingAndTocNoConfig_TocUsesCurrentConfig()
    {
        // Arrange — a new file would be added to tracking but config is NOT in scope
        SetupDryRunJournal(trackedFiles: ["existing.md"]);
        _journalConfiguration.AddEntry(_testPath, string.Empty, "existing.md");

        _fileSystem.CreateFile(_testPath, "pending.md", "# Pending\n");
        var trackingChanges = new ChangeDetectionResult { AddedFiles = ["pending.md"] };

        // Act — configChanges is null (--tracking --toc, no --config)
        var report = _service.BuildDryRunReport(
            _testPath,
            trackingChanges,
            configChanges: null,
            includeToc: true,
            renameTocTarget: null
        );

        // Assert — TOC preview uses current .journalrc; "pending.md" is NOT in the preview
        // because config is out of scope (tracking alone doesn't mutate .journalrc)
        report.TocPreview.ShouldNotBeNull();
        report.TocPreview!.PreviewContent.ShouldNotContain("pending.md");
    }

    // ── todo 24b: toc only — TOC uses current config ─────────────────────────

    [Fact]
    public void BuildDryRunReport_TocOnly_TocUsesCurrentConfig()
    {
        // Arrange
        SetupDryRunJournal(trackedFiles: ["note.md"]);
        _journalConfiguration.AddEntry(_testPath, string.Empty, "note.md");

        // Act — no tracking changes, no config changes (--toc only)
        var report = _service.BuildDryRunReport(
            _testPath,
            trackingChanges: null,
            configChanges: null,
            includeToc: true,
            renameTocTarget: null
        );

        // Assert — TOC preview reflects only what's currently in .journalrc
        report.TocPreview.ShouldNotBeNull();
        report.TocPreview!.PreviewContent.ShouldContain("note.md");
    }

    #endregion
}
