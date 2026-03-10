using System.Diagnostics;
using markdown_journal_cli.Commands.Add;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.DependencyInjection;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.JournalTemplates;
using markdown_journal_cli.Infrastructure.Tracking;
using markdown_journal_cli.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using Xunit;

namespace markdown_journal_cli.Tests.Commands.Add;

/// <summary>
/// Integration tests for AddEntry command using real services and file operations.
/// These tests validate actual performance, file I/O, and end-to-end scenarios.
/// Uses real FileSystem with temporary directories for true integration testing.
/// </summary>
public class AddEntryIntegrationTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly IFileSystem _fileSystem;
    private readonly ITemplateManager _templateManager;
    private readonly IEntryFormatterService _entryFormatter;
    private readonly IJournalConfiguration _journalConfiguration;
    private readonly IFileTracking _fileTracking;
    private readonly ITableOfContentsService _tocGenerator;
    private readonly IOptions<JournalSettings> _journalSettings;
    private readonly CommandAppTester _app;

    public AddEntryIntegrationTests()
    {
        // Create temporary test directory
        _testDirectory = Path.Combine(Path.GetTempPath(), $"journal-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        _journalSettings = Options.Create(
            new JournalSettings
            {
                AppName = "md-journal",
                JournalConfigFileName = ".journalrc",
                DefaultJournalName = "TestJournal",
                TableOfContentsFileName = "1a-TableOfContents",
                TableOfContentsTitle = "Table of Contents",
                IntroductionFileName = "1b-Intro",
                IntroductionTitle = "Introduction",
                JournalEntryTemplateFileName = "1c-Journal_Entry_Template",
                JournalEntryTemplateTitle = "Journal Entry Template",
                TitleSpaceSeparator = "_",
                HeadingSeparator = "-",
            }
        );

        // Use real services
        _fileSystem = new FileSystem(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<FileSystem>.Instance
        );
        _templateManager = new TemplateManager(_journalSettings);
        _entryFormatter = new EntryFormatterService(_journalSettings);
        var hashService = new HashService();
        _fileTracking = new FileTracking(_fileSystem, _journalSettings, hashService);
        _journalConfiguration = new JournalConfiguration(
            _fileSystem,
            _journalSettings,
            NullLogger<JournalConfiguration>.Instance
        );
        _tocGenerator = new TableOfContentsService(
            _fileSystem,
            _journalConfiguration,
            _journalSettings
        );

        // Initialize a test journal
        InitializeTestJournal();

        var console = new TestConsole();
        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(console);
        services.AddSingleton(_fileSystem);
        services.AddSingleton(_templateManager);
        services.AddSingleton(_entryFormatter);
        services.AddSingleton(_journalConfiguration);
        services.AddSingleton(_fileTracking);
        services.AddSingleton(_tocGenerator);
        services.AddSingleton(_journalSettings);
        services.AddSingleton<AddEntry>();

        var registrar = new TypeRegistrar();

        foreach (var service in services)
        {
            if (service.ImplementationInstance != null)
            {
                registrar.RegisterInstance(service.ServiceType, service.ImplementationInstance);
            }
        }

        _app = new CommandAppTester(registrar);
        _app.Configure(config =>
        {
            config.SetApplicationName(_journalSettings.Value.AppName);
            // Register command directly without branch to ensure settings binding works
            config.AddCommand<AddEntry>("add-entry");
        });
    }

    private void InitializeTestJournal()
    {
        // Create .journalrc file with proper structure
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var journalrcContent = System.Text.Json.JsonSerializer.Serialize(
            new
            {
                journalName = "TestJournal",
                tableOfContents = new
                {
                    file = "1a-TableOfContents.md",
                    extensions = new[] { ".md" },
                    ignoreFiles = Array.Empty<string>(),
                    structure = new { topics = Array.Empty<object>() },
                    rootEntries = Array.Empty<object>(),
                },
            },
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true }
        );
        File.WriteAllText(journalrcPath, journalrcContent);

        // Create tracking index file
        var trackingPath = Path.Combine(_testDirectory, ".md-journal");
        File.WriteAllText(trackingPath, "{}");

        // Create table of contents file
        var tocPath = Path.Combine(_testDirectory, "1a-TableOfContents.md");
        var tocContent =
            $@"[Back to All My Journals](1h-All_My_Journals.md)

