using markdown_journal_cli.Infrastructure.Configuration.Models;
using markdown_journal_cli.JournalTemplates;
using markdown_journal_cli.Tests.Infrastructure;
using Microsoft.Extensions.Options;
using Xunit;

namespace markdown_journal_cli.Tests.JournalTemplates;

public class TableOfContentsGeneratorTests
{
    private readonly TestFileSystem _fileSystem;
    private readonly TestJournalConfiguration _journalConfiguration;
    private readonly IOptions<JournalSettings> _journalSettings;
    private readonly TableOfContentsGenerator _generator;

    public TableOfContentsGeneratorTests()
    {
        _fileSystem = new TestFileSystem();
        _journalConfiguration = new TestJournalConfiguration();
        _journalSettings = Options.Create(
            new JournalSettings
            {
                AppName = "md-journal",
                JournalConfigFileName = ".journalrc",
                TableOfContentsFileName = "1a-TableOfContents",
                TableOfContentsTitle = "Table of Contents",
            }
        );
        _generator = new TableOfContentsGenerator(
            _fileSystem,
            _journalConfiguration,
            _journalSettings
        );
    }

    [Fact]
    public void UpdateTableOfContents_WithRootEntriesOnly_GeneratesCorrectFormat()
    {
        // Arrange
        var journalDir = "/test/journal";
        _fileSystem.CreateDirectory(journalDir);

        var config = new JournalConfig
        {
            JournalName = "TestJournal",
            TableOfContents = new TableOfContents
            {
                RootEntries =
                [
                    new() { Name = "Introduction", File = "1b-Intro.md" },
                    new() { Name = "Template", File = "1c-Template.md" },
                ],
                Structure = new Structure { Topics = [] },
            },
        };
        _journalConfiguration.Create(journalDir, config);

        // Act
        _generator.UpdateTableOfContents(journalDir);

        // Assert
        var tocPath = $"{journalDir}/1a-TableOfContents.md";
        var content = _fileSystem.GetFileContent(tocPath);

        Assert.Contains("# Table of Contents", content);
        Assert.Contains("- [Introduction](1b-Intro.md)", content);
        Assert.Contains("- [Template](1c-Template.md)", content);
    }

    [Fact]
    public void UpdateTableOfContents_WithDates_IncludesDateHeaders()
    {
        // Arrange
        var journalDir = "/test/journal";
        _fileSystem.CreateDirectory(journalDir);

        var config = new JournalConfig
        {
            JournalName = "TestJournal",
            TableOfContents = new TableOfContents
            {
                RootEntries = [new() { Name = "Intro", File = "intro.md" }],
                Structure = new Structure { Topics = [] },
            },
        };
        _journalConfiguration.Create(journalDir, config);

        var createdDate = new DateTime(2025, 3, 23);
        var editedDate = new DateTime(2026, 1, 4);

        // Act
        _generator.UpdateTableOfContents(journalDir, createdDate, editedDate);

        // Assert
        var content = _fileSystem.GetFileContent($"{journalDir}/1a-TableOfContents.md");

        Assert.Contains("Created: 3/23/2025", content);
        Assert.Contains("Last Edited: 01/04/2026", content);
    }

    [Fact]
    public void UpdateTableOfContents_WithTopicAndEntries_GeneratesCorrectFormat()
    {
        // Arrange
        var journalDir = "/test/journal";
        _fileSystem.CreateDirectory(journalDir);

        var config = new JournalConfig
        {
            JournalName = "TestJournal",
            TableOfContents = new TableOfContents
            {
                RootEntries = [],
                Structure = new Structure
                {
                    Topics =
                    [
                        new Topic
                        {
                            Name = "Bonsai",
                            Entries =
                            [
                                new() { Name = "Care Guide", File = "Bonsai-Care-Guide.md" },
                                new() { Name = "Species List", File = "Bonsai-Species.md" },
                            ],
                            Subtopics = null,
                        },
                    ],
                },
            },
        };
        _journalConfiguration.Create(journalDir, config);

        // Act
        _generator.UpdateTableOfContents(journalDir);

        // Assert
        var content = _fileSystem.GetFileContent($"{journalDir}/1a-TableOfContents.md");

        Assert.Contains("## Bonsai", content);
        Assert.Contains("  - [Care Guide](Bonsai-Care-Guide.md)", content);
        Assert.Contains("  - [Species List](Bonsai-Species.md)", content);
    }

