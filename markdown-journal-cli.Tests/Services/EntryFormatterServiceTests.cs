using System;
using markdown_journal_cli.Services;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace markdown_journal_cli.Tests.Services;

public class EntryFormatterServiceTests
{
    private readonly IOptions<JournalSettings> _journalSettings;
    private IEntryFormatterService _formatterService;

    public EntryFormatterServiceTests()
    {
        _journalSettings = Options.Create(
            new JournalSettings { TitleSpaceSeperator = "_", HeadingSeperator = "-" }
        );
        _formatterService = new EntryFormatterService(_journalSettings);
    }

    // ==========================
    // Tests for AddSpaceSeperators
    // ==========================

    [Fact]
    public void AddSpaceSeperators_formats_correctly()
    {
        // Given
        string test = "does this actually work";
        // When
        var result = _formatterService.AddSpaceSeperators(test);
        // Then
        result.ShouldBe("does_this_actually_work");
    }

    [Fact]
    public void AddSpaceSeperators_formats_correctly_with_consecutive_spaces()
    {
        // Given
        string test = "does this  actually        work";
        // When
        var result = _formatterService.AddSpaceSeperators(test);
        // Then
        result.ShouldBe("does_this_actually_work");
    }

    [Fact]
    public void AddSpaceSeperators_formats_correctly_with_whitespace_at_start_and_end()
    {
        // Given
        string test = " does this actually work   ";
        // When
        var result = _formatterService.AddSpaceSeperators(test);
        // Then
        result.ShouldBe("does_this_actually_work");
    }

    [Fact]
    public void AddSpaceSeperators_handles_empty_string()
    {
        // Given
        string test = "";
        // When
        var result = _formatterService.AddSpaceSeperators(test);
        // Then
        result.ShouldBe("");
    }

    [Fact]
    public void AddSpaceSeperators_handles_null_input()
    {
        // Given
        string? test = null;
        // When & Then
        Should.Throw<ArgumentNullException>(() => _formatterService.AddSpaceSeperators(test));
    }

    [Fact]
    public void AddSpaceSeperators_handles_single_word()
    {
        // Given
        string test = "word";
        // When
        var result = _formatterService.AddSpaceSeperators(test);
        // Then
        result.ShouldBe("word");
    }

    [Fact]
    public void AddSpaceSeperators_handles_only_whitespace()
    {
        // Given
        string test = "     ";
        // When
        var result = _formatterService.AddSpaceSeperators(test);
        // Then
        result.ShouldBe("");
    }

    [Fact]
    public void AddSpaceSeperators_preserves_existing_underscores()
    {
        // Given
        string test = "already_has underscores_here";
        // When
        var result = _formatterService.AddSpaceSeperators(test);
        // Then
        result.ShouldBe("already_has_underscores_here");
    }

    [Fact]
    public void AddSpaceSeperators_handles_tabs_and_newlines()
    {
        // Given
        string test = "has\ttabs\nand\rnewlines";
        // When
        var result = _formatterService.AddSpaceSeperators(test);
        // Then
        result.ShouldBe("has_tabs_and_newlines");
    }

    [Fact]
    public void AddSpaceSeperators_handles_special_characters()
    {
        // Given
        string test = "test with !@#$ special chars";
        // When
        var result = _formatterService.AddSpaceSeperators(test);
        // Then
        result.ShouldBe("test_with_!@#$_special_chars");
    }

    [Fact]
    public void AddSpaceSeperators_handles_numbers()
    {
        // Given
        string test = "entry 123 test 456";
        // When
        var result = _formatterService.AddSpaceSeperators(test);
        // Then
        result.ShouldBe("entry_123_test_456");
    }

    [Fact]
    public void AddSpaceSeperators_handles_unicode_characters()
    {
        // Given
        string test = "café résumé naïve";
        // When
        var result = _formatterService.AddSpaceSeperators(test);
        // Then
        result.ShouldBe("café_résumé_naïve");
    }

    [Theory]
    [InlineData("one space", "one_space")]
    [InlineData("two  spaces", "two_spaces")]
    [InlineData("three   spaces", "three_spaces")]
    [InlineData("  leading", "leading")]
    [InlineData("trailing  ", "trailing")]
    [InlineData("a", "a")]
    [InlineData("a b", "a_b")]
    public void AddSpaceSeperators_theory_test(string input, string expected)
    {
        // When
        var result = _formatterService.AddSpaceSeperators(input);
        // Then
        result.ShouldBe(expected);
    }

