using markdown_journal_cli.Commands.Update;
using markdown_journal_cli.Exceptions;
using markdown_journal_cli.Infrastructure.Configuration.Models;
using markdown_journal_cli.Infrastructure.Tracking.Models;
using markdown_journal_cli.Infrastructure.Transactions;
using markdown_journal_cli.Services;
using markdown_journal_cli.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using Spectre.Console.Cli;
using Spectre.Console.Testing;

namespace markdown_journal_cli.Tests.Commands.Update;

public class UpdateCommandTests : CommandTestBase
{
    private const string TestPath = "/test/journal";

    // Path helpers built from JournalSettings.AppName ("md-journal") and JournalConfigFileName
    private static readonly string TrackingFilePath = Path.Combine(TestPath, ".md-journal");
    private static readonly string JournalrcPath = Path.Combine(TestPath, ".journalrc");

    // Field initializers run BEFORE the base constructor, ensuring these are available
    // when CommandTestBase calls SetupDefaultBehaviors() during construction.
    private readonly TestConsole _console = new();
    private readonly Mock<IJournalUpdateService> _mockJournalUpdateService = new();
    private readonly Mock<IDryRunRenderer> _mockDryRunRenderer = new();

    protected override void SetupDefaultBehaviors()
    {
        // Tracking file and .journalrc both exist by default
        MockFileSystem.Setup(fs => fs.FileExists(TrackingFilePath)).Returns(true);
        MockFileSystem.Setup(fs => fs.FileExists(JournalrcPath)).Returns(true);

        // No tracking changes by default
        MockFileTracking
            .Setup(ft => ft.DetectChangesWithoutUpdate(TestPath))
            .Returns(new ChangeDetectionResult());

        // No config drift by default
        MockJournalConfiguration
            .Setup(c => c.DetectConfigChanges(TestPath))
            .Returns(new JournalConfigSyncResult());

        // Default dry-run report with no changes
        _mockJournalUpdateService
            .Setup(s =>
                s.BuildDryRunReport(
                    It.IsAny<string>(),
                    It.IsAny<ChangeDetectionResult?>(),
                    It.IsAny<JournalConfigSyncResult?>(),
                    It.IsAny<bool>(),
                    It.IsAny<string?>()
                )
            )
            .Returns(new UpdateDryRunReport());
    }

    private UpdateCommand CreateCommand() =>
        new UpdateCommand(
            _console,
            MockFileSystem.Object,
            _mockJournalUpdateService.Object,
            MockFileTracking.Object,
            JournalSettings,
            MockJournalConfiguration.Object,
            NullLogger<UpdateCommand>.Instance,
            _mockDryRunRenderer.Object,
            NoOpFileTransactionCoordinator.Instance
        );

    private static CommandContext CreateCommandContext() =>
        new CommandContext([], Mock.Of<IRemainingArguments>(), "update", null);

    /// <summary>Override default to simulate modified files in the tracking index.</summary>
    private void SetupModifiedFiles(params string[] fileNames) =>
        MockFileTracking
            .Setup(ft => ft.DetectChangesWithoutUpdate(TestPath))
            .Returns(new ChangeDetectionResult { ModifiedFiles = [.. fileNames] });

    /// <summary>Override default to simulate added files in the tracking index.</summary>
    private void SetupAddedFiles(params string[] fileNames) =>
        MockFileTracking
            .Setup(ft => ft.DetectChangesWithoutUpdate(TestPath))
            .Returns(new ChangeDetectionResult { AddedFiles = [.. fileNames] });

    /// <summary>Override default to simulate config drift (files missing from .journalrc).</summary>
    private void SetupConfigDrift(params string[] filesToAdd) =>
        MockJournalConfiguration
            .Setup(c => c.DetectConfigChanges(TestPath))
            .Returns(new JournalConfigSyncResult { FilesToAdd = [.. filesToAdd] });

    #region No Changes

    [Fact]
    public void Execute_Should_ReturnZeroAndPrintUpToDate_When_NoChanges()
    {
        // Arrange — defaults: no tracking changes, no config drift
        var settings = new UpdateJournalSettings { FilePath = TestPath };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(0);
        _console.Output.ShouldContain("up to date");
    }

    #endregion

    #region Date Updates

    [Fact]
    public void Execute_Should_UpdateLastEditedDate_When_FilesAreModified()
    {
        // Arrange — simulate a modified file
        SetupModifiedFiles("note.md");

        var settings = new UpdateJournalSettings { FilePath = TestPath };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert — service called to update dates and re-index the file
        result.ShouldBe(0);
        _mockJournalUpdateService.Verify(
            s =>
                s.UpdateLastEditedDatesAndTracking(
                    TestPath,
                    It.Is<ChangeDetectionResult>(r => r.ModifiedFiles.Contains("note.md")),
                    false
                ),
            Times.Once
        );
    }

    [Fact]
    public void Execute_Should_UpdateLastEditedDate_When_DateFlagIsSet()
    {
        // Arrange — modified file; explicit --date flag
        SetupModifiedFiles("note.md");

        var settings = new UpdateJournalSettings { FilePath = TestPath, DateFlag = true };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(0);
        _mockJournalUpdateService.Verify(
            s =>
                s.UpdateLastEditedDatesAndTracking(
                    TestPath,
                    It.IsAny<ChangeDetectionResult>(),
                    false
                ),
            Times.Once
        );
    }

    [Fact]
    public void Execute_Should_NotUpdateDates_When_OnlyConfigFlagIsSet()
    {
        // Arrange — config has drift but only --config flag; date update must not run
        SetupConfigDrift("Learning-Rust.md");

        var settings = new UpdateJournalSettings { FilePath = TestPath, ConfigFlag = true };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(0);
        _mockJournalUpdateService.Verify(
            s =>
                s.UpdateLastEditedDatesAndTracking(
                    It.IsAny<string>(),
                    It.IsAny<ChangeDetectionResult>(),
                    It.IsAny<bool>()
                ),
            Times.Never
        );
    }

    [Fact]
    public void Execute_Should_NotUpdateDates_When_OnlyTocFlagIsSet()
    {
        // Arrange — modified files exist but only --toc flag; date update must not run
        SetupModifiedFiles("note.md");

        var settings = new UpdateJournalSettings { FilePath = TestPath, TocFlag = true };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(0);
        _mockJournalUpdateService.Verify(
            s =>
                s.UpdateLastEditedDatesAndTracking(
                    It.IsAny<string>(),
                    It.IsAny<ChangeDetectionResult>(),
                    It.IsAny<bool>()
                ),
            Times.Never
        );
    }

