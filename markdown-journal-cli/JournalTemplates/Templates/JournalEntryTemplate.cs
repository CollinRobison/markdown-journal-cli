using System;

namespace markdown_journal_cli.JournalTemplates.Templates;

public class JournalEntryTemplate : ITemplateGenerator
{
    public string TemplateName => "journal-entry";

    public string GenerateTemplate(Dictionary<string, object>? parameters)
    {
        var title = parameters?.GetValueOrDefault("title", "Title goes here").ToString();
        var body = parameters?.GetValueOrDefault("body", "body goes here.").ToString();
        var createdDate = parameters?.GetValueOrDefault("createdDate", DateTime.Now.ToString("M/d/yyyy")).ToString();
        var lastEditedDate = parameters?.GetValueOrDefault("lastEditedDate", DateTime.Now.ToString("M/d/yyyy")).ToString(); 
        
        return $@"[Back to Table of Contents](1a-TableOfContents.md)

Created: {createdDate}
Last Edited: {lastEditedDate}

# {title}

{body}

[Make sure to add link to any reference here](add-link)
";
    }
}