    // ==========================
    // Tests for RemoveSpaceSeperators
    // ==========================

    [Fact]
    public void RemoveSpaceSeperators_removes_space_seperators()
    {
        // Given
        string test = "does_this_work";
        // When
        var result = _formatterService.RemoveSpaceSeperators(test);
        // Then
        result.ShouldBe("does this work");
    }

    [Fact]
    public void RemoveSpaceSeperators_removes_space_seperators_with_consecutive_seperators()
    {
        // Given
        string test = "does__this___work_________now";
        // When
        var result = _formatterService.RemoveSpaceSeperators(test);
        // Then
        result.ShouldBe("does this work now");
    }

    [Fact]
    public void RemoveSpaceSeperators_removes_space_seperators_with_whitespace_at_start_and_end()
    {
        // Given
        string test = " does_this_work      ";
        // When
        var result = _formatterService.RemoveSpaceSeperators(test);
        // Then
        result.ShouldBe("does this work");
    }

    [Fact]
    public void RemoveSpaceSeperators_handles_empty_string()
    {
        // Given
        string test = "";
        // When
        var result = _formatterService.RemoveSpaceSeperators(test);
        // Then
        result.ShouldBe("");
    }

    [Fact]
    public void RemoveSpaceSeperators_handles_null_input()
    {
        // Given
        string? test = null;
        // When & Then
        Should.Throw<ArgumentNullException>(() => _formatterService.RemoveSpaceSeperators(test));
    }

    [Fact]
    public void RemoveSpaceSeperators_handles_single_word()
    {
        // Given
        string test = "word";
        // When
        var result = _formatterService.RemoveSpaceSeperators(test);
        // Then
        result.ShouldBe("word");
    }

    [Fact]
    public void RemoveSpaceSeperators_handles_only_whitespace()
    {
        // Given
        string test = "     ";
        // When
        var result = _formatterService.RemoveSpaceSeperators(test);
        // Then
        result.ShouldBe("");
    }

    [Fact]
    public void RemoveSpaceSeperators_preserves_existing_spaces()
    {
        // Given
        string test = "already has spaces here";
        // When
        var result = _formatterService.RemoveSpaceSeperators(test);
        // Then
        result.ShouldBe("already has spaces here");
    }

    [Fact]
    public void RemoveSpaceSeperators_handles_tabs_and_newlines()
    {
        // Given
        string test = "has\ttabs\nand\rnewlines";
        // When
        var result = _formatterService.RemoveSpaceSeperators(test);
        // Then
        result.ShouldBe("has tabs and newlines");
    }

    [Fact]
    public void RemoveSpaceSeperators_handles_special_characters()
    {
        // Given
        string test = "test_with_!@#$ special_chars";
        // When
        var result = _formatterService.RemoveSpaceSeperators(test);
        // Then
        result.ShouldBe("test with !@#$ special chars");
    }

    [Fact]
    public void RemoveSpaceSeperators_handles_numbers()
    {
        // Given
        string test = "entry_123_test_456";
        // When
        var result = _formatterService.RemoveSpaceSeperators(test);
        // Then
        result.ShouldBe("entry 123 test 456");
    }

    [Fact]
    public void RemoveSpaceSeperators_handles_unicode_characters()
    {
        // Given
        string test = "café_résumé_naïve";
        // When
        var result = _formatterService.RemoveSpaceSeperators(test);
        // Then
        result.ShouldBe("café résumé naïve");
    }

    [Theory]
    [InlineData("one_space", "one space")]
    [InlineData("two__spaces", "two spaces")]
    [InlineData("three___spaces", "three spaces")]
    [InlineData("__leading", "leading")]
    [InlineData("trailing__", "trailing")]
    [InlineData("a", "a")]
    [InlineData("a_b", "a b")]
    public void RemoveSpaceSeperators_theory_test(string input, string expected)
    {
        // When
        var result = _formatterService.RemoveSpaceSeperators(input);
        // Then
        result.ShouldBe(expected);
    }