    #endregion

    #region Tracking Index Only

    [Fact]
    public void Execute_Should_UpdateTrackingOnlyWithoutModifyingMetadata_When_TrackingFlagSet()
    {
        // Arrange — modified files; --tracking suppresses date writes (trackingOnly=true)
        SetupModifiedFiles("note.md");

        var settings = new UpdateJournalSettings { FilePath = TestPath, Tracking = true };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert — trackingOnly=true passed to service so dates are NOT modified
        result.ShouldBe(0);
        _mockJournalUpdateService.Verify(
            s =>
                s.UpdateLastEditedDatesAndTracking(
                    TestPath,
                    It.IsAny<ChangeDetectionResult>(),
                    /* trackingOnly */ true
                ),
            Times.Once
        );
    }

    [Fact]
    public void Execute_Should_NotUpdateMetadata_When_TrackingFlagOverridesDateFlag()
    {
        // Arrange — both --tracking and --date set; trackingOnly=settings.Tracking=true
        SetupModifiedFiles("note.md");

        var settings = new UpdateJournalSettings
        {
            FilePath = TestPath,
            DateFlag = true,
            Tracking = true,
        };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert — trackingOnly=true, so dates are not written
        result.ShouldBe(0);
        _mockJournalUpdateService.Verify(
            s =>
                s.UpdateLastEditedDatesAndTracking(
                    TestPath,
                    It.IsAny<ChangeDetectionResult>(),
                    true
                ),
            Times.Once
        );
    }

    [Fact]
    public void Execute_Should_HandleAddedFiles_When_TrackingFlagSet()
    {
        // Arrange — new file not yet indexed
        SetupAddedFiles("new-note.md");

        var settings = new UpdateJournalSettings { FilePath = TestPath, Tracking = true };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert — service called to add file to tracking index (trackingOnly=true)
        result.ShouldBe(0);
        _mockJournalUpdateService.Verify(
            s =>
                s.UpdateLastEditedDatesAndTracking(
                    TestPath,
                    It.Is<ChangeDetectionResult>(r => r.AddedFiles.Contains("new-note.md")),
                    true
                ),
            Times.Once
        );
    }

    [Fact]
    public void Execute_Should_HandleDeletedFiles_When_TrackingFlagSet()
    {
        // Arrange — file deleted from disk but still in tracking index
        MockFileTracking
            .Setup(ft => ft.DetectChangesWithoutUpdate(TestPath))
            .Returns(new ChangeDetectionResult { DeletedFiles = ["note.md"] });

        var settings = new UpdateJournalSettings { FilePath = TestPath, Tracking = true };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert — service called to remove file from tracking index
        result.ShouldBe(0);
        _mockJournalUpdateService.Verify(
            s =>
                s.UpdateLastEditedDatesAndTracking(
                    TestPath,
                    It.Is<ChangeDetectionResult>(r => r.DeletedFiles.Contains("note.md")),
                    true
                ),
            Times.Once
        );
    }

    [Fact]
    public void Execute_Should_NotUpdateConfig_When_TrackingFlagSet()
    {
        // Arrange — new file added; --tracking only must not touch config
        SetupAddedFiles("new-note.md");

        var settings = new UpdateJournalSettings { FilePath = TestPath, Tracking = true };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert — config update must NOT run when only --tracking is set
        result.ShouldBe(0);
        _mockJournalUpdateService.Verify(
            s =>
                s.UpdateJournalConfig(
                    It.IsAny<string>(),
                    It.IsAny<JournalConfigSyncResult>()
                ),
            Times.Never
        );
    }

    [Fact]
    public void Execute_Should_NotUpdateToc_When_TrackingFlagSet()
    {
        // Arrange — modified files; --tracking only must not regenerate TOC
        SetupModifiedFiles("note.md");

        var settings = new UpdateJournalSettings { FilePath = TestPath, Tracking = true };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert — TOC update must NOT run when only --tracking is set
        result.ShouldBe(0);
        _mockJournalUpdateService.Verify(
            s => s.UpdateTableOfContents(It.IsAny<string>()),
            Times.Never
        );
    }

    [Fact]
    public void Execute_Should_WorkWithMultipleFiles_When_TrackingFlagSet()
    {
        // Arrange — three files all modified
        MockFileTracking
            .Setup(ft => ft.DetectChangesWithoutUpdate(TestPath))
            .Returns(
                new ChangeDetectionResult
                {
                    ModifiedFiles = ["note1.md", "note2.md", "note3.md"],
                }
            );

        var settings = new UpdateJournalSettings { FilePath = TestPath, Tracking = true };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert — all three files passed to service; dates left untouched (trackingOnly=true)
        result.ShouldBe(0);
        _mockJournalUpdateService.Verify(
            s =>
                s.UpdateLastEditedDatesAndTracking(
                    TestPath,
                    It.Is<ChangeDetectionResult>(r => r.ModifiedFiles.Count == 3),
                    true
                ),
            Times.Once
        );
    }

    #endregion

    #region Multiple Files

    [Fact]
    public void Execute_Should_UpdateMultipleModifiedFiles()
    {
        // Arrange — two of three files modified
        MockFileTracking
            .Setup(ft => ft.DetectChangesWithoutUpdate(TestPath))
            .Returns(
                new ChangeDetectionResult { ModifiedFiles = ["note1.md", "note3.md"] }
            );

        var settings = new UpdateJournalSettings { FilePath = TestPath };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert — both modified files forwarded to service
        result.ShouldBe(0);
        _mockJournalUpdateService.Verify(
            s =>
                s.UpdateLastEditedDatesAndTracking(
                    TestPath,
                    It.Is<ChangeDetectionResult>(r =>
                        r.ModifiedFiles.Count == 2
                        && r.ModifiedFiles.Contains("note1.md")
                        && r.ModifiedFiles.Contains("note3.md")
                    ),
                    false
                ),
            Times.Once
        );
    }

    #endregion

    #region Tracking Index Updated After Date Edit

