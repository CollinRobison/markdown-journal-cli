using markdown_journal_cli.JournalTemplates.Templates;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace markdown_journal_cli.Tests.JournalTemplates.Templates;

/// <summary>
/// Unit tests for the <see cref="TableOfContentsTemplate"/> class, covering template generation
/// with various parameter combinations and date formatting.
/// </summary>
public class TableOfContentsTemplateTests
{
    private readonly TableOfContentsTemplate _template;
    private readonly IOptions<JournalSettings> _journalSettings;

    public TableOfContentsTemplateTests()
    {
        _journalSettings = Options.Create(new JournalSettings
        {
            IntroductionTitle = "Introduction",
            IntroductionFileName = "1b-Intro",
            JournalEntryTemplateTitle = "Journal Entry Template",
            JournalEntryTemplateFileName = "1c-Journal-Entry-Template.md",
            AllJournalsTitle = "All My Journals",
            AllJournalsFileName = "1h-All-My-Journals.md"
        });
        _template = new TableOfContentsTemplate(_journalSettings);
    }

    [Fact]
    public void TemplateName_Should_Return_Correct_Name()
    {
        // When & Then
        _template.TemplateName.ShouldBe("table-of-contents");
    }

    [Fact]
    public void GenerateTemplate_Should_Return_Default_Content_When_Parameters_Are_Null()
    {
        // When
        var result = _template.GenerateTemplate(null);

        // Then
        result.ShouldContain("# Table of Contents");
        result.ShouldContain("- [Introduction](1b-Intro.md)");
        result.ShouldContain("- [Journal Entry Template](1c-Journal-Entry-Template.md)");
        result.ShouldContain("- [All My Journals](1h-All-My-Journals.md)");
        result.ShouldContain("## Example Topic");
        result.ShouldContain("- [example link to content]()");
        result.ShouldContain("Created:");
        result.ShouldContain("Last Edited:");
    }

    [Fact]
    public void GenerateTemplate_Should_Return_Default_Content_When_Parameters_Are_Empty()
    {
        // Given
        var parameters = new Dictionary<string, object>();

        // When
        var result = _template.GenerateTemplate(parameters);

        // Then
        result.ShouldContain("# Table of Contents");
        result.ShouldContain("- [Introduction](1b-Intro.md)");
        result.ShouldContain("- [Journal Entry Template](1c-Journal-Entry-Template.md)");
        result.ShouldContain("- [All My Journals](1h-All-My-Journals.md)");
        result.ShouldContain("## Example Topic");
        result.ShouldContain("- [example link to content]()");
        result.ShouldContain("Created:");
        result.ShouldContain("Last Edited:");
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
    public void GenerateTemplate_Should_Use_Both_Custom_Dates_When_Provided()
    {
        // Given
        var parameters = new Dictionary<string, object>
        {
            ["createdDate"] = "12/25/2022",
            ["lastEditedDate"] = "1/15/2023",
        };

        // When
        var result = _template.GenerateTemplate(parameters);

        // Then
        result.ShouldContain("Created: 12/25/2022");
        result.ShouldContain("Last Edited: 1/15/2023");
    }

    [Fact]
    public void GenerateTemplate_Should_Include_All_Required_Sections()
    {
        // Given
        var parameters = new Dictionary<string, object>();

        // When
        var result = _template.GenerateTemplate(parameters);

        // Then
        // Check for required date fields
        result.ShouldContain("Created:");
        result.ShouldContain("Last Edited:");

        // Check for main heading
        result.ShouldContain("# Table of Contents");

        // Check for required navigation links
        result.ShouldContain("- [Introduction](1b-Intro.md)");
        result.ShouldContain("- [Journal Entry Template](1c-Journal-Entry-Template.md)");
        result.ShouldContain("- [All My Journals](1h-All-My-Journals.md)");

        // Check for example section
        result.ShouldContain("## Example Topic");
        result.ShouldContain("- [example link to content]()");

        // Should end with newline
        result.ShouldEndWith("\n");
    }

    [Fact]
    public void GenerateTemplate_Should_Have_Correct_Structure_And_Format()
    {
        // Given
        var parameters = new Dictionary<string, object>
        {
            ["createdDate"] = "1/1/2023",
            ["lastEditedDate"] = "2/1/2023",
        };

        // When
        var result = _template.GenerateTemplate(parameters);

        // Then
        var lines = result.Split('\n');

        // Check first few lines for correct structure
        lines[0].ShouldBe("Created: 1/1/2023");
        lines[1].ShouldBe("Last Edited: 2/1/2023");
        lines[2].ShouldBe("");
        lines[3].ShouldBe("# Table of Contents");
        lines[4].ShouldBe("- [Introduction](1b-Intro.md)");
        lines[5].ShouldBe("- [Journal Entry Template](1c-Journal-Entry-Template.md)");
        lines[6].ShouldBe("- [All My Journals](1h-All-My-Journals.md)");
        lines[7].ShouldBe("## Example Topic");
        lines[8].ShouldBe("  - [example link to content]()");
        lines[9].ShouldBe("");
        lines[10].ShouldBe("");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void GenerateTemplate_Should_Handle_Empty_And_Whitespace_Date_Values(string dateValue)
    {
        // Given
        var parameters = new Dictionary<string, object>
        {
            ["createdDate"] = dateValue,
            ["lastEditedDate"] = dateValue,
        };

        // When
        var result = _template.GenerateTemplate(parameters);

        // Then
        result.ShouldNotBeNull();
        result.ShouldContain($"Created: {dateValue}");
        result.ShouldContain($"Last Edited: {dateValue}");
    }

    [Fact]
    public void GenerateTemplate_Should_Convert_Non_String_Date_Parameters_To_String()
    {
        // Given
        var parameters = new Dictionary<string, object>
        {
            ["createdDate"] = 123,
            ["lastEditedDate"] = 456.789,
        };

        // When
        var result = _template.GenerateTemplate(parameters);

        // Then
        result.ShouldContain("Created: 123");
        result.ShouldContain("Last Edited: 456.789");
    }

    [Fact]
    public void GenerateTemplate_Should_Handle_Null_Date_Parameter_Values_Gracefully()
    {
        // Given
        var parameters = new Dictionary<string, object>
        {
            ["createdDate"] = null!,
            ["lastEditedDate"] = null!,
        };

        // When & Then
        // The template should not throw when encountering null values
        Should.NotThrow(() => _template.GenerateTemplate(parameters));
    }

    [Fact]
    public void GenerateTemplate_Should_Handle_DateTime_Objects_As_Parameters()
    {
        // Given
        var createdDate = new DateTime(2023, 1, 1);
        var lastEditedDate = new DateTime(2023, 2, 1);
        var parameters = new Dictionary<string, object>
        {
            ["createdDate"] = createdDate,
            ["lastEditedDate"] = lastEditedDate,
        };

        // When
        var result = _template.GenerateTemplate(parameters);

        // Then
        result.ShouldContain($"Created: {createdDate}");
        result.ShouldContain($"Last Edited: {lastEditedDate}");
    }

    [Fact]
    public void GenerateTemplate_Should_Ignore_Unknown_Parameters()
    {
        // Given
        var parameters = new Dictionary<string, object>
        {
            ["unknownParameter"] = "should be ignored",
            ["createdDate"] = "1/1/2023",
        };

        // When
        var result = _template.GenerateTemplate(parameters);

        // Then
        result.ShouldContain("Created: 1/1/2023");
        result.ShouldNotContain("should be ignored");
        result.ShouldContain("# Table of Contents");
    }
}
