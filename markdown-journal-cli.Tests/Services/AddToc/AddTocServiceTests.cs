using markdown_journal_cli.Infrastructure.Configuration.Models;
using markdown_journal_cli.Infrastructure.Transactions;
using markdown_journal_cli.Services.AddToc;
using markdown_journal_cli.Tests.Infrastructure;
using Moq;
using Shouldly;
using Xunit;

namespace markdown_journal_cli.Tests.Services.AddToc;

/// <summary>
/// Unit tests for <see cref="AddTocService"/> covering all artifact-creation combinations.
/// </summary>
public class AddTocServiceTests : ServiceTestBase
{
    private const string JournalDir = "/test/journal";
    private const string MetadataDir = "/test/journal/.mdjournal";
    private const string TocStructurePath = "/test/journal/.mdjournal/.journaltoc";
    private const string TocMdPath = "/test/journal/1a-TableOfContents.md";

    private readonly AddTocService _service;

    private static JournalConfig DefaultConfig =>
        new()
        {
            JournalName = "TestJournal",
            TableOfContents = new TableOfContents { File = "1a-TableOfContents.md" },
        };

    public AddTocServiceTests()
    {
        // Default: .journalrc exists and can be read
        MockJournalConfiguration.Setup(c => c.Read(JournalDir)).Returns(DefaultConfig);

        _service = new AddTocService(
            MockFileSystem.Object,
            MockJournalConfiguration.Object,
            MockTocStructureRepository.Object,
            MockTableOfContentsService.Object,
            NoOpCoordinator,
            NoOpReporter,
            JournalSettings
        );
    }

