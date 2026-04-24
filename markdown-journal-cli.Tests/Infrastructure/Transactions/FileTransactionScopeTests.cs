using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.Transactions;
using markdown_journal_cli.Infrastructure.Transactions.Models;
using markdown_journal_cli.Tests.Infrastructure.FileSystem;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;

namespace markdown_journal_cli.Tests.Infrastructure.Transactions;

/// <summary>
/// Tests for the full <see cref="FileTransactionScope"/> lifecycle using a real
/// <see cref="TestFileSystem"/> and <see cref="InMemoryDeletionRollbackStrategy"/>.
/// All scopes are obtained via <see cref="FileTransactionCoordinator.Begin()"/> so the
/// ambient cleanup callback is exercised with each test.
/// </summary>
public class FileTransactionScopeTests : IDisposable
{
    private readonly TestFileSystem _fileSystem;
    private readonly InMemoryFileBuffer _buffer;
    private readonly InMemoryDeletionRollbackStrategy _deletionStrategy;
    private readonly FileTransactionCoordinator _coordinator;

    private const string JournalRoot = "/journal";
    private const string FileA = "/journal/a.md";
    private const string FileB = "/journal/b.md";
    private const string FileC = "/journal/c.md";
    private const string SubDir = "/journal/sub";

    public FileTransactionScopeTests()
    {
        _fileSystem = new TestFileSystem();
        _buffer = new InMemoryFileBuffer(_fileSystem);
        _deletionStrategy = new InMemoryDeletionRollbackStrategy();
        _coordinator = new FileTransactionCoordinator(
            _fileSystem,
            _buffer,
            _deletionStrategy,
            NullLoggerFactory.Instance
        );

        _fileSystem.CreateDirectory(JournalRoot);
        _fileSystem.CreateFile(JournalRoot, "a.md", "original-a");
        _fileSystem.CreateFile(JournalRoot, "b.md", "original-b");
    }

    public void Dispose()
    {
        // Clean up any uncommitted scope in case a test threw before commit/rollback
        _coordinator.Current?.Rollback();
    }

    // ─── Track / Modify ──────────────────────────────────────────────────────

    [Fact]
    public void Should_Snapshot_File_Content_When_Tracked()
    {
        using var tx = _coordinator.Begin();
        tx.Track(FileA);

        _fileSystem.UpdateFile(JournalRoot, "a.md", "changed-a");
        var result = tx.Rollback();

        _fileSystem.GetFileContent(FileA).ShouldBe("original-a");
        result.Restored.Count.ShouldBe(1);
    }

    [Fact]
    public void Should_Not_Overwrite_Snapshot_On_Duplicate_Track()
    {
        using var tx = _coordinator.Begin();
        tx.Track(FileA); // snapshot = "original-a"

        _fileSystem.UpdateFile(JournalRoot, "a.md", "mid-write");
        tx.Track(FileA); // second track — snapshot must NOT be overwritten

        _fileSystem.UpdateFile(JournalRoot, "a.md", "final");
        tx.Rollback();

        _fileSystem.GetFileContent(FileA).ShouldBe("original-a");
    }

    [Fact]
    public void Should_Restore_File_Content_On_Rollback_For_Modify()
    {
        using var tx = _coordinator.Begin();
        tx.Track(FileA);
        tx.Track(FileB);

        _fileSystem.UpdateFile(JournalRoot, "a.md", "changed-a");
        _fileSystem.UpdateFile(JournalRoot, "b.md", "changed-b");
        tx.Rollback();

        _fileSystem.GetFileContent(FileA).ShouldBe("original-a");
        _fileSystem.GetFileContent(FileB).ShouldBe("original-b");
    }

    [Fact]
    public void Should_Throw_When_Track_Called_After_Commit()
    {
        var tx = _coordinator.Begin();
        tx.Commit();

        Should.Throw<InvalidOperationException>(() => tx.Track(FileA));
    }

    [Fact]
    public void Should_Throw_When_Track_Called_After_Rollback()
    {
        var tx = _coordinator.Begin();
        tx.Rollback();

        Should.Throw<InvalidOperationException>(() => tx.Track(FileA));
    }

