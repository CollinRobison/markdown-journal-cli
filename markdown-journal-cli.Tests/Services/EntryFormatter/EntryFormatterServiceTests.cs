using markdown_journal_cli.Tests.Infrastructure;
using markdown_journal_cli.Services;
using Shouldly;
using Xunit;

namespace markdown_journal_cli.Tests.Services;

public class EntryFormatterServiceTests : ServiceTestBase
{
    private IEntryFormatterService _formatterService;

    public EntryFormatterServiceTests()
    {
        _formatterService = new EntryFormatterService(JournalSettings);
    }

    // ==========================
    // Tests for AddSpaceSeparators
    // ==========================

    [Fact]
    public void AddSpaceSeparators_Should_FormatCorrectly_When_InputHasSpaces()
    {
        // Given
        string test = "does this actually work";
        // When
        var result = _formatterService.AddSpaceSeparators(test);
        // Then
        result.ShouldBe("does_this_actually_work");
    }

    [Fact]
    public void AddSpaceSeparators_Should_FormatCorrectly_When_InputHasConsecutiveSpaces()
    {
        // Given
        string test = "does this  actually        work";
        // When
        var result = _formatterService.AddSpaceSeparators(test);
        // Then
        result.ShouldBe("does_this_actually_work");
    }

    [Fact]
    public void AddSpaceSeparators_Should_FormatCorrectly_When_InputHasLeadingAndTrailingWhitespace()
    {
        // Given
        string test = " does this actually work   ";
        // When
        var result = _formatterService.AddSpaceSeparators(test);
        // Then
        result.ShouldBe("does_this_actually_work");
    }

    [Fact]
    public void AddSpaceSeparators_Should_ReturnEmpty_When_InputIsEmpty()
    {
        // Given
        string test = "";
        // When
        var result = _formatterService.AddSpaceSeparators(test);
        // Then
        result.ShouldBe("");
    }

    [Fact]
    public void AddSpaceSeparators_Should_ReturnEmpty_When_InputIsNull()
    {
        // Given
        string? test = null;
        // When & Then
        Should.Throw<ArgumentNullException>(() => _formatterService.AddSpaceSeparators(test));
    }

    [Fact]
    public void AddSpaceSeparators_Should_ReturnWordUnchanged_When_InputIsSingleWord()
    {
        // Given
        string test = "word";
        // When
        var result = _formatterService.AddSpaceSeparators(test);
        // Then
        result.ShouldBe("word");
    }

    [Fact]
    public void AddSpaceSeparators_Should_ReturnEmpty_When_InputIsOnlyWhitespace()
    {
        // Given
        string test = "     ";
        // When
        var result = _formatterService.AddSpaceSeparators(test);
        // Then
        result.ShouldBe("");
    }

    [Fact]
    public void AddSpaceSeparators_Should_PreserveExistingUnderscores_When_InputHasUnderscores()
    {
        // Given
        string test = "already_has underscores_here";
        // When
        var result = _formatterService.AddSpaceSeparators(test);
        // Then
        result.ShouldBe("already_has_underscores_here");
    }

    [Fact]
    public void AddSpaceSeparators_Should_TreatTabsAndNewlinesAsSpaces_When_InputHasTabsAndNewlines()
    {
        // Given
        string test = "has\ttabs\nand\rnewlines";
        // When
        var result = _formatterService.AddSpaceSeparators(test);
        // Then
        result.ShouldBe("has_tabs_and_newlines");
    }

    [Fact]
    public void AddSpaceSeparators_Should_PreserveSpecialCharacters_When_InputHasSpecialCharacters()
    {
        // Given
        string test = "test with !@#$ special chars";
        // When
        var result = _formatterService.AddSpaceSeparators(test);
        // Then
        result.ShouldBe("test_with_!@#$_special_chars");
    }

    [Fact]
    public void AddSpaceSeparators_Should_PreserveNumbers_When_InputHasNumbers()
    {
        // Given
        string test = "entry 123 test 456";
        // When
        var result = _formatterService.AddSpaceSeparators(test);
        // Then
        result.ShouldBe("entry_123_test_456");
    }

