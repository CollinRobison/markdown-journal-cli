using markdown_journal_cli.Infrastructure.FileSystem;

namespace markdown_journal_cli.Tests.Infrastructure;

public class MarkdownMetadataParserTests
{
    [Fact]
    public void ParseDates_ReturnsNullDates_WhenContentIsEmpty()
    {
        // Act
        var (created, edited) = MarkdownMetadataParser.ParseDates("");

        // Assert
        Assert.Null(created);
        Assert.Null(edited);
    }

    [Fact]
    public void ParseDates_ReturnsNullDates_WhenContentIsNull()
    {
        // Act
        var (created, edited) = MarkdownMetadataParser.ParseDates(null!);

        // Assert
        Assert.Null(created);
        Assert.Null(edited);
    }

    [Fact]
    public void ParseDates_ParsesCreatedDate_WhenPresent()
    {
        // Arrange
        var content = @"Created: 1/15/2024
Last Edited: 02/20/2024

# Title";

        // Act
        var (created, edited) = MarkdownMetadataParser.ParseDates(content);

        // Assert
        Assert.NotNull(created);
        Assert.Equal(new DateTime(2024, 1, 15), created.Value);
    }

    [Fact]
    public void ParseDates_ParsesLastEditedDate_WhenPresent()
    {
        // Arrange
        var content = @"Created: 1/15/2024
Last Edited: 02/20/2024

# Title";

        // Act
        var (created, edited) = MarkdownMetadataParser.ParseDates(content);

        // Assert
        Assert.NotNull(edited);
        Assert.Equal(new DateTime(2024, 2, 20), edited.Value);
    }

    [Fact]
    public void ParseDates_ParsesBothDates_WhenBothPresent()
    {
        // Arrange
        var content = @"Created: 1/15/2024
Last Edited: 02/20/2024

# Title";

        // Act
        var (created, edited) = MarkdownMetadataParser.ParseDates(content);

        // Assert
        Assert.NotNull(created);
        Assert.NotNull(edited);
        Assert.Equal(new DateTime(2024, 1, 15), created.Value);
        Assert.Equal(new DateTime(2024, 2, 20), edited.Value);
    }

    [Fact]
    public void ParseDates_ReturnsNullCreated_WhenOnlyLastEditedPresent()
    {
        // Arrange
        var content = @"Last Edited: 02/20/2024

# Title";

        // Act
        var (created, edited) = MarkdownMetadataParser.ParseDates(content);

        // Assert
        Assert.Null(created);
        Assert.NotNull(edited);
        Assert.Equal(new DateTime(2024, 2, 20), edited.Value);
    }

    [Fact]
    public void ParseDates_ReturnsNullEdited_WhenOnlyCreatedPresent()
    {
        // Arrange
        var content = @"Created: 1/15/2024

# Title";

        // Act
        var (created, edited) = MarkdownMetadataParser.ParseDates(content);

        // Assert
        Assert.NotNull(created);
        Assert.Null(edited);
        Assert.Equal(new DateTime(2024, 1, 15), created.Value);
    }

    [Fact]
    public void ParseDates_IsCaseInsensitive()
    {
        // Arrange
        var content = @"CREATED: 1/15/2024
last edited: 02/20/2024

# Title";

        // Act
        var (created, edited) = MarkdownMetadataParser.ParseDates(content);

        // Assert
        Assert.NotNull(created);
        Assert.NotNull(edited);
        Assert.Equal(new DateTime(2024, 1, 15), created.Value);
        Assert.Equal(new DateTime(2024, 2, 20), edited.Value);
    }

    [Fact]
    public void ParseDates_StopsAtFirstHeading()
    {
        // Arrange
        var content = @"Created: 1/15/2024

# Title

Created: 2/1/2024
Last Edited: 02/20/2024";

        // Act
        var (created, edited) = MarkdownMetadataParser.ParseDates(content);

        // Assert
        Assert.NotNull(created);
        Assert.Null(edited); // Should not find Last Edited after heading
        Assert.Equal(new DateTime(2024, 1, 15), created.Value);
    }

    [Fact]
    public void ParseDates_HandlesExtraWhitespace()
    {
        // Arrange
        var content = @"  Created:   1/15/2024  
    Last Edited:    02/20/2024   

# Title";

        // Act
        var (created, edited) = MarkdownMetadataParser.ParseDates(content);

        // Assert
        Assert.NotNull(created);
        Assert.NotNull(edited);
        Assert.Equal(new DateTime(2024, 1, 15), created.Value);
        Assert.Equal(new DateTime(2024, 2, 20), edited.Value);
    }

    [Fact]
    public void ParseDates_ReturnsNull_WhenDateFormatIsInvalid()
    {
        // Arrange
        var content = @"Created: not-a-date
Last Edited: also-not-a-date

# Title";

        // Act
        var (created, edited) = MarkdownMetadataParser.ParseDates(content);

        // Assert
        Assert.Null(created);
        Assert.Null(edited);
    }

    [Fact]
    public void ParseDates_HandlesContentWithoutDates()
    {
        // Arrange
        var content = @"# Title

Some content here
- List item 1
- List item 2";

        // Act
        var (created, edited) = MarkdownMetadataParser.ParseDates(content);

        // Assert
        Assert.Null(created);
        Assert.Null(edited);
    }

    [Fact]
    public void ParseDates_OnlyChecksFirstSixLines()
    {
        // Arrange - dates on lines 7 and 8 should not be found
        var content = @"Line 1
Line 2
Line 3
Line 4
Line 5
Line 6
Created: 1/15/2024
Last Edited: 02/20/2024";

        // Act
        var (created, edited) = MarkdownMetadataParser.ParseDates(content);

        // Assert
        Assert.Null(created);
        Assert.Null(edited);
    }

