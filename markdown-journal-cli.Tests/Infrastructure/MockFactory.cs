using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.Configuration.Models;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.JournalTemplates;
using markdown_journal_cli.Infrastructure.Tracking;
using markdown_journal_cli.Infrastructure.Validation;
using markdown_journal_cli.Services;
using Microsoft.Extensions.Options;
using Moq;

namespace markdown_journal_cli.Tests.Infrastructure;

/// <summary>
/// Static factory for creating pre-configured Mock&lt;T&gt; instances.
/// Returns Mock&lt;T&gt; (not .Object) so tests can add Setup() and Verify() calls.
/// All mocks use MockBehavior.Loose unless documented otherwise.
/// Update this file when production interface signatures change — single-edit point.
/// </summary>
public static class MockFactory
{
    /// <summary>Creates a pre-configured mock for IFileSystem.</summary>
    public static Mock<IFileSystem> CreateFileSystem()
    {
        var mock = new Mock<IFileSystem>();
        mock.Setup(fs => fs.CombinePaths(It.IsAny<string[]>()))
            .Returns((string[] parts) => Path.Combine(parts));
        // Path utility methods delegate to System.IO.Path so tests relying on
        // directory-name extraction and full-path resolution work without per-test setup.
        mock.Setup(fs => fs.GetFullPath(It.IsAny<string>()))
            .Returns((string p) => Path.GetFullPath(p));
        mock.Setup(fs => fs.GetFileName(It.IsAny<string?>()))
            .Returns((string? p) => Path.GetFileName(p));
        mock.Setup(fs => fs.GetFileNameWithoutExtension(It.IsAny<string?>()))
            .Returns((string? p) => Path.GetFileNameWithoutExtension(p));
        mock.Setup(fs => fs.GetDirectoryName(It.IsAny<string?>()))
            .Returns((string? p) => Path.GetDirectoryName(p));
        return mock;
    }

    /// <summary>Creates a pre-configured mock for IJournalConfiguration.</summary>
    public static Mock<IJournalConfiguration> CreateJournalConfiguration() =>
        new();

    /// <summary>Creates a pre-configured mock for IFileTracking.</summary>
    public static Mock<IFileTracking> CreateFileTracking() =>
        new();

    /// <summary>Creates a pre-configured mock for ITemplateManager.</summary>
    public static Mock<ITemplateManager> CreateTemplateManager() =>
        new();

    /// <summary>Creates a pre-configured mock for ITableOfContentsService.</summary>
    public static Mock<ITableOfContentsService> CreateTableOfContentsService() =>
        new();

    /// <summary>Creates a pre-configured mock for IEntryFormatterService.</summary>
    public static Mock<IEntryFormatterService> CreateEntryFormatterService() =>
        new();

    /// <summary>
    /// Creates a mock IJournalValidator that returns an empty list (valid) by default.
    /// </summary>
    public static Mock<IJournalValidator> CreateJournalValidator()
    {
        var mock = new Mock<IJournalValidator>();
        mock.Setup(v => v.ValidateMetadataDirectory(It.IsAny<string>()))
            .Returns(new List<string>());
        return mock;
    }

    /// <summary>Creates a mock IJournalTocStructureRepository that returns JournalTocStructure.Empty() by default.</summary>
    public static Mock<IJournalTocStructureRepository> CreateTocStructureRepository()
    {
        var mock = new Mock<IJournalTocStructureRepository>();
        mock.Setup(r => r.Load(It.IsAny<string>())).Returns(JournalTocStructure.Empty());
        return mock;
    }

    /// <summary>
    /// Creates an IOptions&lt;JournalSettings&gt; with default test values.
    /// </summary>
    /// <param name="journalPath">Journal path stored in settings (used by some services).</param>
    /// <param name="journalName">Journal name stored in settings.</param>
    public static IOptions<JournalSettings> CreateJournalSettings(
        string journalPath = "/test/journal",
        string journalName = "TestJournal"
    ) =>
        Options.Create(
            new JournalSettings
            {
                AppName = "md-journal",
                JournalConfigFileName = ".journalrc",
                DefaultJournalName = journalName,
                TableOfContentsFileName = "1a-TableOfContents",
                TableOfContentsTitle = "Table of Contents",
                IntroductionFileName = "1b-Intro",
                IntroductionTitle = "Introduction",
                JournalEntryTemplateFileName = "1c-Journal_Entry_Template",
                JournalEntryTemplateTitle = "Journal Entry Template",
                AllJournalsFileName = "1h-All_My_Journals",
                AllJournalsTitle = "All My Journals",
                TitleSpaceSeparator = "_",
                HeadingSeparator = "-",
                DateFormat = "MM/dd/yyyy",
                MetadataDirName = ".mdjournal",
                TrackingFileName = ".journalindex",
                TocStructureFileName = ".journaltoc",
            }
        );
}
