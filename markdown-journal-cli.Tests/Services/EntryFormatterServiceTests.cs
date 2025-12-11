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
            TitleSpaceSeperator = "-",
            HeadingSeperator = "_"
        });
        _formatterService = new EntryFormatterService(_journalSettings);
    }

    [Fact]
    public void AddSectionSeperators_test1()
    {
        // Given
        var test = _formatterService.AddSectionSeperators(["1"]);
        // When

        // Then
        test.ShouldBe("hi");
    }
}
