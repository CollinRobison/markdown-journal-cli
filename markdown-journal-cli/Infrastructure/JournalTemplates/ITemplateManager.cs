namespace markdown_journal_cli.Infrastructure.JournalTemplates;

/// <summary>
/// Contract for a template manager that can register template generators
/// and produce content from named templates.
/// </summary>
public interface ITemplateManager
{
    /// <summary>
    /// Registers a template generator. If a template with the same name already exists,
    /// it will be replaced.
    /// </summary>
    /// <param name="template">The template generator to register.</param>
    void RegisterTemplate(ITemplateGenerator template);

    /// <summary>
    /// Generates content using the specified template name and parameters.
    /// </summary>
    /// <param name="templateName">The name of the template to use.</param>
    /// <param name="parameters">Parameters used by the template generator.</param>
    /// <returns>The generated content.</returns>
    string GenerateFromTemplate(string templateName, Dictionary<string, object>? parameters);

    /// <summary>
    /// Returns the names of available templates.
    /// </summary>
    IEnumerable<string> GetAvailableTemplates();
}
