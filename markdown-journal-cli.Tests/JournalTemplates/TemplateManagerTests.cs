using markdown_journal_cli.JournalTemplates;
using markdown_journal_cli.Tests.Infrastructure;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace markdown_journal_cli.Tests.JournalTemplates;

/// <summary>
/// Unit tests for the <see cref="TemplateManager"/> class, covering template registration
/// and content generation functionality.
/// </summary>
public class TemplateManagerTests
{
    private readonly IOptions<JournalSettings> _journalSettings;

    public TemplateManagerTests()
    {
        _journalSettings = Options.Create(
            new JournalSettings
            {
                AppName = "md-journal",
                JournalConfigFileName = ".journalrc",
                DefaultJournalName = "MyJournal",
                TableOfContentsFileName = "1a-TableOfContents",
                TableOfContentsTitle = "Table of Contents",
                IntroductionFileName = "1b-Intro",
                IntroductionTitle = "Introduction",
                JournalEntryTemplateFileName = "1c-Journal-Entry-Template",
                JournalEntryTemplateTitle = "Journal Entry Template",
                AllJournalsFileName = "1h-All-My-Journals",
                AllJournalsTitle = "All My Journals",
            }
        );
    }

    [Fact]
    public void Constructor_Should_Register_Default_Templates()
    {
        // When
        var manager = new TemplateManager(_journalSettings);

        // Then
        var availableTemplates = manager.GetAvailableTemplates().ToList();
        availableTemplates.ShouldContain("journal-entry");
        availableTemplates.ShouldContain("table-of-contents");
    }

    [Fact]
    public void GenerateFromTemplate_Returns_Registered_Template_Output()
    {
        // Arrange
        var manager = new TemplateManager(_journalSettings);
        var fake = new TestTemplateGenerator("x-test", "EXPECTED");
        manager.RegisterTemplate(fake);

        // Act
        var result = manager.GenerateFromTemplate("x-test", new Dictionary<string, object>());

        // Assert
        result.ShouldBe("EXPECTED");
        manager.GetAvailableTemplates().ShouldContain("x-test");
    }

    [Fact]
    public void RegisterTemplate_Should_Add_New_Template()
    {
        // Given
        var manager = new TemplateManager(_journalSettings);
        var template = new TestTemplateGenerator("new-template", "NEW_OUTPUT");

        // When
        manager.RegisterTemplate(template);

        // Then
        manager.GetAvailableTemplates().ShouldContain("new-template");
        var result = manager.GenerateFromTemplate("new-template", null);
        result.ShouldBe("NEW_OUTPUT");
    }

    [Fact]
    public void RegisterTemplate_Should_Replace_Existing_Template()
    {
        // Given
        var manager = new TemplateManager(_journalSettings);
        var originalTemplate = new TestTemplateGenerator("test-template", "ORIGINAL");
        var replacementTemplate = new TestTemplateGenerator("test-template", "REPLACEMENT");

        // When
        manager.RegisterTemplate(originalTemplate);
        manager.RegisterTemplate(replacementTemplate);

        // Then
        var result = manager.GenerateFromTemplate("test-template", null);
        result.ShouldBe("REPLACEMENT");
        manager.GetAvailableTemplates().Count(t => t == "test-template").ShouldBe(1);
    }

    [Fact]
    public void GenerateFromTemplate_Should_Throw_When_Template_Not_Found()
    {
        // Given
        var manager = new TemplateManager(_journalSettings);

        // When & Then
        var exception = Should.Throw<ArgumentException>(() =>
            manager.GenerateFromTemplate("non-existent", null)
        );

        exception.Message.ShouldContain("Template 'non-existent' not found");
        exception.Message.ShouldContain("Available templates:");
    }

    [Fact]
    public void GenerateFromTemplate_Should_Pass_Parameters_To_Template()
    {
        // Given
        var manager = new TemplateManager(_journalSettings);
        var template = new ParameterCapturingTestTemplate();
        manager.RegisterTemplate(template);
        var parameters = new Dictionary<string, object> { ["key"] = "value" };

        // When
        manager.GenerateFromTemplate("param-test", parameters);

        // Then
        template.ReceivedParameters.ShouldBe(parameters);
    }

