using System.Text.Json;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.Configuration.Models;
using markdown_journal_cli.Infrastructure.Tracking;
using markdown_journal_cli.Infrastructure.Tracking.Models;
using markdown_journal_cli.Services;
using markdown_journal_cli.Tests.Infrastructure.FileSystem;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;

namespace markdown_journal_cli.Tests.Services;

public class JournalRegistrationDriftDetectorTests
{
    private readonly TestFileSystem _fileSystem;
    private readonly string _testDirectory;
    private readonly IOptions<JournalSettings> _journalSettings;
    private readonly JournalTocStructureRepository _tocRepo;
    private readonly JournalConfiguration _journalConfiguration;
    private readonly Mock<IFileTracking> _tracking;
    private readonly JournalRegistrationDriftDetector _detector;

    public JournalRegistrationDriftDetectorTests()
    {
        _fileSystem = new TestFileSystem();
        _testDirectory = "/test/directory";

        _journalSettings = Options.Create(
            new JournalSettings
            {
                JournalConfigFileName = ".journalrc",
                MetadataDirName = ".mdjournal",
                TocStructureFileName = ".journaltoc",
                TrackingFileName = ".journalindex",
                TableOfContentsFileName = "1a-TableOfContents",
            }
        );

        _tocRepo = new JournalTocStructureRepository(_fileSystem, _journalSettings);
        _journalConfiguration = new JournalConfiguration(
            _fileSystem,
            _journalSettings,
            NullLogger<JournalConfiguration>.Instance,
            _tocRepo
        );
        _tracking = new Mock<IFileTracking>();
        _tracking.Setup(t => t.LoadIndex(It.IsAny<string>())).Returns(new JournalIndex());

        _detector = new JournalRegistrationDriftDetector(
            _journalConfiguration,
            _tracking.Object,
            _tocRepo,
            _journalSettings,
            NullLogger<JournalRegistrationDriftDetector>.Instance
        );
    }

    private void SetTrackedFiles(params string[] files)
    {
        var index = new JournalIndex();
        foreach (var file in files)
        {
            index.Files[file] = new FileState
            {
                FilePath = file,
                Hash = "hash",
                LastChecked = DateTime.UtcNow,
            };
        }

        _tracking.Setup(t => t.LoadIndex(_testDirectory)).Returns(index);
    }

    private void CreateConfig(string tocFile = "1a-TableOfContents.md", string[]? ignore = null)
    {
        _fileSystem.CreateDirectory(_testDirectory);
        _journalConfiguration.Create(
            _testDirectory,
            new JournalConfig
            {
                JournalName = "Test",
                TableOfContents = new TableOfContents { File = tocFile, IgnoreFiles = ignore },
                TrackingIndex = new TrackingIndex(),
            }
        );
    }

    private void SetupTocStructure(Topic[]? topics = null, Entries[]? rootEntries = null)
    {
        var structure = new JournalTocStructure
        {
            Structure = new Structure { Topics = topics ?? [] },
            RootEntries = rootEntries ?? [],
        };

        var metadataDir = Path.Combine(_testDirectory, ".mdjournal");
        _fileSystem.CreateDirectory(metadataDir);
        _fileSystem.CreateFile(
            metadataDir,
            ".journaltoc",
            JsonSerializer.Serialize(structure)
        );
    }

    [Fact]
    public void DetectDrift_ReturnsFilesToAdd_WhenTrackedFilesNotRegistered()
    {
        CreateConfig();
        SetTrackedFiles("Learning-Rust.md");

        var result = _detector.DetectDrift(_testDirectory);

        result.FilesToAdd.ShouldContain("Learning-Rust.md");
        result.FilesToRemove.ShouldBeEmpty();
    }

    [Fact]
    public void DetectDrift_ReturnsFilesToRemove_WhenRegisteredFilesNotTracked()
    {
        CreateConfig();
        SetupTocStructure(rootEntries: [new Entries { Name = "Old", File = "old-note.md" }]);
        SetTrackedFiles();

        var result = _detector.DetectDrift(_testDirectory);

        result.FilesToRemove.ShouldContain("old-note.md");
        result.FilesToAdd.ShouldBeEmpty();
    }

    [Fact]
    public void DetectDrift_ExcludesTocFile_FromFilesToAdd()
    {
        CreateConfig();
        SetTrackedFiles("1a-TableOfContents.md");

        var result = _detector.DetectDrift(_testDirectory);

        result.FilesToAdd.ShouldNotContain("1a-TableOfContents.md");
        result.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void DetectDrift_ReturnsEmpty_WhenJournalrcDoesNotExist()
    {
        _fileSystem.CreateDirectory(_testDirectory);
        SetTrackedFiles("note.md");

        var result = _detector.DetectDrift(_testDirectory);

        result.HasChanges.ShouldBeFalse();
        result.FilesToAdd.ShouldBeEmpty();
        result.FilesToRemove.ShouldBeEmpty();
    }

    [Fact]
    public void DetectDrift_HandlesTopicEntries()
    {
        CreateConfig();
        SetupTocStructure(
            topics:
            [
                new Topic
                {
                    Name = "Learning",
                    Entries = [new Entries { Name = "Rust", File = "Learning-Rust.md" }],
                    Subtopics = null,
                },
            ]
        );
        SetTrackedFiles();

        var result = _detector.DetectDrift(_testDirectory);

        result.FilesToRemove.ShouldContain("Learning-Rust.md");
    }

    [Fact]
    public void DetectDrift_IsCaseInsensitive_ForFileComparison()
    {
        CreateConfig();
        SetupTocStructure(rootEntries: [new Entries { Name = "Note", File = "2A-NOTE.MD" }]);
        SetTrackedFiles("2a-Note.md");

        var result = _detector.DetectDrift(_testDirectory);

        result.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void DetectDrift_IgnoredFile_IsTracked_ShouldNotAppearInFilesToAdd()
    {
        CreateConfig(ignore: ["draft.md"]);
        SetTrackedFiles("draft.md");

        var result = _detector.DetectDrift(_testDirectory);

        result.FilesToAdd.ShouldNotContain("draft.md");
        result.FilesToRemove.ShouldNotContain("draft.md");
        result.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void DetectDrift_MixedFiles_IgnoredFilesDoNotInterfereWithOtherChanges()
    {
        CreateConfig(ignore: ["draft.md"]);
        SetupTocStructure(
            rootEntries:
            [
                new Entries { Name = "Intro", File = "1a-Intro.md" },
                new Entries { Name = "Stale", File = "stale.md" },
            ]
        );
        SetTrackedFiles("1a-Intro.md", "draft.md", "new-note.md");

        var result = _detector.DetectDrift(_testDirectory);

        result.FilesToAdd.ShouldContain("new-note.md");
        result.FilesToRemove.ShouldContain("stale.md");
        result.FilesToAdd.ShouldNotContain("draft.md");
        result.FilesToRemove.ShouldNotContain("draft.md");
    }
}
