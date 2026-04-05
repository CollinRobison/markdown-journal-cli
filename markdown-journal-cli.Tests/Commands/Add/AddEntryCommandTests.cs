using System.Diagnostics;
using markdown_journal_cli.Commands.Add;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.DependencyInjection;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.Transactions;
using markdown_journal_cli.Infrastructure.JournalTemplates;
using markdown_journal_cli.Infrastructure.Tracking;
using markdown_journal_cli.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;

namespace markdown_journal_cli.Tests.Commands.Add;

/// <summary>
/// Unit tests for AddEntry command covering positive, negative, and edge cases.
/// Uses Moq for dependency mocking to maintain simplicity.
/// </summary>
public class AddEntryCommandTests
{
    private readonly Mock<IFileSystem> _mockFileSystem;
    private readonly Mock<ITemplateManager> _mockTemplateManager;
    private readonly Mock<IEntryFormatterService> _mockEntryFormatter;
    private readonly Mock<IJournalConfiguration> _mockJournalConfiguration;
    private readonly Mock<IFileTracking> _mockFileTracking;
    private readonly Mock<ITableOfContentsService> _mockTocGenerator;
    private readonly IOptions<JournalSettings> _journalSettings;
    private readonly TestConsole _console;
    private readonly CommandAppTester _app;

