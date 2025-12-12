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
        _journalSettings = Options.Create(new JournalSettings
        {
            TitleSpaceSeperator = "_",
            HeadingSeperator = "-"
        });
        _formatterService = new EntryFormatterService(_journalSettings);
    }

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
    public void AddSpaceSeperators_formats_correctly_with_multiple_spaces_between_words()
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
        string test = null;
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
}
