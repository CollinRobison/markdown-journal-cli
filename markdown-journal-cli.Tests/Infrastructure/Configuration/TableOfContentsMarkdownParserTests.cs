using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.Configuration.Models;

namespace markdown_journal_cli.Tests.Infrastructure.Configuration;

public class TableOfContentsMarkdownParserTests
{
    private readonly TableOfContentsMarkdownParser _parser;

    public TableOfContentsMarkdownParserTests()
    {
        _parser = new TableOfContentsMarkdownParser();
    }

    [Fact]
    public void ParseTableOfContents_EmptyContent_ReturnsEmptyArrays()
    {
        // Act
        var entries = _parser.ParseTableOfContents("");

        // Assert
        Assert.Empty(entries);
    }

    [Fact]
    public void ParseTableOfContents_ExtractsAllLinks()
    {
        // Arrange
        var content =
            @"# Table of Contents
- [Introduction](1b-Intro.md)
- [Template](1c-Template.md)";

        // Act
        var entries = _parser.ParseTableOfContents(content);

        // Assert
        Assert.Equal(2, entries.Length);

        Assert.Equal("Introduction", entries[0].Name);
        Assert.Equal("1b-Intro.md", entries[0].File);

        Assert.Equal("Template", entries[1].Name);
        Assert.Equal("1c-Template.md", entries[1].File);
    }

    [Fact]
    public void ParseTableOfContents_ExtractsLinksFromTopics()
    {
        // Arrange
        var content =
            @"# Table of Contents
## Work
- [Meeting Notes](work-meeting_notes.md)
- [Project Plan](work-project_plan.md)";

        // Act
        var entries = _parser.ParseTableOfContents(content);

        // Assert
        Assert.Equal(2, entries.Length);

        Assert.Equal("Meeting Notes", entries[0].Name);
        Assert.Equal("work-meeting_notes.md", entries[0].File);

        Assert.Equal("Project Plan", entries[1].Name);
        Assert.Equal("work-project_plan.md", entries[1].File);
    }

    [Fact]
    public void ParseTableOfContents_ExtractsLinksFromNestedStructure()
    {
        // Arrange
        var content =
            @"# Table of Contents
## Work
  - Backend
    - [API Design](work-backend-api_design.md)
    - [Database](work-backend-database.md)
  - Frontend
    - [UI Components](work-frontend-ui_components.md)";

        // Act
        var entries = _parser.ParseTableOfContents(content);

        // Assert
        Assert.Equal(3, entries.Length);

        Assert.Equal("API Design", entries[0].Name);
        Assert.Equal("work-backend-api_design.md", entries[0].File);

        Assert.Equal("Database", entries[1].Name);
        Assert.Equal("work-backend-database.md", entries[1].File);

        Assert.Equal("UI Components", entries[2].Name);
        Assert.Equal("work-frontend-ui_components.md", entries[2].File);
    }

    [Fact]
    public void ParseTableOfContents_ExtractsLinksFromHeadings()
    {
        // Arrange
        var content =
            @"# Table of Contents
## [Work Notes](work-notes.md)
- [Entry](work-entry.md)";

        // Act
        var entries = _parser.ParseTableOfContents(content);

        // Assert
        Assert.Equal(2, entries.Length);

        Assert.Equal("Work Notes", entries[0].Name);
        Assert.Equal("work-notes.md", entries[0].File);

        Assert.Equal("Entry", entries[1].Name);
        Assert.Equal("work-entry.md", entries[1].File);
    }

    [Fact]
    public void ParseTableOfContents_IgnoresBlankLines()
    {
        // Arrange
        var content =
            @"# Table of Contents

- [Introduction](intro.md)


## Work

- [Meeting](meeting.md)

";

        // Act
        var entries = _parser.ParseTableOfContents(content);

        // Assert
        Assert.Equal(2, entries.Length);
        Assert.Equal("Introduction", entries[0].Name);
        Assert.Equal("Meeting", entries[1].Name);
    }

