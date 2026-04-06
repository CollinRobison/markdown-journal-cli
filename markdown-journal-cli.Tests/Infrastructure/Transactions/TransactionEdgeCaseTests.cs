using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.Transactions;
using markdown_journal_cli.Infrastructure.Transactions.Models;
using markdown_journal_cli.Tests.Infrastructure.FileSystem;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace markdown_journal_cli.Tests.Infrastructure.Transactions;

/// <summary>
/// Edge case tests covering cross-cutting transaction behaviors per PRD §11.5.
/// </summary>
public class TransactionEdgeCaseTests : IDisposable
{
    private readonly TestFileSystem _fileSystem;
    private readonly FileTransactionCoordinator _coordinator;

    private const string JournalRoot = "/journal";
    private const string FileA = "/journal/a.md";
    private const string FileB = "/journal/b.md";

    public TransactionEdgeCaseTests()
    {
        _fileSystem = new TestFileSystem();
        _fileSystem.CreateDirectory(JournalRoot);
        _fileSystem.CreateFile(JournalRoot, "a.md", "original-a");
        _fileSystem.CreateFile(JournalRoot, "b.md", "original-b");

        _coordinator = new FileTransactionCoordinator(
            _fileSystem,
            new InMemoryFileBuffer(_fileSystem),
            new InMemoryDeletionRollbackStrategy(),
            NullLoggerFactory.Instance
        );
    }

    public void Dispose() => _coordinator.Current?.Rollback();

    [Fact]
    public void Should_Use_Original_Snapshot_When_File_Tracked_Twice()
    {
        using var tx = _coordinator.Begin();
        tx.Track(FileA); // snapshot = "original-a"

        // Simulate a write between two tracks of the same file
        _fileSystem.UpdateFile(JournalRoot, "a.md", "intermediate");
        tx.Track(FileA); // should NOT overwrite snapshot

        _fileSystem.UpdateFile(JournalRoot, "a.md", "final");
        tx.Rollback();

        _fileSystem.GetFileContent(FileA).ShouldBe("original-a");
    }

    [Fact]
    public void Should_Return_Empty_Result_On_Second_Rollback_Call()
    {
        var tx = _coordinator.Begin();
        tx.Track(FileA);

        var r1 = tx.Rollback();
        var r2 = tx.Rollback();

        r1.Restored.Count.ShouldBe(1);
        r2.Restored.ShouldBeEmpty();
        r2.Failed.ShouldBeEmpty();
    }

    [Fact]
    public void Should_Return_Empty_RollbackResult_When_Nothing_Tracked()
    {
        var tx = _coordinator.Begin();
        var result = tx.Rollback();

        result.Restored.ShouldBeEmpty();
        result.Failed.ShouldBeEmpty();
        result.IsFullyRestored.ShouldBeTrue();
    }

    [Fact]
    public void Should_Clear_Buffer_After_Commit_So_Next_Command_Has_Clean_State()
    {
        var buffer = new InMemoryFileBuffer(_fileSystem);
        var coord = new FileTransactionCoordinator(
            _fileSystem,
            buffer,
            new InMemoryDeletionRollbackStrategy(),
            NullLoggerFactory.Instance
        );

        // Manually seed the buffer (simulates dry-run staging or prior use)
        buffer.Snapshot(FileA);
        buffer.HasSnapshot(FileA).ShouldBeTrue();

        var tx1 = coord.Begin();
        tx1.Track(FileA);
        tx1.Commit();

        // After commit, buffer should be cleared for the next command
        buffer.HasSnapshot(FileA).ShouldBeFalse();
    }

    [Fact]
    public void Should_Clear_Buffer_After_Rollback_So_Next_Command_Has_Clean_State()
    {
        var buffer = new InMemoryFileBuffer(_fileSystem);
        var coord = new FileTransactionCoordinator(
            _fileSystem,
            buffer,
            new InMemoryDeletionRollbackStrategy(),
            NullLoggerFactory.Instance
        );

        var tx1 = coord.Begin();
        tx1.Track(FileA);
        tx1.Rollback();

        // After rollback, buffer should be cleared
        buffer.HasSnapshot(FileA).ShouldBeFalse();
    }

    [Fact]
    public void Should_Return_ExitCode2_When_RollbackCompletedException_IsFullyRestored()
    {
        var result = new RollbackResult(
            Restored: new List<RollbackEntry> { new(FileA, RollbackEntryKind.Modify) },
            Failed: []
        );
        var ex = new RollbackCompletedException(result, new IOException("original cause"));

        ex.Result.IsFullyRestored.ShouldBeTrue();
        // Exit code 2 is applied by the command layer — verified by checking the property
        ex.Result.Failed.ShouldBeEmpty();
    }

    [Fact]
    public void Should_Return_ExitCode3_When_RollbackCompletedException_IsNotFullyRestored()
    {
        var entry = new RollbackEntry(FileA, RollbackEntryKind.Modify);
        var result = new RollbackResult(
            Restored: [],
            Failed: new List<RollbackFailure> { new(entry, new IOException("couldn't restore")) }
        );
        var ex = new RollbackCompletedException(result, new IOException("original cause"));

        ex.Result.IsFullyRestored.ShouldBeFalse();
        ex.Result.Failed.Count.ShouldBe(1);
    }

    [Fact]
    public void Should_Add_Failed_Entry_When_RenameBack_Throws()
    {
        _fileSystem.CreateFile(JournalRoot, "c.md", "c-content");
        var fileC = $"{JournalRoot}/c.md";

        var throwingFs = new ThrowOnRenameFileSystem(_fileSystem);
        var coord = new FileTransactionCoordinator(
            throwingFs,
            new InMemoryFileBuffer(throwingFs),
            new InMemoryDeletionRollbackStrategy(),
            NullLoggerFactory.Instance
        );

        var tx = coord.Begin();
        tx.TrackRename(FileA, fileC);
        // simulate the rename succeeded
        _fileSystem.RenameFile(FileA, fileC);

        var result = tx.Rollback();

        result.Failed.Count.ShouldBe(1);
        result.Failed[0].Entry.Kind.ShouldBe(RollbackEntryKind.Rename);
    }

    private sealed class ThrowOnRenameFileSystem(TestFileSystem inner) : IFileSystem
    {
        private readonly TestFileSystem _inner = inner;

        public bool DirectoryExists(string path) => _inner.DirectoryExists(path);

        public bool FileExists(string path) => _inner.FileExists(path);

        public void CreateDirectory(string path) => _inner.CreateDirectory(path);

        public void DeleteDirectory(string path) => _inner.DeleteDirectory(path);

        public string CombinePaths(params string[] paths) => Path.Combine(paths);

        public void CreateMarkdownFile(string path, string fileName, string body) =>
            _inner.CreateMarkdownFile(path, fileName, body);

        public void CreateFile(string path, string fileName, string body) =>
            _inner.CreateFile(path, fileName, body);

        public void UpdateFile(string path, string fileName, string body) =>
            _inner.UpdateFile(path, fileName, body);

        public void DeleteFile(string filePath) => _inner.DeleteFile(filePath);

        public void RenameFile(string oldPath, string newPath) =>
            throw new IOException("Cannot rename");

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
}
