using markdown_journal_cli.JournalTemplates;

namespace markdown_journal_cli.Tests.Infrastructure;

public class TestTemplateGenerator : ITemplateGenerator
{
    public string TemplateName { get; }

    private readonly string _output;

    public TestTemplateGenerator(
        string templateName = "test-template",
        string output = "TEST_OUTPUT"
    )
    {
        TemplateName = templateName;
        _output = output;
    }

    public string GenerateTemplate(Dictionary<string, object>? parameters)
    {
        return _output;
    }
}