Created: {DateTime.Now:M/d/yyyy}
Last Edited: {DateTime.Now:M/d/yyyy}

# Table of Contents

## Entries
";
        File.WriteAllText(tocPath, tocContent);
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
        var result = _app.Run(
            ["add-entry", "PerfTest25", "--sh", subheadingString, "-p", _testDirectory]
        );

        stopwatch.Stop();

        // Assert
        result.ExitCode.ShouldBe(0);
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(
            1000,
            $"Entry creation took {stopwatch.ElapsedMilliseconds}ms, expected < 1000ms"
        );

        // Verify file was actually created
        var expectedFileName = $"{subheadingString}-PerfTest25.md";
        var filePath = Path.Combine(_testDirectory, expectedFileName);
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
        var result = _app.Run(
            [
                "add-entry",
                "PerfTest25H",
                "--he",
                "Main",
                "--sh",
                subheadingString,
                "-p",
                _testDirectory,
            ]
        );

        stopwatch.Stop();

        // Assert
        result.ExitCode.ShouldBe(0);
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(
            1000,
            $"Entry creation with heading and 25 subheadings took {stopwatch.ElapsedMilliseconds}ms, expected < 1000ms"
        );

        // Verify file was created with correct structure
        var expectedFileName = $"Main-{subheadingString}-PerfTest25H.md";
        var filePath = Path.Combine(_testDirectory, expectedFileName);
        File.Exists(filePath).ShouldBeTrue();
    }

    #endregion

    #region End-to-End Integration Tests

    [Fact]
    public void Should_Create_Entry_And_Update_All_Journal_Files()
    {
        // Act
        var result = _app.Run(
            new[] { "add-entry", "IntegrationTest", "--he", "Tech", "-p", _testDirectory }
        );

        // Assert - Show actual output if failed
        if (result.ExitCode != 0)
        {
            throw new Exception(
                $"Command failed with exit code {result.ExitCode}. Output: {result.Output}"
            );
        }

        result.ExitCode.ShouldBe(0);

        // Verify markdown file was created
        var entryFile = Path.Combine(_testDirectory, "Tech-IntegrationTest.md");
        File.Exists(entryFile).ShouldBeTrue();
        var entryContent = File.ReadAllText(entryFile);
        entryContent.ShouldContain("# IntegrationTest");
        entryContent.ShouldContain("Created:");
        entryContent.ShouldContain("Last Edited:");

        // Verify .journalrc was updated
        var journalrcPath = Path.Combine(_testDirectory, ".journalrc");
        var journalrcContent = File.ReadAllText(journalrcPath);
        journalrcContent.ShouldContain("IntegrationTest");
        journalrcContent.ShouldContain("Tech");

        // Verify tracking index was updated
        var trackingPath = Path.Combine(_testDirectory, ".md-journal");
        var trackingContent = File.ReadAllText(trackingPath);
        trackingContent.ShouldContain("Tech-IntegrationTest.md");

        // Verify table of contents was updated
        var tocPath = Path.Combine(_testDirectory, "1a-TableOfContents.md");
        var tocContent = File.ReadAllText(tocPath);
        tocContent.ShouldContain("IntegrationTest");
    }

    [Fact]
    public void Should_Handle_Multiple_Entries_With_Same_Heading()
    {
        // Act - Create multiple entries under same heading
        var result1 = _app.Run(
            ["add-entry", "Entry1", "--he", "CommonHeading", "-p", _testDirectory]
        );
        var result2 = _app.Run(
            ["add-entry", "Entry2", "--he", "CommonHeading", "-p", _testDirectory]
        );
        var result3 = _app.Run(
            ["add-entry", "Entry3", "--he", "CommonHeading", "-p", _testDirectory]
        );

        // Assert
        result1.ExitCode.ShouldBe(0);
        result2.ExitCode.ShouldBe(0);
        result3.ExitCode.ShouldBe(0);

        // Verify all files exist
        File.Exists(Path.Combine(_testDirectory, "CommonHeading-Entry1.md")).ShouldBeTrue();
        File.Exists(Path.Combine(_testDirectory, "CommonHeading-Entry2.md")).ShouldBeTrue();
        File.Exists(Path.Combine(_testDirectory, "CommonHeading-Entry3.md")).ShouldBeTrue();

        // Verify journalrc has all entries under the same heading
        var journalrcContent = File.ReadAllText(Path.Combine(_testDirectory, ".journalrc"));
        journalrcContent.ShouldContain("Entry1");
        journalrcContent.ShouldContain("Entry2");
        journalrcContent.ShouldContain("Entry3");
    }

    [Fact]
    public void Should_Create_Entry_With_Complex_Hierarchy()
    {
        // Act - Create entry with heading and nested subheadings
        var result = _app.Run(
            [
                "add-entry",
                "ComplexEntry",
                "--he",
                "Category",
                "--sh",
                "Sub1-Sub2-Sub3",
                "-p",
                _testDirectory,
            ]
        );

        // Assert
        result.ExitCode.ShouldBe(0);

        var expectedFile = Path.Combine(_testDirectory, "Category-Sub1-Sub2-Sub3-ComplexEntry.md");
        File.Exists(expectedFile).ShouldBeTrue();

        // Verify journalrc has correct hierarchy
        var journalrcContent = File.ReadAllText(Path.Combine(_testDirectory, ".journalrc"));
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
        var result = _app.Run(
            ["add-entry", "My Special Entry", "--he", "Tech News", "-p", _testDirectory]
        );

        // Assert
        result.ExitCode.ShouldBe(0);

        // Verify filename is properly formatted
        var expectedFile = Path.Combine(_testDirectory, "Tech_News-My_Special_Entry.md");
        File.Exists(expectedFile).ShouldBeTrue();

        var content = File.ReadAllText(expectedFile);
        content.ShouldContain("# My Special Entry"); // Title preserves spaces from input
    }

    [Fact]
    public void Should_Update_TableOfContents_LastEdited_Date()
    {
        // Arrange - Read initial TOC
        var tocPath = Path.Combine(_testDirectory, "1a-TableOfContents.md");
        var initialContent = File.ReadAllText(tocPath);

        // Wait a moment to ensure timestamp difference
        Thread.Sleep(100);

        // Act - Create new entry
        var result = _app.Run(["add-entry", "DateTest", "-p", _testDirectory]);

        // Assert
        result.ExitCode.ShouldBe(0);

        var updatedContent = File.ReadAllText(tocPath);
        updatedContent.ShouldNotBe(initialContent);
        updatedContent.ShouldContain("DateTest");
        updatedContent.ShouldContain("Last Edited:"); // Should have been updated
    }

    [Fact]
    public void Should_Create_Valid_Markdown_With_Custom_Title()
    {
        // Act
        var result = _app.Run(
            ["add-entry", "file_name", "-t", "My Custom Title", "-p", _testDirectory]
        );

        // Assert
        result.ExitCode.ShouldBe(0);

        var filePath = Path.Combine(_testDirectory, "file_name.md");
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
        var result1 = _app.Run(["add-entry", "DuplicateTest", "-p", _testDirectory]);
        result1.ExitCode.ShouldBe(0);

        // Act - Try to create duplicate
        var result2 = _app.Run(["add-entry", "DuplicateTest", "-p", _testDirectory]);

        // Assert
        result2.ExitCode.ShouldBe(1);
        result2.Output.ShouldContain("Error:");
        result2.Output.ShouldContain("already exists");
    }

    #endregion

    public void Dispose()
    {
        // Cleanup test directory
        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }
}