    [Fact]
    public void ParseTableOfContents_IgnoresMetadata()
    {
        // Arrange
        var content =
            @"Created: 1/1/2026
Last Edited: 1/15/2026

# Table of Contents
- [Entry1](entry1.md)";

        // Act
        var entries = _parser.ParseTableOfContents(content);

        // Assert
        Assert.Single(entries);
        Assert.Equal("Entry1", entries[0].Name);
    }

    [Fact]
    public void ParseTableOfContents_IgnoresPlainTextItems()
    {
        // Arrange
        var content =
            @"# Table of Contents
- Plain text without link
- [Entry With Link](entry.md)
## Topic
- Plain text item
- [Another Entry](another.md)";

        // Act
        var entries = _parser.ParseTableOfContents(content);

        // Assert
        Assert.Equal(2, entries.Length); // Only linked entries
        Assert.Equal("Entry With Link", entries[0].Name);
        Assert.Equal("Another Entry", entries[1].Name);
    }

    [Fact]
    public void ParseTableOfContents_PreservesSpecialCharacters()
    {
        // Arrange - Note: nested brackets in link text may not work with simple regex
        var content =
            @"# Table of Contents
- [Entry With (Parens)](special-parens.md)
- [Entry With ""Quotes""](special-quotes.md)";

        // Act
        var entries = _parser.ParseTableOfContents(content);

        // Assert
        Assert.Equal(2, entries.Length);

        Assert.Equal("Entry With (Parens)", entries[0].Name);
        Assert.Equal("Entry With \"Quotes\"", entries[1].Name);
    }

    [Fact]
    public void ParseTableOfContents_HandlesDuplicateNames()
    {
        // Arrange
        var content =
            @"# Table of Contents
- [Entry](duplicate-v1.md)
- [Entry](duplicate-v2.md)
- [Entry](duplicate-v3.md)";

        // Act
        var entries = _parser.ParseTableOfContents(content);

        // Assert
        Assert.Equal(3, entries.Length);

        Assert.Equal("Entry", entries[0].Name);
        Assert.Equal("duplicate-v1.md", entries[0].File);
        Assert.Equal("Entry", entries[1].Name);
        Assert.Equal("duplicate-v2.md", entries[1].File);
        Assert.Equal("Entry", entries[2].Name);
        Assert.Equal("duplicate-v3.md", entries[2].File);
    }

    [Fact]
    public void ParseTableOfContents_OnlyExtractsMarkdownFiles()
    {
        // Arrange
        var content =
            @"# Table of Contents
- [Markdown Entry](entry.md)
- [Text File](entry.txt)
- [Another MD](another.md)";

        // Act
        var entries = _parser.ParseTableOfContents(content);

        // Assert
        Assert.Equal(2, entries.Length); // Only .md files
        Assert.Equal("Markdown Entry", entries[0].Name);
        Assert.Equal("Another MD", entries[1].Name);
    }

    [Fact]
    public void ParseTableOfContents_ComplexMixedStructure_ExtractsAllLinks()
    {
        // Arrange
        var content =
            @"Created: 1/1/2026
Last Edited: 1/15/2026

# Table of Contents

- [Root Entry 1](1b-root1.md)
- [Root Entry 2](1c-root2.md)

## [Topic As Link](topic.md)

## Work
- [Meeting Notes](work-meeting.md)
  - Subtopic Without Link
    - [Nested Entry](work-subtopic-nested.md)
- Plain text
- [Project Plan](work-project.md)

## Personal
  - Indented Subtopic
    - [Deep Entry 1](personal-subtopic-deep1.md)
    - [Deep Entry 2](personal-subtopic-deep2.md)";

        // Act
        var entries = _parser.ParseTableOfContents(content);

        // Assert
        Assert.Equal(8, entries.Length); // All markdown links

        // Verify all files are present
        var files = entries.Select(e => e.File).ToArray();
        Assert.Contains("1b-root1.md", files);
        Assert.Contains("1c-root2.md", files);
        Assert.Contains("topic.md", files);
        Assert.Contains("work-meeting.md", files);
        Assert.Contains("work-subtopic-nested.md", files);
        Assert.Contains("work-project.md", files);
        Assert.Contains("personal-subtopic-deep1.md", files);
        Assert.Contains("personal-subtopic-deep2.md", files);
    }
}