    public AddEntryCommandTests()
    {
        _console = new TestConsole();
        _mockFileSystem = new Mock<IFileSystem>();
        _mockTemplateManager = new Mock<ITemplateManager>();
        _mockEntryFormatter = new Mock<IEntryFormatterService>();
        _mockJournalConfiguration = new Mock<IJournalConfiguration>();
        _mockFileTracking = new Mock<IFileTracking>();
        _mockTocGenerator = new Mock<ITableOfContentsService>();

        _journalSettings = Options.Create(
            new JournalSettings
            {
                AppName = "md-journal",
                JournalConfigFileName = ".journalrc",
                DefaultJournalName = "MyJournal",
                TableOfContentsFileName = "1a-TableOfContents",
                TableOfContentsTitle = "Table of Contents",
            }
        );

        SetupDefaultMockBehaviors();

        var journalEntryService = new JournalEntryService(
            _mockFileSystem.Object,
            _mockJournalConfiguration.Object,
            _journalSettings,
            _mockEntryFormatter.Object,
            _mockTemplateManager.Object,
            _mockFileTracking.Object,
            _mockTocGenerator.Object,
            NoOpFileTransactionCoordinator.Instance,
            NoOpRollbackReporter.Instance,
            NullLogger<JournalEntryService>.Instance
        );

        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(_console);
        services.AddSingleton(_console);
        services.AddSingleton(_mockFileSystem.Object);
        services.AddSingleton(_mockTemplateManager.Object);
        services.AddSingleton(_mockEntryFormatter.Object);
        services.AddSingleton(_mockJournalConfiguration.Object);
        services.AddSingleton(_mockFileTracking.Object);
        services.AddSingleton(_mockTocGenerator.Object);
        services.AddSingleton(_journalSettings);
        services.AddSingleton<IJournalEntryService>(journalEntryService);
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
            config.AddBranch<AddSettings>(
                "add",
                add =>
                {
                    add.AddCommand<AddEntry>("entry");
                }
            );
        });
    }

    private void SetupDefaultMockBehaviors()
    {
        // Default: .journalrc and .md-journal files exist
        _mockFileSystem
            .Setup(fs => fs.FileExists(It.Is<string>(s => s.Contains(".journalrc"))))
            .Returns(true);
        _mockFileSystem
            .Setup(fs => fs.FileExists(It.Is<string>(s => s.Contains(".md-journal"))))
            .Returns(true);

        // Default: entry markdown files don't exist yet
        _mockFileSystem
            .Setup(fs =>
                fs.FileExists(
                    It.Is<string>(s => s.EndsWith(".md") && !s.Contains("TableOfContents"))
                )
            )
            .Returns(false);

        // Default entry formatter behaviors
        _mockEntryFormatter
            .Setup(ef => ef.RemoveSpaceSeparators(It.IsAny<string>()))
            .Returns((string input) => input?.Replace("_", " ").Trim() ?? "");

        _mockEntryFormatter
            .Setup(ef => ef.AddSpaceSeparators(It.IsAny<string>()))
            .Returns((string input) => input?.Replace(" ", "_") ?? "");

        _mockEntryFormatter
            .Setup(ef => ef.AddHeadingSeparators(It.IsAny<string[]>()))
            .Returns((string[] parts) => string.Join("-", parts));

        // BuildHeadingArray is called by JournalEntryService to produce the topic path for journalrc
        _mockEntryFormatter
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
        _mockTemplateManager
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
        _mockFileSystem.Setup(fs =>
            fs.CreateMarkdownFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())
        );

        // Default: AddEntry succeeds
        _mockJournalConfiguration.Setup(jc =>
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
        _mockFileTracking.Setup(ft => ft.UpdateFileInIndex(It.IsAny<string>(), It.IsAny<string>()));

        // Default: TOC update succeeds
        _mockTocGenerator.Setup(tg =>
            tg.UpdateTableOfContents(
                It.IsAny<string>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>()
            )
        );

        _mockFileSystem
            .Setup(fs => fs.CombinePaths(It.IsAny<string[]>()))
            .Returns((string[] paths) => Path.Combine(paths));
    }

    #region Positive Cases

    [Fact]
    public void Should_Create_Entry_With_Simple_Name()
    {
        // Act
        var result = _app.Run(["add", "entry", "MyEntry", "-p", "."]);

        // Assert
        result.ExitCode.ShouldBe(0);
        _mockFileSystem.Verify(
            fs => fs.CreateMarkdownFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Once
        );
        _mockFileTracking.Verify(ft => ft.UpdateFileInIndex(".", It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void Should_Create_Entry_With_Heading()
    {
        // Act
        var result = _app.Run(["add", "entry", "MyEntry", "--he", "Tech", "-p", "."]);

        // Assert
        result.ExitCode.ShouldBe(0);
        _mockFileSystem.Verify(
            fs => fs.CreateMarkdownFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Once
        );
    }

    [Fact]
    public void Should_Create_Entry_With_Subheading()
    {
        // Arrange
        _mockEntryFormatter
            .Setup(ef => ef.SeperateSubheadingString("AI-ML"))
            .Returns(new[] { "AI", "ML" });

        // Act
        var result = _app.Run(["add", "entry", "MyEntry", "--sh", "AI-ML", "-p", "."]);

        // Assert
        result.ExitCode.ShouldBe(0);
    }

    [Fact]
    public void Should_Create_Entry_With_Heading_And_Subheading()
    {
        // Arrange
        _mockEntryFormatter
            .Setup(ef => ef.SeperateSubheadingString("AI-ML"))
            .Returns(new[] { "AI", "ML" });

        // Act
        var result = _app.Run(
            ["add", "entry", "MyEntry", "--he", "Tech", "--sh", "AI-ML", "-p", "."]
        );

        // Assert
        result.ExitCode.ShouldBe(0);
        _mockFileSystem.Verify(
            fs => fs.CreateMarkdownFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Once
        );
    }

    [Fact]
    public void Should_Use_Custom_Title_When_Provided()
    {
        // Act
        var result = _app.Run(["add", "entry", "my_file_name", "-t", "My Custom Title", "-p", "."]);

        // Assert
        // RemoveSpaceSeparators replaces underscores with spaces; "My Custom Title" has no underscores
        // so the title passed to the template is "My Custom Title"
        result.ExitCode.ShouldBe(0);
        _mockTemplateManager.Verify(
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
    public void Should_Return_Error_When_Journalrc_Not_Found()
    {
        // Arrange
        _mockFileSystem.Setup(fs => fs.FileExists("./.journalrc")).Returns(false);

        // Act
        var result = _app.Run(["add", "entry", "MyEntry", "-p", "."]);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Output.ShouldContain("Error:");
        _mockFileSystem.Verify(
            fs => fs.CreateMarkdownFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never
        );
    }

    [Fact]
    public void Should_Return_Error_When_Tracking_Index_Not_Found()
    {
        // Arrange
        _mockFileSystem.Setup(fs => fs.FileExists("./.md-journal")).Returns(false);

        // Act
        var result = _app.Run(["add", "entry", "MyEntry", "-p", "."]);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Output.ShouldContain("Error:");
        _mockFileSystem.Verify(
            fs => fs.CreateMarkdownFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never
        );
    }

    [Fact]
    public void Should_Return_Error_When_Entry_Already_Exists()
    {
        // Arrange
        _mockFileSystem
            .Setup(fs => fs.FileExists(It.Is<string>(s => s.EndsWith(It.IsAny<string>()))))
            .Returns(true);

        // Act
        var result = _app.Run(["add", "entry", "MyEntry", "-p", "."]);

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
    public void Should_Reject_Entry_Names_With_Invalid_Characters(string invalidName)
    {
        // Act
        var result = _app.Run(["add", "entry", invalidName, "-p", "."]);

        // Assert
        result.ExitCode.ShouldNotBe(0);
        result.Output.ShouldContain("character");
    }

    [Theory]
    [InlineData("Invalid/Heading")]
    [InlineData("Invalid<Heading")]
    public void Should_Reject_Headings_With_Invalid_Characters(string invalidHeading)
    {
        // Act
        var result = _app.Run(["add", "entry", "MyEntry", "--he", invalidHeading, "-p", "."]);

        // Assert
        result.ExitCode.ShouldNotBe(0);
        result.Output.ShouldContain("character");
    }

    [Theory]
    [InlineData("Invalid/Sub")]
    [InlineData("Invalid Sub")]
    [InlineData("Invalid<Sub")]
    public void Should_Reject_Subheadings_With_Invalid_Characters(string invalidSubheading)
    {
        // Act
        var result = _app.Run(["add", "entry", "MyEntry", "--sh", invalidSubheading, "-p", "."]);

        // Assert
        result.ExitCode.ShouldNotBe(0);
        result.Output.ShouldContain("character");
    }

    [Fact]
    public void Should_Handle_Exception_From_JournalConfiguration()
    {
        // Arrange
        _mockJournalConfiguration
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
        var result = _app.Run(["add", "entry", "MyEntry", "-p", "."]);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Output.ShouldContain("Error:");
        result.Output.ShouldContain("unexpected error");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Should_Handle_Entry_Name_With_Underscores()
    {
        // Act
        var result = _app.Run(["add", "entry", "my_entry_name", "-p", "."]);

        // Assert
        result.ExitCode.ShouldBe(0);
        _mockFileSystem.Verify(
            fs => fs.CreateMarkdownFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Once
        );
    }

    [Fact]
    public void Should_Handle_Entry_Name_With_Numbers()
    {
        // Act
        var result = _app.Run(["add", "entry", "Entry123", "-p", "."]);

        // Assert
        result.ExitCode.ShouldBe(0);
    }

    [Fact]
    public void Should_Handle_Heading_With_Spaces()
    {
        // Act
        var result = _app.Run(["add", "entry", "MyEntry", "--he", "Tech News", "-p", "."]);

        // Assert
        result.ExitCode.ShouldBe(0);
        _mockFileSystem.Verify(
            fs => fs.CreateMarkdownFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Once
        );
    }

    [Fact]
    public void Should_Handle_Multiple_Subheadings_In_Chain()
    {
        // Arrange
        _mockEntryFormatter
            .Setup(ef => ef.SeperateSubheadingString("Level1-Level2-Level3-Level4"))
            .Returns(new[] { "Level1", "Level2", "Level3", "Level4" });

        // Act
        var result = _app.Run(
            ["add", "entry", "MyEntry", "--sh", "Level1-Level2-Level3-Level4", "-p", "."]
        );

        // Assert
        result.ExitCode.ShouldBe(0);
    }

    [Fact]
    public void Should_Handle_Subheading_Only_Without_Heading()
    {
        // Arrange
        _mockEntryFormatter
            .Setup(ef => ef.SeperateSubheadingString("SubOnly"))
            .Returns(new[] { "SubOnly" });

        // Act
        var result = _app.Run(["add", "entry", "MyEntry", "--sh", "SubOnly", "-p", "."]);

        // Assert
        result.ExitCode.ShouldBe(0);
    }

    [Fact]
    public void Should_Handle_Very_Long_Entry_Name()
    {
        // Arrange
        var longName = new string('A', 200);

        // Act
        var result = _app.Run(["add", "entry", longName, "-p", "."]);

        // Assert
        result.ExitCode.ShouldBe(0);
        _mockFileSystem.Verify(
            fs => fs.CreateMarkdownFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Once
        );
    }

    [Fact]
    public void Should_Handle_Custom_Path()
    {
        // Act - The path parameter is passed through command settings
        var result = _app.Run(["add", "entry", "MyEntry", "-p", "."]);

        // Assert
        result.ExitCode.ShouldBe(0);
        _mockFileSystem.Verify(
            fs => fs.CreateMarkdownFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Once
        );
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void Should_Update_Table_Of_Contents_After_Entry_Creation()
    {
        // Act
        var result = _app.Run(["add", "entry", "MyEntry", "-p", "."]);

        // Assert
        result.ExitCode.ShouldBe(0);
        _mockTocGenerator.Verify(
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
    public void Should_Not_Update_Configuration_If_File_Creation_Fails()
    {
        // Arrange
        _mockFileSystem
            .Setup(fs =>
                fs.CreateMarkdownFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())
            )
            .Throws(new IOException("Disk full"));

        // Act
        var result = _app.Run(["add", "entry", "MyEntry", "-p", "."]);

        // Assert
        result.ExitCode.ShouldBe(1);
        _mockFileTracking.Verify(
            ft => ft.UpdateFileInIndex(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never
        );
    }

    #endregion

    #region Ignore File Tests

    [Fact]
    public void Should_Not_Update_TableOfContents_When_IgnoreFile_Is_True()
    {
        // Act
        var result = _app.Run(["add", "entry", "MyEntry", "--ignore", "-p", "."]);

        // Assert
        result.ExitCode.ShouldBe(0);
        // File should still be created
        _mockFileSystem.Verify(
            fs => fs.CreateMarkdownFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Once
        );
        // File should still be tracked
        _mockFileTracking.Verify(
            ft => ft.UpdateFileInIndex(It.IsAny<string>(), It.IsAny<string>()),
            Times.Once
        );
        // But TOC should NOT be updated
        _mockTocGenerator.Verify(
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
    public void Should_Add_Entry_To_Configuration_With_IgnoreFile_Flag()
    {
        // Act
        var result = _app.Run(["add", "entry", "MyEntry", "--ignore", "-p", "."]);

        // Assert
        result.ExitCode.ShouldBe(0);
        // Should call AddEntry with ignoreFile = true
        _mockJournalConfiguration.Verify(
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
    public void Should_Add_Entry_To_Configuration_Without_IgnoreFile_Flag()
    {
        // Act
        var result = _app.Run(["add", "entry", "MyEntry", "-p", "."]);

        // Assert
        result.ExitCode.ShouldBe(0);
        // Should call AddEntry with ignoreFile = false (default)
        _mockJournalConfiguration.Verify(
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
    public void Should_Update_TableOfContents_When_IgnoreFile_Is_False()
    {
        // Act
        var result = _app.Run(["add", "entry", "MyEntry", "-p", "."]);

        // Assert
        result.ExitCode.ShouldBe(0);
        // TOC should be updated when ignore flag is not set
        _mockTocGenerator.Verify(
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
    public void Should_Handle_IgnoreFile_With_Heading_And_Subheading()
    {
        // Arrange
        _mockEntryFormatter
            .Setup(ef => ef.SeperateSubheadingString("AI-ML"))
            .Returns(new[] { "AI", "ML" });

        // Act
        var result = _app.Run(
            ["add", "entry", "MyEntry", "--he", "Tech", "--sh", "AI-ML", "--ignore", "-p", "."]
        );

        // Assert
        result.ExitCode.ShouldBe(0);
        // File should be created
        _mockFileSystem.Verify(
            fs => fs.CreateMarkdownFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Once
        );
        // Should be added to configuration with ignoreFile = true
        _mockJournalConfiguration.Verify(
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
        _mockTocGenerator.Verify(
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
    public void Should_Handle_IgnoreFile_With_Custom_Title()
    {
        // Act
        var result = _app.Run(
            ["add", "entry", "my_file_name", "-t", "My Custom Title", "--ignore", "-p", "."]
        );

        // Assert
        result.ExitCode.ShouldBe(0);
        // Should use custom title even with ignore flag
        _mockTemplateManager.Verify(
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
        _mockTocGenerator.Verify(
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
    public void Should_Still_Track_File_When_IgnoreFile_Is_True()
    {
        // Act
        var result = _app.Run(["add", "entry", "MyEntry", "--ignore", "-p", "."]);

        // Assert
        result.ExitCode.ShouldBe(0);
        // File tracking should still occur even when ignoring in TOC
        _mockFileTracking.Verify(
            ft => ft.UpdateFileInIndex(It.IsAny<string>(), It.IsAny<string>()),
            Times.Once
        );
    }

    #endregion
}