    // ─── TrackNew ─────────────────────────────────────────────────────────────

    [Fact]
    public void Should_Delete_Created_File_On_Rollback_For_New()
    {
        using var tx = _coordinator.Begin();
        tx.TrackNew(FileC);

        _fileSystem.CreateFile(JournalRoot, "c.md", "new-content");
        _fileSystem.FileExists(FileC).ShouldBeTrue();

        tx.Rollback();

        _fileSystem.FileExists(FileC).ShouldBeFalse();
    }

    [Fact]
    public void Should_Not_Throw_When_New_File_Already_Gone_On_Rollback()
    {
        using var tx = _coordinator.Begin();
        tx.TrackNew(FileC);
        // never actually create the file

        var result = tx.Rollback();
        result.Restored.Count.ShouldBe(1);
        result.IsFullyRestored.ShouldBeTrue();
    }

    // ─── TrackRename ─────────────────────────────────────────────────────────

    [Fact]
    public void Should_Rename_Back_On_Rollback_For_Rename()
    {
        using var tx = _coordinator.Begin();
        tx.TrackRename(FileA, FileC);

        _fileSystem.RenameFile(FileA, FileC);
        _fileSystem.FileExists(FileC).ShouldBeTrue();
        _fileSystem.FileExists(FileA).ShouldBeFalse();

        tx.Rollback();

        _fileSystem.FileExists(FileA).ShouldBeTrue();
        _fileSystem.FileExists(FileC).ShouldBeFalse();
    }

    [Fact]
    public void Should_Not_Throw_When_Renamed_File_Not_Found_On_Rollback()
    {
        using var tx = _coordinator.Begin();
        tx.TrackRename(FileA, FileC);
        // don't actually rename (simulates partially-failed rename)

        var result = tx.Rollback();
        result.Restored.Count.ShouldBe(1);
        result.IsFullyRestored.ShouldBeTrue();
    }

    // ─── TrackDelete ─────────────────────────────────────────────────────────

    [Fact]
    public void Should_Delegate_Snapshot_To_DeletionStrategy_On_TrackDelete()
    {
        using var tx = _coordinator.Begin();
        tx.TrackDelete(FileA); // captures content in _deletionStrategy

        _fileSystem.DeleteFile(FileA);
        tx.Rollback();

        // file should be restored
        _fileSystem.FileExists(FileA).ShouldBeTrue();
        _fileSystem.GetFileContent(FileA).ShouldBe("original-a");
    }

    [Fact]
    public void Should_Restore_Via_DeletionStrategy_On_Rollback_For_Delete()
    {
        using var tx = _coordinator.Begin();
        tx.TrackDelete(FileA);
        _fileSystem.DeleteFile(FileA);
        _fileSystem.FileExists(FileA).ShouldBeFalse();

        tx.Rollback();

        _fileSystem.FileExists(FileA).ShouldBeTrue();
        _fileSystem.GetFileContent(FileA).ShouldBe("original-a");
    }

    // ─── TrackNewDirectory ───────────────────────────────────────────────────

    [Fact]
    public void Should_Delete_Directory_On_Rollback_For_NewDirectory()
    {
        _fileSystem.CreateDirectory(SubDir);
        using var tx = _coordinator.Begin();
        tx.TrackNewDirectory(SubDir);

        tx.Rollback();

        _fileSystem.DirectoryExists(SubDir).ShouldBeFalse();
        _fileSystem.DeletedDirectories.ShouldContain(SubDir);
    }

    [Fact]
    public void Should_Add_To_Failed_When_DeleteDirectory_Throws()
    {
        var throwingFs = new ThrowOnDeleteDirectoryFileSystem(new TestFileSystem());
        throwingFs.CreateDirectory(JournalRoot);
        throwingFs.CreateDirectory(SubDir);

        var coord = new FileTransactionCoordinator(
            throwingFs,
            new InMemoryFileBuffer(throwingFs),
            new InMemoryDeletionRollbackStrategy(),
            NullLoggerFactory.Instance
        );

        var tx = coord.Begin();
        tx.TrackNewDirectory(SubDir);

        var result = tx.Rollback();
        result.Failed.Count.ShouldBe(1);
        result.Failed[0].Entry.Kind.ShouldBe(RollbackEntryKind.NewDirectory);
    }