    [Fact]
    public void UpdateTableOfContents_WithSubtopics_GeneratesCorrectIndentation()
    {
        // Arrange
        var journalDir = "/test/journal";
        _fileSystem.CreateDirectory(journalDir);

        var config = new JournalConfig
        {
            JournalName = "TestJournal",
            TableOfContents = new TableOfContents
            {
                RootEntries = [],
                Structure = new Structure
                {
                    Topics =
                    [
                        new Topic
                        {
                            Name = "Bonsai",
                            Entries = [],
                            Subtopics =
                            [
                                new Topic
                                {
                                    Name = "Articles",
                                    Entries =
                                    [
                                        new()
                                        {
                                            Name = "Discussion on Deciduous",
                                            File = "Bonsai-Articles-Deciduous.md",
                                        },
                                        new()
                                        {
                                            Name = "Branching Fundamentals",
                                            File = "Bonsai-Articles-Branching.md",
                                        },
                                    ],
                                    Subtopics = null,
                                },
                            ],
                        },
                    ],
                },
            },
        };
        _journalConfiguration.Create(journalDir, config);

        // Act
        _generator.UpdateTableOfContents(journalDir);

        // Assert
        var content = _fileSystem.GetFileContent($"{journalDir}/1a-TableOfContents.md");

        Assert.Contains("## Bonsai", content);
        Assert.Contains("  - Articles", content);
        Assert.Contains("    - [Discussion on Deciduous](Bonsai-Articles-Deciduous.md)", content);
        Assert.Contains("    - [Branching Fundamentals](Bonsai-Articles-Branching.md)", content);
    }

    [Fact]
    public void UpdateTableOfContents_WithDeeplyNestedSubtopics_GeneratesCorrectIndentation()
    {
        // Arrange
        var journalDir = "/test/journal";
        _fileSystem.CreateDirectory(journalDir);

        var config = new JournalConfig
        {
            JournalName = "TestJournal",
            TableOfContents = new TableOfContents
            {
                RootEntries = [],
                Structure = new Structure
                {
                    Topics =
                    [
                        new Topic
                        {
                            Name = "Succulents",
                            Entries = [],
                            Subtopics =
                            [
                                new Topic
                                {
                                    Name = "Species",
                                    Entries = [],
                                    Subtopics =
                                    [
                                        new Topic
                                        {
                                            Name = "Aloe Vera",
                                            Entries =
                                            [
                                                new()
                                                {
                                                    Name = "Care Guide",
                                                    File = "Succulent-Aloe-Vera-Care-Guide.md",
                                                },
                                            ],
                                            Subtopics = null,
                                        },
                                        new Topic
                                        {
                                            Name = "Crassula Ovata",
                                            Entries = [],
                                            Subtopics =
                                            [
                                                new Topic
                                                {
                                                    Name = "Ogre Ears",
                                                    Entries = [],
                                                    Subtopics = null,
                                                },
                                            ],
                                        },
                                    ],
                                },
                            ],
                        },
                    ],
                },
            },
        };
        _journalConfiguration.Create(journalDir, config);

        // Act
        _generator.UpdateTableOfContents(journalDir);

        // Assert
        var content = _fileSystem.GetFileContent($"{journalDir}/1a-TableOfContents.md");

        Assert.Contains("## Succulents", content);
        Assert.Contains("  - Species", content);
        Assert.Contains("    - Aloe Vera", content);
        Assert.Contains("      - [Care Guide](Succulent-Aloe-Vera-Care-Guide.md)", content);
        Assert.Contains("    - Crassula Ovata", content);
        Assert.Contains("      - Ogre Ears", content);
    }

