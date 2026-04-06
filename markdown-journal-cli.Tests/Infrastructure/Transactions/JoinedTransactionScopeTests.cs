using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.Transactions;
using markdown_journal_cli.Infrastructure.Transactions.Models;
using markdown_journal_cli.Tests.Infrastructure.FileSystem;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace markdown_journal_cli.Tests.Infrastructure.Transactions;

/// <summary>
/// Tests for <see cref="JoinedTransactionScope"/> — the scope returned by
/// <see cref="FileTransactionCoordinator.BeginOrJoin()"/> when a root scope is active.
/// </summary>
public class JoinedTransactionScopeTests : IDisposable
{
    private readonly TestFileSystem _fileSystem;
    private readonly FileTransactionCoordinator _coordinator;

    private const string JournalRoot = "/journal";
    private const string FileA = "/journal/a.md";
    private const string FileB = "/journal/b.md";

    public JoinedTransactionScopeTests()
    {
        _fileSystem = new TestFileSystem();
        var buffer = new InMemoryFileBuffer(_fileSystem);
        var deletionStrategy = new InMemoryDeletionRollbackStrategy();
        _coordinator = new FileTransactionCoordinator(
            _fileSystem,
            buffer,
            deletionStrategy,
            NullLoggerFactory.Instance
        );

        _fileSystem.CreateDirectory(JournalRoot);
        _fileSystem.CreateFile(JournalRoot, "a.md", "original-a");
        _fileSystem.CreateFile(JournalRoot, "b.md", "original-b");
    }

    public void Dispose() => _coordinator.Current?.Rollback();

    [Fact]
    public void Should_Delegate_Track_Calls_To_Root_Scope()
    {
        using var root = _coordinator.Begin();
        using var joined = _coordinator.BeginOrJoin();

        joined.Track(FileA);
        _fileSystem.UpdateFile(JournalRoot, "a.md", "changed");

        // Rollback the ROOT — it should restore FileA even though joined tracked it
        root.Rollback();

        _fileSystem.GetFileContent(FileA).ShouldBe("original-a");
    }

    [Fact]
    public void Should_Not_Commit_Root_On_Joined_Commit()
    {
        using var root = _coordinator.Begin();
        root.Track(FileA);

        var joined = _coordinator.BeginOrJoin();
        joined.Track(FileB);
        _fileSystem.UpdateFile(JournalRoot, "a.md", "a-changed");
        _fileSystem.UpdateFile(JournalRoot, "b.md", "b-changed");

        joined.Commit(); // should NOT commit root

        // Root is still active; rollback it
        root.Rollback();

        _fileSystem.GetFileContent(FileA).ShouldBe("original-a");
        _fileSystem.GetFileContent(FileB).ShouldBe("original-b");
    }

    [Fact]
    public void Should_Delegate_Rollback_To_Root_When_Rollback_Called()
    {
        using var root = _coordinator.Begin();
        root.Track(FileA);
        _fileSystem.UpdateFile(JournalRoot, "a.md", "a-changed");

        var joined = _coordinator.BeginOrJoin();
        joined.Track(FileB);
        _fileSystem.UpdateFile(JournalRoot, "b.md", "b-changed");

        var result = joined.Rollback();

        result.Restored.Count.ShouldBe(2);
        _fileSystem.GetFileContent(FileA).ShouldBe("original-a");
        _fileSystem.GetFileContent(FileB).ShouldBe("original-b");
    }

    [Fact]
    public void Should_Auto_Rollback_Root_Via_Dispose_When_Not_Committed()
    {
        var root = _coordinator.Begin();
        root.Track(FileA);
        _fileSystem.UpdateFile(JournalRoot, "a.md", "a-changed");

        {
            using var joined = _coordinator.BeginOrJoin();
            joined.Track(FileB);
            _fileSystem.UpdateFile(JournalRoot, "b.md", "b-changed");
            // joined.Dispose() called — should rollback root
        }

        _fileSystem.GetFileContent(FileA).ShouldBe("original-a");
        _fileSystem.GetFileContent(FileB).ShouldBe("original-b");
    }

    [Fact]
    public void Should_Not_Auto_Rollback_Root_Via_Dispose_When_Committed()
    {
        using var root = _coordinator.Begin();
        root.Track(FileA);

        var joined = _coordinator.BeginOrJoin();
        joined.Track(FileB);
        _fileSystem.UpdateFile(JournalRoot, "a.md", "a-changed");
        _fileSystem.UpdateFile(JournalRoot, "b.md", "b-changed");

        joined.Commit();
        joined.Dispose(); // Should NOT rollback root

        // Root scope still valid; commit it
        root.Commit();

        _fileSystem.GetFileContent(FileA).ShouldBe("a-changed");
        _fileSystem.GetFileContent(FileB).ShouldBe("b-changed");
    }

    [Fact]
    public void Should_Return_Root_IsRolledBack_State()
    {
        using var root = _coordinator.Begin();
        var joined = _coordinator.BeginOrJoin();

        joined.IsRolledBack.ShouldBeFalse();
        root.Rollback();

        joined.IsRolledBack.ShouldBeTrue();
    }
}