    #region Constructor Validation

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenFileSystemIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new AddTocService(
                null!,
                MockJournalConfiguration.Object,
                MockTocStructureRepository.Object,
                MockTableOfContentsService.Object,
                NoOpCoordinator,
                NoOpReporter,
                JournalSettings
            )
        );
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenJournalConfigurationIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new AddTocService(
                MockFileSystem.Object,
                null!,
                MockTocStructureRepository.Object,
                MockTableOfContentsService.Object,
                NoOpCoordinator,
                NoOpReporter,
                JournalSettings
            )
        );
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenTocStructureRepositoryIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new AddTocService(
                MockFileSystem.Object,
                MockJournalConfiguration.Object,
                null!,
                MockTableOfContentsService.Object,
                NoOpCoordinator,
                NoOpReporter,
                JournalSettings
            )
        );
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenTableOfContentsServiceIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new AddTocService(
                MockFileSystem.Object,
                MockJournalConfiguration.Object,
                MockTocStructureRepository.Object,
                null!,
                NoOpCoordinator,
                NoOpReporter,
                JournalSettings
            )
        );
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenTxCoordinatorIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new AddTocService(
                MockFileSystem.Object,
                MockJournalConfiguration.Object,
                MockTocStructureRepository.Object,
                MockTableOfContentsService.Object,
                null!,
                NoOpReporter,
                JournalSettings
            )
        );
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenRollbackReporterIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new AddTocService(
                MockFileSystem.Object,
                MockJournalConfiguration.Object,
                MockTocStructureRepository.Object,
                MockTableOfContentsService.Object,
                NoOpCoordinator,
                null!,
                JournalSettings
            )
        );
    }

    #endregion

    #region Both Artifacts (no flags)

    [Fact]
    public void Execute_ReturnCreated_WhenBothArtifactsAreCreated()
    {
        // Arrange — neither artifact exists
        MockFileSystem.Setup(fs => fs.FileExists(TocStructurePath)).Returns(false);
        MockFileSystem.Setup(fs => fs.FileExists(TocMdPath)).Returns(false);

        // Act
        var result = _service.Execute(JournalDir);

        // Assert
        result.ShouldBe(AddTocResult.Created);
        MockTocStructureRepository.Verify(
            r => r.Save(It.IsAny<JournalTocStructure>(), MetadataDir),
            Times.Once
        );
        MockTableOfContentsService.Verify(
            s => s.UpdateTableOfContents(JournalDir, It.IsAny<DateTime?>(), It.IsAny<DateTime?>()),
            Times.Once
        );
    }

    [Fact]
    public void Execute_ReturnsAlreadyExists_WhenBothArtifactsAlreadyExist()
    {
        // Arrange — both exist
        MockFileSystem.Setup(fs => fs.FileExists(TocStructurePath)).Returns(true);
        MockFileSystem.Setup(fs => fs.FileExists(TocMdPath)).Returns(true);

        // Act
        var result = _service.Execute(JournalDir);

        // Assert
        result.ShouldBe(AddTocResult.AlreadyExists);
        MockTocStructureRepository.Verify(
            r => r.Save(It.IsAny<JournalTocStructure>(), It.IsAny<string>()),
            Times.Never
        );
        MockTableOfContentsService.Verify(
            s =>
                s.UpdateTableOfContents(
                    It.IsAny<string>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<DateTime?>()
                ),
            Times.Never
        );
    }

    [Fact]
    public void Execute_ReturnsPartiallyCreated_WhenStructureExistsButMdDoesNot()
    {
        // Arrange — structure exists, markdown does not
        MockFileSystem.Setup(fs => fs.FileExists(TocStructurePath)).Returns(true);
        MockFileSystem.Setup(fs => fs.FileExists(TocMdPath)).Returns(false);

        // Act
        var result = _service.Execute(JournalDir);

        // Assert
        result.ShouldBe(AddTocResult.PartiallyCreated);
        MockTocStructureRepository.Verify(
            r => r.Save(It.IsAny<JournalTocStructure>(), It.IsAny<string>()),
            Times.Never
        );
        MockTableOfContentsService.Verify(
            s => s.UpdateTableOfContents(JournalDir, It.IsAny<DateTime?>(), It.IsAny<DateTime?>()),
            Times.Once
        );
    }

    [Fact]
    public void Execute_ReturnsPartiallyCreated_WhenMdExistsButStructureDoesNot()
    {
        // Arrange — markdown exists, structure does not
        MockFileSystem.Setup(fs => fs.FileExists(TocStructurePath)).Returns(false);
        MockFileSystem.Setup(fs => fs.FileExists(TocMdPath)).Returns(true);

        // Act
        var result = _service.Execute(JournalDir);

        // Assert
        result.ShouldBe(AddTocResult.PartiallyCreated);
        MockTocStructureRepository.Verify(
            r => r.Save(It.IsAny<JournalTocStructure>(), MetadataDir),
            Times.Once
        );
        MockTableOfContentsService.Verify(
            s =>
                s.UpdateTableOfContents(
                    It.IsAny<string>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<DateTime?>()
                ),
            Times.Never
        );
    }

    #endregion

    #region --structure-only

    [Fact]
    public void Execute_StructureOnly_ReturnsCreated_WhenStructureDoesNotExist()
    {
        // Arrange — structure doesn't exist
        MockFileSystem.Setup(fs => fs.FileExists(TocStructurePath)).Returns(false);
        MockFileSystem.Setup(fs => fs.FileExists(TocMdPath)).Returns(false);

        // Act
        var result = _service.Execute(JournalDir, structureOnly: true);

        // Assert
        result.ShouldBe(AddTocResult.Created);
        MockTocStructureRepository.Verify(
            r => r.Save(It.IsAny<JournalTocStructure>(), MetadataDir),
            Times.Once
        );
        // Markdown TOC should NOT be created
        MockTableOfContentsService.Verify(
            s =>
                s.UpdateTableOfContents(
                    It.IsAny<string>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<DateTime?>()
                ),
            Times.Never
        );
    }

    [Fact]
    public void Execute_StructureOnly_ReturnsAlreadyExists_WhenStructureAlreadyExists()
    {
        // Arrange — structure already exists
        MockFileSystem.Setup(fs => fs.FileExists(TocStructurePath)).Returns(true);

        // Act
        var result = _service.Execute(JournalDir, structureOnly: true);

        // Assert
        result.ShouldBe(AddTocResult.AlreadyExists);
        MockTocStructureRepository.Verify(
            r => r.Save(It.IsAny<JournalTocStructure>(), It.IsAny<string>()),
            Times.Never
        );
    }

    #endregion

    #region --md-only

    [Fact]
    public void Execute_MdOnly_ReturnsCreated_WhenMarkdownDoesNotExist()
    {
        // Arrange — markdown doesn't exist
        MockFileSystem.Setup(fs => fs.FileExists(TocStructurePath)).Returns(false);
        MockFileSystem.Setup(fs => fs.FileExists(TocMdPath)).Returns(false);

        // Act
        var result = _service.Execute(JournalDir, mdOnly: true);

        // Assert
        result.ShouldBe(AddTocResult.Created);
        MockTableOfContentsService.Verify(
            s => s.UpdateTableOfContents(JournalDir, It.IsAny<DateTime?>(), It.IsAny<DateTime?>()),
            Times.Once
        );
        // Structure file should NOT be created
        MockTocStructureRepository.Verify(
            r => r.Save(It.IsAny<JournalTocStructure>(), It.IsAny<string>()),
            Times.Never
        );
    }

    [Fact]
    public void Execute_MdOnly_ReturnsAlreadyExists_WhenMarkdownAlreadyExists()
    {
        // Arrange — markdown already exists
        MockFileSystem.Setup(fs => fs.FileExists(TocMdPath)).Returns(true);

        // Act
        var result = _service.Execute(JournalDir, mdOnly: true);

        // Assert
        result.ShouldBe(AddTocResult.AlreadyExists);
        MockTableOfContentsService.Verify(
            s =>
                s.UpdateTableOfContents(
                    It.IsAny<string>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<DateTime?>()
                ),
            Times.Never
        );
    }

    #endregion

    #region Guard Clauses

    [Fact]
    public void Execute_ThrowsArgumentException_WhenJournalDirIsNull()
    {
        Should.Throw<ArgumentException>(() => _service.Execute(null!));
    }

    [Fact]
    public void Execute_ThrowsArgumentException_WhenJournalDirIsWhitespace()
    {
        Should.Throw<ArgumentException>(() => _service.Execute("   "));
    }

    [Fact]
    public void Execute_ThrowsInvalidOperationException_WhenConfigCannotBeRead()
    {
        MockJournalConfiguration.Setup(c => c.Read(JournalDir)).Returns((JournalConfig?)null);
        MockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(false);

        Should.Throw<InvalidOperationException>(() => _service.Execute(JournalDir));
    }

    #endregion
}
