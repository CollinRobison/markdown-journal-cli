using markdown_journal_cli.JournalTemplates.Templates;
using Shouldly;
using Xunit;

namespace markdown_journal_cli.Tests.JournalTemplates.Templates;

/// <summary>
/// Unit tests for the <see cref="JournalEntryTemplate"/> class, covering template generation
/// with various parameter combinations and edge cases.
/// </summary>
public class JournalEntryTemplateTests
{
    private readonly JournalEntryTemplate _template;

    public JournalEntryTemplateTests()
    {
        _template = new JournalEntryTemplate();
    }

    [Fact]
    public void TemplateName_Should_Return_Correct_Name()
    {
        // When & Then
        _template.TemplateName.ShouldBe("journal-entry");
    }

    [Fact]
    public void GenerateTemplate_Should_Return_Default_Content_When_Parameters_Are_Null()
    {
        // When
        var result = _template.GenerateTemplate(null);

        // Then
        result.ShouldContain("[Back to Table of Contents](1a-TableOfContents.md)");
        // When parameters is null, GetValueOrDefault returns null, so ToString() returns empty
        result.ShouldContain("# ");
        result.ShouldContain("Created: ");
        result.ShouldContain("Last Edited: ");
    }

    [Fact]
    public void GenerateTemplate_Should_Return_Default_Content_When_Parameters_Are_Empty()
    {
        // Given
        var parameters = new Dictionary<string, object>();

        // When
        var result = _template.GenerateTemplate(parameters);

        // Then
        result.ShouldContain("[Back to Table of Contents](1a-TableOfContents.md)");
        result.ShouldContain("# Title goes here");
        result.ShouldContain("body goes here.");
        result.ShouldContain("[Make sure to add link to any reference here](add-link)");
        result.ShouldContain("Created:");
        result.ShouldContain("Last Edited:");
    }

    [Fact]
    public void GenerateTemplate_Should_Use_Custom_Title_When_Provided()
    {
        // Given
        var parameters = new Dictionary<string, object> { ["title"] = "Custom Title" };

        // When
        var result = _template.GenerateTemplate(parameters);

        // Then
        result.ShouldContain("# Custom Title");
        result.ShouldNotContain("# Title goes here");
    }

    [Fact]
    public void GenerateTemplate_Should_Use_Custom_Body_When_Provided()
    {
        // Given
        var parameters = new Dictionary<string, object>
        {
            ["body"] = "This is custom body content.",
        };

        // When
        var result = _template.GenerateTemplate(parameters);

        // Then
        result.ShouldContain("This is custom body content.");
        result.ShouldNotContain("body goes here.");
    }

    [Fact]
    public void GenerateTemplate_Should_Hide_Sources_When_AddSourceBlock_Is_False()
    {
        // Given
        var parameters = new Dictionary<string, object> { ["addSourceBlock"] = false };

        // When
        var result = _template.GenerateTemplate(parameters);

        // Then
        result.ShouldNotContain("[Make sure to add link to any reference here](add-link)");
        result.ShouldNotContain("sources");
    }

    [Fact]
    public void GenerateTemplate_Should_Show_Custom_Sources_When_Provided()
    {
        // Given
        var parameters = new Dictionary<string, object>
        {
            ["addSourceBlock"] = true,
            ["sources"] = "Custom source content",
        };

        // When
        var result = _template.GenerateTemplate(parameters);

        // Then
        result.ShouldContain("Custom source content");
        result.ShouldNotContain("[Make sure to add link to any reference here](add-link)");
    }

    [Fact]
    public void GenerateTemplate_Should_Use_Custom_Created_Date_When_Provided()
    {
        // Given
        var parameters = new Dictionary<string, object> { ["createdDate"] = "1/1/2023" };

        // When
        var result = _template.GenerateTemplate(parameters);

        // Then
        result.ShouldContain("Created: 1/1/2023");
    }

    [Fact]
    public void GenerateTemplate_Should_Use_Custom_Last_Edited_Date_When_Provided()
    {
        // Given
        var parameters = new Dictionary<string, object> { ["lastEditedDate"] = "2/1/2023" };

        // When
        var result = _template.GenerateTemplate(parameters);

        // Then
        result.ShouldContain("Last Edited: 2/1/2023");
    }

    [Fact]
    public void GenerateTemplate_Should_Use_Current_Date_For_Dates_When_Not_Provided()
    {
        // Given
        var parameters = new Dictionary<string, object>();
        var today = DateTime.Now.ToString("M/d/yyyy");

        // When
        var result = _template.GenerateTemplate(parameters);

        // Then
        result.ShouldContain($"Created: {today}");
        result.ShouldContain($"Last Edited: {today}");
    }