    [Fact]
    public void UpdateTableOfContents_WithTopicMatchingEntryName_CreatesLinkedHeading()
    {
        // Arrange
        var journalDir = "/test/journal";
        _fileSystem.CreateDirectory(journalDir);

        var config = new JournalConfig
        {
            JournalName = "TestJournal",
            TableOfContents = new TableOfContents
            {
                RootEntries = [],
                Structure = new Structure
                {
                    Topics =
                    [
                        new Topic
                        {
                            Name = "APIs",
                            Entries = [new() { Name = "APIs", File = "APIs.md" }],
                            Subtopics = null,
                        },
                    ],
                },
            },
        };
        _journalConfiguration.Create(journalDir, config);

        // Act
        _generator.UpdateTableOfContents(journalDir);

        // Assert
        var content = _fileSystem.GetFileContent($"{journalDir}/1a-TableOfContents.md");

        Assert.Contains("## [APIs](APIs.md)", content);
        // Should not have a separate entry line
        Assert.DoesNotContain("  - [APIs](APIs.md)", content);
    }

    [Fact]
    public void UpdateTableOfContents_WithMultipleTopicsAndSubtopics_GeneratesCompleteStructure()
    {
        // Arrange
        var journalDir = "/test/journal";
        _fileSystem.CreateDirectory(journalDir);

        var config = new JournalConfig
        {
            JournalName = "TestJournal",
            TableOfContents = new TableOfContents
            {
                RootEntries =
                [
                    new() { Name = "Introduction", File = "1b-Intro.md" },
                    new() { Name = "All My Journals", File = "1h-All-My-Journals.md" },
                ],
                Structure = new Structure
                {
                    Topics =
                    [
                        new Topic
                        {
                            Name = "AI",
                            Entries =
                            [
                                new() { Name = "AI Protocols", File = "AI-Protocols.md" },
                                new() { Name = "AI Resources", File = "AI-Resources.md" },
                            ],
                            Subtopics = null,
                        },
                        new Topic
                        {
                            Name = "Cloud Computing",
                            Entries = [],
                            Subtopics =
                            [
                                new Topic
                                {
                                    Name = "Azure",
                                    Entries =
                                    [
                                        new() { Name = "AZ-900 Notes", File = "Azure-AZ900.md" },
                                    ],
                                    Subtopics = null,
                                },
                            ],
                        },
                    ],
                },
            },
        };
        _journalConfiguration.Create(journalDir, config);

        // Act
        _generator.UpdateTableOfContents(journalDir);

        // Assert
        var content = _fileSystem.GetFileContent($"{journalDir}/1a-TableOfContents.md");

        // Root entries
        Assert.Contains("- [Introduction](1b-Intro.md)", content);
        Assert.Contains("- [All My Journals](1h-All-My-Journals.md)", content);

        // AI topic
        Assert.Contains("## AI", content);
        Assert.Contains("  - [AI Protocols](AI-Protocols.md)", content);
        Assert.Contains("  - [AI Resources](AI-Resources.md)", content);

        // Cloud Computing with subtopic
        Assert.Contains("## Cloud Computing", content);
        Assert.Contains("  - Azure", content);
        Assert.Contains("    - [AZ-900 Notes](Azure-AZ900.md)", content);
    }

    [Fact]
    public void UpdateTableOfContents_WithNullDirectory_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _generator.UpdateTableOfContents(null!));
    }

    [Fact]
    public void UpdateTableOfContents_WithEmptyDirectory_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _generator.UpdateTableOfContents(""));
    }

    [Fact]
    public void UpdateTableOfContents_WithWhitespaceDirectory_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _generator.UpdateTableOfContents("   "));
    }

    [Fact]
    public void UpdateTableOfContents_WithNoConfiguration_ThrowsException()
    {
        // Arrange
        var journalDir = "/test/nonexistent";

        // Act & Assert
        // TestJournalConfiguration throws FileNotFoundException when config doesn't exist
        Assert.Throws<FileNotFoundException>(
            () => _generator.UpdateTableOfContents(journalDir)
        );
    }
}
