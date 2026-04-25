using System.Diagnostics;
using markdown_journal_cli.Commands.Add;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.JournalTemplates;
using markdown_journal_cli.Infrastructure.Tracking;
using markdown_journal_cli.Infrastructure.Transactions;
using markdown_journal_cli.Services;
using markdown_journal_cli.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Spectre.Console.Testing;
using Xunit;

namespace markdown_journal_cli.Tests.Commands.Add;

/// <summary>
/// Integration tests for AddEntry command using real services and file operations.
/// These tests validate actual performance, file I/O, and end-to-end scenarios.
/// Uses real FileSystem with temporary directories for true integration testing.
/// </summary>
public class AddEntryIntegrationTests : JournalIntegrationTestBase
{
    private readonly AddEntry _addEntryCommand;
    private readonly TestConsole _console;

    public AddEntryIntegrationTests() : base("TestJournal")
    {
        // Initialize journal files on disk
        InitializeJournal();

        var templateManager = new TemplateManager(JournalSettings);
        var entryFormatter = new EntryFormatterService(JournalSettings);
        var hashService = new HashService();
        var fileTracking = new FileTracking(FileSystem, JournalSettings, hashService);
        var tocStructureRepository = new JournalTocStructureRepository(FileSystem, JournalSettings);
        var journalConfiguration = new JournalConfiguration(
            FileSystem,
            JournalSettings,
            NullLogger<JournalConfiguration>.Instance,
            fileTracking,
            tocStructureRepository
        );
        var tocGenerator = new TableOfContentsService(
            FileSystem,
            journalConfiguration,
            JournalSettings,
            NullLogger<TableOfContentsService>.Instance,
            tocStructureRepository
        );

        _console = new TestConsole();
        var journalEntryService = new JournalEntryService(
            FileSystem,
            journalConfiguration,
            JournalSettings,
            entryFormatter,
            templateManager,
            fileTracking,
            tocGenerator,
            NoOpFileTransactionCoordinator.Instance,
            NoOpRollbackReporter.Instance,
            NullLogger<JournalEntryService>.Instance
        );
        _addEntryCommand = new AddEntry(_console, journalEntryService);
    }

    private int RunCommand(
        string entryName,
        string? path = null,
        string? heading = null,
        string? subheading = null,
        string? title = null,
        bool ignoreFile = false
    )
    {
        var settings = new AddEntrySettings
        {
            EntryName = entryName,
            FilePath = path ?? JournalPath,
            Heading = heading,
            Subheading = subheading,
            EntryTitle = title,
            IgnoreFile = ignoreFile,
        };
        return _addEntryCommand.Execute(null!, settings);
    }

    #region Performance Tests

    [Fact]
    public void Should_Complete_Entry_Creation_With_25_Subheadings_In_Under_One_Second()
    {
        // Arrange - Create 25 levels of subheadings
        var subheadings = Enumerable.Range(1, 25).Select(i => $"L{i}").ToArray();
        var subheadingString = string.Join("-", subheadings);

        var stopwatch = Stopwatch.StartNew();

        // Act
        var exitCode = RunCommand("PerfTest25", subheading: subheadingString);

        stopwatch.Stop();

        // Assert
        exitCode.ShouldBe(0);
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(
            1000,
            $"Entry creation took {stopwatch.ElapsedMilliseconds}ms, expected < 1000ms"
        );

        // Verify file was actually created
        var expectedFileName = $"{subheadingString}-PerfTest25.md";
        var filePath = Path.Combine(JournalPath, expectedFileName);
        File.Exists(filePath).ShouldBeTrue($"File should exist at {filePath}");
    }

    [Fact]
    public void Should_Complete_Entry_Creation_With_Heading_And_25_Subheadings_In_Under_One_Second()
    {
        // Arrange - Create heading + 25 subheadings
        var subheadings = Enumerable.Range(1, 25).Select(i => $"S{i}").ToArray();
        var subheadingString = string.Join("-", subheadings);

        var stopwatch = Stopwatch.StartNew();

        // Act
        var exitCode = RunCommand("PerfTest25H", heading: "Main", subheading: subheadingString);

        stopwatch.Stop();

        // Assert
        exitCode.ShouldBe(0);
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(
            1000,
            $"Entry creation with heading and 25 subheadings took {stopwatch.ElapsedMilliseconds}ms, expected < 1000ms"
        );

        // Verify file was created with correct structure
        var expectedFileName = $"Main-{subheadingString}-PerfTest25H.md";
        var filePath = Path.Combine(JournalPath, expectedFileName);
        File.Exists(filePath).ShouldBeTrue();
    }

    #endregion

    #region End-to-End Integration Tests

