using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Tests.Infrastructure.FileSystem;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace markdown_journal_cli.Tests.Infrastructure.FileSystem;

/// <summary>
/// Unit tests for <see cref="MarkdownLinkRewriter"/> covering inline link rewriting
/// and file discovery by link target.
/// </summary>
public class MarkdownLinkRewriterTests
{
    private const string OldFileName = "1a-TableOfContents.md";
    private const string NewFileName = "MyContents.md";

    private readonly TestFileSystem _fileSystem;
    private readonly MarkdownLinkRewriter _rewriter;

    public MarkdownLinkRewriterTests()
    {
        _fileSystem = new TestFileSystem();
        _rewriter = new MarkdownLinkRewriter(_fileSystem, NullLogger<MarkdownLinkRewriter>.Instance);
    }

    // ------------------------------------------------------------------
    // RewriteLinks
    // ------------------------------------------------------------------

    #region RewriteLinks

    [Fact]
    public void RewriteLinks_ReplacesInlineLink_WhenFileNameMatchesExactly()
    {
        var content = $"See the [TOC]({OldFileName}) for details.";

        var result = _rewriter.RewriteLinks(content, OldFileName, NewFileName);

        result.ShouldBe($"See the [TOC]({NewFileName}) for details.");
    }

    [Fact]
    public void RewriteLinks_PreservesLeadingPath_WhenLinkHasPathPrefix()
    {
        var content = $"Back to [TOC](../docs/{OldFileName}).";

        var result = _rewriter.RewriteLinks(content, OldFileName, NewFileName);

        result.ShouldBe($"Back to [TOC](../docs/{NewFileName}).");
    }

    [Fact]
    public void RewriteLinks_ReturnsContentUnchanged_WhenNoMatchingLink()
    {
        var content = "No links here at all.";

        var result = _rewriter.RewriteLinks(content, OldFileName, NewFileName);

        result.ShouldBe(content);
    }

    [Fact]
    public void RewriteLinks_ReplacesAllOccurrences_WhenMultipleLinksPresent()
    {
        var content =
            $"[TOC]({OldFileName}) and again [TOC]({OldFileName}) and once more [Index]({OldFileName}).";

        var result = _rewriter.RewriteLinks(content, OldFileName, NewFileName);

        result.ShouldNotContain(OldFileName);
        result.ShouldContain($"[TOC]({NewFileName})");
        result.ShouldContain($"[Index]({NewFileName})");
    }

    [Fact]
    public void RewriteLinks_DoesNotMatchPartialFilename()
    {
        // A file named "Old-TableOfContents.md" should not be rewritten when target is "TableOfContents.md"
        var content = "[TOC](Old-TableOfContents.md)";

        var result = _rewriter.RewriteLinks(content, "TableOfContents.md", NewFileName);

        result.ShouldBe(content);
    }

    [Fact]
    public void RewriteLinks_ReturnsEmptyString_WhenContentIsEmpty()
    {
        var result = _rewriter.RewriteLinks(string.Empty, OldFileName, NewFileName);

        result.ShouldBe(string.Empty);
    }

    #endregion

    // ------------------------------------------------------------------
    // FindFilesWithLinkTo
    // ------------------------------------------------------------------

    #region FindFilesWithLinkTo

    [Fact]
    public void FindFilesWithLinkTo_ReturnsOnlyMatchingFile_WhenOneFileContainsLink()
    {
        const string journalPath = "/journal";
        _fileSystem.CreateDirectory(journalPath);
        _fileSystem.CreateFile(journalPath, "intro.md",
            $"Welcome. See [TOC]({OldFileName}).");
        _fileSystem.CreateFile(journalPath, "chapter-1.md",
            "No link here.");

        var results = _rewriter.FindFilesWithLinkTo(journalPath, OldFileName);

        results.ShouldHaveSingleItem();
        results[0].ShouldBe("intro.md");
    }

    [Fact]
    public void FindFilesWithLinkTo_ReturnsEmpty_WhenNoMarkdownFilesExist()
    {
        const string journalPath = "/empty-journal";
        _fileSystem.CreateDirectory(journalPath);

        var results = _rewriter.FindFilesWithLinkTo(journalPath, OldFileName);

        results.ShouldBeEmpty();
    }

    [Fact]
    public void FindFilesWithLinkTo_ReturnsEmpty_WhenNoFileContainsLink()
    {
        const string journalPath = "/journal";
        _fileSystem.CreateDirectory(journalPath);
        _fileSystem.CreateFile(journalPath, "note.md", "Just plain text.");

        var results = _rewriter.FindFilesWithLinkTo(journalPath, OldFileName);

        results.ShouldBeEmpty();
    }

    [Fact]
    public void FindFilesWithLinkTo_ReturnsMultipleFiles_WhenSeveralContainLink()
    {
        const string journalPath = "/journal";
        _fileSystem.CreateDirectory(journalPath);
        _fileSystem.CreateFile(journalPath, "intro.md",
            $"[TOC]({OldFileName})");
        _fileSystem.CreateFile(journalPath, "chapter-1.md",
            $"Back to [TOC]({OldFileName})");
        _fileSystem.CreateFile(journalPath, "other.md",
            "No link.");

        var results = _rewriter.FindFilesWithLinkTo(journalPath, OldFileName);

        results.Count.ShouldBe(2);
        results.ShouldContain("intro.md");
        results.ShouldContain("chapter-1.md");
    }

    [Fact]
    public void FindFilesWithLinkTo_ReturnsRelativePaths()
    {
        const string journalPath = "/journal";
        _fileSystem.CreateDirectory(journalPath);
        _fileSystem.CreateFile(journalPath, "note.md",
            $"See [TOC]({OldFileName}).");

        var results = _rewriter.FindFilesWithLinkTo(journalPath, OldFileName);

        results.ShouldHaveSingleItem();
        results[0].ShouldNotContain(journalPath);
        results[0].ShouldBe("note.md");
    }

    #endregion
}