    [Fact]
    public void AddSpaceSeparators_Should_PreserveUnicodeCharacters_When_InputHasUnicode()
    {
        // Given
        string test = "café résumé naïve";
        // When
        var result = _formatterService.AddSpaceSeparators(test);
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
    public void AddSpaceSeparators_Should_FormatCorrectly_When_GivenInputExpectedOutput(string input, string expected)
    {
        // When
        var result = _formatterService.AddSpaceSeparators(input);
        // Then
        result.ShouldBe(expected);
    }

    [Fact]
    public void AddSpaceSeparators_Handles_Very_Long_String()
    {
        // Given - Very long string (1000 words)
        var longString = string.Join(" ", Enumerable.Repeat("word", 1000));

        // When
        var result = _formatterService.AddSpaceSeparators(longString);

        // Then
        result.Split('_').Length.ShouldBe(1000);
        result.ShouldStartWith("word_");
        result.ShouldEndWith("_word");
    }

    // ==========================
    // Tests for RemoveSpaceSeparators
    // ==========================

    [Fact]
    public void RemoveSpaceSeparators_Should_RemoveSpaceSeparators_When_InputHasSeparators()
    {
        // Given
        string test = "does_this_work";
        // When
        var result = _formatterService.RemoveSpaceSeparators(test);
        // Then
        result.ShouldBe("does this work");
    }

    [Fact]
    public void RemoveSpaceSeparators_Should_RemoveConsecutiveSeparators_When_InputHasConsecutiveSeparators()
    {
        // Given
        string test = "does__this___work_________now";
        // When
        var result = _formatterService.RemoveSpaceSeparators(test);
        // Then
        result.ShouldBe("does this work now");
    }

    [Fact]
    public void RemoveSpaceSeparators_Should_TrimResult_When_InputHasLeadingAndTrailingWhitespace()
    {
        // Given
        string test = " does_this_work      ";
        // When
        var result = _formatterService.RemoveSpaceSeparators(test);
        // Then
        result.ShouldBe("does this work");
    }

    [Fact]
    public void RemoveSpaceSeparators_Should_ReturnEmpty_When_InputIsEmpty()
    {
        // Given
        string test = "";
        // When
        var result = _formatterService.RemoveSpaceSeparators(test);
        // Then
        result.ShouldBe("");
    }

    [Fact]
    public void RemoveSpaceSeparators_Should_ReturnEmpty_When_InputIsNull()
    {
        // Given
        string? test = null;
        // When & Then
        Should.Throw<ArgumentNullException>(() => _formatterService.RemoveSpaceSeparators(test));
    }

    [Fact]
    public void RemoveSpaceSeparators_Should_ReturnWordUnchanged_When_InputIsSingleWord()
    {
        // Given
        string test = "word";
        // When
        var result = _formatterService.RemoveSpaceSeparators(test);
        // Then
        result.ShouldBe("word");
    }

    [Fact]
    public void RemoveSpaceSeparators_Should_ReturnEmpty_When_InputIsOnlyWhitespace()
    {
        // Given
        string test = "     ";
        // When
        var result = _formatterService.RemoveSpaceSeparators(test);
        // Then
        result.ShouldBe("");
    }

    [Fact]
    public void RemoveSpaceSeparators_Should_PreserveSpaces_When_InputHasSpaces()
    {
        // Given
        string test = "already has spaces here";
        // When
        var result = _formatterService.RemoveSpaceSeparators(test);
        // Then
        result.ShouldBe("already has spaces here");
    }

    [Fact]
    public void RemoveSpaceSeparators_Should_HandleTabsAndNewlines_When_InputHasTabsAndNewlines()
    {
        // Given
        string test = "has\ttabs\nand\rnewlines";
        // When
        var result = _formatterService.RemoveSpaceSeparators(test);
        // Then
        result.ShouldBe("has tabs and newlines");
    }

    [Fact]
    public void RemoveSpaceSeparators_Should_PreserveSpecialCharacters_When_InputHasSpecialCharacters()
    {
        // Given
        string test = "test_with_!@#$ special_chars";
        // When
        var result = _formatterService.RemoveSpaceSeparators(test);
        // Then
        result.ShouldBe("test with !@#$ special chars");
    }

    [Fact]
    public void RemoveSpaceSeparators_Should_PreserveNumbers_When_InputHasNumbers()
    {
        // Given
        string test = "entry_123_test_456";
        // When
        var result = _formatterService.RemoveSpaceSeparators(test);
        // Then
        result.ShouldBe("entry 123 test 456");
    }

    [Fact]
    public void RemoveSpaceSeparators_Should_PreserveUnicodeCharacters_When_InputHasUnicode()
    {
        // Given
        string test = "café_résumé_naïve";
        // When
        var result = _formatterService.RemoveSpaceSeparators(test);
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
    public void RemoveSpaceSeparators_Should_ConvertCorrectly_When_GivenInputExpectedOutput(string input, string expected)
    {
        // When
        var result = _formatterService.RemoveSpaceSeparators(input);
        // Then
        result.ShouldBe(expected);
    }

    [Fact]
    public void RemoveSpaceSeparators_Should_ReplaceAllUnderscores_When_InputHasOnlyUnderscores()
    {
        // Given
        string test = "____";
        // When
        var result = _formatterService.RemoveSpaceSeparators(test);
        // Then
        result.ShouldBe("");
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
    public void SeperateSubheadingString_Splits_String_Into_Array_With_Space_Separators()
    {
        // Given
        string test = "heading_1-heading_2-heading_3";
        // When
        var result = _formatterService.SeperateSubheadingString(test);
        // Then
        result.ShouldBe(["heading 1", "heading 2", "heading 3"]);
    }

    [Fact]
    public void SeperateSubheadingString_Splits_String_Into_Array_With_Consecutive_Separators()
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
    public void SeperateSubheadingString_Splits_String_Into_Array_with_whitespace_in_Separators_start_and_ends()
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
        result.ShouldBe(["heading1", "heading 2", "heading3"]);
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
    [InlineData("heading_1-heading_2-heading_3", new[] { "heading 1", "heading 2", "heading 3" })]
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

    [Fact]
    public void SeperateSubheadingString_Handles_Only_Whitespace()
    {
        // Given
        string test = "     ";
        // When
        var result = _formatterService.SeperateSubheadingString(test);
        // Then
        result.ShouldBe([]);
    }

    [Fact]
    public void SeperateSubheadingString_Handles_Single_Character_Headings()
    {
        // Given
        string test = "a-b-c-d";
        // When
        var result = _formatterService.SeperateSubheadingString(test);
        // Then
        result.ShouldBe(["a", "b", "c", "d"]);
    }

    [Fact]
    public void SeperateSubheadingString_Handles_Very_Long_Heading_Names()
    {
        // Given - Very long heading names
        var longHeading = string.Concat(Enumerable.Repeat("VeryLongHeadingName", 50));
        var test = $"{longHeading}-heading2-heading3";

        // When
        var result = _formatterService.SeperateSubheadingString(test);

        // Then
        result.Length.ShouldBe(3);
        result[0].ShouldBe(longHeading);
        result[1].ShouldBe("heading2");
        result[2].ShouldBe("heading3");
    }

    // ==========================
    // Tests for AddHeadingSeparators
    // ==========================

    [Fact]
    public void AddHeadingSeparators_Should_CombineStringsWithSeparator_When_GivenMultipleStrings()
    {
        // Given
        string[] test = ["Heading 1", "Heading_2-Heading_3", "title of journal entry"];
        // When
        var result = _formatterService.AddHeadingSeparators(test);
        // Then
        result.ShouldBe("Heading_1-Heading_2-Heading_3-title_of_journal_entry");
    }

    [Fact]
    public void AddHeadingSeparators_Combines_Strings_With_Separators()
    {
        // Given
        string[] test = ["Heading 1", "Heading_2", "Heading-3"];
        // When
        var result = _formatterService.AddHeadingSeparators(test);
        // Then
        result.ShouldBe("Heading_1-Heading_2-Heading-3");
    }

    [Fact]
    public void AddHeadingSeparators_Handles_Empty_Array()
    {
        // Given
        string[] test = [];
        // When
        var result = _formatterService.AddHeadingSeparators(test);
        // Then
        result.ShouldBe("");
    }

    [Fact]
    public void AddHeadingSeparators_Handles_Null_Input()
    {
        // Given
        string[]? test = null;
        // When & Then
        Should.Throw<ArgumentNullException>(() => _formatterService.AddHeadingSeparators(test));
    }

    [Fact]
    public void AddHeadingSeparators_Trims_Whitespace_From_Sections()
    {
        // Given
        string[] test = ["  Heading 1  ", " Heading_2 ", " Heading-3 "];
        // When
        var result = _formatterService.AddHeadingSeparators(test);
        // Then
        result.ShouldBe("Heading_1-Heading_2-Heading-3");
    }

    [Fact]
    public void AddHeadingSeparators_Handles_Special_Characters()
    {
        // Given
        string[] test = ["Heading!@#$", "Heading%^&*", "Heading()_+"];
        // When
        var result = _formatterService.AddHeadingSeparators(test);
        // Then
        result.ShouldBe("Heading!@#$-Heading%^&*-Heading()_+");
    }

    [Fact]
    public void AddHeadingSeparators_Handles_Unicode_Characters()
    {
        // Given
        string[] test = ["café", "résumé", "naïve"];
        // When
        var result = _formatterService.AddHeadingSeparators(test);
        // Then
        result.ShouldBe("café-résumé-naïve");
    }

    [Fact]
    public void AddHeadingSeparators_Handles_Empty_Strings_In_Array()
    {
        // Given
        string[] test = ["Heading 1", "", "Heading 3"];
        // When
        var result = _formatterService.AddHeadingSeparators(test);
        // Then
        result.ShouldBe("Heading_1-Heading_3");
    }

    [Fact]
    public void AddHeadingSeparators_Handles_Only_Empty_Strings()
    {
        // Given
        string[] test = ["", "", ""];
        // When
        var result = _formatterService.AddHeadingSeparators(test);
        // Then
        result.ShouldBe("");
    }

    [Fact]
    public void AddHeadingSeparators_Handles_Mixed_Separators()
    {
        // Given
        string[] test = ["Heading 1", "Heading_2", "Heading-3"];
        // When
        var result = _formatterService.AddHeadingSeparators(test);
        // Then
        result.ShouldBe("Heading_1-Heading_2-Heading-3");
    }

    [Theory]
    [InlineData(new string[] { "Heading1", "Heading2" }, "Heading1-Heading2")]
    [InlineData(new string[] { "", "Heading2" }, "Heading2")]
    [InlineData(new string[] { "Heading1", "" }, "Heading1")]
    [InlineData(new string[] { "" }, "")]
    [InlineData(new string[] { "Heading1" }, "Heading1")]
    public void AddHeadingSeparators_Theory_Tests(string[] input, string expected)
    {
        // When
        var result = _formatterService.AddHeadingSeparators(input);
        // Then
        result.ShouldBe(expected);
    }

    [Fact]
    public void AddHeadingSeparators_Handles_Only_Whitespace_Strings()
    {
        // Given
        string[] test = ["  ", "   ", "  "];
        // When
        var result = _formatterService.AddHeadingSeparators(test);
        // Then
        result.ShouldBe("");
    }

    [Fact]
    public void AddHeadingSeparators_Handles_Single_Element_With_Spaces()
    {
        // Given
        string[] test = ["Hello World Test"];
        // When
        var result = _formatterService.AddHeadingSeparators(test);
        // Then
        result.ShouldBe("Hello_World_Test");
    }

    [Fact]
    public void AddHeadingSeparators_Handles_Tabs_And_Newlines_In_Sections()
    {
        // Given
        string[] test = ["Hello\tWorld", "Test\nEntry", "Sample\rData"];
        // When
        var result = _formatterService.AddHeadingSeparators(test);
        // Then
        result.ShouldBe("Hello_World-Test_Entry-Sample_Data");
    }

    [Fact]
    public void AddHeadingSeparators_Handles_Large_Array()
    {
        // Given - Large array (100 sections)
        var largeArray = Enumerable.Range(1, 100).Select(i => $"Section {i}").ToArray();

        // When
        var result = _formatterService.AddHeadingSeparators(largeArray);

        // Then
        result.Split('-').Length.ShouldBe(100);
        result.ShouldStartWith("Section_1-");
        result.ShouldEndWith("-Section_100");
    }
}