    [Fact]
    public void Should_Create_Entry_And_Update_All_Journal_Files()
    {
        // Act
        var exitCode = RunCommand("IntegrationTest", heading: "Tech");

        // Assert - Show actual output if failed
        exitCode.ShouldBe(0, "Command failed — check journal setup.");

        // Verify markdown file was created
        var entryFile = Path.Combine(JournalPath, "Tech-IntegrationTest.md");
        File.Exists(entryFile).ShouldBeTrue();
        var entryContent = File.ReadAllText(entryFile);
        entryContent.ShouldContain("# IntegrationTest");
        entryContent.ShouldContain("Created:");
        entryContent.ShouldContain("Last Edited:");

        // Verify .journalrc was updated
        var journalrcPath = Path.Combine(JournalPath, ".journalrc");
        var journalrcContent = File.ReadAllText(journalrcPath);
        journalrcContent.ShouldContain("IntegrationTest");
        journalrcContent.ShouldContain("Tech");

        // Verify tracking index was updated
        var trackingPath = Path.Combine(JournalPath, ".mdjournal", ".journalindex");
        var trackingContent = File.ReadAllText(trackingPath);
        trackingContent.ShouldContain("Tech-IntegrationTest.md");

        // Verify table of contents was updated
        var tocPath = Path.Combine(JournalPath, "1a-TableOfContents.md");
        var tocContent = File.ReadAllText(tocPath);
        tocContent.ShouldContain("IntegrationTest");
    }

    [Fact]
    public void Should_Handle_Multiple_Entries_With_Same_Heading()
    {
        // Act - Create multiple entries under same heading
        var exitCode1 = RunCommand("Entry1", heading: "CommonHeading");
        var exitCode2 = RunCommand("Entry2", heading: "CommonHeading");
        var exitCode3 = RunCommand("Entry3", heading: "CommonHeading");

        // Assert
        exitCode1.ShouldBe(0);
        exitCode2.ShouldBe(0);
        exitCode3.ShouldBe(0);

        // Verify all files exist
        File.Exists(Path.Combine(JournalPath, "CommonHeading-Entry1.md")).ShouldBeTrue();
        File.Exists(Path.Combine(JournalPath, "CommonHeading-Entry2.md")).ShouldBeTrue();
        File.Exists(Path.Combine(JournalPath, "CommonHeading-Entry3.md")).ShouldBeTrue();

        // Verify journalrc has all entries under the same heading
        var journalrcContent = File.ReadAllText(Path.Combine(JournalPath, ".journalrc"));
        journalrcContent.ShouldContain("Entry1");
        journalrcContent.ShouldContain("Entry2");
        journalrcContent.ShouldContain("Entry3");
    }

    [Fact]
    public void Should_Create_Entry_With_Complex_Hierarchy()
    {
        // Act - Create entry with heading and nested subheadings
        var exitCode = RunCommand(
            "ComplexEntry",
            heading: "Category",
            subheading: "Sub1-Sub2-Sub3"
        );

        // Assert
        exitCode.ShouldBe(0);

        var expectedFile = Path.Combine(JournalPath, "Category-Sub1-Sub2-Sub3-ComplexEntry.md");
        File.Exists(expectedFile).ShouldBeTrue();

        // Verify journalrc has correct hierarchy
        var journalrcContent = File.ReadAllText(Path.Combine(JournalPath, ".journalrc"));
        journalrcContent.ShouldContain("Category");
        journalrcContent.ShouldContain("Sub1");
        journalrcContent.ShouldContain("Sub2");
        journalrcContent.ShouldContain("Sub3");
        journalrcContent.ShouldContain("ComplexEntry");
    }

    [Fact]
    public void Should_Properly_Handle_Special_Characters_In_Filenames()
    {
        // Act - Create entry with underscores and spaces
        var exitCode = RunCommand("My Special Entry", heading: "Tech News");

        // Assert
        exitCode.ShouldBe(0);

        // Verify filename is properly formatted
        var expectedFile = Path.Combine(JournalPath, "Tech_News-My_Special_Entry.md");
        File.Exists(expectedFile).ShouldBeTrue();

        var content = File.ReadAllText(expectedFile);
        content.ShouldContain("# My Special Entry"); // Title preserves spaces from input
    }

    [Fact]
    public void Should_Update_TableOfContents_LastEdited_Date()
    {
        // Arrange - Read initial TOC
        var tocPath = Path.Combine(JournalPath, "1a-TableOfContents.md");
        var initialContent = File.ReadAllText(tocPath);

        // Wait a moment to ensure timestamp difference
        Thread.Sleep(100);

        // Act - Create new entry
        var exitCode = RunCommand("DateTest");

        // Assert
        exitCode.ShouldBe(0);

        var updatedContent = File.ReadAllText(tocPath);
        updatedContent.ShouldNotBe(initialContent);
        updatedContent.ShouldContain("DateTest");
        updatedContent.ShouldContain("Last Edited:"); // Should have been updated
    }

    [Fact]
    public void Should_Create_Valid_Markdown_With_Custom_Title()
    {
        // Act
        var exitCode = RunCommand("file_name", title: "My Custom Title");

        // Assert
        exitCode.ShouldBe(0);

        var filePath = Path.Combine(JournalPath, "file_name.md");
        File.Exists(filePath).ShouldBeTrue();

        var content = File.ReadAllText(filePath);
        content.ShouldContain("# My Custom Title"); // Custom title in content with spaces
        content.ShouldContain("[Back to Table of Contents]");
        content.ShouldContain("Created:");
        content.ShouldContain("Last Edited:");
    }

    [Fact]
    public void Should_Prevent_Duplicate_Entry_Creation()
    {
        // Arrange - Create first entry
        var exitCode1 = RunCommand("DuplicateTest");
        exitCode1.ShouldBe(0);

        // Act - Try to create duplicate
        var exitCode2 = RunCommand("DuplicateTest");

        // Assert
        exitCode2.ShouldBe(1);
        _console.Output.ShouldContain("Error:");
        _console.Output.ShouldContain("already exists");
    }

    #endregion

}
