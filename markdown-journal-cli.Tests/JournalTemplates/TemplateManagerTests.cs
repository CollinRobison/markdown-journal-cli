using markdown_journal_cli.JournalTemplates;
using markdown_journal_cli.Tests.Infrastructure;
using Shouldly;
using Xunit;

namespace markdown_journal_cli.Tests.JournalTemplates;

public class TemplateManagerTests
{
    [Fact]
    public void GenerateFromTemplate_Returns_Registered_Template_Output()
    {
        // Arrange
        var manager = new markdown_journal_cli.JournalTemplates.TemplateManager();
        var fake = new TestTemplateGenerator("x-test", "EXPECTED");
        manager.RegisterTemplate(fake);

        // Act
        var result = manager.GenerateFromTemplate("x-test", new Dictionary<string, object>());

        // Assert
        result.ShouldBe("EXPECTED");
        manager.GetAvailableTemplates().ShouldContain("x-test");
    }
}
