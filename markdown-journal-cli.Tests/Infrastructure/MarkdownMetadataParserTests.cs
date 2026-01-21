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
}
