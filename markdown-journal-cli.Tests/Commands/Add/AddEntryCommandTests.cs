using markdown_journal_cli.Commands.Add;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.JournalTemplates;
using markdown_journal_cli.Infrastructure.Tracking;
using markdown_journal_cli.Infrastructure.Transactions;
using markdown_journal_cli.Services;
using markdown_journal_cli.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using Spectre.Console.Cli;
using Spectre.Console.Testing;

namespace markdown_journal_cli.Tests.Commands.Add;

/// <summary>
/// Unit tests for AddEntry command covering positive, negative, and edge cases.
/// Uses Moq for dependency mocking to maintain simplicity.
/// </summary>
public class AddEntryCommandTests : CommandTestBase
{
    private CommandAppTester BuildAddEntryApp()
    {
        var journalEntryService = new JournalEntryService(
            MockFileSystem.Object,
            MockJournalConfiguration.Object,
            JournalSettings,
            MockEntryFormatterService.Object,
            MockTemplateManager.Object,
            MockFileTracking.Object,
            MockTableOfContentsService.Object,
            NoOpFileTransactionCoordinator.Instance,
            NoOpRollbackReporter.Instance,
            NullLogger<JournalEntryService>.Instance
        );

        return BuildApp(
            config =>
            {
                config.SetApplicationName(JournalSettings.Value.AppName);
                config.AddBranch<AddSettings>(
                    "add",
                    add => { add.AddCommand<AddEntry>("entry"); }
                );
            },
            services =>
            {
                services.AddSingleton<IJournalEntryService>(journalEntryService);
                services.AddSingleton<AddEntry>();
            }
        );
    }