    [Fact]
    public void Execute_Should_UpdateTrackingIndexAfterDateEdit()
    {
        // Arrange — modified file; on success the service writes new hash to tracking index
        SetupModifiedFiles("note.md");

        var settings = new UpdateJournalSettings { FilePath = TestPath };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert — service invoked; re-indexing is service responsibility
        result.ShouldBe(0);
        _mockJournalUpdateService.Verify(
            s =>
                s.UpdateLastEditedDatesAndTracking(
                    TestPath,
                    It.IsAny<ChangeDetectionResult>(),
                    false
                ),
            Times.Once
        );
    }

    #endregion

    #region Date Format

    [Fact]
    public void Execute_Should_UseConfiguredDateFormat()
    {
        // Date formatting is a service responsibility; the command just delegates.
        // Verify the command invokes the service so that format is applied downstream.
        SetupModifiedFiles("note.md");

        var settings = new UpdateJournalSettings { FilePath = TestPath };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(0);
        _mockJournalUpdateService.Verify(
            s =>
                s.UpdateLastEditedDatesAndTracking(
                    TestPath,
                    It.IsAny<ChangeDetectionResult>(),
                    false
                ),
            Times.Once
        );
    }

    #endregion

    #region Added Files (no date update)

    [Fact]
    public void Execute_Should_NotUpdateDatesForAddedFilesButTrackThemInIndex()
    {
        // Arrange — new file detected as "added" (not yet in tracking index)
        SetupAddedFiles("new-note.md");

        var settings = new UpdateJournalSettings { FilePath = TestPath };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert — service called with trackingOnly=false (all-mode);
        //           decision not to date-stamp added files is inside the service
        result.ShouldBe(0);
        _mockJournalUpdateService.Verify(
            s =>
                s.UpdateLastEditedDatesAndTracking(
                    TestPath,
                    It.Is<ChangeDetectionResult>(r => r.AddedFiles.Contains("new-note.md")),
                    false
                ),
            Times.Once
        );
    }

    #endregion

    #region Deleted Files

    [Fact]
    public void Execute_Should_RemoveDeletedFilesFromTrackingIndex()
    {
        // Arrange — file deleted from disk
        MockFileTracking
            .Setup(ft => ft.DetectChangesWithoutUpdate(TestPath))
            .Returns(new ChangeDetectionResult { DeletedFiles = ["doomed.md"] });

        var settings = new UpdateJournalSettings { FilePath = TestPath };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert — service removes the deleted file from the index
        result.ShouldBe(0);
        _mockJournalUpdateService.Verify(
            s =>
                s.UpdateLastEditedDatesAndTracking(
                    TestPath,
                    It.Is<ChangeDetectionResult>(r => r.DeletedFiles.Contains("doomed.md")),
                    false
                ),
            Times.Once
        );
    }

    [Fact]
    public void Execute_Should_HandleAllChangeTypesSimultaneously()
    {
        // Arrange — modified, added, and deleted files in the same pass
        MockFileTracking
            .Setup(ft => ft.DetectChangesWithoutUpdate(TestPath))
            .Returns(
                new ChangeDetectionResult
                {
                    ModifiedFiles = ["modified.md"],
                    AddedFiles = ["added.md"],
                    DeletedFiles = ["deleted.md"],
                }
            );

        var settings = new UpdateJournalSettings { FilePath = TestPath };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert — all change types passed to service in a single call
        result.ShouldBe(0);
        _mockJournalUpdateService.Verify(
            s =>
                s.UpdateLastEditedDatesAndTracking(
                    TestPath,
                    It.Is<ChangeDetectionResult>(r =>
                        r.ModifiedFiles.Contains("modified.md")
                        && r.AddedFiles.Contains("added.md")
                        && r.DeletedFiles.Contains("deleted.md")
                    ),
                    false
                ),
            Times.Once
        );
    }

    #endregion

    #region File Without Metadata

    [Fact]
    public void Execute_Should_InsertLastEditedDate_When_FileHasNoMetadata()
    {
        // Arrange — modified file with no front-matter; metadata insertion is service responsibility
        SetupModifiedFiles("bare.md");

        var settings = new UpdateJournalSettings { FilePath = TestPath };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(0);
        _mockJournalUpdateService.Verify(
            s =>
                s.UpdateLastEditedDatesAndTracking(
                    TestPath,
                    It.Is<ChangeDetectionResult>(r => r.ModifiedFiles.Contains("bare.md")),
                    false
                ),
            Times.Once
        );
    }

    #endregion

    #region Config Update

    [Fact]
    public void Execute_Should_AddNewFilesToConfig_When_ConfigFlagSet()
    {
        // Arrange — file tracked but absent from .journalrc
        SetupConfigDrift("Learning-Rust.md");

        var settings = new UpdateJournalSettings { FilePath = TestPath, ConfigFlag = true };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert — config sync called with the detected drift
        result.ShouldBe(0);
        _mockJournalUpdateService.Verify(
            s =>
                s.UpdateJournalConfig(
                    TestPath,
                    It.Is<JournalConfigSyncResult>(r =>
                        r.FilesToAdd.Contains("Learning-Rust.md")
                    )
                ),
            Times.Once
        );
    }

    [Fact]
    public void Execute_Should_RemoveDeletedFilesFromConfig_When_AllFlagsRun()
    {
        // Arrange — file deleted from disk; config still references it
        MockFileTracking
            .Setup(ft => ft.DetectChangesWithoutUpdate(TestPath))
            .Returns(new ChangeDetectionResult { DeletedFiles = ["Learning-Rust.md"] });
        MockJournalConfiguration
            .Setup(c => c.DetectConfigChanges(TestPath))
            .Returns(new JournalConfigSyncResult { FilesToRemove = ["Learning-Rust.md"] });

        var settings = new UpdateJournalSettings { FilePath = TestPath }; // all flags

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert — config sync removes the stale entry
        result.ShouldBe(0);
        _mockJournalUpdateService.Verify(
            s =>
                s.UpdateJournalConfig(
                    TestPath,
                    It.Is<JournalConfigSyncResult>(r =>
                        r.FilesToRemove.Contains("Learning-Rust.md")
                    )
                ),
            Times.Once
        );
    }

    [Fact]
    public void Execute_Should_NotUpdateConfig_When_OnlyDateFlagSet()
    {
        // Arrange — modified files but only --date flag; config must not run
        SetupModifiedFiles("Learning-Rust.md");

        var settings = new UpdateJournalSettings { FilePath = TestPath, DateFlag = true };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(0);
        _mockJournalUpdateService.Verify(
            s =>
                s.UpdateJournalConfig(
                    It.IsAny<string>(),
                    It.IsAny<JournalConfigSyncResult>()
                ),
            Times.Never
        );
    }

