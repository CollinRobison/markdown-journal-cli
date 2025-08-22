using System;

namespace markdown_journal_cli.JournalTemplates.Templates;

public class TableOfContentsTemplate : ITemplateGenerator
{
    public string TemplateName => "table-of-contents";

    public string GenerateTemplate(Dictionary<string, object>? parameters)
    {
        var createdDate = parameters?.GetValueOrDefault("createdDate", DateTime.Now.ToString("M/d/yyyy")).ToString();
        var lastEditedDate = parameters?.GetValueOrDefault("lastEditedDate", DateTime.Now.ToString("M/d/yyyy")).ToString();

        return $@"Created: {createdDate}
Last Edited: {lastEditedDate}

# Table of Contents
- [Introduction](1b-Intro.md)
- [Journal Entry Template](1c-Journal-Entry-Template.md)
- [All My Journals](1h-All-My-Journals.md)
## Example Topic
  - [example link to content]()

";
    }
}
