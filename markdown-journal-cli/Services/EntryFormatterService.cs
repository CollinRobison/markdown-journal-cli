using System;
using System.Text.RegularExpressions;
using markdown_journal_cli.JournalTemplates;
using Microsoft.Extensions.Options;

namespace markdown_journal_cli.Services;

public partial class EntryFormatterService(IOptions<JournalSettings> journalSettings)
    : IEntryFormatterService
{
    private readonly JournalSettings _journalSettings = journalSettings.Value;

    public string AddSpaceSeperators(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return MatchWhiteSpaceInstancesRegex()
            .Replace(input.Trim(), _journalSettings.TitleSpaceSeperator);
    }

    public string RemoveSpaceSeperators(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return MatchWhiteSpaceInstancesRegex()
            .Replace(MatchTitleSeparatorRegex().Replace(input, " ").Trim(), " ");
    }

    public string[] SeperateSubheadingString(string subheadings)
    {
        ArgumentNullException.ThrowIfNull(subheadings);
        return MatchHeadingSeparatorRegex()
            .Replace(subheadings.Trim(), _journalSettings.HeadingSeperator)
            .Split(_journalSettings.HeadingSeperator, StringSplitOptions.RemoveEmptyEntries) // Exclude empty entries
            .Select(s => s.Trim())
            .ToArray();
    }

    public string AddHeadingSeperators(string[] sections)
    {
        ArgumentNullException.ThrowIfNull(sections);

        // Filter out empty or whitespace-only strings
        var filteredSections = sections
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => AddSpaceSeperators(s.Trim()));

        // If no valid sections remain, return an empty string
        if (!filteredSections.Any())
        {
            return string.Empty;
        }

        // Join the filtered sections with the heading separator
        return string.Join(_journalSettings.HeadingSeperator, filteredSections);
    }

    private Regex MatchTitleSeparatorRegex()
    {
        var title = Regex.Escape(_journalSettings.TitleSpaceSeperator);
        var pattern = $"{title}+";
        return new Regex(pattern, RegexOptions.Compiled);
    }

    private Regex MatchHeadingSeparatorRegex()
    {
        var heading = Regex.Escape(_journalSettings.HeadingSeperator);
        var pattern = $"{heading}+";
        return new Regex(pattern, RegexOptions.Compiled);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex MatchWhiteSpaceInstancesRegex();
}