    [Fact]
    public void Execute_Should_UpdateConfig_When_NoFlagsSet()
    {
        // Arrange — new file in tracking but not in config (no flags = all defaults)
        SetupAddedFiles("Learning-Go.md");
        SetupConfigDrift("Learning-Go.md");

        var settings = new UpdateJournalSettings { FilePath = TestPath }; // no flags = all

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert — config was updated
        result.ShouldBe(0);
        _mockJournalUpdateService.Verify(
            s => s.UpdateJournalConfig(TestPath, It.IsAny<JournalConfigSyncResult>()),
            Times.Once
        );
    }

    #endregion

    #region Table of Contents Update

    [Fact]
    public void Execute_Should_UpdateTableOfContents_When_TocFlagSet()
    {
        // Arrange — tracking changes ensure hasAnythingToDo=true with --toc flag
        SetupModifiedFiles("Learning-CSharp.md");

        var settings = new UpdateJournalSettings { FilePath = TestPath, TocFlag = true };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(0);
        _mockJournalUpdateService.Verify(
            s => s.UpdateTableOfContents(TestPath),
            Times.Once
        );
    }

    [Fact]
    public void Execute_Should_UpdateTableOfContents_When_NoFlagsSet()
    {
        // Arrange — changes detected (no flags = all)
        SetupModifiedFiles("note.md");

        var settings = new UpdateJournalSettings { FilePath = TestPath };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(0);
        _mockJournalUpdateService.Verify(
            s => s.UpdateTableOfContents(TestPath),
            Times.Once
        );
    }

    [Fact]
    public void Execute_Should_NotUpdateToc_When_OnlyDateFlagSet()
    {
        // Arrange — modified files but only --date flag
        SetupModifiedFiles("note.md");

        var settings = new UpdateJournalSettings { FilePath = TestPath, DateFlag = true };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(0);
        _mockJournalUpdateService.Verify(
            s => s.UpdateTableOfContents(It.IsAny<string>()),
            Times.Never
        );
    }

    [Fact]
    public void Execute_Should_NotUpdateToc_When_OnlyConfigFlagSet()
    {
        // Arrange — config drift but only --config flag
        SetupConfigDrift("note.md");

        var settings = new UpdateJournalSettings { FilePath = TestPath, ConfigFlag = true };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(0);
        _mockJournalUpdateService.Verify(
            s => s.UpdateTableOfContents(It.IsAny<string>()),
            Times.Never
        );
    }

    [Fact]
    public void Execute_Should_TocReflectConfigChanges_When_AllFlagsRun()
    {
        // Arrange — new file causes both config and TOC to update in all-mode
        SetupAddedFiles("Learning-Rust.md");
        SetupConfigDrift("Learning-Rust.md");

        var settings = new UpdateJournalSettings { FilePath = TestPath }; // no flags = all

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert — config and TOC both updated
        result.ShouldBe(0);
        _mockJournalUpdateService.Verify(
            s => s.UpdateJournalConfig(TestPath, It.IsAny<JournalConfigSyncResult>()),
            Times.Once
        );
        _mockJournalUpdateService.Verify(
            s => s.UpdateTableOfContents(TestPath),
            Times.Once
        );
    }

    [Fact]
    public void Execute_Should_PreserveTocCreatedDate()
    {
        // Created-date preservation is handled inside UpdateTableOfContents (service responsibility).
        // This test verifies the command correctly delegates to the service when TocFlag is set.
        SetupModifiedFiles("note.md");

        var settings = new UpdateJournalSettings { FilePath = TestPath, TocFlag = true };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(0);
        _mockJournalUpdateService.Verify(
            s => s.UpdateTableOfContents(TestPath),
            Times.Once
        );
    }

    #endregion

    #region Error Handling

    [Fact]
    public void Execute_Should_ReturnOne_When_TrackingIndexNotFound()
    {
        // Arrange — tracking file absent
        MockFileSystem.Setup(fs => fs.FileExists(TrackingFilePath)).Returns(false);

        var settings = new UpdateJournalSettings { FilePath = TestPath };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(1);
        _console.Output.ShouldContain("tracking file");
        _console.Output.ShouldContain("not found");
    }

    [Fact]
    public void Execute_Should_ReturnOne_When_JournalrcNotFoundWithAllDefaults()
    {
        // Arrange — tracking file exists but .journalrc is missing
        MockFileSystem.Setup(fs => fs.FileExists(JournalrcPath)).Returns(false);

        var settings = new UpdateJournalSettings { FilePath = TestPath }; // all defaults require .journalrc

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(1);
        _console.Output.ShouldContain(".journalrc");
        _console.Output.ShouldContain("not found");
    }

    [Fact]
    public void Execute_Should_ReturnOne_When_JournalrcNotFoundAndConfigFlagSet()
    {
        // Arrange
        MockFileSystem.Setup(fs => fs.FileExists(JournalrcPath)).Returns(false);

        var settings = new UpdateJournalSettings { FilePath = TestPath, ConfigFlag = true };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(1);
        _console.Output.ShouldContain(".journalrc");
    }

    [Fact]
    public void Execute_Should_ReturnOne_When_JournalrcNotFoundAndTocFlagSet()
    {
        // Arrange
        MockFileSystem.Setup(fs => fs.FileExists(JournalrcPath)).Returns(false);

        var settings = new UpdateJournalSettings { FilePath = TestPath, TocFlag = true };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(1);
        _console.Output.ShouldContain(".journalrc");
    }

    [Fact]
    public void Execute_Should_NotRequireJournalrc_When_OnlyDateFlagSet()
    {
        // Arrange — tracking exists but no .journalrc; --date flag must not require journalrc
        MockFileSystem.Setup(fs => fs.FileExists(JournalrcPath)).Returns(false);
        SetupModifiedFiles("note.md");

        var settings = new UpdateJournalSettings { FilePath = TestPath, DateFlag = true };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert — succeeds because --date does not require .journalrc
        result.ShouldBe(0);
        _mockJournalUpdateService.Verify(
            s =>
                s.UpdateLastEditedDatesAndTracking(
                    TestPath,
                    It.IsAny<ChangeDetectionResult>(),
                    false
                ),
            Times.Once
        );
    }

    #endregion

    #region TOC File Exclusion Tests