    [Fact]
    public void GenerateFromTemplate_Should_Handle_Null_Parameters()
    {
        // Given
        var manager = new TemplateManager(_journalSettings);
        var template = new ParameterCapturingTestTemplate();
        manager.RegisterTemplate(template);

        // When
        manager.GenerateFromTemplate("param-test", null);

        // Then
        template.ReceivedParameters.ShouldBeNull();
    }

    [Fact]
    public void GenerateFromTemplate_Should_Handle_Empty_Parameters()
    {
        // Given
        var manager = new TemplateManager(_journalSettings);
        var template = new ParameterCapturingTestTemplate();
        manager.RegisterTemplate(template);
        var parameters = new Dictionary<string, object>();

        // When
        manager.GenerateFromTemplate("param-test", parameters);

        // Then
        template.ReceivedParameters.ShouldBe(parameters);
        template.ReceivedParameters.ShouldNotBeNull();
        template.ReceivedParameters.Count.ShouldBe(0);
    }

    [Fact]
    public void GetAvailableTemplates_Should_Return_All_Registered_Templates()
    {
        // Given
        var manager = new TemplateManager(_journalSettings);
        var template1 = new TestTemplateGenerator("template1", "output1");
        var template2 = new TestTemplateGenerator("template2", "output2");

        // When
        manager.RegisterTemplate(template1);
        manager.RegisterTemplate(template2);

        // Then
        var templates = manager.GetAvailableTemplates().ToList();
        templates.ShouldContain("template1");
        templates.ShouldContain("template2");
        templates.ShouldContain("journal-entry"); // Default template
        templates.ShouldContain("table-of-contents"); // Default template
    }

    [Fact]
    public void GetAvailableTemplates_Should_Return_Empty_Collection_When_No_Custom_Templates()
    {
        // Given
        var manager = new TemplateManager(_journalSettings);

        // When
        var templates = manager.GetAvailableTemplates().ToList();

        // Then
        templates.ShouldNotBeEmpty(); // Should contain default templates
        templates.Count.ShouldBe(2); // journal-entry and table-of-contents
    }

    [Fact]
    public void Default_JournalEntry_Template_Should_Be_Available()
    {
        // Given
        var manager = new TemplateManager(_journalSettings);

        // When
        var result = manager.GenerateFromTemplate("journal-entry", null);

        // Then
        result.ShouldNotBeNullOrEmpty();
        result.ShouldContain("[Back to Table of Contents](1a-TableOfContents.md)");
    }

    [Fact]
    public void Default_TableOfContents_Template_Should_Be_Available()
    {
        // Given
        var manager = new TemplateManager(_journalSettings);

        // When
        var result = manager.GenerateFromTemplate("table-of-contents", null);

        // Then
        result.ShouldNotBeNullOrEmpty();
        result.ShouldContain("# Table of Contents");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void GenerateFromTemplate_Should_Throw_For_Empty_Template_Names(string templateName)
    {
        // Given
        var manager = new TemplateManager(_journalSettings);

        // When & Then
        Should.Throw<ArgumentException>(() => manager.GenerateFromTemplate(templateName, null));
    }

    [Fact]
    public void GenerateFromTemplate_Should_Throw_For_Null_Template_Name()
    {
        // Given
        var manager = new TemplateManager(_journalSettings);

        // When & Then
        Should.Throw<ArgumentException>(() => manager.GenerateFromTemplate(null!, null));
    }

    [Fact]
    public void Multiple_Templates_With_Same_Interface_Should_Work()
    {
        // Given
        var manager = new TemplateManager(_journalSettings);
        var template1 = new TestTemplateGenerator("multi1", "OUTPUT1");
        var template2 = new TestTemplateGenerator("multi2", "OUTPUT2");

        // When
        manager.RegisterTemplate(template1);
        manager.RegisterTemplate(template2);

        // Then
        manager.GenerateFromTemplate("multi1", null).ShouldBe("OUTPUT1");
        manager.GenerateFromTemplate("multi2", null).ShouldBe("OUTPUT2");
    }

    /// <summary>
    /// Test template that captures the parameters passed to it for verification.
    /// </summary>
    private class ParameterCapturingTestTemplate : ITemplateGenerator
    {
        public string TemplateName => "param-test";
        public Dictionary<string, object>? ReceivedParameters { get; private set; }

        public string GenerateTemplate(Dictionary<string, object>? parameters)
        {
            ReceivedParameters = parameters;
            return "TEST_OUTPUT";
        }
    }
}