    // ==========================
    // Tests for SeperateSubheadingString
    // ==========================

    [Fact]
    public void SeperateSubheadingString_Splits_String_Into_Array()
    {
        // Given
        string test = "heading1-heading2-heading3";
        // When
        var result = _formatterService.SeperateSubheadingString(test);
        // Then
        result.ShouldBe(["heading1", "heading2", "heading3"]);
    }

    [Fact]
    public void SeperateSubheadingString_Splits_String_Into_Array_With_Space_Seperators()
    {
        // Given
        string test = "heading_1-heading_2-heading_3";
        // When
        var result = _formatterService.SeperateSubheadingString(test);
        // Then
        result.ShouldBe(["heading_1", "heading_2", "heading_3"]);
    }

    [Fact]
    public void SeperateSubheadingString_Splits_String_Into_Array_With_Consecutive_Seperators()
    {
        // Given
        string test = "heading1--heading2---heading3-----Heading4";
        // When
        var result = _formatterService.SeperateSubheadingString(test);
        // Then
        result.ShouldBe(["heading1", "heading2", "heading3", "Heading4"]);
    }

    [Fact]
    public void SeperateSubheadingString_Splits_String_Into_Array_with_whitespace_at_start_and_end()
    {
        // Given
        string test = "   heading1-heading2-heading3 ";
        // When
        var result = _formatterService.SeperateSubheadingString(test);
        // Then
        result.ShouldBe(["heading1", "heading2", "heading3"]);
    }

    [Fact]
    public void SeperateSubheadingString_Splits_String_Into_Array_with_whitespace_in_seperators_start_and_ends()
    {
        // Given
        string test = "heading1-  heading2  - heading3";
        // When
        var result = _formatterService.SeperateSubheadingString(test);
        // Then
        result.ShouldBe(["heading1", "heading2", "heading3"]);
    }

    [Fact]
    public void SeperateSubheadingString_Handles_Empty_String()
    {
        // Given
        string test = "";
        // When
        var result = _formatterService.SeperateSubheadingString(test);
        // Then
        result.ShouldBe([]);
    }

    [Fact]
    public void SeperateSubheadingString_Throws_On_Null_Input()
    {
        // Given
        string? test = null;
        // When & Then
        Should.Throw<ArgumentNullException>(() => _formatterService.SeperateSubheadingString(test));
    }

    [Fact]
    public void SeperateSubheadingString_Handles_No_Separators()
    {
        // Given
        string test = "heading1";
        // When
        var result = _formatterService.SeperateSubheadingString(test);
        // Then
        result.ShouldBe(["heading1"]);
    }

    [Fact]
    public void SeperateSubheadingString_Handles_Only_Separators()
    {
        // Given
        string test = "---";
        // When
        var result = _formatterService.SeperateSubheadingString(test);
        // Then
        result.ShouldBe([]);
    }

    [Fact]
    public void SeperateSubheadingString_Handles_Mixed_Separators()
    {
        // Given
        string test = "heading1-heading_2--heading3";
        // When
        var result = _formatterService.SeperateSubheadingString(test);
        // Then
        result.ShouldBe(["heading1", "heading_2", "heading3"]);
    }

    [Fact]
    public void SeperateSubheadingString_Handles_Special_Characters()
    {
        // Given
        string test = "heading1-!@#$-heading2";
        // When
        var result = _formatterService.SeperateSubheadingString(test);
        // Then
        result.ShouldBe(["heading1", "!@#$", "heading2"]);
    }

    [Fact]
    public void SeperateSubheadingString_Handles_Unicode_Characters()
    {
        // Given
        string test = "café-résumé-naïve";
        // When
        var result = _formatterService.SeperateSubheadingString(test);
        // Then
        result.ShouldBe(["café", "résumé", "naïve"]);
    }

    [Theory]
    [InlineData("heading1-heading2-heading3", new[] { "heading1", "heading2", "heading3" })]
    [InlineData("heading_1-heading_2-heading_3", new[] { "heading_1", "heading_2", "heading_3" })]
    [InlineData(
        "heading1--heading2---heading3-----Heading4",
        new[] { "heading1", "heading2", "heading3", "Heading4" }
    )]
    [InlineData("   heading1-heading2-heading3 ", new[] { "heading1", "heading2", "heading3" })]
    [InlineData("heading1-  heading2  - heading3", new[] { "heading1", "heading2", "heading3" })]
    public void SeperateSubheadingString_theory_test(string input, string[] expected)
    {
        // When
        var result = _formatterService.SeperateSubheadingString(input);
        // Then
        result.ShouldBe(expected);
    }