    [Fact]
    public void Execute_Should_NotAddTocFileAsEntry_When_ConfigFlagSet()
    {
        // TOC file exclusion is IJournalConfiguration.DetectConfigChanges responsibility.
        // Verify the command passes drift unchanged (no TOC file in result) to UpdateJournalConfig.
        MockJournalConfiguration
            .Setup(c => c.DetectConfigChanges(TestPath))
            .Returns(
                new JournalConfigSyncResult { FilesToAdd = ["note.md"] } // TOC excluded upstream
            );

        var settings = new UpdateJournalSettings { FilePath = TestPath, ConfigFlag = true };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert — UpdateJournalConfig called with result that excludes the TOC file
        result.ShouldBe(0);
        _mockJournalUpdateService.Verify(
            s =>
                s.UpdateJournalConfig(
                    TestPath,
                    It.Is<JournalConfigSyncResult>(r =>
                        !r.FilesToAdd.Contains("1a-TableOfContents.md")
                    )
                ),
            Times.Once
        );
    }

    [Fact]
    public void Execute_Should_NotAddTocFileAsEntry_When_AllFlagsDefault()
    {
        // Arrange — all-mode; DetectConfigChanges correctly excludes the TOC file
        SetupAddedFiles("note.md");
        MockJournalConfiguration
            .Setup(c => c.DetectConfigChanges(TestPath))
            .Returns(
                new JournalConfigSyncResult { FilesToAdd = ["note.md"] } // no TOC file
            );

        var settings = new UpdateJournalSettings { FilePath = TestPath }; // all flags

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert — UpdateJournalConfig does not receive the TOC file
        result.ShouldBe(0);
        _mockJournalUpdateService.Verify(
            s =>
                s.UpdateJournalConfig(
                    TestPath,
                    It.Is<JournalConfigSyncResult>(r =>
                        !r.FilesToAdd.Contains("1a-TableOfContents.md")
                    )
                ),
            Times.Once
        );
    }

    [Fact]
    public void Execute_Should_NotAddCustomTocFileAsEntry()
    {
        // Arrange — custom TOC filename; exclusion is service responsibility.
        // DetectConfigChanges returns empty (custom TOC correctly excluded).
        SetupModifiedFiles("note.md");
        MockJournalConfiguration
            .Setup(c => c.DetectConfigChanges(TestPath))
            .Returns(new JournalConfigSyncResult()); // custom TOC excluded

        var settings = new UpdateJournalSettings { FilePath = TestPath };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert — UpdateJournalConfig called with empty result; no custom TOC file added
        result.ShouldBe(0);
        _mockJournalUpdateService.Verify(
            s =>
                s.UpdateJournalConfig(
                    TestPath,
                    It.Is<JournalConfigSyncResult>(r =>
                        r.FilesToAdd.Count == 0 && r.FilesToRemove.Count == 0
                    )
                ),
            Times.Once
        );
    }

    #endregion

    #region RenameToc

    [Fact]
    public void Execute_Should_CallRenameToc_When_RenameTocFlagProvided()
    {
        // Arrange
        var settings = new UpdateJournalSettings { FilePath = TestPath, RenameToc = "MyContents" };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(0);
        _mockJournalUpdateService.Verify(
            s => s.RenameToc(TestPath, "MyContents"),
            Times.Once
        );
    }

    [Fact]
    public void Execute_Should_ReturnOneAndPrintError_When_TocRenameConflictExceptionThrown()
    {
        // Arrange — RenameToc throws a conflict exception
        _mockJournalUpdateService
            .Setup(s => s.RenameToc(TestPath, "MyContents"))
            .Throws(new TocRenameConflictException(TestPath, "MyContents.md"));

        var settings = new UpdateJournalSettings { FilePath = TestPath, RenameToc = "MyContents" };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(1);
        _console.Output.ShouldContain("Error:");
        _console.Output.ShouldContain("MyContents.md");
    }

    [Fact]
    public void Execute_Should_CallRenameTocAndOtherUpdates_When_CombinedWithOtherFlags()
    {
        // Arrange — --rename-toc combined with --date; both operations must run
        SetupModifiedFiles("note.md");

        var settings = new UpdateJournalSettings
        {
            FilePath = TestPath,
            RenameToc = "MyContents",
            DateFlag = true,
        };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert — rename and date update both happened
        result.ShouldBe(0);
        _mockJournalUpdateService.Verify(s => s.RenameToc(TestPath, "MyContents"), Times.Once);
        _mockJournalUpdateService.Verify(
            s =>
                s.UpdateLastEditedDatesAndTracking(
                    TestPath,
                    It.IsAny<ChangeDetectionResult>(),
                    false
                ),
            Times.Once
        );
    }

