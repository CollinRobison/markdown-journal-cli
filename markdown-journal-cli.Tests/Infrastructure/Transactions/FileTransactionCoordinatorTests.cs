using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.Transactions;
using markdown_journal_cli.Tests.Infrastructure.FileSystem;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace markdown_journal_cli.Tests.Infrastructure.Transactions;

/// <summary>
/// Tests for <see cref="FileTransactionCoordinator"/>.
/// Each test uses a fresh coordinator instance to avoid [ThreadStatic] pollution.
/// The base class ensures leftover scopes are cleaned up in Dispose.
/// </summary>
public class FileTransactionCoordinatorTests : IDisposable
{
    protected readonly FileTransactionCoordinator Coordinator;
    private readonly TestFileSystem _fileSystem;

    private const string JournalRoot = "/journal";
    private const string FileA = "/journal/a.md";

    public FileTransactionCoordinatorTests()
    {
        _fileSystem = new TestFileSystem();
        _fileSystem.CreateDirectory(JournalRoot);
        _fileSystem.CreateFile(JournalRoot, "a.md", "original-a");

        Coordinator = new FileTransactionCoordinator(
            _fileSystem,
            new InMemoryFileBuffer(_fileSystem),
            new InMemoryDeletionRollbackStrategy(),
            NullLoggerFactory.Instance);

        // Fail fast if prior test left ambient state
        Coordinator.Current.ShouldBeNull();
    }

    public void Dispose()
    {
        // Guard against leaking ambient scope
        Coordinator.Current?.Rollback();
        Coordinator.Current.ShouldBeNull();
    }

    [Fact]
    public void Should_Return_New_Root_Scope_On_Begin()
    {
        var scope = Coordinator.Begin();
        scope.ShouldNotBeNull();
        scope.Dispose();
    }

    [Fact]
    public void Should_Set_Current_When_Begin_Called()
    {
        Coordinator.Current.ShouldBeNull();

        var scope = Coordinator.Begin();

        Coordinator.Current.ShouldBe(scope);
        scope.Dispose();
    }

    [Fact]
    public void Should_Clear_Current_When_Scope_Committed()
    {
        var scope = Coordinator.Begin();
        scope.Commit();

        Coordinator.Current.ShouldBeNull();
    }

    [Fact]
    public void Should_Clear_Current_When_Scope_Rolled_Back()
    {
        var scope = Coordinator.Begin();
        scope.Rollback();

        Coordinator.Current.ShouldBeNull();
    }

    [Fact]
    public void Should_Return_Joined_Scope_When_Active_Root_Exists_On_BeginOrJoin()
    {
        using var root = Coordinator.Begin();
        var joined = Coordinator.BeginOrJoin();

        joined.ShouldNotBe(root);
        joined.ShouldNotBeNull();

        joined.Commit();
        root.Rollback();
    }

    [Fact]
    public void Should_Return_New_Root_Scope_When_No_Active_Root_On_BeginOrJoin()
    {
        Coordinator.Current.ShouldBeNull();

        var scope = Coordinator.BeginOrJoin();

        Coordinator.Current.ShouldBe(scope);
        scope.Dispose();
    }

    [Fact]
    public void Should_Return_Null_Current_Before_Begin()
    {
        Coordinator.Current.ShouldBeNull();
    }

    [Fact]
    public void Should_Throw_When_Begin_Called_While_Scope_Is_Active()
    {
        var first = Coordinator.Begin();

        Should.Throw<InvalidOperationException>(() => Coordinator.Begin());

        first.Dispose();
    }

    [Fact]
    public void Should_Allow_New_Begin_After_Prior_Scope_Committed()
    {
        var first = Coordinator.Begin();
        first.Commit();

        Coordinator.Current.ShouldBeNull();

        var second = Coordinator.Begin();
        second.ShouldNotBeNull();
        second.Dispose();
    }

    [Fact]
    public void Should_Allow_New_Begin_After_Prior_Scope_Rolled_Back()
    {
        var first = Coordinator.Begin();
        first.Rollback();

        var second = Coordinator.Begin();
        second.ShouldNotBeNull();
        second.Dispose();
    }
}
