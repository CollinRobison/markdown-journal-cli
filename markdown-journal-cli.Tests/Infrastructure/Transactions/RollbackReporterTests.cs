using markdown_journal_cli.Infrastructure.Transactions;
using markdown_journal_cli.Infrastructure.Transactions.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Spectre.Console.Testing;

namespace markdown_journal_cli.Tests.Infrastructure.Transactions;

/// <summary>
/// Tests for <see cref="RollbackReporter"/> using Spectre.Console.Testing.TestConsole.
/// </summary>
public class RollbackReporterTests
{
    private readonly TestConsole _console;
    private readonly RollbackReporter _reporter;
    private const string JournalRoot = "/journal";

    public RollbackReporterTests()
    {
        _console = new TestConsole();
        _reporter = new RollbackReporter(_console, NullLogger<RollbackReporter>.Instance);
    }

    [Fact]
    public void Should_Render_Error_Message_With_Cause_For_Starting()
    {
        _reporter.ReportRollbackStarting("add journal entry", new IOException("Disk full"));

        _console.Output.ShouldContain("Failed to add journal entry");
        _console.Output.ShouldContain("Disk full");
    }

    [Fact]
    public void Should_Render_Rolling_Back_Message_For_Starting()
    {
        _reporter.ReportRollbackStarting("add journal entry", new IOException("Disk full"));

        _console.Output.ShouldContain("Rolling back");
    }

    [Fact]
    public void Should_Render_Table_With_Restored_Entries_Using_Relative_Paths()
    {
        var result = new RollbackResult(
            Restored: new List<RollbackEntry>
            {
                new("/journal/entry.md", RollbackEntryKind.Modify),
                new("/journal/new-entry.md", RollbackEntryKind.New),
                new("/journal/old.md", RollbackEntryKind.Rename, "/journal/renamed.md"),
                new("/journal/deleted.md", RollbackEntryKind.Delete),
                new("/journal/sub", RollbackEntryKind.NewDirectory),
            },
            Failed: []
        );

        _reporter.ReportRollbackComplete(result, JournalRoot);

        _console.Output.ShouldContain("Rollback Summary");
        _console.Output.ShouldContain("entry.md");
        _console.Output.ShouldContain("new-entry.md");
        _console.Output.ShouldContain("deleted.md");
        _console.Output.ShouldContain("sub");
        // Should not contain absolute paths
        _console.Output.ShouldNotContain("/journal/entry.md");
    }

    [Fact]
    public void Should_Render_Success_Line_When_Fully_Restored()
    {
        var result = new RollbackResult(
            Restored: new List<RollbackEntry>
            {
                new("/journal/entry.md", RollbackEntryKind.New),
            },
            Failed: []
        );

        _reporter.ReportRollbackComplete(result, JournalRoot);

        _console.Output.ShouldContain("All changes have been rolled back");
        _console.Output.ShouldContain("unchanged");
    }

    [Fact]
    public void Should_Render_Warning_And_Failed_Files_When_Partial()
    {
        var result = new RollbackResult(
            Restored: new List<RollbackEntry>
            {
                new("/journal/a.md", RollbackEntryKind.Modify),
            },
            Failed: new List<RollbackFailure>
            {
                new(new RollbackEntry("/journal/b.md", RollbackEntryKind.Modify),
                    new IOException("Access denied")),
            }
        );

        _reporter.ReportRollbackComplete(result, JournalRoot);

        _console.Output.ShouldContain("WARNING");
        _console.Output.ShouldContain("b.md");
        _console.Output.ShouldContain("Access denied");
        _console.Output.ShouldNotContain("All changes have been rolled back");
    }

    [Fact]
    public void Should_Render_NoOp_Message_When_Entry_Lists_Empty()
    {
        var result = new RollbackResult(Restored: [], Failed: []);

        _reporter.ReportRollbackComplete(result, JournalRoot);

        _console.Output.ShouldContain("No changes to roll back");
        _console.Output.ShouldNotContain("Rollback Summary");
    }

    [Fact]
    public void Constructor_NullConsole_Throws()
    {
        Should.Throw<ArgumentNullException>(() =>
            new RollbackReporter(null!, NullLogger<RollbackReporter>.Instance));
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        Should.Throw<ArgumentNullException>(() =>
            new RollbackReporter(_console, null!));
    }
}
