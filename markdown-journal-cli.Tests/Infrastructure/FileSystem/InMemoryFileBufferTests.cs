using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Tests.Infrastructure.FileSystem;
using Shouldly;

namespace markdown_journal_cli.Tests.Infrastructure.FileSystem;

public class InMemoryFileBufferTests
{
    private readonly TestFileSystem _fileSystem;
    private readonly InMemoryFileBuffer _buffer;
    private const string TestPath = "/journal/toc.md";

    public InMemoryFileBufferTests()
    {
        _fileSystem = new TestFileSystem();
        _fileSystem.CreateFile("/journal", "toc.md", "# Table of Contents");
        _buffer = new InMemoryFileBuffer(_fileSystem);
    }

    #region Constructor

    [Fact]
    public void Constructor_NullFileSystem_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new InMemoryFileBuffer(null!));
    }

    #endregion

    #region Snapshot

    [Fact]
    public void Snapshot_StoresCurrentDiskContent()
    {
        _buffer.Snapshot(TestPath);

        _buffer.GetSnapshot(TestPath).ShouldBe("# Table of Contents");
    }

    [Fact]
    public void HasSnapshot_ReturnsFalse_BeforeSnapshot()
    {
        _buffer.HasSnapshot(TestPath).ShouldBeFalse();
    }

    [Fact]
    public void HasSnapshot_ReturnsTrue_AfterSnapshot()
    {
        _buffer.Snapshot(TestPath);

        _buffer.HasSnapshot(TestPath).ShouldBeTrue();
    }

    [Fact]
    public void GetSnapshot_ReturnsNull_WhenNotSnapshotted()
    {
        _buffer.GetSnapshot(TestPath).ShouldBeNull();
    }

    #endregion

    #region Stage

    [Fact]
    public void Stage_StoresContentInMemory_WithoutWritingToDisk()
    {
        _buffer.Stage(TestPath, "# New Content");

        _buffer.GetStaged(TestPath).ShouldBe("# New Content");
        _fileSystem.GetFileContent(TestPath).ShouldBe("# Table of Contents");
    }

    [Fact]
    public void HasStaged_ReturnsFalse_BeforeStage()
    {
        _buffer.HasStaged(TestPath).ShouldBeFalse();
    }

    [Fact]
    public void HasStaged_ReturnsTrue_AfterStage()
    {
        _buffer.Stage(TestPath, "content");

        _buffer.HasStaged(TestPath).ShouldBeTrue();
    }

    [Fact]
    public void GetStaged_ReturnsNull_WhenNotStaged()
    {
        _buffer.GetStaged(TestPath).ShouldBeNull();
    }

    [Fact]
    public void Stage_OverwritesPreviousStagedContent()
    {
        _buffer.Stage(TestPath, "first");
        _buffer.Stage(TestPath, "second");

        _buffer.GetStaged(TestPath).ShouldBe("second");
    }

    #endregion

    #region Commit

    [Fact]
    public void Commit_WritesStagedContentToDisk()
    {
        _buffer.Stage(TestPath, "# Updated TOC");

        _buffer.Commit(TestPath);

        _fileSystem.GetFileContent(TestPath).ShouldBe("# Updated TOC");
    }

    [Fact]
    public void Commit_ThrowsInvalidOperationException_WhenNothingStaged()
    {
        Should.Throw<InvalidOperationException>(() => _buffer.Commit(TestPath))
            .Message.ShouldContain(TestPath);
    }

    #endregion

    #region Restore

    [Fact]
    public void Restore_WritesSnapshotContentBackToDisk()
    {
        _buffer.Snapshot(TestPath);
        _fileSystem.UpdateFile("/journal", "toc.md", "# Modified TOC");

        _buffer.Restore(TestPath);

        _fileSystem.GetFileContent(TestPath).ShouldBe("# Table of Contents");
    }

    [Fact]
    public void Restore_ThrowsInvalidOperationException_WhenNoSnapshot()
    {
        Should.Throw<InvalidOperationException>(() => _buffer.Restore(TestPath))
            .Message.ShouldContain(TestPath);
    }

    #endregion

    #region Clear

    [Fact]
    public void Clear_RemovesAllStagedAndSnapshotState()
    {
        _buffer.Snapshot(TestPath);
        _buffer.Stage(TestPath, "staged content");

        _buffer.Clear();

        _buffer.HasSnapshot(TestPath).ShouldBeFalse();
        _buffer.HasStaged(TestPath).ShouldBeFalse();
        _buffer.GetSnapshot(TestPath).ShouldBeNull();
        _buffer.GetStaged(TestPath).ShouldBeNull();
    }

    [Fact]
    public void Clear_OnEmptyBuffer_DoesNotThrow()
    {
        Should.NotThrow(() => _buffer.Clear());
    }

    #endregion

    #region Snapshot and Stage Independence

    [Fact]
    public void Snapshot_DoesNotAffectStagedContent()
    {
        _buffer.Stage(TestPath, "staged");
        _buffer.Snapshot(TestPath);

        _buffer.GetStaged(TestPath).ShouldBe("staged");
    }

    [Fact]
    public void Stage_DoesNotAffectSnapshot()
    {
        _buffer.Snapshot(TestPath);
        _buffer.Stage(TestPath, "staged");

        _buffer.GetSnapshot(TestPath).ShouldBe("# Table of Contents");
    }

    #endregion
}
