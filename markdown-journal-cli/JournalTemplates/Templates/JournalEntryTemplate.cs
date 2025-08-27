using System;

namespace markdown_journal_cli.JournalTemplates.Templates;

public class JournalEntryTemplate : ITemplateGenerator
{
    public string TemplateName => "journal-entry";

    public string GenerateTemplate(Dictionary<string, object>? parameters)
    {
        var title = (parameters?.GetValueOrDefault("title", "Title goes here") ?? "Title goes here").ToString();
        var body = (parameters?.GetValueOrDefault("body", "body goes here.") ?? "body goes here.").ToString();
        var addSourceBlockValue = parameters?.GetValueOrDefault("addSourceBlock", true) ?? true;
        var addSourceBlock = ConvertToBoolean(addSourceBlockValue);
        var sources = !addSourceBlock
            ? ""
            : (parameters
                ?.GetValueOrDefault(
                    "sources",
                    "[Make sure to add link to any reference here](add-link)"
                ) ?? "[Make sure to add link to any reference here](add-link)")
                .ToString();
        var createdDate = (parameters
            ?.GetValueOrDefault("createdDate", DateTime.Now.ToString("M/d/yyyy"))
            ?? DateTime.Now.ToString("M/d/yyyy"))
            .ToString();
        var lastEditedDate = (parameters
            ?.GetValueOrDefault("lastEditedDate", DateTime.Now.ToString("M/d/yyyy"))
            ?? DateTime.Now.ToString("M/d/yyyy"))
            .ToString();

        return $@"[Back to Table of Contents](1a-TableOfContents.md)

Created: {createdDate}
Last Edited: {lastEditedDate}

# {title}

{body}

{sources}
";
    }

    private static bool ConvertToBoolean(object value)
    {
        return value switch
        {
            bool b => b,
            string s => !IsFalseString(s),
            int i => i != 0,
            _ => true
        };
    }

    private static bool IsFalseString(string s)
    {
        // Common false representations
        var falseStrings = new[] { "false", "0", "no", "off", "n", "f" };
        return Array.Exists(falseStrings, fs => string.Equals(s.Trim(), fs, StringComparison.OrdinalIgnoreCase));
    }
}
