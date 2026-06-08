using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.Configuration.Models;
using markdown_journal_cli.Services;
using markdown_journal_cli.Tests.Infrastructure.FileSystem;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace markdown_journal_cli.Tests.Infrastructure.JournalTemplates;

public class TableOfContentsGeneratorTests
{
    private readonly TestFileSystem _fileSystem;
    private readonly TestJournalConfiguration _journalConfiguration;
    private readonly IOptions<JournalSettings> _journalSettings;
    private readonly TableOfContentsService _generator;
    private readonly Mock<IJournalTocStructureRepository> _mockTocStructureRepository;

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
        _mockTocStructureRepository = new Mock<IJournalTocStructureRepository>();
        _mockTocStructureRepository
            .Setup(r => r.Load(It.IsAny<string>()))
            .Returns(JournalTocStructure.Empty());
        _generator = new TableOfContentsService(
            _fileSystem,
            _journalConfiguration,
            _journalSettings,
            NullLogger<TableOfContentsService>.Instance,
            _mockTocStructureRepository.Object
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
            TableOfContents = new TableOfContents { },
        };
        _journalConfiguration.Create(journalDir, config);

        // Act
        _mockTocStructureRepository
            .Setup(r => r.Load(It.IsAny<string>()))
            .Returns(
                new JournalTocStructure
                {
                    Structure = new Structure { Topics = [] },
                    RootEntries =
                    [
                        new() { Name = "Introduction", File = "1b-Intro.md" },
                        new() { Name = "Template", File = "1c-Template.md" },
                    ],
                }
            );
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
            TableOfContents = new TableOfContents { },
        };
        _journalConfiguration.Create(journalDir, config);

        var createdDate = new DateTime(2025, 3, 23);
        var editedDate = new DateTime(2026, 1, 4);

        // Act
        _mockTocStructureRepository
            .Setup(r => r.Load(It.IsAny<string>()))
            .Returns(
                new JournalTocStructure
                {
                    Structure = new Structure { Topics = [] },
                    RootEntries = [new() { Name = "Intro", File = "intro.md" }],
                }
            );
        _generator.UpdateTableOfContents(journalDir, createdDate, editedDate);

        // Assert
        var content = _fileSystem.GetFileContent($"{journalDir}/1a-TableOfContents.md");

        Assert.Contains("Created: 03/23/2025", content);
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
            TableOfContents = new TableOfContents { },
        };
        _journalConfiguration.Create(journalDir, config);

        // Act
        _mockTocStructureRepository
            .Setup(r => r.Load(It.IsAny<string>()))
            .Returns(
                new JournalTocStructure
                {
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
                    RootEntries = [],
                }
            );
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
            TableOfContents = new TableOfContents { },
        };
        _journalConfiguration.Create(journalDir, config);

        // Act
        _mockTocStructureRepository
            .Setup(r => r.Load(It.IsAny<string>()))
            .Returns(
                new JournalTocStructure
                {
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
                    RootEntries = [],
                }
            );
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
            TableOfContents = new TableOfContents { },
        };
        _journalConfiguration.Create(journalDir, config);

        // Act
        _mockTocStructureRepository
            .Setup(r => r.Load(It.IsAny<string>()))
            .Returns(
                new JournalTocStructure
                {
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
                    RootEntries = [],
                }
            );
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
            TableOfContents = new TableOfContents { },
        };
        _journalConfiguration.Create(journalDir, config);

        // Act
        _mockTocStructureRepository
            .Setup(r => r.Load(It.IsAny<string>()))
            .Returns(
                new JournalTocStructure
                {
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
                    RootEntries = [],
                }
            );
        _generator.UpdateTableOfContents(journalDir);

        // Assert
        var content = _fileSystem.GetFileContent($"{journalDir}/1a-TableOfContents.md");

        // Topic name preserves casing (APIs stays APIs)
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
            TableOfContents = new TableOfContents { },
        };
        _journalConfiguration.Create(journalDir, config);

        // Act
        _mockTocStructureRepository
            .Setup(r => r.Load(It.IsAny<string>()))
            .Returns(
                new JournalTocStructure
                {
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
                                            new()
                                            {
                                                Name = "AZ-900 Notes",
                                                File = "Azure-AZ900.md",
                                            },
                                        ],
                                        Subtopics = null,
                                    },
                                ],
                            },
                        ],
                    },
                    RootEntries =
                    [
                        new() { Name = "Introduction", File = "1b-Intro.md" },
                        new() { Name = "All My Journals", File = "1h-All-My-Journals.md" },
                    ],
                }
            );
        _generator.UpdateTableOfContents(journalDir);

        // Assert
        var content = _fileSystem.GetFileContent($"{journalDir}/1a-TableOfContents.md");

        // Root entries
        Assert.Contains("- [Introduction](1b-Intro.md)", content);
        Assert.Contains("- [All My Journals](1h-All-My-Journals.md)", content);

        // AI topic (preserves casing as 'AI')
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
        Assert.Throws<FileNotFoundException>(() => _generator.UpdateTableOfContents(journalDir));
    }

    [Fact]
    public void UpdateTableOfContents_WithLowercaseTopicName_ConvertsToTitleCase()
    {
        // Arrange
        var journalDir = "/test/journal";
        _fileSystem.CreateDirectory(journalDir);

        var config = new JournalConfig
        {
            JournalName = "TestJournal",
            TableOfContents = new TableOfContents { },
        };
        _journalConfiguration.Create(journalDir, config);

        // Act
        _mockTocStructureRepository
            .Setup(r => r.Load(It.IsAny<string>()))
            .Returns(
                new JournalTocStructure
                {
                    Structure = new Structure
                    {
                        Topics =
                        [
                            new Topic
                            {
                                Name = "cloud computing",
                                Entries = [new() { Name = "Azure Notes", File = "cloud-azure.md" }],
                                Subtopics = null,
                            },
                        ],
                    },
                    RootEntries = [],
                }
            );
        _generator.UpdateTableOfContents(journalDir);

        // Assert
        var content = _fileSystem.GetFileContent($"{journalDir}/1a-TableOfContents.md");

        Assert.Contains("## Cloud Computing", content);
        Assert.DoesNotContain("## cloud computing", content);
    }

    [Fact]
    public void UpdateTableOfContents_WithUppercaseTopicName_ConvertsToTitleCase()
    {
        // Arrange
        var journalDir = "/test/journal";
        _fileSystem.CreateDirectory(journalDir);

        var config = new JournalConfig
        {
            JournalName = "TestJournal",
            TableOfContents = new TableOfContents { },
        };
        _journalConfiguration.Create(journalDir, config);

        // Act
        _mockTocStructureRepository
            .Setup(r => r.Load(It.IsAny<string>()))
            .Returns(
                new JournalTocStructure
                {
                    Structure = new Structure
                    {
                        Topics =
                        [
                            new Topic
                            {
                                Name = "ARTIFICIAL INTELLIGENCE",
                                Entries =
                                [
                                    new() { Name = "AI Resources", File = "ai-resources.md" },
                                ],
                                Subtopics = null,
                            },
                        ],
                    },
                    RootEntries = [],
                }
            );
        _generator.UpdateTableOfContents(journalDir);

        // Assert
        var content = _fileSystem.GetFileContent($"{journalDir}/1a-TableOfContents.md");

        // Each word starts with capital, rest preserved
        Assert.Contains("## ARTIFICIAL INTELLIGENCE", content);
    }

    [Fact]
    public void UpdateTableOfContents_WithMixedCaseTopicName_ConvertsToTitleCase()
    {
        // Arrange
        var journalDir = "/test/journal";
        _fileSystem.CreateDirectory(journalDir);

        var config = new JournalConfig
        {
            JournalName = "TestJournal",
            TableOfContents = new TableOfContents { },
        };
        _journalConfiguration.Create(journalDir, config);

        // Act
        _mockTocStructureRepository
            .Setup(r => r.Load(It.IsAny<string>()))
            .Returns(
                new JournalTocStructure
                {
                    Structure = new Structure
                    {
                        Topics =
                        [
                            new Topic
                            {
                                Name = "mAchIne LeArNinG",
                                Entries = [new() { Name = "ML Guide", File = "ml-guide.md" }],
                                Subtopics = null,
                            },
                        ],
                    },
                    RootEntries = [],
                }
            );
        _generator.UpdateTableOfContents(journalDir);

        // Assert
        var content = _fileSystem.GetFileContent($"{journalDir}/1a-TableOfContents.md");

        // Only first letter of each word capitalized, rest preserved
        Assert.Contains("## MAchIne LeArNinG", content);
        Assert.DoesNotContain("## mAchIne LeArNinG", content);
    }

    [Fact]
    public void UpdateTableOfContents_WithLinkedHeading_AppliesTitleCase()
    {
        // Arrange
        var journalDir = "/test/journal";
        _fileSystem.CreateDirectory(journalDir);

        var config = new JournalConfig
        {
            JournalName = "TestJournal",
            TableOfContents = new TableOfContents { },
        };
        _journalConfiguration.Create(journalDir, config);

        // Act
        _mockTocStructureRepository
            .Setup(r => r.Load(It.IsAny<string>()))
            .Returns(
                new JournalTocStructure
                {
                    Structure = new Structure
                    {
                        Topics =
                        [
                            new Topic
                            {
                                Name = "web development",
                                Entries = [new() { Name = "web development", File = "web-dev.md" }],
                                Subtopics = null,
                            },
                        ],
                    },
                    RootEntries = [],
                }
            );
        _generator.UpdateTableOfContents(journalDir);

        // Assert
        var content = _fileSystem.GetFileContent($"{journalDir}/1a-TableOfContents.md");

        // Only first letter capitalized, rest preserved
        Assert.Contains("## [Web Development](web-dev.md)", content);
    }

    [Fact]
    public void UpdateTableOfContents_SubtopicsAlsoTitleCased_WhenFlagIsTrue()
    {
        // Arrange
        var journalDir = "/test/journal";
        _fileSystem.CreateDirectory(journalDir);

        var config = new JournalConfig
        {
            JournalName = "TestJournal",
            TableOfContents = new TableOfContents { },
        };
        _journalConfiguration.Create(journalDir, config);

        // Act
        _mockTocStructureRepository
            .Setup(r => r.Load(It.IsAny<string>()))
            .Returns(
                new JournalTocStructure
                {
                    Structure = new Structure
                    {
                        Topics =
                        [
                            new Topic
                            {
                                Name = "programming languages",
                                Entries = [],
                                Subtopics =
                                [
                                    new Topic
                                    {
                                        Name = "rust language",
                                        Entries = [new() { Name = "Rust Guide", File = "rust.md" }],
                                        Subtopics = null,
                                    },
                                ],
                            },
                        ],
                    },
                    RootEntries = [],
                }
            );
        _generator.UpdateTableOfContents(journalDir);

        // Assert
        var content = _fileSystem.GetFileContent($"{journalDir}/1a-TableOfContents.md");

        // Top-level topic should be title-cased
        Assert.Contains("## Programming Languages", content);
        // Subtopic should also be title-cased when flag is true
        Assert.Contains("  - Rust Language", content);
        Assert.DoesNotContain("  - rust language", content);
    }

    [Fact]
    public void UpdateTableOfContents_WithCapitalizationDisabled_LeavesAllTopicsAsIs()
    {
        // Arrange
        var journalDir = "/test/journal";
        _fileSystem.CreateDirectory(journalDir);

        var settingsWithoutCaps = Options.Create(
            new JournalSettings
            {
                AppName = "md-journal",
                JournalConfigFileName = ".journalrc",
                TableOfContentsFileName = "1a-TableOfContents",
                TableOfContentsTitle = "Table of Contents",
                CapitalizeTopicHeadings = false, // Disable capitalization
            }
        );

        var generatorWithoutCaps = new TableOfContentsService(
            _fileSystem,
            _journalConfiguration,
            settingsWithoutCaps,
            NullLogger<TableOfContentsService>.Instance,
            _mockTocStructureRepository.Object
        );

        var config = new JournalConfig
        {
            JournalName = "TestJournal",
            TableOfContents = new TableOfContents { },
        };
        _journalConfiguration.Create(journalDir, config);

        // Act
        _mockTocStructureRepository
            .Setup(r => r.Load(It.IsAny<string>()))
            .Returns(
                new JournalTocStructure
                {
                    Structure = new Structure
                    {
                        Topics =
                        [
                            new Topic
                            {
                                Name = "cloud computing",
                                Entries = [],
                                Subtopics =
                                [
                                    new Topic
                                    {
                                        Name = "azure services",
                                        Entries =
                                        [
                                            new() { Name = "Azure Notes", File = "cloud-azure.md" },
                                        ],
                                        Subtopics = null,
                                    },
                                ],
                            },
                        ],
                    },
                    RootEntries = [],
                }
            );
        generatorWithoutCaps.UpdateTableOfContents(journalDir);

        // Assert
        var content = _fileSystem.GetFileContent($"{journalDir}/1a-TableOfContents.md");

        // Topic and subtopic should remain lowercase since capitalization is disabled
        Assert.Contains("## cloud computing", content);
        Assert.DoesNotContain("## Cloud Computing", content);
        Assert.Contains("  - azure services", content);
        Assert.DoesNotContain("  - Azure Services", content);
    }

    [Fact]
    public void UpdateTableOfContents_WithCapitalizationEnabled_CapitalizesTopics()
    {
        // Arrange
        var journalDir = "/test/journal";
        _fileSystem.CreateDirectory(journalDir);

        var settingsWithCaps = Options.Create(
            new JournalSettings
            {
                AppName = "md-journal",
                JournalConfigFileName = ".journalrc",
                TableOfContentsFileName = "1a-TableOfContents",
                TableOfContentsTitle = "Table of Contents",
                CapitalizeTopicHeadings = true, // Enable capitalization
            }
        );

        var generatorWithCaps = new TableOfContentsService(
            _fileSystem,
            _journalConfiguration,
            settingsWithCaps,
            NullLogger<TableOfContentsService>.Instance,
            _mockTocStructureRepository.Object
        );

        var config = new JournalConfig
        {
            JournalName = "TestJournal",
            TableOfContents = new TableOfContents { },
        };
        _journalConfiguration.Create(journalDir, config);

        // Act
        _mockTocStructureRepository
            .Setup(r => r.Load(It.IsAny<string>()))
            .Returns(
                new JournalTocStructure
                {
                    Structure = new Structure
                    {
                        Topics =
                        [
                            new Topic
                            {
                                Name = "machine learning",
                                Entries = [new() { Name = "ML Guide", File = "ml-guide.md" }],
                                Subtopics = null,
                            },
                        ],
                    },
                    RootEntries = [],
                }
            );
        generatorWithCaps.UpdateTableOfContents(journalDir);

        // Assert
        var content = _fileSystem.GetFileContent($"{journalDir}/1a-TableOfContents.md");

        // Topic should be capitalized
        Assert.Contains("## Machine Learning", content);
        Assert.DoesNotContain("## machine learning", content);
    }

    [Fact]
    public void UpdateTableOfContents_PreservesExistingCreatedDate_WhenNotProvided()
    {
        // Arrange
        var journalDir = "/test/journal";
        _fileSystem.CreateDirectory(journalDir);

        var config = new JournalConfig
        {
            JournalName = "TestJournal",
            TableOfContents = new TableOfContents { },
        };
        _journalConfiguration.Create(journalDir, config);

        var originalCreated = new DateTime(2024, 1, 1);

        // Create TOC with original created date
        _mockTocStructureRepository
            .Setup(r => r.Load(It.IsAny<string>()))
            .Returns(
                new JournalTocStructure
                {
                    Structure = new Structure { Topics = [] },
                    RootEntries = [new() { Name = "Entry", File = "entry.md" }],
                }
            );
        _generator.UpdateTableOfContents(journalDir, createdDate: originalCreated);

        // Act - Update without providing created date
        var newLastEdited = new DateTime(2024, 2, 15);
        _generator.UpdateTableOfContents(journalDir, lastEditedDate: newLastEdited);

        // Assert
        var content = _fileSystem.GetFileContent($"{journalDir}/1a-TableOfContents.md");
        Assert.Contains("Created: 01/01/2024", content);
        Assert.Contains("Last Edited: 02/15/2024", content);
    }

    [Fact]
    public void UpdateTableOfContents_PreservesExistingLastEditedDate_WhenNotProvided()
    {
        // Arrange
        var journalDir = "/test/journal";
        _fileSystem.CreateDirectory(journalDir);

        var config = new JournalConfig
        {
            JournalName = "TestJournal",
            TableOfContents = new TableOfContents { },
        };
        _journalConfiguration.Create(journalDir, config);

        var originalLastEdited = new DateTime(2024, 1, 15);

        // Create TOC with original last edited date
        _mockTocStructureRepository
            .Setup(r => r.Load(It.IsAny<string>()))
            .Returns(
                new JournalTocStructure
                {
                    Structure = new Structure { Topics = [] },
                    RootEntries = [new() { Name = "Entry", File = "entry.md" }],
                }
            );
        _generator.UpdateTableOfContents(journalDir, lastEditedDate: originalLastEdited);

        // Act - Update without providing last edited date (simulating read-only operations)
        _generator.UpdateTableOfContents(journalDir);

        // Assert
        var content = _fileSystem.GetFileContent($"{journalDir}/1a-TableOfContents.md");
        Assert.Contains("Last Edited: 01/15/2024", content);
    }

    [Fact]
    public void UpdateTableOfContents_OverridesExistingDates_WhenNewDatesProvided()
    {
        // Arrange
        var journalDir = "/test/journal";
        _fileSystem.CreateDirectory(journalDir);

        var config = new JournalConfig
        {
            JournalName = "TestJournal",
            TableOfContents = new TableOfContents { },
        };
        _journalConfiguration.Create(journalDir, config);

        var originalCreated = new DateTime(2024, 1, 1);
        var originalEdited = new DateTime(2024, 1, 15);

        // Create TOC with original dates
        _mockTocStructureRepository
            .Setup(r => r.Load(It.IsAny<string>()))
            .Returns(
                new JournalTocStructure
                {
                    Structure = new Structure { Topics = [] },
                    RootEntries = [new() { Name = "Entry", File = "entry.md" }],
                }
            );
        _generator.UpdateTableOfContents(journalDir, originalCreated, originalEdited);

        // Act - Update with new dates (both should be overridden)
        var newCreated = new DateTime(2024, 3, 1);
        var newEdited = new DateTime(2024, 3, 15);
        _generator.UpdateTableOfContents(journalDir, newCreated, newEdited);

        // Assert
        var content = _fileSystem.GetFileContent($"{journalDir}/1a-TableOfContents.md");
        Assert.Contains("Created: 03/01/2024", content);
        Assert.Contains("Last Edited: 03/15/2024", content);
        Assert.DoesNotContain("Created: 01/01/2024", content);
        Assert.DoesNotContain("Last Edited: 01/15/2024", content);
    }

    [Fact]
    public void UpdateTableOfContents_HandlesNoExistingDates_WhenNoneProvided()
    {
        // Arrange
        var journalDir = "/test/journal";
        _fileSystem.CreateDirectory(journalDir);

        var config = new JournalConfig
        {
            JournalName = "TestJournal",
            TableOfContents = new TableOfContents { },
        };
        _journalConfiguration.Create(journalDir, config);

        // Act - Create TOC without any dates
        _mockTocStructureRepository
            .Setup(r => r.Load(It.IsAny<string>()))
            .Returns(
                new JournalTocStructure
                {
                    Structure = new Structure { Topics = [] },
                    RootEntries = [new() { Name = "Entry", File = "entry.md" }],
                }
            );
        _generator.UpdateTableOfContents(journalDir);

        // Assert
        var content = _fileSystem.GetFileContent($"{journalDir}/1a-TableOfContents.md");
        Assert.DoesNotContain("Created:", content);
        Assert.DoesNotContain("Last Edited:", content);
    }

    #region Parent-Child Detection Tests

    [Fact]
    public void UpdateTableOfContents_DetectsParentChildRelationship_WhenEntryMatchesSubtopic()
    {
        // Arrange
        var journalDir = "/test/journal";
        _fileSystem.CreateDirectory(journalDir);

        var config = new JournalConfig
        {
            JournalName = "TestJournal",
            TableOfContents = new TableOfContents { },
        };
        _journalConfiguration.Create(journalDir, config);

        // Act
        _mockTocStructureRepository
            .Setup(r => r.Load(It.IsAny<string>()))
            .Returns(
                new JournalTocStructure
                {
                    Structure = new Structure
                    {
                        Topics =
                        [
                            new()
                            {
                                Name = "Test Topic",
                                Entries =
                                [
                                    new()
                                    {
                                        Name = "test file 5",
                                        File = "abc-test_2-test_file_5.md",
                                    },
                                ],
                                Subtopics =
                                [
                                    new()
                                    {
                                        Name = "test file 5",
                                        Entries =
                                        [
                                            new()
                                            {
                                                Name = "test file 7",
                                                File = "abc-test_2-test_file_5-test_file_7.md",
                                            },
                                        ],
                                        Subtopics = null,
                                    },
                                ],
                            },
                        ],
                    },
                    RootEntries = [],
                }
            );
        _generator.UpdateTableOfContents(journalDir);

        // Assert
        var content = _fileSystem.GetFileContent($"{journalDir}/1a-TableOfContents.md");

        // Should have parent entry as a link
        Assert.Contains("- [test file 5](abc-test_2-test_file_5.md)", content);
        // Should have child nested under parent (with more indentation)
        Assert.Contains("    - [test file 7](abc-test_2-test_file_5-test_file_7.md)", content);
        // Should NOT render subtopic heading separately
        var lines = (content ?? "").Split('\n');
        var subtopicHeadingCount = lines.Count(l =>
            l.Trim() == "- test file 5" || l.Trim() == "- Test File 5"
        );
        Assert.Equal(0, subtopicHeadingCount);
    }

    [Fact]
    public void UpdateTableOfContents_IgnoresParentFile_ShowsOnlyChildren()
    {
        // Arrange
        var journalDir = "/test/journal";
        _fileSystem.CreateDirectory(journalDir);

        var config = new JournalConfig
        {
            JournalName = "TestJournal",
            TableOfContents = new TableOfContents { IgnoreFiles = ["abc-test_2-test_file_5.md"] },
        };
        _journalConfiguration.Create(journalDir, config);

        // Act
        _mockTocStructureRepository
            .Setup(r => r.Load(It.IsAny<string>()))
            .Returns(
                new JournalTocStructure
                {
                    Structure = new Structure
                    {
                        Topics =
                        [
                            new()
                            {
                                Name = "Test Topic",
                                Entries =
                                [
                                    new()
                                    {
                                        Name = "test file 5",
                                        File = "abc-test_2-test_file_5.md",
                                    },
                                ],
                                Subtopics =
                                [
                                    new()
                                    {
                                        Name = "test file 5",
                                        Entries =
                                        [
                                            new()
                                            {
                                                Name = "test file 7",
                                                File = "abc-test_2-test_file_5-test_file_7.md",
                                            },
                                        ],
                                        Subtopics = null,
                                    },
                                ],
                            },
                        ],
                    },
                    RootEntries = [],
                }
            );
        _generator.UpdateTableOfContents(journalDir);

        // Assert
        var content = _fileSystem.GetFileContent($"{journalDir}/1a-TableOfContents.md");

        // Parent file should NOT appear
        Assert.DoesNotContain("abc-test_2-test_file_5.md", content);
        // Child should still appear (as a regular subtopic entry)
        Assert.Contains("[test file 7](abc-test_2-test_file_5-test_file_7.md)", content);
    }

    [Fact]
    public void UpdateTableOfContents_NameMismatch_RendersAsSeparateEntries()
    {
        // Arrange
        var journalDir = "/test/journal";
        _fileSystem.CreateDirectory(journalDir);

        var config = new JournalConfig
        {
            JournalName = "TestJournal",
            TableOfContents = new TableOfContents { },
        };
        _journalConfiguration.Create(journalDir, config);

        // Act
        _mockTocStructureRepository
            .Setup(r => r.Load(It.IsAny<string>()))
            .Returns(
                new JournalTocStructure
                {
                    Structure = new Structure
                    {
                        Topics =
                        [
                            new()
                            {
                                Name = "ABC",
                                Entries =
                                [
                                    new() { Name = "test file uno", File = "abc-test_file_1.md" },
                                ],
                                Subtopics =
                                [
                                    new()
                                    {
                                        Name = "test 2",
                                        Entries =
                                        [
                                            new()
                                            {
                                                Name = "test 2 entry",
                                                File = "abc-test_file_1-test_2.md",
                                            },
                                        ],
                                        Subtopics = null,
                                    },
                                ],
                            },
                        ],
                    },
                    RootEntries = [],
                }
            );
        _generator.UpdateTableOfContents(journalDir);

        // Assert
        var content = _fileSystem.GetFileContent($"{journalDir}/1a-TableOfContents.md");

        // Both should appear - parent entry and subtopic
        Assert.Contains("[test file uno](abc-test_file_1.md)", content);
        Assert.Contains("test 2", content);
        Assert.Contains("[test 2 entry](abc-test_file_1-test_2.md)", content);

        // Since names don't match, they should be separate (not nested as parent-child)
        var lines = (content ?? "").Split('\n').Select(l => l.TrimEnd()).ToArray();
        var parentIndex = Array.FindIndex(
            lines,
            l => l.Contains("[test file uno](abc-test_file_1.md)")
        );

        Assert.True(parentIndex >= 0);
        // Parent should not have the subtopic's entries as direct children
    }

    [Fact]
    public void UpdateTableOfContents_DeepNesting_DetectsParentChildAtAllLevels()
    {
        // Arrange
        var journalDir = "/test/journal";
        _fileSystem.CreateDirectory(journalDir);

        var config = new JournalConfig
        {
            JournalName = "TestJournal",
            TableOfContents = new TableOfContents { },
        };
        _journalConfiguration.Create(journalDir, config);

        // Act
        _mockTocStructureRepository
            .Setup(r => r.Load(It.IsAny<string>()))
            .Returns(
                new JournalTocStructure
                {
                    Structure = new Structure
                    {
                        Topics =
                        [
                            new()
                            {
                                Name = "Level 1",
                                Entries = [new() { Name = "level 2", File = "level_1-level_2.md" }],
                                Subtopics =
                                [
                                    new()
                                    {
                                        Name = "level 2",
                                        Entries =
                                        [
                                            new()
                                            {
                                                Name = "level 3",
                                                File = "level_1-level_2-level_3.md",
                                            },
                                        ],
                                        Subtopics =
                                        [
                                            new()
                                            {
                                                Name = "level 3",
                                                Entries =
                                                [
                                                    new()
                                                    {
                                                        Name = "final",
                                                        File = "level_1-level_2-level_3-final.md",
                                                    },
                                                ],
                                                Subtopics = null,
                                            },
                                        ],
                                    },
                                ],
                            },
                        ],
                    },
                    RootEntries = [],
                }
            );
        _generator.UpdateTableOfContents(journalDir);

        // Assert
        var content = _fileSystem.GetFileContent($"{journalDir}/1a-TableOfContents.md");

        // All levels should be nested properly
        Assert.Contains("[level 2](level_1-level_2.md)", content);
        Assert.Contains("[level 3](level_1-level_2-level_3.md)", content);
        Assert.Contains("[final](level_1-level_2-level_3-final.md)", content);

        // Verify nesting with indentation
        var lines = (content ?? "").Split('\n').Select(l => l.TrimEnd()).ToArray();
        var level2Index = Array.FindIndex(lines, l => l.Contains("[level 2](level_1-level_2.md)"));
        var level3Index = Array.FindIndex(
            lines,
            l => l.Contains("[level 3](level_1-level_2-level_3.md)")
        );
        var finalIndex = Array.FindIndex(
            lines,
            l => l.Contains("[final](level_1-level_2-level_3-final.md)")
        );

        // Get indentation levels
        var level2Indent = lines[level2Index].TakeWhile(c => c == ' ').Count();
        var level3Indent = lines[level3Index].TakeWhile(c => c == ' ').Count();
        var finalIndent = lines[finalIndex].TakeWhile(c => c == ' ').Count();

        // Each level should be more indented than the previous
        Assert.True(level3Indent > level2Indent);
        Assert.True(finalIndent > level3Indent);
    }

    [Fact]
    public void UpdateTableOfContents_MultipleChildrenUnderParent_AllNested()
    {
        // Arrange
        var journalDir = "/test/journal";
        _fileSystem.CreateDirectory(journalDir);

        var config = new JournalConfig
        {
            JournalName = "TestJournal",
            TableOfContents = new TableOfContents { },
        };
        _journalConfiguration.Create(journalDir, config);

        // Act
        _mockTocStructureRepository
            .Setup(r => r.Load(It.IsAny<string>()))
            .Returns(
                new JournalTocStructure
                {
                    Structure = new Structure
                    {
                        Topics =
                        [
                            new()
                            {
                                Name = "Topic",
                                Entries = [new() { Name = "parent", File = "topic-parent.md" }],
                                Subtopics =
                                [
                                    new()
                                    {
                                        Name = "parent",
                                        Entries =
                                        [
                                            new()
                                            {
                                                Name = "child 1",
                                                File = "topic-parent-child_1.md",
                                            },
                                            new()
                                            {
                                                Name = "child 2",
                                                File = "topic-parent-child_2.md",
                                            },
                                            new()
                                            {
                                                Name = "child 3",
                                                File = "topic-parent-child_3.md",
                                            },
                                        ],
                                        Subtopics = null,
                                    },
                                ],
                            },
                        ],
                    },
                    RootEntries = [],
                }
            );
        _generator.UpdateTableOfContents(journalDir);

        // Assert
        var content = _fileSystem.GetFileContent($"{journalDir}/1a-TableOfContents.md");

        // Parent should be a link
        Assert.Contains("[parent](topic-parent.md)", content);
        // All children should be present and nested
        Assert.Contains("[child 1](topic-parent-child_1.md)", content);
        Assert.Contains("[child 2](topic-parent-child_2.md)", content);
        Assert.Contains("[child 3](topic-parent-child_3.md)", content);

        // Verify children are more indented than parent
        var lines = (content ?? "").Split('\n').Select(l => l.TrimEnd()).ToArray();
        var parentIndex = Array.FindIndex(lines, l => l.Contains("[parent](topic-parent.md)"));
        var child1Index = Array.FindIndex(
            lines,
            l => l.Contains("[child 1](topic-parent-child_1.md)")
        );

        var parentIndent = lines[parentIndex].TakeWhile(c => c == ' ').Count();
        var child1Indent = lines[child1Index].TakeWhile(c => c == ' ').Count();

        Assert.True(child1Indent > parentIndent);
    }

    [Fact]
    public void UpdateTableOfContents_IgnoreFiles_FiltersFromRootEntries()
    {
        // Arrange
        var journalDir = "/test/journal";
        _fileSystem.CreateDirectory(journalDir);

        var config = new JournalConfig
        {
            JournalName = "TestJournal",
            TableOfContents = new TableOfContents { IgnoreFiles = ["1d-Draft.md"] },
        };
        _journalConfiguration.Create(journalDir, config);

        // Act
        _mockTocStructureRepository
            .Setup(r => r.Load(It.IsAny<string>()))
            .Returns(
                new JournalTocStructure
                {
                    Structure = new Structure { Topics = [] },
                    RootEntries =
                    [
                        new() { Name = "Introduction", File = "1b-Intro.md" },
                        new() { Name = "Template", File = "1c-Template.md" },
                        new() { Name = "Draft", File = "1d-Draft.md" },
                    ],
                }
            );
        _generator.UpdateTableOfContents(journalDir);

        // Assert
        var content = _fileSystem.GetFileContent($"{journalDir}/1a-TableOfContents.md");

        // Should show non-ignored entries
        Assert.Contains("[Introduction](1b-Intro.md)", content);
        Assert.Contains("[Template](1c-Template.md)", content);
        // Should NOT show ignored entry
        Assert.DoesNotContain("1d-Draft.md", content);
    }

    [Fact]
    public void UpdateTableOfContents_MixedIgnoredAndNonIgnored_ShowsOnlyNonIgnored()
    {
        // Arrange
        var journalDir = "/test/journal";
        _fileSystem.CreateDirectory(journalDir);

        var config = new JournalConfig
        {
            JournalName = "TestJournal",
            TableOfContents = new TableOfContents
            {
                IgnoreFiles = ["abc-test_2-test_file_5.md", "abc-test_2-test_file_6.md"],
            },
        };
        _journalConfiguration.Create(journalDir, config);

        // Act
        _mockTocStructureRepository
            .Setup(r => r.Load(It.IsAny<string>()))
            .Returns(
                new JournalTocStructure
                {
                    Structure = new Structure
                    {
                        Topics =
                        [
                            new()
                            {
                                Name = "ABC",
                                Entries = [],
                                Subtopics =
                                [
                                    new()
                                    {
                                        Name = "Test 2",
                                        Entries =
                                        [
                                            new()
                                            {
                                                Name = "test file 5",
                                                File = "abc-test_2-test_file_5.md",
                                            },
                                            new()
                                            {
                                                Name = "test file 6",
                                                File = "abc-test_2-test_file_6.md",
                                            },
                                            new()
                                            {
                                                Name = "test file 7",
                                                File = "abc-test_2-test_file_7.md",
                                            },
                                        ],
                                        Subtopics = null,
                                    },
                                ],
                            },
                        ],
                    },
                    RootEntries = [],
                }
            );
        _generator.UpdateTableOfContents(journalDir);

        // Assert
        var content = _fileSystem.GetFileContent($"{journalDir}/1a-TableOfContents.md");

        // Should NOT show ignored files
        Assert.DoesNotContain("abc-test_2-test_file_5.md", content);
        Assert.DoesNotContain("abc-test_2-test_file_6.md", content);
        // Should show non-ignored file
        Assert.Contains("[test file 7](abc-test_2-test_file_7.md)", content);
    }

    [Fact]
    public void UpdateTableOfContents_ParentChildWithSiblings_MaintainsCorrectOrder()
    {
        // Arrange
        var journalDir = "/test/journal";
        _fileSystem.CreateDirectory(journalDir);

        var config = new JournalConfig
        {
            JournalName = "TestJournal",
            TableOfContents = new TableOfContents { },
        };
        _journalConfiguration.Create(journalDir, config);

        // Act
        _mockTocStructureRepository
            .Setup(r => r.Load(It.IsAny<string>()))
            .Returns(
                new JournalTocStructure
                {
                    Structure = new Structure
                    {
                        Topics =
                        [
                            new()
                            {
                                Name = "Topic",
                                Entries =
                                [
                                    new() { Name = "entry 1", File = "topic-entry_1.md" },
                                    new() { Name = "parent", File = "topic-parent.md" },
                                    new() { Name = "entry 2", File = "topic-entry_2.md" },
                                ],
                                Subtopics =
                                [
                                    new()
                                    {
                                        Name = "parent",
                                        Entries =
                                        [
                                            new()
                                            {
                                                Name = "child",
                                                File = "topic-parent-child.md",
                                            },
                                        ],
                                        Subtopics = null,
                                    },
                                ],
                            },
                        ],
                    },
                    RootEntries = [],
                }
            );
        _generator.UpdateTableOfContents(journalDir);

        // Assert
        var content = _fileSystem.GetFileContent($"{journalDir}/1a-TableOfContents.md");

        // All entries should be present
        Assert.Contains("[entry 1](topic-entry_1.md)", content);
        Assert.Contains("[parent](topic-parent.md)", content);
        Assert.Contains("[entry 2](topic-entry_2.md)", content);
        Assert.Contains("[child](topic-parent-child.md)", content);

        // Verify order: entry1, parent with child nested, entry2
        var lines = (content ?? "").Split('\n').Select(l => l.TrimEnd()).ToArray();
        var entry1Index = Array.FindIndex(lines, l => l.Contains("[entry 1](topic-entry_1.md)"));
        var parentIndex = Array.FindIndex(lines, l => l.Contains("[parent](topic-parent.md)"));
        var childIndex = Array.FindIndex(lines, l => l.Contains("[child](topic-parent-child.md)"));
        var entry2Index = Array.FindIndex(lines, l => l.Contains("[entry 2](topic-entry_2.md)"));

        Assert.True(entry1Index < parentIndex);
        Assert.True(parentIndex < childIndex);
        Assert.True(childIndex < entry2Index);
    }

    [Fact]
    public void UpdateTableOfContents_TopicWithMatchingEntryAndSubtopics_RendersHeadingAndSubtopics()
    {
        // Arrange - topic "abc" has entry "abc.md" AND subtopics that should still be shown
        var journalDir = "/test/journal";
        _fileSystem.CreateDirectory(journalDir);

        var config = new JournalConfig
        {
            JournalName = "TestJournal",
            TableOfContents = new TableOfContents { },
        };
        _journalConfiguration.Create(journalDir, config);

        // Act
        _mockTocStructureRepository
            .Setup(r => r.Load(It.IsAny<string>()))
            .Returns(
                new JournalTocStructure
                {
                    Structure = new Structure
                    {
                        Topics =
                        [
                            new()
                            {
                                Name = "abc",
                                Entries = [new() { Name = "abc", File = "abc.md" }],
                                Subtopics =
                                [
                                    new()
                                    {
                                        Name = "test 2",
                                        Entries =
                                        [
                                            new()
                                            {
                                                Name = "test file 1",
                                                File = "abc-test_2-test_file_1.md",
                                            },
                                            new()
                                            {
                                                Name = "test file 10",
                                                File = "abc-test_2-test_file_10.md",
                                            },
                                        ],
                                        Subtopics =
                                        [
                                            new()
                                            {
                                                Name = "test file 1",
                                                Entries =
                                                [
                                                    new()
                                                    {
                                                        Name = "test file 1",
                                                        File =
                                                            "abc-test_2-test_file_1-test_file_1.md",
                                                    },
                                                ],
                                                Subtopics = null,
                                            },
                                        ],
                                    },
                                ],
                            },
                            new()
                            {
                                Name = "test 2",
                                Entries = [new() { Name = "test 2", File = "test_2.md" }],
                                Subtopics = null,
                            },
                        ],
                    },
                    RootEntries = [],
                }
            );
        _generator.UpdateTableOfContents(journalDir);

        // Assert
        var content = _fileSystem.GetFileContent($"{journalDir}/1a-TableOfContents.md");

        // Should have "abc" as a heading with link
        Assert.Contains("## [Abc](abc.md)", content);

        // Should ALSO show the subtopics (capitalized due to CapitalizeTopicHeadings setting)
        Assert.Contains("- Test 2", content);
        Assert.Contains("[test file 1](abc-test_2-test_file_1.md)", content);
        Assert.Contains("[test file 10](abc-test_2-test_file_10.md)", content);
        Assert.Contains("[test file 1](abc-test_2-test_file_1-test_file_1.md)", content);

        // Verify structure
        var lines = (content ?? "").Split('\n').Select(l => l.TrimEnd()).ToArray();

        // Find the abc heading
        var abcHeadingIndex = Array.FindIndex(lines, l => l.Contains("## [Abc](abc.md)"));
        Assert.True(abcHeadingIndex >= 0, "Should have abc heading");

        // Find subtopic "Test 2" - should be after abc heading
        var test2SubtopicIndex = Array.FindIndex(lines, l => l.Contains("- Test 2"));
        Assert.True(
            test2SubtopicIndex > abcHeadingIndex,
            "Test 2 subtopic should appear after abc heading"
        );

        // Find the separate "test 2" top-level topic
        var test2TopicIndex = Array.FindIndex(lines, l => l.Contains("## [Test 2](test_2.md)"));
        Assert.True(
            test2TopicIndex > test2SubtopicIndex,
            "Test 2 topic should appear after abc section"
        );
    }

    #endregion

    #region TOC File Filtering Tests

    [Fact]
    public void UpdateTableOfContents_ShouldNotIncludeTocFileInOutput()
    {
        // Arrange
        var journalDir = "/test/journal";
        _fileSystem.CreateDirectory(journalDir);

        var config = new JournalConfig
        {
            JournalName = "TestJournal",
            TableOfContents = new TableOfContents { File = "newtoc.md" },
        };
        _journalConfiguration.Create(journalDir, config);

        // Act
        _mockTocStructureRepository
            .Setup(r => r.Load(It.IsAny<string>()))
            .Returns(
                new JournalTocStructure
                {
                    Structure = new Structure { Topics = [] },
                    RootEntries =
                    [
                        new() { Name = "Introduction", File = "1b-Intro.md" },
                        new() { Name = "Newtoc", File = "newtoc.md" }, // TOC file shouldn't appear
                        new() { Name = "Other", File = "other.md" },
                    ],
                }
            );
        _generator.UpdateTableOfContents(journalDir);

        // Assert
        var content = _fileSystem.GetFileContent($"{journalDir}/newtoc.md");
        Assert.Contains("[Introduction](1b-Intro.md)", content);
        Assert.Contains("[Other](other.md)", content);
        Assert.DoesNotContain("[Newtoc](newtoc.md)", content);
        Assert.DoesNotContain("newtoc.md", content);
    }

    [Fact]
    public void UpdateTableOfContents_ShouldNotIncludeTocFileInTopics()
    {
        // Arrange
        var journalDir = "/test/journal";
        _fileSystem.CreateDirectory(journalDir);

        var config = new JournalConfig
        {
            JournalName = "TestJournal",
            TableOfContents = new TableOfContents { File = "newtoc.md" },
        };
        _journalConfiguration.Create(journalDir, config);

        // Act
        _mockTocStructureRepository
            .Setup(r => r.Load(It.IsAny<string>()))
            .Returns(
                new JournalTocStructure
                {
                    Structure = new Structure
                    {
                        Topics =
                        [
                            new Topic
                            {
                                Name = "newtoc",
                                Entries =
                                [
                                    new() { Name = "Newtoc", File = "newtoc.md" }, // Should be filtered
                                ],
                                Subtopics = null,
                            },
                            new Topic
                            {
                                Name = "other",
                                Entries = [new() { Name = "Other", File = "other.md" }],
                                Subtopics = null,
                            },
                        ],
                    },
                    RootEntries = [],
                }
            );
        _generator.UpdateTableOfContents(journalDir);

        // Assert
        var content = _fileSystem.GetFileContent($"{journalDir}/newtoc.md");
        // Topic with only TOC file should not appear (filtered out completely)
        Assert.DoesNotContain("## Newtoc", content);
        Assert.DoesNotContain("newtoc.md", content);
        // Other topic should appear as linked heading (since it has one entry with matching name)
        Assert.Contains("## [Other](other.md)", content);
        // Should not have a duplicate entry line
        Assert.DoesNotContain("  - [Other](other.md)", content);
    }

    [Fact]
    public void UpdateTableOfContents_ShouldFilterTocFileCaseInsensitive()
    {
        // Arrange
        var journalDir = "/test/journal";
        _fileSystem.CreateDirectory(journalDir);

        var config = new JournalConfig
        {
            JournalName = "TestJournal",
            TableOfContents = new TableOfContents { File = "newtoc.md" },
        };
        _journalConfiguration.Create(journalDir, config);

        // Act
        _mockTocStructureRepository
            .Setup(r => r.Load(It.IsAny<string>()))
            .Returns(
                new JournalTocStructure
                {
                    Structure = new Structure { Topics = [] },
                    RootEntries =
                    [
                        new() { Name = "Introduction", File = "1b-Intro.md" },
                        new() { Name = "Newtoc", File = "NewTOC.md" }, // Different casing
                    ],
                }
            );
        _generator.UpdateTableOfContents(journalDir);

        // Assert
        var content = _fileSystem.GetFileContent($"{journalDir}/newtoc.md");
        Assert.DoesNotContain("NewTOC.md", content);
        Assert.DoesNotContain("[Newtoc]", content);
    }

    [Fact]
    public void UpdateTableOfContents_ShouldFilterTocFileFromIgnoreFilesList()
    {
        // Arrange
        var journalDir = "/test/journal";
        _fileSystem.CreateDirectory(journalDir);

        var config = new JournalConfig
        {
            JournalName = "TestJournal",
            TableOfContents = new TableOfContents
            {
                File = "toc.md",
                IgnoreFiles = ["draft.md"], // toc.md added automatically
            },
        };
        _journalConfiguration.Create(journalDir, config);

        // Act
        _mockTocStructureRepository
            .Setup(r => r.Load(It.IsAny<string>()))
            .Returns(
                new JournalTocStructure
                {
                    Structure = new Structure { Topics = [] },
                    RootEntries =
                    [
                        new() { Name = "Introduction", File = "1b-Intro.md" },
                        new() { Name = "TOC", File = "toc.md" },
                        new() { Name = "Draft", File = "draft.md" },
                    ],
                }
            );
        _generator.UpdateTableOfContents(journalDir);

        // Assert
        var content = _fileSystem.GetFileContent($"{journalDir}/toc.md");
        Assert.Contains("[Introduction](1b-Intro.md)", content);
        // Both toc.md and draft.md should be filtered out
        Assert.DoesNotContain("[TOC](toc.md)", content);
        Assert.DoesNotContain("[Draft](draft.md)", content);
    }

    #endregion
}