    protected override void SetupDefaultBehaviors()
    {
        // Default: .journalrc and .md-journal files exist
        MockFileSystem
            .Setup(fs => fs.FileExists(It.Is<string>(s => s.Contains(".journalrc"))))
            .Returns(true);
        MockFileSystem
            .Setup(fs => fs.FileExists(It.Is<string>(s => s.Contains(".md-journal"))))
            .Returns(true);

        // Default: entry markdown files don't exist yet
        MockFileSystem
            .Setup(fs =>
                fs.FileExists(
                    It.Is<string>(s => s.EndsWith(".md") && !s.Contains("TableOfContents"))
                )
            )
            .Returns(false);

        // Default entry formatter behaviors
        MockEntryFormatterService
            .Setup(ef => ef.RemoveSpaceSeparators(It.IsAny<string>()))
            .Returns((string input) => input?.Replace("_", " ").Trim() ?? "");

        MockEntryFormatterService
            .Setup(ef => ef.AddSpaceSeparators(It.IsAny<string>()))
            .Returns((string input) => input?.Replace(" ", "_") ?? "");

        MockEntryFormatterService
            .Setup(ef => ef.AddHeadingSeparators(It.IsAny<string[]>()))
            .Returns((string[] parts) => string.Join("-", parts));

        // BuildHeadingArray is called by JournalEntryService to produce the topic path for journalrc
        MockEntryFormatterService
            .Setup(ef => ef.BuildHeadingArray(It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(
                (string? heading, string? subheading) =>
                {
                    if (heading == null && subheading == null)
                        return Array.Empty<string>();
                    var parts = new List<string>();
                    if (heading != null)
                        parts.Add(heading.Replace(" ", "_"));
                    if (subheading != null)
                        parts.AddRange(
                            subheading.Split('-', StringSplitOptions.RemoveEmptyEntries)
                        );
                    return parts.ToArray();
                }
            );

        // Default template returns proper journal entry format
        MockTemplateManager
            .Setup(tm =>
                tm.GenerateFromTemplate("journal-entry", It.IsAny<Dictionary<string, object>>())
            )
            .Returns(
                (string templateName, Dictionary<string, object>? parameters) =>
                {
                    var title = parameters?["title"]?.ToString() ?? "Title";
                    var date = DateTime.Now.ToString("M/d/yyyy");
                    return $@"[Back to Table of Contents](1a-TableOfContents.md)

Created: {date}
Last Edited: {date}

# {title}

body goes here.

[Make sure to add link to any reference here](add-link)
";
                }
            );

        // Default: File system operations succeed
        MockFileSystem.Setup(fs =>
            fs.CreateMarkdownFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())
        );

        // Default: AddEntry succeeds
        MockJournalConfiguration.Setup(jc =>
            jc.AddEntry(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string[]>(),
                It.IsAny<int?>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()
            )
        );

        // Default: File tracking succeeds
        MockFileTracking.Setup(ft => ft.UpdateFileInIndex(It.IsAny<string>(), It.IsAny<string>()));

        // Default: TOC update succeeds
        MockTableOfContentsService.Setup(tg =>
            tg.UpdateTableOfContents(
                It.IsAny<string>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>()
            )
        );

        MockFileSystem
            .Setup(fs => fs.CombinePaths(It.IsAny<string[]>()))
            .Returns((string[] paths) => Path.Combine(paths));
    }

    #region Positive Cases

    [Fact]
    public void Execute_Should_CreateEntryWithSimpleName()
    {
        // Act
        var result = BuildAddEntryApp().Run(["add", "entry", "MyEntry", "-p", "."]);

        // Assert
        result.ExitCode.ShouldBe(0);
        MockFileSystem.Verify(
            fs => fs.CreateMarkdownFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Once
        );
        MockFileTracking.Verify(ft => ft.UpdateFileInIndex(".", It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void Execute_Should_CreateEntryWithHeading()
    {
        // Act
        var result = BuildAddEntryApp().Run(["add", "entry", "MyEntry", "--he", "Tech", "-p", "."]);

        // Assert
        result.ExitCode.ShouldBe(0);
        MockFileSystem.Verify(
            fs => fs.CreateMarkdownFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Once
        );
    }

    [Fact]
    public void Execute_Should_CreateEntryWithSubheading()
    {
        // Arrange
        MockEntryFormatterService
            .Setup(ef => ef.SeperateSubheadingString("AI-ML"))
            .Returns(new[] { "AI", "ML" });

        // Act
        var result = BuildAddEntryApp().Run(["add", "entry", "MyEntry", "--sh", "AI-ML", "-p", "."]);

        // Assert
        result.ExitCode.ShouldBe(0);
    }

    [Fact]
    public void Execute_Should_CreateEntryWithHeadingAndSubheading()
    {
        // Arrange
        MockEntryFormatterService
            .Setup(ef => ef.SeperateSubheadingString("AI-ML"))
            .Returns(new[] { "AI", "ML" });

        // Act
        var result = BuildAddEntryApp().Run(
            ["add", "entry", "MyEntry", "--he", "Tech", "--sh", "AI-ML", "-p", "."]
        );

        // Assert
        result.ExitCode.ShouldBe(0);
        MockFileSystem.Verify(
            fs => fs.CreateMarkdownFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Once
        );
    }

    [Fact]
    public void Execute_Should_UseCustomTitle_When_TitleProvided()
    {
        // Act
        var result = BuildAddEntryApp().Run(["add", "entry", "my_file_name", "-t", "My Custom Title", "-p", "."]);

        // Assert
        // RemoveSpaceSeparators replaces underscores with spaces; "My Custom Title" has no underscores
        // so the title passed to the template is "My Custom Title"
        result.ExitCode.ShouldBe(0);
        MockTemplateManager.Verify(
            tm =>
                tm.GenerateFromTemplate(
                    "journal-entry",
                    It.Is<Dictionary<string, object>>(d =>
                        d["title"].ToString() == "My Custom Title"
                    )
                ),
            Times.Once
        );
    }

    #endregion

    #region Negative Cases

    [Fact]
    public void Execute_Should_ReturnError_When_JournalrcNotFound()
    {
        // Arrange
        MockFileSystem.Setup(fs => fs.FileExists("./.journalrc")).Returns(false);

        // Act
        var result = BuildAddEntryApp().Run(["add", "entry", "MyEntry", "-p", "."]);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Output.ShouldContain("Error:");
        MockFileSystem.Verify(
            fs => fs.CreateMarkdownFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never
        );
    }

    [Fact]
    public void Execute_Should_ReturnError_When_TrackingIndexNotFound()
    {
        // Arrange
        MockFileSystem.Setup(fs => fs.FileExists("./.md-journal")).Returns(false);

        // Act
        var result = BuildAddEntryApp().Run(["add", "entry", "MyEntry", "-p", "."]);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Output.ShouldContain("Error:");
        MockFileSystem.Verify(
            fs => fs.CreateMarkdownFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never
        );
    }

    [Fact]
    public void Execute_Should_ReturnError_When_EntryAlreadyExists()
    {
        // Arrange
        MockFileSystem
            .Setup(fs => fs.FileExists(It.Is<string>(s => s.EndsWith(It.IsAny<string>()))))
            .Returns(true);

        // Act
        var result = BuildAddEntryApp().Run(["add", "entry", "MyEntry", "-p", "."]);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Output.ShouldContain("Error:");
    }

    [Theory]
    [InlineData("Invalid/Name")]
    [InlineData("Invalid<Name")]
    [InlineData("Invalid>Name")]
    [InlineData("Invalid|Name")]
    [InlineData("Invalid:Name")]
    public void Execute_Should_ReturnError_When_EntryNameHasInvalidCharacters(string invalidName)
    {
        // Act
        var result = BuildAddEntryApp().Run(["add", "entry", invalidName, "-p", "."]);

        // Assert
        result.ExitCode.ShouldNotBe(0);
        result.Output.ShouldContain("character");
    }

    [Theory]
    [InlineData("Invalid/Heading")]
    [InlineData("Invalid<Heading")]
    public void Execute_Should_ReturnError_When_HeadingHasInvalidCharacters(string invalidHeading)
    {
        // Act
        var result = BuildAddEntryApp().Run(["add", "entry", "MyEntry", "--he", invalidHeading, "-p", "."]);

        // Assert
        result.ExitCode.ShouldNotBe(0);
        result.Output.ShouldContain("character");
    }

    [Theory]
    [InlineData("Invalid/Sub")]
    [InlineData("Invalid Sub")]
    [InlineData("Invalid<Sub")]
    public void Execute_Should_ReturnError_When_SubheadingHasInvalidCharacters(string invalidSubheading)
    {
        // Act
        var result = BuildAddEntryApp().Run(["add", "entry", "MyEntry", "--sh", invalidSubheading, "-p", "."]);

        // Assert
        result.ExitCode.ShouldNotBe(0);
        result.Output.ShouldContain("character");
    }

    [Fact]
    public void Execute_Should_HandleException_When_JournalConfigurationThrows()
    {
        // Arrange
        MockJournalConfiguration
            .Setup(jc =>
                jc.AddEntry(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<int?>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>()
                )
            )
            .Throws(new InvalidOperationException("Configuration error"));

        // Act
        var result = BuildAddEntryApp().Run(["add", "entry", "MyEntry", "-p", "."]);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Output.ShouldContain("Error:");
        result.Output.ShouldContain("unexpected error");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Execute_Should_HandleEntryNameWithUnderscores()
    {
        // Act
        var result = BuildAddEntryApp().Run(["add", "entry", "my_entry_name", "-p", "."]);

        // Assert
        result.ExitCode.ShouldBe(0);
        MockFileSystem.Verify(
            fs => fs.CreateMarkdownFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Once
        );
    }

    [Fact]
    public void Execute_Should_HandleEntryNameWithNumbers()
    {
        // Act
        var result = BuildAddEntryApp().Run(["add", "entry", "Entry123", "-p", "."]);

        // Assert
        result.ExitCode.ShouldBe(0);
    }

    [Fact]
    public void Execute_Should_HandleHeadingWithSpaces()
    {
        // Act
        var result = BuildAddEntryApp().Run(["add", "entry", "MyEntry", "--he", "Tech News", "-p", "."]);

        // Assert
        result.ExitCode.ShouldBe(0);
        MockFileSystem.Verify(
            fs => fs.CreateMarkdownFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Once
        );
    }

    [Fact]
    public void Execute_Should_HandleMultipleSubheadingsInChain()
    {
        // Arrange
        MockEntryFormatterService
            .Setup(ef => ef.SeperateSubheadingString("Level1-Level2-Level3-Level4"))
            .Returns(new[] { "Level1", "Level2", "Level3", "Level4" });

        // Act
        var result = BuildAddEntryApp().Run(
            ["add", "entry", "MyEntry", "--sh", "Level1-Level2-Level3-Level4", "-p", "."]
        );

        // Assert
        result.ExitCode.ShouldBe(0);
    }

    [Fact]
    public void Execute_Should_HandleSubheadingOnlyWithoutHeading()
    {
        // Arrange
        MockEntryFormatterService
            .Setup(ef => ef.SeperateSubheadingString("SubOnly"))
            .Returns(new[] { "SubOnly" });

        // Act
        var result = BuildAddEntryApp().Run(["add", "entry", "MyEntry", "--sh", "SubOnly", "-p", "."]);

        // Assert
        result.ExitCode.ShouldBe(0);
    }

    [Fact]
    public void Execute_Should_HandleVeryLongEntryName()
    {
        // Arrange
        var longName = new string('A', 200);

        // Act
        var result = BuildAddEntryApp().Run(["add", "entry", longName, "-p", "."]);

        // Assert
        result.ExitCode.ShouldBe(0);
        MockFileSystem.Verify(
            fs => fs.CreateMarkdownFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Once
        );
    }

    [Fact]
    public void Execute_Should_HandleCustomPath()
    {
        // Act - The path parameter is passed through command settings
        var result = BuildAddEntryApp().Run(["add", "entry", "MyEntry", "-p", "."]);

        // Assert
        result.ExitCode.ShouldBe(0);
        MockFileSystem.Verify(
            fs => fs.CreateMarkdownFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Once
        );
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void Execute_Should_NotUpdateConfiguration_When_FileCreationFails()
    {
        // Arrange
        MockFileSystem
            .Setup(fs =>
                fs.CreateMarkdownFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())
            )
            .Throws(new IOException("Disk full"));

        // Act
        var result = BuildAddEntryApp().Run(["add", "entry", "MyEntry", "-p", "."]);

        // Assert
        result.ExitCode.ShouldBe(1);
        MockFileTracking.Verify(
            ft => ft.UpdateFileInIndex(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never
        );
    }

    #endregion

    #region Ignore File Tests

    [Fact]
    public void Execute_Should_NotUpdateTableOfContents_When_IgnoreFileIsTrue()
    {
        // Act
        var result = BuildAddEntryApp().Run(["add", "entry", "MyEntry", "--ignore", "-p", "."]);

        // Assert
        result.ExitCode.ShouldBe(0);
        // File should still be created
        MockFileSystem.Verify(
            fs => fs.CreateMarkdownFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Once
        );
        // File should still be tracked
        MockFileTracking.Verify(
            ft => ft.UpdateFileInIndex(It.IsAny<string>(), It.IsAny<string>()),
            Times.Once
        );
        // But TOC should NOT be updated
        MockTableOfContentsService.Verify(
            toc =>
                toc.UpdateTableOfContents(
                    It.IsAny<string>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<DateTime?>()
                ),
            Times.Never
        );
    }

    [Fact]
    public void Execute_Should_AddEntryToConfiguration_When_IgnoreFileFlagSet()
    {
        // Act
        var result = BuildAddEntryApp().Run(["add", "entry", "MyEntry", "--ignore", "-p", "."]);

        // Assert
        result.ExitCode.ShouldBe(0);
        // Should call AddEntry with ignoreFile = true
        MockJournalConfiguration.Verify(
            jc =>
                jc.AddEntry(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<int?>(),
                    It.IsAny<bool>(),
                    true
                ),
            Times.Once
        );
    }

    [Fact]
    public void Execute_Should_AddEntryToConfiguration_When_IgnoreFileFlagNotSet()
    {
        // Act
        var result = BuildAddEntryApp().Run(["add", "entry", "MyEntry", "-p", "."]);

        // Assert
        result.ExitCode.ShouldBe(0);
        // Should call AddEntry with ignoreFile = false (default)
        MockJournalConfiguration.Verify(
            jc =>
                jc.AddEntry(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<int?>(),
                    It.IsAny<bool>(),
                    false
                ),
            Times.Once
        );
    }

    [Fact]
    public void Execute_Should_UpdateTableOfContents_When_IgnoreFileIsFalse()
    {
        // Act
        var result = BuildAddEntryApp().Run(["add", "entry", "MyEntry", "-p", "."]);

        // Assert
        result.ExitCode.ShouldBe(0);
        // TOC should be updated when ignore flag is not set
        MockTableOfContentsService.Verify(
            toc =>
                toc.UpdateTableOfContents(
                    It.IsAny<string>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<DateTime?>()
                ),
            Times.Once
        );
    }

    [Fact]
    public void Execute_Should_HandleIgnoreFileWithHeadingAndSubheading()
    {
        // Arrange
        MockEntryFormatterService
            .Setup(ef => ef.SeperateSubheadingString("AI-ML"))
            .Returns(new[] { "AI", "ML" });

        // Act
        var result = BuildAddEntryApp().Run(
            ["add", "entry", "MyEntry", "--he", "Tech", "--sh", "AI-ML", "--ignore", "-p", "."]
        );

        // Assert
        result.ExitCode.ShouldBe(0);
        // File should be created
        MockFileSystem.Verify(
            fs => fs.CreateMarkdownFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Once
        );
        // Should be added to configuration with ignoreFile = true
        MockJournalConfiguration.Verify(
            jc =>
                jc.AddEntry(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<int?>(),
                    It.IsAny<bool>(),
                    true
                ),
            Times.Once
        );
        // TOC should NOT be updated
        MockTableOfContentsService.Verify(
            toc =>
                toc.UpdateTableOfContents(
                    It.IsAny<string>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<DateTime?>()
                ),
            Times.Never
        );
    }

    [Fact]
    public void Execute_Should_HandleIgnoreFileWithCustomTitle()
    {
        // Act
        var result = BuildAddEntryApp().Run(
            ["add", "entry", "my_file_name", "-t", "My Custom Title", "--ignore", "-p", "."]
        );

        // Assert
        result.ExitCode.ShouldBe(0);
        // Should use custom title even with ignore flag
        MockTemplateManager.Verify(
            tm =>
                tm.GenerateFromTemplate(
                    "journal-entry",
                    It.Is<Dictionary<string, object>>(d =>
                        d["title"].ToString() == "My Custom Title"
                    )
                ),
            Times.Once
        );
        // TOC should NOT be updated
        MockTableOfContentsService.Verify(
            toc =>
                toc.UpdateTableOfContents(
                    It.IsAny<string>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<DateTime?>()
                ),
            Times.Never
        );
    }

    [Fact]
    public void Execute_Should_StillTrackFile_When_IgnoreFileIsTrue()
    {
        // Act
        var result = BuildAddEntryApp().Run(["add", "entry", "MyEntry", "--ignore", "-p", "."]);

        // Assert
        result.ExitCode.ShouldBe(0);
        // File tracking should still occur even when ignoring in TOC
        MockFileTracking.Verify(
            ft => ft.UpdateFileInIndex(It.IsAny<string>(), It.IsAny<string>()),
            Times.Once
        );
    }

    #endregion
}
