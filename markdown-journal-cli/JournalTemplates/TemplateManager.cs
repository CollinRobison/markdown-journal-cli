using markdown_journal_cli.JournalTemplates;
using markdown_journal_cli.JournalTemplates.Templates;

namespace markdown_journal_cli.JournalTemplates;

/// <summary>
/// Manages available template generators and provides helper methods to
/// register templates and generate content from a named template.
/// </summary>
public class TemplateManager : ITemplateManager
{
    /// <summary>
    /// Stores registered templates keyed by their name.
    /// </summary>
    private readonly Dictionary<string, ITemplateGenerator> _templates;

    /// <summary>
    /// Initializes a new instance of the <see cref="TemplateManager"/> class
    /// and registers default templates.
    /// </summary>
    public TemplateManager()
    {
        _templates = new Dictionary<string, ITemplateGenerator>();
        RegisterDefaultTemplates();
    }

    /// <summary>
    /// Registers the set of default templates supported by the application.
    /// </summary>
    private void RegisterDefaultTemplates()
    {
        RegisterTemplate(new JournalEntryTemplate());
        RegisterTemplate(new TableOfContentsTemplate());
    }

    /// <summary>
    /// Registers a template generator. If a template with the same name already
    /// exists, it will be replaced with the supplied implementation.
    /// </summary>
    /// <param name="template">The template generator to register.</param>
    public void RegisterTemplate(ITemplateGenerator template)
    {
        _templates[template.TemplateName] = template;
    }

    /// <summary>
    /// Generates content using the specified template and parameters.
    /// </summary>
    /// <param name="templateName">The name of the template to use.</param>
    /// <param name="parameters">Template parameters used to customize the output.</param>
    /// <returns>The generated content.</returns>
    /// <exception cref="ArgumentException">Thrown when the specified template name is not registered.</exception>
    public string GenerateFromTemplate(string templateName, Dictionary<string, object>? parameters)
    {
        if (!_templates.ContainsKey(templateName))
        {
            throw new ArgumentException(
                $"Template '{templateName}' not found. Available templates: {string.Join(", ", GetAvailableTemplates())}"
            );
        }

        return _templates[templateName].GenerateTemplate(parameters);
    }

    /// <summary>
    /// Returns the set of registered template names.
    /// </summary>
    /// <returns>An enumerable of available template names.</returns>
    public IEnumerable<string> GetAvailableTemplates()
    {
        return _templates.Keys;
    }
}
