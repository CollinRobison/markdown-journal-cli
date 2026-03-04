using markdown_journal_cli.Infrastructure.JournalTemplates;

namespace markdown_journal_cli.Tests.Infrastructure;

/// <summary>
/// A simple test double for <see cref="ITemplateGenerator"/> that returns a fixed output string.
/// Used in unit tests to provide predictable template content without complex template logic.
/// </summary>
public class TestTemplateGenerator : ITemplateGenerator
{
    /// <summary>
    /// Gets the name of this template generator.
    /// </summary>
    public string TemplateName { get; }

    /// <summary>
    /// The fixed output string that this template generator will return.
    /// </summary>
    private readonly string _output;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestTemplateGenerator"/> class.
    /// </summary>
    /// <param name="templateName">The name to use for this template generator. Defaults to "test-template".</param>
    /// <param name="output">The fixed output string to return. Defaults to "TEST_OUTPUT".</param>
    public TestTemplateGenerator(
        string templateName = "test-template",
        string output = "TEST_OUTPUT"
    )
    {
        TemplateName = templateName;
        _output = output;
    }

    /// <summary>
    /// Generates template content by returning the fixed output string, ignoring any parameters.
    /// </summary>
    /// <param name="parameters">Template parameters (ignored in this test implementation).</param>
    /// <returns>The fixed output string specified in the constructor.</returns>
    public string GenerateTemplate(Dictionary<string, object>? parameters)
    {
        return _output;
    }
}
