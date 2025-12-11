using System;
using Microsoft.Extensions.Options;

namespace markdown_journal_cli.Services;

public class EntryFormatterService(
    IOptions<JournalSettings> journalSettings
) : IEntryFormatterService
{ //TODO add logic and add tests to test file
    private readonly JournalSettings _journalSettings = journalSettings.Value;

    public string AddSectionSeperators(string[] sections)
    {
        return "hi";
    }

    public string AddSpaceSeperators(string input)
    {
        throw new NotImplementedException();
    }

    public string RemoveSpaceSeperators(string input)
    {
        throw new NotImplementedException();
    }

    public string[] SeperateSubheadingString(string subheadings)
    {
        throw new NotImplementedException();
    }
}