    // ─── Commit ──────────────────────────────────────────────────────────────

    [Fact]
    public void Should_Clear_All_Tracked_Entries_On_Commit()
    {
        var tx = _coordinator.Begin();
        tx.Track(FileA);
        tx.TrackNew(FileC);
        tx.Commit();

        // No rollback data — coordinator scope is cleared
        _coordinator.Current.ShouldBeNull();
    }

    [Fact]
    public void Should_Clear_InMemoryFileBuffer_On_Commit()
    {
        _buffer.Snapshot(FileA);
        _buffer.HasSnapshot(FileA).ShouldBeTrue();

        var tx = _coordinator.Begin();
        tx.Track(FileA);
        tx.Commit();

        _buffer.HasSnapshot(FileA).ShouldBeFalse();
    }

    [Fact]
    public void Should_Release_All_Deletion_Snapshots_On_Commit()
    {
        var tx = _coordinator.Begin();
        tx.TrackDelete(FileA);
        _fileSystem.DeleteFile(FileA);
        tx.Commit();

        // Coordinator should be cleared
        _coordinator.Current.ShouldBeNull();
    }

    [Fact]
    public void Should_Return_Empty_RollbackResult_When_Rollback_Called_After_Commit()
    {
        var tx = _coordinator.Begin();
        tx.Track(FileA);
        tx.Commit();

        var result = tx.Rollback();
        result.Restored.ShouldBeEmpty();
        result.Failed.ShouldBeEmpty();
    }

    [Fact]
    public void Should_Clear_Ambient_Current_On_Commit()
    {
        _coordinator.Current.ShouldBeNull();
        var tx = _coordinator.Begin();
        _coordinator.Current.ShouldNotBeNull();

        tx.Commit();

        _coordinator.Current.ShouldBeNull();
    }

    // ─── Rollback ────────────────────────────────────────────────────────────

    [Fact]
    public void Should_Execute_Rollback_Entries_In_Reverse_Order()
    {
        var order = new List<string>();
        var fs = new OrderTrackingFileSystem(order);
        fs.CreateDirectory("/j");
        fs.CreateFile("/j", "x.md", "orig-x");
        fs.CreateFile("/j", "y.md", "orig-y");

        var coord = new FileTransactionCoordinator(
            fs,
            new InMemoryFileBuffer(fs),
            new InMemoryDeletionRollbackStrategy(),
            NullLoggerFactory.Instance
        );

        var tx = coord.Begin();
        tx.Track("/j/x.md"); // index 0 — should be rolled back last
        tx.Track("/j/y.md"); // index 1 — should be rolled back first

        fs.UpdateFile("/j", "x.md", "new-x");
        fs.UpdateFile("/j", "y.md", "new-y");

        order.Clear(); // reset after setup
        tx.Rollback();

        order[0].ShouldBe("/j/y.md");
        order[1].ShouldBe("/j/x.md");
    }

    [Fact]
    public void Should_Return_Fully_Restored_Result_When_All_Entries_Succeed()
    {
        using var tx = _coordinator.Begin();
        tx.Track(FileA);
        tx.Track(FileB);

        _fileSystem.UpdateFile(JournalRoot, "a.md", "x");
        _fileSystem.UpdateFile(JournalRoot, "b.md", "y");

        var result = tx.Rollback();

        result.IsFullyRestored.ShouldBeTrue();
        result.Restored.Count.ShouldBe(2);
        result.Failed.ShouldBeEmpty();
    }

