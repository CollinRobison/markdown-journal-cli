using System;
using Microsoft.Extensions.Options;

namespace markdown_journal_cli.Infrastructure.JournalTemplates.Templates;

public class TableOfContentsTemplate : ITemplateGenerator
{
    private readonly JournalSettings _journalSettings;
    public string TemplateName => "table-of-contents";

    public TableOfContentsTemplate(IOptions<JournalSettings> journalSettings)
    {
        _journalSettings = journalSettings.Value;
    }

    public string GenerateTemplate(Dictionary<string, object>? parameters)
    {
        var createdDate = (
            parameters?.GetValueOrDefault("createdDate", DateTime.Now.ToString("M/d/yyyy"))
            ?? DateTime.Now.ToString("M/d/yyyy")
        ).ToString();
        var lastEditedDate = (
            parameters?.GetValueOrDefault("lastEditedDate", DateTime.Now.ToString("M/d/yyyy"))
            ?? DateTime.Now.ToString("M/d/yyyy")
        ).ToString();

        return $@"Created: {createdDate}
Last Edited: {lastEditedDate}

# Table of Contents
- [{_journalSettings.IntroductionTitle}]({_journalSettings.IntroductionFileName}{FileConstants.MarkdownExtension})
- [{_journalSettings.JournalEntryTemplateTitle}]({_journalSettings.JournalEntryTemplateFileName})
- [{_journalSettings.AllJournalsTitle}]({_journalSettings.AllJournalsFileName})
## Example Topic
  - [example link to content]()

";
    }
}
