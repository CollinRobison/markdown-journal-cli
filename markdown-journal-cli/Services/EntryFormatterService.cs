using System;
using System.Text.RegularExpressions;
using markdown_journal_cli.JournalTemplates;
using Microsoft.Extensions.Options;

namespace markdown_journal_cli.Services;

public partial class EntryFormatterService(
    IOptions<JournalSettings> journalSettings
) : IEntryFormatterService
{
    private readonly JournalSettings _journalSettings = journalSettings.Value;
    
    public string AddSpaceSeperators(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return MatchWhiteSpaceInstancesRegex().Replace(input.Trim(), _journalSettings.TitleSpaceSeperator);
    }
    
    public string RemoveSpaceSeperators(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return input.Trim().Replace(_journalSettings.TitleSpaceSeperator, " ");
    }

    public string[] SeperateSubheadingString(string subheadings)
    {
        ArgumentNullException.ThrowIfNull(subheadings);
        return subheadings.Trim().Split(_journalSettings.HeadingSeperator);
    }

    public string AddHeadingSeperators(string[] sections)
    {
        ArgumentNullException.ThrowIfNull(sections);
        return AddSpaceSeperators(string.Join(_journalSettings.HeadingSeperator, sections.Select(s => s.Trim()).ToArray()));
    }
    
    
    [GeneratedRegex(@"\s+")]
    private static partial Regex MatchWhiteSpaceInstancesRegex();
}