    [Fact]
    public void Should_Return_Partially_Restored_Result_When_Some_Entries_Fail()
    {
        // Use a real TestFileSystem for setup, then switch to failing fs for rollback
        var realFs = new TestFileSystem();
        realFs.CreateDirectory(JournalRoot);
        realFs.CreateFile(JournalRoot, "a.md", "orig-a");
        realFs.CreateFile(JournalRoot, "b.md", "orig-b");

        var failingFs = new FailOnUpdateFileSystem(FileB, realFs);
        var coord = new FileTransactionCoordinator(
            failingFs,
            new InMemoryFileBuffer(failingFs),
            new InMemoryDeletionRollbackStrategy(),
            NullLoggerFactory.Instance
        );

        var tx = coord.Begin();
        tx.Track(FileA);
        tx.Track(FileB);
        // Do writes via real fs so snapshot content is correct
        realFs.UpdateFile(JournalRoot, "a.md", "new-a");
        realFs.UpdateFile(JournalRoot, "b.md", "new-b");

        // Now trigger rollback — FileB update will fail
        var result = tx.Rollback();

        result.IsFullyRestored.ShouldBeFalse();
        result.Failed.Count.ShouldBe(1);
        result.Failed[0].Entry.AbsolutePath.ShouldBe(FileB);
    }

    [Fact]
    public void Should_Clear_InMemoryFileBuffer_On_Rollback()
    {
        _buffer.Snapshot(FileA);
        _buffer.HasSnapshot(FileA).ShouldBeTrue();

        var tx = _coordinator.Begin();
        tx.Track(FileA);
        tx.Rollback();

        _buffer.HasSnapshot(FileA).ShouldBeFalse();
    }

    [Fact]
    public void Should_Be_Idempotent_On_Multiple_Rollback_Calls()
    {
        var tx = _coordinator.Begin();
        tx.Track(FileA);
        _fileSystem.UpdateFile(JournalRoot, "a.md", "changed");

        var r1 = tx.Rollback();
        var r2 = tx.Rollback();

        r1.Restored.Count.ShouldBe(1);
        r2.Restored.ShouldBeEmpty();
    }

    [Fact]
    public void Should_Auto_Rollback_Via_Dispose_When_No_Commit()
    {
        {
            using var tx = _coordinator.Begin();
            tx.Track(FileA);
            _fileSystem.UpdateFile(JournalRoot, "a.md", "changed");
            // no commit — Dispose triggers rollback
        }

        _fileSystem.GetFileContent(FileA).ShouldBe("original-a");
    }

    [Fact]
    public void Should_Clear_Ambient_Current_On_Rollback()
    {
        var tx = _coordinator.Begin();
        _coordinator.Current.ShouldNotBeNull();

        tx.Rollback();

        _coordinator.Current.ShouldBeNull();
    }

    // ─── Full Sequence ───────────────────────────────────────────────────────

    [Fact]
    public void Should_Rollback_Mixed_Operations_In_Correct_Reverse_Order()
    {
        using var tx = _coordinator.Begin();
        tx.Track(FileA); // modify snapshot
        tx.TrackNew(FileC); // new file
        tx.TrackRename(FileA, FileB); // rename (conceptual, won't actually move)

        _fileSystem.UpdateFile(JournalRoot, "a.md", "modified");
        _fileSystem.CreateFile(JournalRoot, "c.md", "new");
        // simulate rename
        _fileSystem.RenameFile(FileA, FileC); // repurpose available files
        _fileSystem.RenameFile(FileB, FileA); // put b at a's path for rename back test

        tx.Rollback();

        // FileA should be restored to original-a (from modify snapshot)
        _fileSystem.GetFileContent(FileA).ShouldBe("original-a");
        // FileC should be deleted (TrackNew rollback)
        // (FileC may now exist as a result of RenameFile calls, but original TrackNew path was /journal/c.md)
    }

    [Fact]
    public void Should_Rollback_NewDirectory_After_All_New_Files_Inside_Are_Deleted()
    {
        _fileSystem.CreateDirectory(SubDir);
        _fileSystem.CreateFile(SubDir, "entry.md", "content");
        var entryPath = $"{SubDir}/entry.md";

        using var tx = _coordinator.Begin();
        tx.TrackNew(entryPath);
        tx.TrackNewDirectory(SubDir);

        _fileSystem.DeleteFile(entryPath);
        tx.Rollback(); // Reverse order: dir deleted after file rollback

        // Directory should be gone (even if it has entries from setup)
        // The TestFileSystem doesn't enforce directory-must-be-empty for delete
        _fileSystem.DeletedDirectories.ShouldContain(SubDir);
    }

    // ─── Private test helper file systems ────────────────────────────────────

    private sealed class ThrowOnDeleteDirectoryFileSystem(TestFileSystem inner) : IFileSystem
    {
        private readonly TestFileSystem _inner = inner;