    [Fact]
    public void ParseDates_SupportsVariousDateFormats()
    {
        // Arrange
        var content = @"Created: 12/25/2023
Last Edited: 01/01/2024

# Title";

        // Act
        var (created, edited) = MarkdownMetadataParser.ParseDates(content);

        // Assert
        Assert.NotNull(created);
        Assert.NotNull(edited);
        Assert.Equal(new DateTime(2023, 12, 25), created.Value);
        Assert.Equal(new DateTime(2024, 1, 1), edited.Value);
    }

    #region UpdateLastEditedDate Tests

    [Fact]
    public void UpdateLastEditedDate_ReturnsDateLine_WhenContentIsEmpty()
    {
        // Arrange
        var date = new DateTime(2026, 2, 10);

        // Act
        var result = MarkdownMetadataParser.UpdateLastEditedDate("", date);

        // Assert
        Assert.Contains("Last Edited: 02/10/2026", result);
    }

    [Fact]
    public void UpdateLastEditedDate_ReplacesExistingLastEditedLine()
    {
        // Arrange
        var content = @"Created: 01/15/2024
Last Edited: 01/20/2024

# Title

Some content";
        var date = new DateTime(2026, 2, 10);

        // Act
        var result = MarkdownMetadataParser.UpdateLastEditedDate(content, date);

        // Assert
        Assert.Contains("Last Edited: 02/10/2026", result);
        Assert.DoesNotContain("01/20/2024", result);
        Assert.Contains("Created: 01/15/2024", result);
        Assert.Contains("# Title", result);
    }

    [Fact]
    public void UpdateLastEditedDate_InsertsAfterCreatedLine_WhenNoLastEditedExists()
    {
        // Arrange
        var content = @"Created: 01/15/2024

# Title

Some content";
        var date = new DateTime(2026, 2, 10);

        // Act
        var result = MarkdownMetadataParser.UpdateLastEditedDate(content, date);

        // Assert
        Assert.Contains("Last Edited: 02/10/2026", result);
        Assert.Contains("Created: 01/15/2024", result);
        // Last Edited should come after Created
        var lines = result.Split('\n');
        var createdIndex = Array.FindIndex(lines, l => l.Trim().StartsWith("Created:"));
        var editedIndex = Array.FindIndex(lines, l => l.Trim().StartsWith("Last Edited:"));
        Assert.True(editedIndex > createdIndex, "Last Edited should be after Created");
    }

    [Fact]
    public void UpdateLastEditedDate_InsertsAtTop_WhenNoMetadataExists()
    {
        // Arrange
        var content = @"# Title

Some content";
        var date = new DateTime(2026, 2, 10);

        // Act
        var result = MarkdownMetadataParser.UpdateLastEditedDate(content, date);

        // Assert
        Assert.StartsWith("Last Edited: 02/10/2026", result);
        Assert.Contains("# Title", result);
    }

    [Fact]
    public void UpdateLastEditedDate_PreservesRestOfContent()
    {
        // Arrange
        var content = @"Created: 01/15/2024
Last Edited: 01/20/2024

# Title

- List item 1
- List item 2

## Section
Some paragraph";
        var date = new DateTime(2026, 2, 10);

        // Act
        var result = MarkdownMetadataParser.UpdateLastEditedDate(content, date);

        // Assert
        Assert.Contains("- List item 1", result);
        Assert.Contains("- List item 2", result);
        Assert.Contains("## Section", result);
        Assert.Contains("Some paragraph", result);
    }

    [Fact]
    public void UpdateLastEditedDate_UsesCustomDateFormat()
    {
        // Arrange
        var content = @"Created: 01/15/2024
Last Edited: 01/20/2024

# Title";
        var date = new DateTime(2026, 2, 10);

        // Act
        var result = MarkdownMetadataParser.UpdateLastEditedDate(content, date, "yyyy-MM-dd");

        // Assert
        Assert.Contains("Last Edited: 2026-02-10", result);
    }

    [Fact]
    public void UpdateLastEditedDate_UsesDefaultFormat_WhenNoFormatSpecified()
    {
        // Arrange
        var content = @"Created: 01/15/2024
Last Edited: 01/20/2024

# Title";
        var date = new DateTime(2026, 2, 10);

        // Act
        var result = MarkdownMetadataParser.UpdateLastEditedDate(content, date);

        // Assert
        Assert.Contains("Last Edited: 02/10/2026", result);
    }

    [Fact]
    public void UpdateLastEditedDate_IsCaseInsensitive_WhenReplacingExistingLine()
    {
        // Arrange
        var content = @"Created: 01/15/2024
last edited: 01/20/2024

# Title";
        var date = new DateTime(2026, 2, 10);

        // Act
        var result = MarkdownMetadataParser.UpdateLastEditedDate(content, date);

        // Assert
        Assert.Contains("Last Edited: 02/10/2026", result);
        Assert.DoesNotContain("01/20/2024", result);
    }

    [Fact]
    public void UpdateLastEditedDate_DoesNotModifyLastEditedAfterHeading()
    {
        // Arrange — "Last Edited:" after heading should not be touched
        var content = @"Created: 01/15/2024

# Title

Last Edited: 01/20/2024";
        var date = new DateTime(2026, 2, 10);

        // Act
        var result = MarkdownMetadataParser.UpdateLastEditedDate(content, date);

        // Assert — the one after the heading should be untouched
        Assert.Contains("Last Edited: 01/20/2024", result);
        // A new Last Edited should have been inserted in the header
        var lines = result.Split('\n');
        var headerEdited = lines.Take(3).Any(l => l.Contains("02/10/2026"));
        Assert.True(headerEdited, "New Last Edited date should be in the header");
    }

    #endregion
}