    // ==========================
    // Tests for AddHeadingSeperators
    // ==========================

    [Fact]
    public void AddHeadingSeperators_combines_heading_strings()
    {
        // Given
        string[] test = ["Heading 1", "Heading_2-Heading_3", "title of journal entry"];
        // When
        var result = _formatterService.AddHeadingSeperators(test);
        // Then
        result.ShouldBe("Heading_1-Heading_2-Heading_3-title_of_journal_entry");
    }

    [Fact]
    public void AddHeadingSeperators_Combines_Strings_With_Separators()
    {
        // Given
        string[] test = ["Heading 1", "Heading_2", "Heading-3"];
        // When
        var result = _formatterService.AddHeadingSeperators(test);
        // Then
        result.ShouldBe("Heading_1-Heading_2-Heading-3");
    }

    [Fact]
    public void AddHeadingSeperators_Handles_Empty_Array()
    {
        // Given
        string[] test = [];
        // When
        var result = _formatterService.AddHeadingSeperators(test);
        // Then
        result.ShouldBe("");
    }

    [Fact]
    public void AddHeadingSeperators_Handles_Null_Input()
    {
        // Given
        string[]? test = null;
        // When & Then
        Should.Throw<ArgumentNullException>(() => _formatterService.AddHeadingSeperators(test));
    }

    [Fact]
    public void AddHeadingSeperators_Trims_Whitespace_From_Sections()
    {
        // Given
        string[] test = ["  Heading 1  ", " Heading_2 ", " Heading-3 "];
        // When
        var result = _formatterService.AddHeadingSeperators(test);
        // Then
        result.ShouldBe("Heading_1-Heading_2-Heading-3");
    }

    [Fact]
    public void AddHeadingSeperators_Handles_Special_Characters()
    {
        // Given
        string[] test = ["Heading!@#$", "Heading%^&*", "Heading()_+"];
        // When
        var result = _formatterService.AddHeadingSeperators(test);
        // Then
        result.ShouldBe("Heading!@#$-Heading%^&*-Heading()_+");
    }

    [Fact]
    public void AddHeadingSeperators_Handles_Unicode_Characters()
    {
        // Given
        string[] test = ["café", "résumé", "naïve"];
        // When
        var result = _formatterService.AddHeadingSeperators(test);
        // Then
        result.ShouldBe("café-résumé-naïve");
    }

    [Fact]
    public void AddHeadingSeperators_Handles_Empty_Strings_In_Array()
    {
        // Given
        string[] test = ["Heading 1", "", "Heading 3"];
        // When
        var result = _formatterService.AddHeadingSeperators(test);
        // Then
        result.ShouldBe("Heading_1-Heading_3");
    }

    [Fact]
    public void AddHeadingSeperators_Handles_Only_Empty_Strings()
    {
        // Given
        string[] test = ["", "", ""];
        // When
        var result = _formatterService.AddHeadingSeperators(test);
        // Then
        result.ShouldBe("");
    }

    [Fact]
    public void AddHeadingSeperators_Handles_Mixed_Separators()
    {
        // Given
        string[] test = ["Heading 1", "Heading_2", "Heading-3"];
        // When
        var result = _formatterService.AddHeadingSeperators(test);
        // Then
        result.ShouldBe("Heading_1-Heading_2-Heading-3");
    }

    [Theory]
    [InlineData(new string[] { "Heading1", "Heading2" }, "Heading1-Heading2")]
    [InlineData(new string[] { "", "Heading2" }, "Heading2")]
    [InlineData(new string[] { "Heading1", "" }, "Heading1")]
    [InlineData(new string[] { "" }, "")]
    [InlineData(new string[] { "Heading1" }, "Heading1")]
    public void AddHeadingSeperators_Theory_Tests(string[] input, string expected)
    {
        // When
        var result = _formatterService.AddHeadingSeperators(input);
        // Then
        result.ShouldBe(expected);
    }
}