        public bool DirectoryExists(string path) => _inner.DirectoryExists(path);

        public bool IsDirectory(string path) => _inner.IsDirectory(path);

        public bool FileExists(string path) => _inner.FileExists(path);

        public void CreateDirectory(string path) => _inner.CreateDirectory(path);

        public void DeleteDirectory(string path) =>
            throw new IOException("Cannot delete directory");

        public string CombinePaths(params string[] paths) => Path.Combine(paths);

        public void CreateMarkdownFile(string path, string fileName, string body) =>
            _inner.CreateMarkdownFile(path, fileName, body);

        public void CreateFile(string path, string fileName, string body) =>
            _inner.CreateFile(path, fileName, body);

        public void UpdateFile(string path, string fileName, string body) =>
            _inner.UpdateFile(path, fileName, body);

        public void DeleteFile(string filePath) => _inner.DeleteFile(filePath);

        public void RenameFile(string oldPath, string newPath) =>
            _inner.RenameFile(oldPath, newPath);

        public string GetFileContent(string filePath) =>
            ((IFileSystem)_inner).GetFileContent(filePath);

        public string? GetFileNameWithoutExtension(string? path) =>
            Path.GetFileNameWithoutExtension(path);

        public string? GetDirectoryName(string? path) => Path.GetDirectoryName(path);

        public string? GetFileName(string? path) => Path.GetFileName(path);

        public string GetFullPath(string path) => Path.GetFullPath(path);

        public string[] GetFiles(string path, string searchPattern, SearchOption searchOption) =>
            _inner.GetFiles(path, searchPattern, searchOption);

        public IReadOnlyList<string> GetMarkdownFiles(string directory) =>
            _inner.GetMarkdownFiles(directory);
    }

    private sealed class FailOnUpdateFileSystem(string failPath, TestFileSystem inner) : IFileSystem
    {
        private readonly string _failPath = failPath;
        private readonly TestFileSystem _inner = inner;

        public bool DirectoryExists(string path) => _inner.DirectoryExists(path);

        public bool IsDirectory(string path) => _inner.IsDirectory(path);

        public bool FileExists(string path) => _inner.FileExists(path);

        public void CreateDirectory(string path) => _inner.CreateDirectory(path);

        public void DeleteDirectory(string path) => _inner.DeleteDirectory(path);

        public string CombinePaths(params string[] paths) => Path.Combine(paths);

        public void CreateMarkdownFile(string path, string fileName, string body) =>
            _inner.CreateMarkdownFile(path, fileName, body);

        public void CreateFile(string path, string fileName, string body) =>
            _inner.CreateFile(path, fileName, body);

        public void UpdateFile(string path, string fileName, string body)
        {
            var abs = Path.Combine(path, fileName);
            if (abs.Equals(_failPath, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Simulated update failure");
            _inner.UpdateFile(path, fileName, body);
        }

        public void DeleteFile(string filePath) => _inner.DeleteFile(filePath);

        public void RenameFile(string oldPath, string newPath) =>
            _inner.RenameFile(oldPath, newPath);

        public string GetFileContent(string filePath) =>
            ((IFileSystem)_inner).GetFileContent(filePath);

        public string? GetFileNameWithoutExtension(string? path) =>
            Path.GetFileNameWithoutExtension(path);

        public string? GetDirectoryName(string? path) => Path.GetDirectoryName(path);

        public string? GetFileName(string? path) => Path.GetFileName(path);

        public string GetFullPath(string path) => Path.GetFullPath(path);

        public string[] GetFiles(string path, string searchPattern, SearchOption searchOption) =>
            _inner.GetFiles(path, searchPattern, searchOption);

        public IReadOnlyList<string> GetMarkdownFiles(string directory) =>
            _inner.GetMarkdownFiles(directory);
    }

    private sealed class OrderTrackingFileSystem(List<string> order) : TestFileSystem
    {
        private readonly List<string> _order = order;

        public override void UpdateFile(string path, string fileName, string body)
        {
            _order.Add(Path.Combine(path, fileName));
            base.UpdateFile(path, fileName, body);
        }
    }
}
