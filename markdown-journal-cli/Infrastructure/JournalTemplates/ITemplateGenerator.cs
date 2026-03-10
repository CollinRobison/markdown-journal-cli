namespace markdown_journal_cli.Infrastructure.JournalTemplates;

/// <summary>
/// Defines a contract for generating journal entry templates using string interpolation.
/// </summary>
public interface ITemplateGenerator
{
    /// <summary>
    /// Generates the body of the journal template using the provided parameters.
    /// </summary>
    /// <param name="parameters">A dictionary of parameters to customize the template.</param>
    /// <returns>The generated journal template as a string.</returns>
    string GenerateTemplate(Dictionary<string, object>? parameters);

    /// <summary>
    /// Gets the name of the template.
    /// </summary>
    string TemplateName { get; }
}