    [Fact]
    public void GenerateTemplate_Should_Handle_All_Custom_Parameters()
    {
        // Given
        var parameters = new Dictionary<string, object>
        {
            ["title"] = "My Custom Journal Entry",
            ["body"] = "This is my detailed journal entry content.",
            ["addSourceBlock"] = true,
            ["sources"] = "[Reference 1](http://example.com)",
            ["createdDate"] = "12/25/2022",
            ["lastEditedDate"] = "1/15/2023",
        };

        // When
        var result = _template.GenerateTemplate(parameters);

        // Then
        result.ShouldContain("# My Custom Journal Entry");
        result.ShouldContain("This is my detailed journal entry content.");
        result.ShouldContain("[Reference 1](http://example.com)");
        result.ShouldContain("Created: 12/25/2022");
        result.ShouldContain("Last Edited: 1/15/2023");
        result.ShouldContain("[Back to Table of Contents](1a-TableOfContents.md)");
    }

    [Theory]
    [InlineData(true)]
    [InlineData("true")]
    [InlineData(1)]
    [InlineData("")] // Empty string now evaluates to true
    [InlineData("   ")] // Whitespace now evaluates to true
    [InlineData("anything")] // Any non-"false" string evaluates to true
    public void GenerateTemplate_Should_Handle_Various_AddSourceBlock_Types_True(
        object addSourceBlockValue
    )
    {
        // Given
        var parameters = new Dictionary<string, object>
        {
            ["addSourceBlock"] = addSourceBlockValue,
        };

        // When
        var result = _template.GenerateTemplate(parameters);

        // Then
        result.ShouldNotBeNull();
        result.ShouldContain("[Make sure to add link to any reference here](add-link)");
    }

    [Theory]
    [InlineData(false)]
    [InlineData("false")]
    [InlineData(0)]
    public void GenerateTemplate_Should_Handle_Various_AddSourceBlock_Types_False(
        object addSourceBlockValue
    )
    {
        // Given
        var parameters = new Dictionary<string, object>
        {
            ["addSourceBlock"] = addSourceBlockValue,
        };

        // When
        var result = _template.GenerateTemplate(parameters);

        // Then
        result.ShouldNotBeNull();
        result.ShouldNotContain("[Make sure to add link to any reference here](add-link)");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void GenerateTemplate_Should_Handle_Empty_And_Whitespace_Values(string value)
    {
        // Given
        var parameters = new Dictionary<string, object>
        {
            ["title"] = value,
            ["body"] = value,
            ["sources"] = value,
        };

        // When
        var result = _template.GenerateTemplate(parameters);

        // Then
        result.ShouldNotBeNull();
        result.ShouldContain($"# {value}");
        result.ShouldContain(value);
    }

    [Fact]
    public void GenerateTemplate_Should_Convert_Non_String_Parameters_To_String()
    {
        // Given
        var parameters = new Dictionary<string, object>
        {
            ["title"] = 123,
            ["body"] = 456.789,
            ["sources"] = true,
        };

        // When
        var result = _template.GenerateTemplate(parameters);

        // Then
        result.ShouldContain("# 123");
        result.ShouldContain("456.789");
        result.ShouldContain("True");
    }

    [Fact]
    public void GenerateTemplate_Should_Handle_Null_Parameter_Values_Gracefully()
    {
        // Given
        var parameters = new Dictionary<string, object>
        {
            ["title"] = null!,
            ["body"] = null!,
            ["sources"] = null!,
        };

        // When & Then
        // The template should not throw when encountering null values
        Should.NotThrow(() => _template.GenerateTemplate(parameters));
    }

    [Fact]
    public void GenerateTemplate_Should_Default_To_Showing_Sources_For_Empty_And_Whitespace_AddSourceBlock()
    {
        // Given - Testing the "default to true unless explicitly false" behavior
        var emptyStringParams = new Dictionary<string, object> { ["addSourceBlock"] = "" };
        var whitespaceParams = new Dictionary<string, object> { ["addSourceBlock"] = "   " };
        var tabParams = new Dictionary<string, object> { ["addSourceBlock"] = "\t" };

        // When
        var emptyResult = _template.GenerateTemplate(emptyStringParams);
        var whitespaceResult = _template.GenerateTemplate(whitespaceParams);
        var tabResult = _template.GenerateTemplate(tabParams);

        // Then - All should show sources (default behavior)
        emptyResult.ShouldContain("[Make sure to add link to any reference here](add-link)");
        whitespaceResult.ShouldContain("[Make sure to add link to any reference here](add-link)");
        tabResult.ShouldContain("[Make sure to add link to any reference here](add-link)");
    }
}