    [Fact]
    public void Execute_Should_NotCallRenameToc_When_NoFlagsProvided()
    {
        // Arrange — all-mode with no --rename-toc
        SetupModifiedFiles("1a-TableOfContents.md");

        var settings = new UpdateJournalSettings { FilePath = TestPath };

        // Act
        CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert — RenameToc must never be called
        _mockJournalUpdateService.Verify(
            s => s.RenameToc(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never
        );
    }

    #endregion

    #region Config Sync Regression and Behavioral Tests

    [Fact]
    public void Execute_Should_AddNewFilesToConfig_When_TrackingOnlyThenConfig()
    {
        // Regression: --tracking run adds file to tracking index but NOT to config.
        // A subsequent --config run must still detect and add the file.

        // Run 1: --tracking only
        SetupAddedFiles("Learning-Rust.md");

        var trackingResult = CreateCommand().Execute(
            CreateCommandContext(),
            new UpdateJournalSettings { FilePath = TestPath, Tracking = true }
        );

        trackingResult.ShouldBe(0);
        _mockJournalUpdateService.Verify(
            s =>
                s.UpdateLastEditedDatesAndTracking(
                    TestPath,
                    It.IsAny<ChangeDetectionResult>(),
                    true
                ),
            Times.Once
        );
        _mockJournalUpdateService.Verify(
            s =>
                s.UpdateJournalConfig(
                    It.IsAny<string>(),
                    It.IsAny<JournalConfigSyncResult>()
                ),
            Times.Never
        );

        // Run 2: --config only — file is now in tracking index so drift is detected
        SetupConfigDrift("Learning-Rust.md");

        var configResult = CreateCommand().Execute(
            CreateCommandContext(),
            new UpdateJournalSettings { FilePath = TestPath, ConfigFlag = true }
        );

        configResult.ShouldBe(0);
        _mockJournalUpdateService.Verify(
            s =>
                s.UpdateJournalConfig(
                    TestPath,
                    It.Is<JournalConfigSyncResult>(r =>
                        r.FilesToAdd.Contains("Learning-Rust.md")
                    )
                ),
            Times.Once
        );
    }

    [Fact]
    public void Execute_Should_StillUpdateConfig_When_NoTrackingChangesButConfigDrift()
    {
        // When file hashes are all current (no tracking changes) but config is missing entries,
        // the command must NOT return early — it must still update config.
        SetupConfigDrift("Learning-Go.md"); // no tracking changes (default)

        var settings = new UpdateJournalSettings { FilePath = TestPath, ConfigFlag = true };

        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        result.ShouldBe(0);
        _mockJournalUpdateService.Verify(
            s =>
                s.UpdateJournalConfig(
                    TestPath,
                    It.Is<JournalConfigSyncResult>(r => r.FilesToAdd.Contains("Learning-Go.md"))
                ),
            Times.Once
        );
    }

    [Fact]
    public void Execute_Should_NotCallDetectConfigChanges_When_TrackingFlagSet()
    {
        // When only --tracking is set, config sync must NOT be triggered at all.
        SetupModifiedFiles("Learning-Rust.md");

        var settings = new UpdateJournalSettings { FilePath = TestPath, Tracking = true };

        CreateCommand().Execute(CreateCommandContext(), settings);

        MockJournalConfiguration.Verify(
            c => c.DetectConfigChanges(It.IsAny<string>()),
            Times.Never
        );
    }

    [Fact]
    public void Execute_Should_ReturnZeroWithMessage_When_NoChangesInTrackingAndConfig()
    {
        // Both tracking index and config fully in sync — command must print "up to date"
        // (defaults: no tracking changes, no config drift)
        var settings = new UpdateJournalSettings { FilePath = TestPath }; // all flags

        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        result.ShouldBe(0);
        _console.Output.ShouldContain("up to date");
    }

    #endregion

    // =========================================================================
    // Dry-run tests
    // =========================================================================

    #region DryRun — general

    [Fact]
    public void Execute_Should_ReturnZeroAndShowNothingToDo_When_DryRunAndTrackingAndConfigInSync()
    {
        // Arrange — BuildDryRunReport returns empty report (default); nothing to do
        var settings = new UpdateJournalSettings
        {
            FilePath = TestPath,
            DryRun = true,
            Tracking = true,
            ConfigFlag = true,
        };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(0);
        _console.Output.ShouldContain("up to date");
        _console.Output.ShouldContain("--dry-run");
    }

    [Fact]
    public void Execute_Should_NotWriteAnyFiles_When_DryRunAndChangesDetected()
    {
        // Arrange — report has changes but no mutating methods must be called
        _mockJournalUpdateService
            .Setup(s =>
                s.BuildDryRunReport(
                    It.IsAny<string>(),
                    It.IsAny<ChangeDetectionResult?>(),
                    It.IsAny<JournalConfigSyncResult?>(),
                    It.IsAny<bool>(),
                    It.IsAny<string?>()
                )
            )
            .Returns(
                new UpdateDryRunReport
                {
                    TrackingChanges = new ChangeDetectionResult
                    {
                        AddedFiles = ["Learning-Rust.md"],
                    },
                }
            );

        var settings = new UpdateJournalSettings { FilePath = TestPath, DryRun = true };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert — no mutating service methods were called
        result.ShouldBe(0);
        _mockJournalUpdateService.Verify(
            s =>
                s.UpdateLastEditedDatesAndTracking(
                    It.IsAny<string>(),
                    It.IsAny<ChangeDetectionResult>(),
                    It.IsAny<bool>()
                ),
            Times.Never
        );
        _mockJournalUpdateService.Verify(
            s =>
                s.UpdateJournalConfig(
                    It.IsAny<string>(),
                    It.IsAny<JournalConfigSyncResult>()
                ),
            Times.Never
        );
        _mockJournalUpdateService.Verify(
            s => s.UpdateTableOfContents(It.IsAny<string>()),
            Times.Never
        );
    }

    [Fact]
    public void Execute_Should_ShowFooterMessage_When_DryRun()
    {
        // Arrange — report with changes so the footer is shown
        _mockJournalUpdateService
            .Setup(s =>
                s.BuildDryRunReport(
                    It.IsAny<string>(),
                    It.IsAny<ChangeDetectionResult?>(),
                    It.IsAny<JournalConfigSyncResult?>(),
                    It.IsAny<bool>(),
                    It.IsAny<string?>()
                )
            )
            .Returns(
                new UpdateDryRunReport
                {
                    TrackingChanges = new ChangeDetectionResult { AddedFiles = ["new.md"] },
                }
            );

        var settings = new UpdateJournalSettings { FilePath = TestPath, DryRun = true };

        // Act
        CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert
        _console.Output.ShouldContain("No changes were applied");
    }

    [Fact]
    public void Execute_Should_ReturnOne_When_DryRunAndTrackingFileMissing()
    {
        // Arrange — tracking file missing
        MockFileSystem.Setup(fs => fs.FileExists(TrackingFilePath)).Returns(false);

        var settings = new UpdateJournalSettings { FilePath = TestPath, DryRun = true };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(1);
        _console.Output.ShouldContain("Error:");
    }

    [Fact]
    public void Execute_Should_ReturnOne_When_DryRunAndJournalrcMissingAndConfigRequested()
    {
        // Arrange — .journalrc missing; --config flag requires it
        MockFileSystem.Setup(fs => fs.FileExists(JournalrcPath)).Returns(false);

        var settings = new UpdateJournalSettings
        {
            FilePath = TestPath,
            DryRun = true,
            ConfigFlag = true,
        };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(1);
        _console.Output.ShouldContain("Error:");
    }

    #endregion

    #region DryRun — flag scoping

    [Fact]
    public void Execute_Should_ShowTrackingSection_When_DryRunAndTrackingOnly()
    {
        // Arrange — report with tracking changes
        var report = new UpdateDryRunReport
        {
            TrackingChanges = new ChangeDetectionResult { AddedFiles = ["new-entry.md"] },
        };
        _mockJournalUpdateService
            .Setup(s =>
                s.BuildDryRunReport(
                    It.IsAny<string>(),
                    It.IsAny<ChangeDetectionResult?>(),
                    It.IsAny<JournalConfigSyncResult?>(),
                    It.IsAny<bool>(),
                    It.IsAny<string?>()
                )
            )
            .Returns(report);

        var settings = new UpdateJournalSettings
        {
            FilePath = TestPath,
            DryRun = true,
            Tracking = true,
        };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert — renderer invoked with a report that has tracking changes
        result.ShouldBe(0);
        _mockDryRunRenderer.Verify(
            r =>
                r.Render(
                    It.Is<UpdateDryRunReport>(rep =>
                        rep.TrackingChanges != null
                        && rep.TrackingChanges.AddedFiles.Contains("new-entry.md")
                    ),
                    TestPath
                ),
            Times.Once
        );
    }

    [Fact]
    public void Execute_Should_ShowConfigSection_When_DryRunAndConfigOnly()
    {
        // Arrange — report with config changes
        var report = new UpdateDryRunReport
        {
            ConfigChanges = new JournalConfigSyncResult { FilesToAdd = ["unregistered.md"] },
        };
        _mockJournalUpdateService
            .Setup(s =>
                s.BuildDryRunReport(
                    It.IsAny<string>(),
                    It.IsAny<ChangeDetectionResult?>(),
                    It.IsAny<JournalConfigSyncResult?>(),
                    It.IsAny<bool>(),
                    It.IsAny<string?>()
                )
            )
            .Returns(report);

        var settings = new UpdateJournalSettings
        {
            FilePath = TestPath,
            DryRun = true,
            ConfigFlag = true,
        };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert — renderer invoked with a report that has config changes
        result.ShouldBe(0);
        _mockDryRunRenderer.Verify(
            r =>
                r.Render(
                    It.Is<UpdateDryRunReport>(rep =>
                        rep.ConfigChanges != null
                        && rep.ConfigChanges.FilesToAdd.Contains("unregistered.md")
                    ),
                    TestPath
                ),
            Times.Once
        );
    }

    [Fact]
    public void Execute_Should_ShowTocSection_When_DryRunAndTocOnly()
    {
        // Arrange — report with TOC preview showing a diff
        var report = new UpdateDryRunReport
        {
            TocPreview = new TocDiffResult
            {
                CurrentContent = "old",
                PreviewContent = "old\nnew entry",
            },
        };
        _mockJournalUpdateService
            .Setup(s =>
                s.BuildDryRunReport(
                    It.IsAny<string>(),
                    It.IsAny<ChangeDetectionResult?>(),
                    It.IsAny<JournalConfigSyncResult?>(),
                    It.IsAny<bool>(),
                    It.IsAny<string?>()
                )
            )
            .Returns(report);

        var settings = new UpdateJournalSettings
        {
            FilePath = TestPath,
            DryRun = true,
            TocFlag = true,
        };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert — renderer invoked with a report that has a TOC preview
        result.ShouldBe(0);
        _mockDryRunRenderer.Verify(
            r =>
                r.Render(
                    It.Is<UpdateDryRunReport>(rep =>
                        rep.TocPreview != null && rep.TocPreview.HasChanges
                    ),
                    TestPath
                ),
            Times.Once
        );
    }

    [Fact]
    public void Execute_Should_ShowBothSections_When_DryRunAndMultiFlagTrackingAndConfig()
    {
        // Arrange — report with both tracking and config changes
        var report = new UpdateDryRunReport
        {
            TrackingChanges = new ChangeDetectionResult { ModifiedFiles = ["entry.md"] },
            ConfigChanges = new JournalConfigSyncResult { FilesToAdd = ["entry.md"] },
        };
        _mockJournalUpdateService
            .Setup(s =>
                s.BuildDryRunReport(
                    It.IsAny<string>(),
                    It.IsAny<ChangeDetectionResult?>(),
                    It.IsAny<JournalConfigSyncResult?>(),
                    It.IsAny<bool>(),
                    It.IsAny<string?>()
                )
            )
            .Returns(report);

        var settings = new UpdateJournalSettings
        {
            FilePath = TestPath,
            DryRun = true,
            Tracking = true,
            ConfigFlag = true,
        };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert — renderer invoked with both sections populated
        result.ShouldBe(0);
        _mockDryRunRenderer.Verify(
            r =>
                r.Render(
                    It.Is<UpdateDryRunReport>(rep =>
                        rep.TrackingChanges != null && rep.ConfigChanges != null
                    ),
                    TestPath
                ),
            Times.Once
        );
    }

    [Fact]
    public void Execute_Should_ShowRenamePreview_When_DryRunAndRenameToc()
    {
        // Arrange — report with rename preview
        var report = new UpdateDryRunReport
        {
            RenamePreview = new TocRenameDryRunResult
            {
                CurrentName = "1a-TableOfContents.md",
                NewName = "MyNotes.md",
                FilesWithBacklinks = [],
            },
        };
        _mockJournalUpdateService
            .Setup(s =>
                s.BuildDryRunReport(
                    It.IsAny<string>(),
                    It.IsAny<ChangeDetectionResult?>(),
                    It.IsAny<JournalConfigSyncResult?>(),
                    It.IsAny<bool>(),
                    It.IsAny<string?>()
                )
            )
            .Returns(report);

        var settings = new UpdateJournalSettings
        {
            FilePath = TestPath,
            DryRun = true,
            RenameToc = "MyNotes",
        };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert — renderer called with rename preview; actual rename must NOT happen
        result.ShouldBe(0);
        _mockDryRunRenderer.Verify(
            r =>
                r.Render(
                    It.Is<UpdateDryRunReport>(rep =>
                        rep.RenamePreview != null && rep.RenamePreview.NewName == "MyNotes.md"
                    ),
                    TestPath
                ),
            Times.Once
        );
        _mockJournalUpdateService.Verify(
            s => s.RenameToc(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never
        );
    }

    [Fact]
    public void Execute_Should_NotCallMutatingServiceMethods_When_DryRun()
    {
        // Verify that UpdateLastEditedDatesAndTracking, UpdateJournalConfig,
        // UpdateTableOfContents, and RenameToc are never called when --dry-run is active.
        _mockJournalUpdateService
            .Setup(s =>
                s.BuildDryRunReport(
                    It.IsAny<string>(),
                    It.IsAny<ChangeDetectionResult?>(),
                    It.IsAny<JournalConfigSyncResult?>(),
                    It.IsAny<bool>(),
                    It.IsAny<string?>()
                )
            )
            .Returns(
                new UpdateDryRunReport
                {
                    TrackingChanges = new ChangeDetectionResult { AddedFiles = ["file.md"] },
                }
            );

        var settings = new UpdateJournalSettings { FilePath = TestPath, DryRun = true };

        CreateCommand().Execute(CreateCommandContext(), settings);

        _mockJournalUpdateService.Verify(
            s =>
                s.UpdateLastEditedDatesAndTracking(
                    It.IsAny<string>(),
                    It.IsAny<ChangeDetectionResult>(),
                    It.IsAny<bool>()
                ),
            Times.Never
        );
        _mockJournalUpdateService.Verify(
            s =>
                s.UpdateJournalConfig(
                    It.IsAny<string>(),
                    It.IsAny<JournalConfigSyncResult>()
                ),
            Times.Never
        );
        _mockJournalUpdateService.Verify(
            s => s.UpdateTableOfContents(It.IsAny<string>()),
            Times.Never
        );
        _mockJournalUpdateService.Verify(
            s => s.RenameToc(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never
        );
    }

    #endregion

    #region Sync Flag

    [Fact]
    public void ExecuteCore_Should_UpdateTrackingConfigToc_When_SyncFlagSet()
    {
        // Arrange
        SetupModifiedFiles("file.md");
        var settings = new UpdateJournalSettings { FilePath = TestPath, Sync = true };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert
        result.ShouldBe(0);
        _mockJournalUpdateService.Verify(
            s => s.UpdateLastEditedDatesAndTracking(
                TestPath,
                It.IsAny<ChangeDetectionResult>(),
                true
            ),
            Times.Once
        );
        _mockJournalUpdateService.Verify(
            s => s.UpdateJournalConfig(TestPath, It.IsAny<JournalConfigSyncResult>()),
            Times.Once
        );
        _mockJournalUpdateService.Verify(
            s => s.UpdateTableOfContents(TestPath),
            Times.Once
        );
        _console.Output.ShouldContain("--sync active");
    }

    [Fact]
    public void ExecuteCore_Should_PrintSyncActiveLine_When_SyncFlagAndChangesExist()
    {
        // Arrange
        SetupModifiedFiles("file.md");
        var settings = new UpdateJournalSettings { FilePath = TestPath, Sync = true };

        // Act
        CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert
        _console.Output.ShouldContain("--sync active: Last Edited dates were not updated");
    }

    [Fact]
    public void ExecuteCore_Should_NotPrintSyncActiveLine_When_SyncFlagAndNoChanges()
    {
        // Arrange — default: no changes
        var settings = new UpdateJournalSettings { FilePath = TestPath, Sync = true };

        // Act
        CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert
        _console.Output.ShouldNotContain("--sync active");
        _console.Output.ShouldContain("Everything is up to date.");
    }

    [Fact]
    public void ExecuteCore_Should_NotCallUpdateLastEditedDates_When_SyncFlagSet()
    {
        // Arrange
        SetupModifiedFiles("file.md");
        var settings = new UpdateJournalSettings { FilePath = TestPath, Sync = true };

        // Act
        CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert — trackingOnly=false must NEVER be called; trackingOnly=true must be called once
        _mockJournalUpdateService.Verify(
            s => s.UpdateLastEditedDatesAndTracking(
                It.IsAny<string>(),
                It.IsAny<ChangeDetectionResult>(),
                false
            ),
            Times.Never
        );
        _mockJournalUpdateService.Verify(
            s => s.UpdateLastEditedDatesAndTracking(
                It.IsAny<string>(),
                It.IsAny<ChangeDetectionResult>(),
                true
            ),
            Times.Once
        );
    }

    [Fact]
    public void ExecuteDryRun_Should_IncludeAllSections_When_SyncFlag()
    {
        // Arrange — report with tracking, config, and TOC changes
        var report = new UpdateDryRunReport
        {
            TrackingChanges = new ChangeDetectionResult { ModifiedFiles = ["entry.md"] },
            ConfigChanges = new JournalConfigSyncResult { FilesToAdd = ["entry.md"] },
            TocPreview = new TocDiffResult { CurrentContent = "old", PreviewContent = "new" },
        };
        _mockJournalUpdateService
            .Setup(s =>
                s.BuildDryRunReport(
                    It.IsAny<string>(),
                    It.IsAny<ChangeDetectionResult?>(),
                    It.IsAny<JournalConfigSyncResult?>(),
                    It.IsAny<bool>(),
                    It.IsAny<string?>()
                )
            )
            .Returns(report);

        var settings = new UpdateJournalSettings { FilePath = TestPath, DryRun = true, Sync = true };

        // Act
        var result = CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert — BuildDryRunReport called with non-null tracking, config, and includeToc=true
        result.ShouldBe(0);
        _mockJournalUpdateService.Verify(
            s => s.BuildDryRunReport(
                TestPath,
                It.IsNotNull<ChangeDetectionResult?>(),
                It.IsNotNull<JournalConfigSyncResult?>(),
                true,
                null
            ),
            Times.Once
        );
    }

    [Fact]
    public void ExecuteDryRun_Should_WriteNoFiles_When_SyncDryRun()
    {
        // Arrange
        var settings = new UpdateJournalSettings { FilePath = TestPath, DryRun = true, Sync = true };

        // Act
        CreateCommand().Execute(CreateCommandContext(), settings);

        // Assert — no file write methods called
        MockFileSystem.Verify(
            fs => fs.CreateFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never
        );
        MockFileSystem.Verify(
            fs => fs.UpdateFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never
        );
        MockFileSystem.Verify(
            fs => fs.CreateMarkdownFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never
        );
    }

    [Fact]
    public void Validate_Should_ReturnError_When_SyncAndDateCombined()
    {
        var settings = new UpdateJournalSettings { Sync = true, DateFlag = true };
        var result = settings.Validate();
        result.Successful.ShouldBeFalse();
        result.Message.ShouldContain("--date");
    }

    [Fact]
    public void Validate_Should_ReturnError_When_SyncAndTrackingCombined()
    {
        var settings = new UpdateJournalSettings { Sync = true, Tracking = true };
        var result = settings.Validate();
        result.Successful.ShouldBeFalse();
        result.Message.ShouldContain("--tracking");
    }

    #endregion
}
