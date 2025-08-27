Based on your CLI project structure, here's a comprehensive C# string interpolation implementation that supports multiple templates:

## Single Template Implementation

First, let's create a simple template generator for your journal entry:

```csharp
public class JournalTemplateGenerator
{
    public string GenerateJournalEntry(string title, string body = "body goes here.")
    {
        var createdDate = DateTime.Now.ToString("M/d/yyyy");
        var lastEditedDate = DateTime.Now.ToString("M/d/yyyy");

        return $@"[Back to Table of Contents](1a-TableOfContents.md)

Created: {createdDate}
Last Edited: {lastEditedDate}

# {title}

{body}

[Make sure to add link to any reference here](add-link)
";
    }
}
```

## Multiple Template Implementation

Here's a more robust implementation that supports multiple template types:

```csharp
public interface ITemplateGenerator
{
    string GenerateTemplate(Dictionary<string, object> parameters);
    string TemplateName { get; }
}

public class JournalEntryTemplate : ITemplateGenerator
{
    public string TemplateName => "journal-entry";

    public string GenerateTemplate(Dictionary<string, object> parameters)
    {
        var title = parameters.GetValueOrDefault("title", "Title goes here").ToString();
        var body = parameters.GetValueOrDefault("body", "body goes here.").ToString();
        var createdDate = DateTime.Now.ToString("M/d/yyyy");
        var lastEditedDate = DateTime.Now.ToString("M/d/yyyy");

        return $@"[Back to Table of Contents](1a-TableOfContents.md)

Created: {createdDate}
Last Edited: {lastEditedDate}

# {title}

{body}

[Make sure to add link to any reference here](add-link)
";
    }
}

public class MeetingNotesTemplate : ITemplateGenerator
{
    public string TemplateName => "meeting-notes";

    public string GenerateTemplate(Dictionary<string, object> parameters)
    {
        var title = parameters.GetValueOrDefault("title", "Meeting Notes").ToString();
        var date = DateTime.Now.ToString("M/d/yyyy");
        var attendees = parameters.GetValueOrDefault("attendees", "").ToString();

        return $@"# {title}

**Date:** {date}
**Attendees:** {attendees}

## Agenda


## Discussion


## Action Items
- [ ] 

## Next Steps

";
    }
}

public class ProjectPlanTemplate : ITemplateGenerator
{
    public string TemplateName => "project-plan";

    public string GenerateTemplate(Dictionary<string, object> parameters)
    {
        var projectName = parameters.GetValueOrDefault("project", "Project Name").ToString();
        var date = DateTime.Now.ToString("M/d/yyyy");

        return $@"# {projectName} - Project Plan

**Created:** {date}
**Last Updated:** {date}

## Overview


## Objectives


## Timeline


## Resources


## Risks & Mitigation


## Success Criteria

";
    }
}
```

## Template Manager

```csharp
public class TemplateManager
{
    private readonly Dictionary<string, ITemplateGenerator> _templates;

    public TemplateManager()
    {
        _templates = new Dictionary<string, ITemplateGenerator>();
        RegisterDefaultTemplates();
    }

    private void RegisterDefaultTemplates()
    {
        RegisterTemplate(new JournalEntryTemplate());
        RegisterTemplate(new MeetingNotesTemplate());
        RegisterTemplate(new ProjectPlanTemplate());
    }

    public void RegisterTemplate(ITemplateGenerator template)
    {
        _templates[template.TemplateName] = template;
    }

    public string GenerateFromTemplate(string templateName, Dictionary<string, object> parameters)
    {
        if (!_templates.ContainsKey(templateName))
        {
            throw new ArgumentException($"Template '{templateName}' not found. Available templates: {string.Join(", ", GetAvailableTemplates())}");
        }

        return _templates[templateName].GenerateTemplate(parameters);
    }

    public IEnumerable<string> GetAvailableTemplates()
    {
        return _templates.Keys;
    }
}
```

## CLI Integration

Update your CLI commands to use the template system:

```csharp
using Spectre.Console.Cli;
using System.ComponentModel;

public class CreateCommand : Command<CreateCommand.Settings>
{
    private readonly TemplateManager _templateManager;

    public CreateCommand()
    {
        _templateManager = new TemplateManager();
    }

    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<template>")]
        [Description("Template type to use")]
        public string Template { get; set; } = "journal-entry";

        [CommandOption("-t|--title")]
        [Description("Title for the document")]
        public string? Title { get; set; }

        [CommandOption("-o|--output")]
        [Description("Output file path")]
        public string? OutputPath { get; set; }

        [CommandOption("--body")]
        [Description("Body content")]
        public string? Body { get; set; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        try
        {
            var parameters = new Dictionary<string, object>();
            
            if (!string.IsNullOrEmpty(settings.Title))
                parameters["title"] = settings.Title;
            
            if (!string.IsNullOrEmpty(settings.Body))
                parameters["body"] = settings.Body;

            var content = _templateManager.GenerateFromTemplate(settings.Template, parameters);
            
            var outputPath = settings.OutputPath ?? $"{settings.Template}-{DateTime.Now:yyyy-MM-dd}.md";
            
            File.WriteAllText(outputPath, content);
            
            Console.WriteLine($"Created {settings.Template} template at: {outputPath}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}

public class ListTemplatesCommand : Command
{
    private readonly TemplateManager _templateManager;

    public ListTemplatesCommand()
    {
        _templateManager = new TemplateManager();
    }

    public override int Execute(CommandContext context)
    {
        Console.WriteLine("Available templates:");
        foreach (var template in _templateManager.GetAvailableTemplates())
        {
            Console.WriteLine($"  - {template}");
        }
        return 0;
    }
}
```

## Update Program.cs

```csharp
using Spectre.Console.Cli;

namespace markdown_journal_cli;

public static class Program
{
    public static int Main(string[] args)
    {
        var app = new CommandApp();
        app.Configure(config =>
        {
            config.SetApplicationName("md-journal");
            config.ValidateExamples();
            
            // Add template commands
            config.AddCommand<CreateCommand>("create")
                .WithDescription("Create a new document from template");
            config.AddCommand<ListTemplatesCommand>("templates")
                .WithDescription("List available templates");
                
            // Your existing commands
            config.AddCommand<RunCommand>("run");
        });

        return app.Run(args);
    }
}
```

## Usage Examples

```bash
# Create journal entry
md-journal create journal-entry --title "My Daily Journal" --output "2024-08-11-journal.md"

# Create meeting notes
md-journal create meeting-notes --title "Weekly Standup" --attendees "John, Jane, Bob"

# List available templates
md-journal templates
```

This implementation gives you:
- Clean separation of templates
- Easy extensibility for new templates
- Type safety with interfaces
- Integration with your existing Spectre.Console.Cli setup
- Zero external dependencies
- Simple string interpolation approach