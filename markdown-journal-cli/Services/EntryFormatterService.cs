using System;
using System.Text.RegularExpressions;
using markdown_journal_cli.Services;
using Microsoft.Extensions.Options;

namespace markdown_journal_cli.Services;

public partial class EntryFormatterService(IOptions<JournalSettings> journalSettings)
    : IEntryFormatterService
{
    private readonly JournalSettings _journalSettings = journalSettings.Value;

    public string AddSpaceSeparators(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return MatchWhiteSpaceInstancesRegex()
            .Replace(input.Trim(), _journalSettings.TitleSpaceSeparator);
    }

    public string RemoveSpaceSeparators(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return MatchWhiteSpaceInstancesRegex()
            .Replace(MatchTitleSeparatorRegex().Replace(input, " ").Trim(), " ");
    }

    public string[] SeperateSubheadingString(string subheadings)
    {
        ArgumentNullException.ThrowIfNull(subheadings);
        return MatchHeadingSeparatorRegex()
            .Replace(subheadings.Trim(), _journalSettings.HeadingSeparator)
            .Split(_journalSettings.HeadingSeparator, StringSplitOptions.RemoveEmptyEntries) // Exclude empty entries
            .Select(s => RemoveSpaceSeparators(s.Trim()))
            .ToArray();
    }

    public string AddHeadingSeparators(string[] sections)
    {
        ArgumentNullException.ThrowIfNull(sections);

        // Filter out empty or whitespace-only strings
        var filteredSections = sections
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => AddSpaceSeparators(s.Trim()));

        // If no valid sections remain, return an empty string
        if (!filteredSections.Any())
        {
            return string.Empty;
        }

        // Join the filtered sections with the heading separator
        return string.Join(_journalSettings.HeadingSeparator, filteredSections);
    }

    private Regex MatchTitleSeparatorRegex()
    {
        var title = Regex.Escape(_journalSettings.TitleSpaceSeparator);
        var pattern = $"{title}+";
        return new Regex(pattern, RegexOptions.Compiled);
    }

    private Regex MatchHeadingSeparatorRegex()
    {
        var heading = Regex.Escape(_journalSettings.HeadingSeparator);
        var pattern = $"{heading}+";
        return new Regex(pattern, RegexOptions.Compiled);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex MatchWhiteSpaceInstancesRegex();
}
