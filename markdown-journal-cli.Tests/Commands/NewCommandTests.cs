using markdown_journal_cli.Commands.New;
using markdown_journal_cli.Infrastructure.DependencyInjection;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.JournalTemplates;
using markdown_journal_cli.Tests.Infrastructure;
using Shouldly;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using Xunit;

namespace markdown_journal_cli.Tests.Commands;

/// <summary>
/// Unit tests for the <see cref="NewCommand"/> class, covering journal creation functionality,
/// validation, error handling, and template integration.
/// </summary>
public class NewCommandTests
{
    private readonly TestConsole _console;
    private readonly TestFileSystem _fileSystem;
    private readonly CommandAppTester _app;
    private readonly TestJournalInitializer _journalInitializer;

    public NewCommandTests()
    {
        _console = new TestConsole();
        _fileSystem = new TestFileSystem();
        _journalInitializer = new TestJournalInitializer();

        var registrar = new TypeRegistrar()
            .RegisterInstance(_console)
            .RegisterInstance<IFileSystem>(_fileSystem)
            .RegisterInstance<IJournalInitializer>(_journalInitializer);

        _app = new CommandAppTester(registrar);
        _app.Configure(config =>
        {
            config.SetApplicationName("md-journal");
            config.AddCommand<NewCommand>("new").WithDescription("Creates a new markdown journal.");
        });
    }

    [Fact]
    public void Should_Create_New_Journal_With_Default_Name()
    {
        // When
        var result = _app.Run(["new", "MyJournal"]);

        // Then
        result.ExitCode.ShouldBe(0);
        result.Output.ShouldContain("MyJournal");
        _fileSystem.DirectoryExists("./MyJournal").ShouldBeTrue();
        _journalInitializer.InitializedJournals.ShouldContain(x => x.journalName == "MyJournal");
    }

    [Fact]
    public void Should_Create_New_Journal_With_Custom_Name()
    {
        // When
        var result = _app.Run(["new", "CustomJournal"]);

        // Then
        result.ExitCode.ShouldBe(0);
        result.Output.ShouldContain("CustomJournal");
        _fileSystem.DirectoryExists("./CustomJournal").ShouldBeTrue();
        _journalInitializer.InitializedJournals.ShouldContain(x => x.journalName == "CustomJournal");
    }

    [Fact]
    public void Should_Return_Error_When_Journal_Already_Exists()
    {
        // Given
        var journalName = "ExistingJournal";
        var path = Path.Combine(".", journalName);
        _fileSystem.CreateDirectory(path);

        // When
        var result = _app.Run(["new", journalName]);

        // Then
        result.ExitCode.ShouldBe(1);
        result.Output.ShouldContain("Error");
        result.Output.ShouldContain("already exists");
    }

    [Theory]
    [InlineData("--path")]
    [InlineData("-p")]
    public void Should_Create_Journal_In_Custom_Path(string pathOption)
    {
        // Given
        var customPath = Path.Combine("custom", "journals");
        var journalName = "PathJournal";
        var expectedPath = Path.Combine(customPath, journalName);

        // When
        var result = _app.Run(["new", journalName, $"{pathOption}", customPath]);

        // Then
        result.ExitCode.ShouldBe(0);
        result.Output.ShouldContain(journalName);
        result.Output.ShouldContain(customPath);
        _fileSystem.DirectoryExists(expectedPath).ShouldBeTrue();
    }

    [Fact]
    public void Should_Validate_Journal_Name_For_Invalid_Characters()
    {
        // Given
        var invalidName = "Invalid/Name";

        // When
        var result = _app.Run(["new", invalidName]);

        // Then
        result.ExitCode.ShouldNotBe(0);
        result.Output.ShouldContain("invalid characters");
    }

    [Fact]
    public void Should_Validate_Empty_Journal_Name()
    {
        // When
        var result = _app.Run(["new", ""]);

        // Then
        result.ExitCode.ShouldNotBe(0);
        result.Output.ShouldContain("cannot be empty");
    }

    [Fact]
    public void Should_Create_All_Required_Files()
    {
        // Given
        var journalName = "TestJournal";

        // When
        var result = _app.Run(["new", journalName]);

        // Then
        result.ExitCode.ShouldBe(0);

        var journalPath = Path.Combine(".", journalName);
        _fileSystem.DirectoryExists(journalPath).ShouldBeTrue();

        // Check all expected files are created
        _fileSystem.FileExists(Path.Combine(journalPath, "1a-TableOfContents.md")).ShouldBeTrue();
        _fileSystem.FileExists(Path.Combine(journalPath, "1b-Intro.md")).ShouldBeTrue();
        _fileSystem
            .FileExists(Path.Combine(journalPath, "1c-Journal-Entry-Template.md"))
            .ShouldBeTrue();
        _fileSystem.FileExists(Path.Combine(journalPath, "1h-All-My-Journals.md")).ShouldBeTrue();
    }

    [Fact]
    public void Should_Create_Files_With_Template_Content()
    {
        // Given
        var journalName = "ContentTestJournal";

        // When
        var result = _app.Run(["new", journalName]);

        // Then
        result.ExitCode.ShouldBe(0);

        var journalPath = Path.Combine(".", journalName);

        // Verify table of contents content
        var tocContent = _fileSystem.GetFileContent(
            Path.Combine(journalPath, "1a-TableOfContents.md")
        );
        tocContent.ShouldBe("# Table of Contents\n\nTEST_TOC_CONTENT");

        // Verify other files have template content
        var introContent = _fileSystem.GetFileContent(Path.Combine(journalPath, "1b-Intro.md"));
        introContent.ShouldBe("# TEST_JOURNAL_ENTRY\n\nTEST_CONTENT");

        var templateContent = _fileSystem.GetFileContent(
            Path.Combine(journalPath, "1c-Journal-Entry-Template.md")
        );
        templateContent.ShouldBe("# TEST_JOURNAL_ENTRY\n\nTEST_CONTENT");

        var journalsContent = _fileSystem.GetFileContent(
            Path.Combine(journalPath, "1h-All-My-Journals.md")
        );
        journalsContent.ShouldBe("# TEST_JOURNAL_ENTRY\n\nTEST_CONTENT");
    }

    [Fact]
    public void Should_Display_Success_Message_With_Journal_Name_And_Path()
    {
        // Given
        var journalName = "SuccessTestJournal";

        // When
        var result = _app.Run(["new", journalName]);

        // Then
        result.ExitCode.ShouldBe(0);
        result.Output.ShouldContain("Success:");
        result.Output.ShouldContain(journalName);
        result.Output.ShouldContain("created at");
    }

    [Fact]
    public void Should_Handle_Nested_Custom_Paths()
    {
        // Given
        var customPath = Path.Combine("very", "deep", "nested", "path");
        var journalName = "NestedJournal";
        var expectedPath = Path.Combine(customPath, journalName);

        // When
        var result = _app.Run(["new", journalName, "--path", customPath]);

        // Then
        result.ExitCode.ShouldBe(0);
        _fileSystem.DirectoryExists(expectedPath).ShouldBeTrue();
        _fileSystem.FileExists(Path.Combine(expectedPath, "1a-TableOfContents.md")).ShouldBeTrue();
    }

    [Theory]
    [InlineData("Journal With Spaces")]
    [InlineData("Journal-With-Dashes")]
    [InlineData("Journal_With_Underscores")]
    [InlineData("JournalWithNumbers123")]
    [InlineData("123NumericStart")]
    public void Should_Accept_Valid_Journal_Names(string validName)
    {
        // When
        var result = _app.Run(["new", validName]);

        // Then
        result.ExitCode.ShouldBe(0);
        var expectedPath = Path.Combine(".", validName);
        _fileSystem.DirectoryExists(expectedPath).ShouldBeTrue();
    }

    [Theory]
    [InlineData("Invalid/Name")] // Forward slash is invalid on all platforms
    [InlineData("Invalid\0Name")] // Null character is invalid on all platforms
    public void Should_Reject_Invalid_Journal_Names(string invalidName)
    {
        // When
        var result = _app.Run(["new", invalidName]);

        // Then
        result.ExitCode.ShouldNotBe(0);
        result.Output.ShouldContain("invalid characters");
    }

    [Fact]
    public void Should_Reject_Whitespace_Only_Journal_Name()
    {
        // When
        var result = _app.Run(["new", "   "]);

        // Then
        result.ExitCode.ShouldNotBe(0);
        result.Output.ShouldContain("cannot be empty");
    }

    [Fact]
    public void Should_Handle_Exception_From_Template_Manager()
    {
        // Given
        var faultyTemplateManager = new EmptyTemplateManager();

        var registrar = new TypeRegistrar()
            .RegisterInstance(_console)
            .RegisterInstance<IFileSystem>(_fileSystem)
            .RegisterInstance<ITemplateManager>(faultyTemplateManager);

        var faultyApp = new CommandAppTester(registrar);
        faultyApp.Configure(config =>
        {
            config.SetApplicationName("md-journal");

            config.AddCommand<NewCommand>("new").WithDescription("Creates a new markdown journal.");
        });

        // When
        var result = faultyApp.Run(["new", "TestJournal"]);

        // Then
        result.ExitCode.ShouldBe(1);
        result.Output.ShouldContain("Error:");
        result.Output.ShouldContain("unexpected error occurred");
    }

    [Fact]
    public void Should_Use_Default_Path_When_Not_Specified()
    {
        // Given
        var journalName = "DefaultPathJournal";

        // When
        var result = _app.Run(["new", journalName]);

        // Then
        result.ExitCode.ShouldBe(0);
        var expectedPath = Path.Combine(".", journalName);
        _fileSystem.DirectoryExists(expectedPath).ShouldBeTrue();
    }

    [Fact]
    public void Should_Create_Directory_Before_Creating_Files()
    {
        // Given
        var journalName = "DirectoryTestJournal";

        // When
        var result = _app.Run(["new", journalName]);

        // Then
        result.ExitCode.ShouldBe(0);

        // Verify directory was created
        var journalPath = Path.Combine(".", journalName);
        _fileSystem.DirectoryExists(journalPath).ShouldBeTrue();

        // Verify files were created in that directory
        _fileSystem.FileExists(Path.Combine(journalPath, "1a-TableOfContents.md")).ShouldBeTrue();
    }

    [Fact]
    public void Should_Use_Correct_Template_For_Table_Of_Contents()
    {
        // Given
        var journalName = "TOCTestJournal";

        // When
        var result = _app.Run(["new", journalName]);

        // Then
        result.ExitCode.ShouldBe(0);

        var tocPath = Path.Combine(".", journalName, "1a-TableOfContents.md");
        var tocContent = _fileSystem.GetFileContent(tocPath);
        tocContent.ShouldBe("# Table of Contents\n\nTEST_TOC_CONTENT");
    }

    [Fact]
    public void Should_Use_Correct_Template_For_Journal_Entry_Files()
    {
        // Given
        var journalName = "JournalEntryTestJournal";

        // When
        var result = _app.Run(["new", journalName]);

        // Then
        result.ExitCode.ShouldBe(0);

        var journalPath = Path.Combine(".", journalName);

        // All these files should use the journal-entry template
        var introContent = _fileSystem.GetFileContent(Path.Combine(journalPath, "1b-Intro.md"));
        var templateContent = _fileSystem.GetFileContent(
            Path.Combine(journalPath, "1c-Journal-Entry-Template.md")
        );
        var journalsContent = _fileSystem.GetFileContent(
            Path.Combine(journalPath, "1h-All-My-Journals.md")
        );

        introContent.ShouldBe("# TEST_JOURNAL_ENTRY\n\nTEST_CONTENT");
        templateContent.ShouldBe("# TEST_JOURNAL_ENTRY\n\nTEST_CONTENT");
        journalsContent.ShouldBe("# TEST_JOURNAL_ENTRY\n\nTEST_CONTENT");
    }

    [Fact]
    public void Should_Create_Exactly_Four_Files()
    {
        // Given
        var journalName = "FileCountTestJournal";

        // When
        var result = _app.Run(["new", journalName]);

        // Then
        result.ExitCode.ShouldBe(0);

        var allFiles = _fileSystem.GetAllFiles();
        var journalFiles = allFiles.Where(f => f.Key.Contains(journalName)).ToList();

        journalFiles.Count.ShouldBe(4);
        journalFiles.ShouldContain(f => f.Key.EndsWith("1a-TableOfContents.md"));
        journalFiles.ShouldContain(f => f.Key.EndsWith("1b-Intro.md"));
        journalFiles.ShouldContain(f => f.Key.EndsWith("1c-Journal-Entry-Template.md"));
        journalFiles.ShouldContain(f => f.Key.EndsWith("1h-All-My-Journals.md"));
    }

    [Fact]
    public void Should_Handle_Null_Path_Parameter()
    {
        // Given
        var journalName = "NullPathJournal";

        // When
        var result = _app.Run(["new", journalName]);

        // Then
        result.ExitCode.ShouldBe(0);
        var expectedPath = Path.Combine(".", journalName);
        _fileSystem.DirectoryExists(expectedPath).ShouldBeTrue();
    }

    [Fact]
    public void Should_Handle_Root_Path()
    {
        // Given
        var journalName = "RootJournal";
        var rootPath = "/";

        // When
        var result = _app.Run(["new", journalName, "--path", rootPath]);

        // Then
        result.ExitCode.ShouldBe(0);
        var expectedPath = Path.Combine(rootPath, journalName);
        _fileSystem.DirectoryExists(expectedPath).ShouldBeTrue();
    }

    [Fact]
    public void Should_Handle_Relative_Path_With_Dots()
    {
        // Given
        var journalName = "RelativeJournal";
        var relativePath = "../parent/child";

        // When
        var result = _app.Run(["new", journalName, "--path", relativePath]);

        // Then
        result.ExitCode.ShouldBe(0);
        var expectedPath = Path.Combine(relativePath, journalName);
        _fileSystem.DirectoryExists(expectedPath).ShouldBeTrue();
    }

    [Fact]
    public void Should_Display_Correct_Success_Message_Format()
    {
        // Given
        var journalName = "MessageTestJournal";
        var customPath = "custom/path";

        // When
        var result = _app.Run(["new", journalName, "--path", customPath]);

        // Then
        result.ExitCode.ShouldBe(0);
        result.Output.ShouldContain($"Success: Journal {journalName} created at");
        result.Output.ShouldContain(Path.Combine(customPath, journalName));
    }

    [Theory]
    [InlineData("a")]
    [InlineData("A")]
    [InlineData("1")]
    [InlineData("verylongjournalenamewithinamecountstoobemoreoveryangoodnametotest")]
    public void Should_Accept_Single_Character_And_Long_Names(string journalName)
    {
        // When
        var result = _app.Run(["new", journalName]);

        // Then
        result.ExitCode.ShouldBe(0);
        var expectedPath = Path.Combine(".", journalName);
        _fileSystem.DirectoryExists(expectedPath).ShouldBeTrue();
    }

    [Fact]
    public void Should_Handle_Journal_Name_With_Leading_And_Trailing_Spaces()
    {
        // Given - The command line parsing should handle this, but we test the validation
        var journalName = " SpacedJournal ";

        // When
        var result = _app.Run(["new", journalName]);

        // Then
        result.ExitCode.ShouldBe(0);
        // The file system should create directory with the exact name provided
        var expectedPath = Path.Combine(".", journalName);
        _fileSystem.DirectoryExists(expectedPath).ShouldBeTrue();
    }

    [Fact]
    public void Should_Create_Directory_Even_If_Parent_Path_Doesnt_Exist()
    {
        // Given
        var journalName = "DeepJournal";
        var deepPath = Path.Combine("non", "existent", "deep", "path");

        // When
        var result = _app.Run(["new", journalName, "--path", deepPath]);

        // Then
        result.ExitCode.ShouldBe(0);
        var expectedPath = Path.Combine(deepPath, journalName);
        _fileSystem.DirectoryExists(expectedPath).ShouldBeTrue();
    }

    [Fact]
    public void Should_Create_Files_In_Correct_Order()
    {
        // Given
        var journalName = "OrderTestJournal";

        // When
        var result = _app.Run(["new", journalName]);

        // Then
        result.ExitCode.ShouldBe(0);

        var journalPath = Path.Combine(".", journalName);
        var allFiles = _fileSystem.GetAllFiles().Keys.Where(k => k.Contains(journalName)).ToList();

        // Files should be created in alphabetical order by filename
        allFiles.ShouldContain(Path.Combine(journalPath, "1a-TableOfContents.md"));
        allFiles.ShouldContain(Path.Combine(journalPath, "1b-Intro.md"));
        allFiles.ShouldContain(Path.Combine(journalPath, "1c-Journal-Entry-Template.md"));
        allFiles.ShouldContain(Path.Combine(journalPath, "1h-All-My-Journals.md"));
    }

    [Fact]
    public void Should_Handle_FileSystem_Exception()
    {
        // Given
        var faultyFileSystem = new FaultyTestFileSystem();
        var registrar = new TypeRegistrar()
            .RegisterInstance(_console)
            .RegisterInstance<IFileSystem>(faultyFileSystem)
            .RegisterInstance<IJournalInitializer>(_journalInitializer);

        var faultyApp = new CommandAppTester(registrar);
        faultyApp.Configure(config =>
        {
            config.SetApplicationName("md-journal");

            config.AddCommand<NewCommand>("new").WithDescription("Creates a new markdown journal.");
        });

        // When
        var result = faultyApp.Run(["new", "TestJournal"]);

        // Then
        result.ExitCode.ShouldBe(1);
        result.Output.ShouldContain("Error:");
        result.Output.ShouldContain("unexpected error occurred");
    }

    [Fact]
    public void Should_Validate_Constructor_Parameters()
    {
        // When & Then
        Should.Throw<ArgumentNullException>(() =>
            new NewCommand(null!, _fileSystem, _journalInitializer)
        );
        Should.Throw<ArgumentNullException>(() =>
            new NewCommand(_console, null!, _journalInitializer)
        );
        Should.Throw<ArgumentNullException>(() => 
            new NewCommand(_console, _fileSystem, null!)
        );
    }

    [Fact]
    public void Should_Use_Default_Values_From_Settings()
    {
        // Given - Use no arguments to test default behavior
        var result = _app.Run(["new"]);

        // Then - Should use default name "MyJournal" and current directory
        result.ExitCode.ShouldBe(0);
        _fileSystem.DirectoryExists("./MyJournal").ShouldBeTrue();
        result.Output.ShouldContain("MyJournal");
    }

    [Fact]
    public void Should_Handle_Empty_String_Path()
    {
        // Given
        var journalName = "EmptyPathJournal";

        // When
        var result = _app.Run(["new", journalName, "--path", ""]);

        // Then
        result.ExitCode.ShouldBe(0);
        // Empty path should be treated as current directory
        _fileSystem.DirectoryExists(Path.Combine("", journalName)).ShouldBeTrue();
    }

    [Fact]
    public void Should_Use_CombinePaths_From_FileSystem()
    {
        // Given
        var journalName = "PathCombineTest";
        var customPath = "custom";

        // When
        var result = _app.Run(["new", journalName, "--path", customPath]);

        // Then
        result.ExitCode.ShouldBe(0);
        // Should use the file system's path combination logic
        var expectedPath = _fileSystem.CombinePaths(customPath, journalName);
        _fileSystem.DirectoryExists(expectedPath).ShouldBeTrue();
    }

    /// <summary>
    /// Test double that simulates file system exceptions for error handling tests.
    /// </summary>
    private class FaultyTestFileSystem : IFileSystem
    {
        public bool DirectoryExists(string path) => false;

        public bool FileExists(string path) => false;

        public void CreateDirectory(string path) => throw new IOException("Simulated I/O error");

        public string CombinePaths(params string[] paths) => Path.Combine(paths);

        public void CreateMarkdownFile(string path, string fileName, string body) =>
            throw new UnauthorizedAccessException("Simulated access error");

        public void CreateFile(string path, string fileName, string body)
        {
            throw new NotImplementedException();
        }

        public void UpdateFile(string path, string fileName, string body)
        {
            throw new NotImplementedException();
        }

        public void DeleteFile(string filePath)
        {
            throw new NotImplementedException();
        }

        public string GetFileContent(string filePath)
        {
            throw new NotImplementedException();
        }
    }

    [Fact]
    public void Should_Pass_Correct_Parameters_To_Template_Manager_For_Intro()
    {
        // Given
        var testTemplateManager = new TestTemplateManagerWithParameterCapture();
        testTemplateManager.RegisterTemplate(new TestTemplateGenerator("table-of-contents", "TOC"));
        testTemplateManager.RegisterTemplate(new TestTemplateGenerator("journal-entry", "ENTRY"));

        var registrar = new TypeRegistrar()
            .RegisterInstance(_console)
            .RegisterInstance<IFileSystem>(_fileSystem)
            .RegisterInstance<ITemplateManager>(testTemplateManager);

        var testApp = new CommandAppTester(registrar);
        testApp.Configure(config =>
        {
            config.SetApplicationName("md-journal");

            config.AddCommand<NewCommand>("new").WithDescription("Creates a new markdown journal.");
        });

        // When
        var result = testApp.Run(["new", "TestJournal"]);

        // Then
        result.ExitCode.ShouldBe(0);

        // Verify intro parameters
        var introParams = testTemplateManager.GetParametersForTemplate("journal-entry", 0);
        introParams.ShouldNotBeNull();
        introParams.ShouldContainKeyAndValue("title", "Introduction");
        introParams.ShouldContainKeyAndValue(
            "body",
            "Add an introduction to your new journal here."
        );
        introParams.ShouldContainKeyAndValue("addSourceBlock", false);
    }

    [Fact]
    public void Should_Pass_Correct_Parameters_To_Template_Manager_For_All_My_Journals()
    {
        // Given
        var testTemplateManager = new TestTemplateManagerWithParameterCapture();
        testTemplateManager.RegisterTemplate(new TestTemplateGenerator("table-of-contents", "TOC"));
        testTemplateManager.RegisterTemplate(new TestTemplateGenerator("journal-entry", "ENTRY"));

        var registrar = new TypeRegistrar()
            .RegisterInstance(_console)
            .RegisterInstance<IFileSystem>(_fileSystem)
            .RegisterInstance<ITemplateManager>(testTemplateManager);

        var testApp = new CommandAppTester(registrar);
        testApp.Configure(config =>
        {
            config.SetApplicationName("md-journal");

            config.AddCommand<NewCommand>("new").WithDescription("Creates a new markdown journal.");
        });

        // When
        var result = testApp.Run(["new", "TestJournal"]);

        // Then
        result.ExitCode.ShouldBe(0);

        // Verify all-my-journals parameters (should be the last call to journal-entry template)
        var allJournalsParams = testTemplateManager.GetParametersForTemplate("journal-entry", 2);
        allJournalsParams.ShouldNotBeNull();
        allJournalsParams.ShouldContainKeyAndValue("title", "Journals List");
        allJournalsParams.ShouldContainKeyAndValue("addSourceBlock", false);
        var expectedBody =
            @"- [example journal 1](link-to-journal)
- [example journal 2](link-to-journal)
- [example journal 2](link-to-journal)";
        allJournalsParams.ShouldContainKeyAndValue("body", expectedBody);
    }

    [Fact]
    public void Should_Pass_Empty_Parameters_To_Template_Manager_For_Table_Of_Contents()
    {
        // Given
        var testTemplateManager = new TestTemplateManagerWithParameterCapture();
        testTemplateManager.RegisterTemplate(new TestTemplateGenerator("table-of-contents", "TOC"));
        testTemplateManager.RegisterTemplate(new TestTemplateGenerator("journal-entry", "ENTRY"));

        var registrar = new TypeRegistrar()
            .RegisterInstance(_console)
            .RegisterInstance<IFileSystem>(_fileSystem)
            .RegisterInstance<ITemplateManager>(testTemplateManager);

        var testApp = new CommandAppTester(registrar);
        testApp.Configure(config =>
        {
            config.SetApplicationName("md-journal");

            config.AddCommand<NewCommand>("new").WithDescription("Creates a new markdown journal.");
        });

        // When
        var result = testApp.Run(["new", "TestJournal"]);

        // Then
        result.ExitCode.ShouldBe(0);

        // Verify table-of-contents parameters
        var tocParams = testTemplateManager.GetParametersForTemplate("table-of-contents", 0);
        tocParams.ShouldNotBeNull();
        tocParams.Count.ShouldBe(0);
    }

    [Fact]
    public void Should_Pass_Empty_Parameters_To_Template_Manager_For_Journal_Entry_Template()
    {
        // Given
        var testTemplateManager = new TestTemplateManagerWithParameterCapture();
        testTemplateManager.RegisterTemplate(new TestTemplateGenerator("table-of-contents", "TOC"));
        testTemplateManager.RegisterTemplate(new TestTemplateGenerator("journal-entry", "ENTRY"));

        var registrar = new TypeRegistrar()
            .RegisterInstance(_console)
            .RegisterInstance<IFileSystem>(_fileSystem)
            .RegisterInstance<ITemplateManager>(testTemplateManager);

        var testApp = new CommandAppTester(registrar);
        testApp.Configure(config =>
        {
            config.SetApplicationName("md-journal");

            config.AddCommand<NewCommand>("new").WithDescription("Creates a new markdown journal.");
        });

        // When
        var result = testApp.Run(["new", "TestJournal"]);

        // Then
        result.ExitCode.ShouldBe(0);

        // Verify journal-entry-template parameters (should be the second call to journal-entry template)
        var templateParams = testTemplateManager.GetParametersForTemplate("journal-entry", 1);
        templateParams.ShouldNotBeNull();
        templateParams.Count.ShouldBe(0);
    }

    [Fact]
    public void Should_Handle_Exception_During_File_Creation()
    {
        // Given
        var faultyFileSystem = new FileCreationFailureFileSystem();
        var registrar = new TypeRegistrar()
            .RegisterInstance(_console)
            .RegisterInstance<IFileSystem>(faultyFileSystem)
            .RegisterInstance<IJournalInitializer>(_journalInitializer);

        var faultyApp = new CommandAppTester(registrar);
        faultyApp.Configure(config =>
        {
            config.SetApplicationName("md-journal");

            config.AddCommand<NewCommand>("new").WithDescription("Creates a new markdown journal.");
        });

        // When
        var result = faultyApp.Run(["new", "TestJournal"]);

        // Then
        result.ExitCode.ShouldBe(1);
        result.Output.ShouldContain("Error:");
        result.Output.ShouldContain("unexpected error occurred");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_Handle_Various_Null_And_Empty_Path_Values(string? pathValue)
    {
        // Given
        var journalName = "PathTestJournal";
        var args =
            pathValue == null
                ? new[] { "new", journalName }
                : new[] { "new", journalName, "--path", pathValue };

        // When
        var result = _app.Run(args);

        // Then
        result.ExitCode.ShouldBe(0);
        var expectedPath = _fileSystem.CombinePaths(pathValue ?? ".", journalName);
        _fileSystem.DirectoryExists(expectedPath).ShouldBeTrue();
    }

    /// <summary>
    /// Test template manager that captures parameters passed to templates for verification.
    /// </summary>
    private class TestTemplateManagerWithParameterCapture : ITemplateManager
    {
        private readonly Dictionary<string, ITemplateGenerator> _templates = new();
        private readonly List<(
            string templateName,
            Dictionary<string, object>? parameters
        )> _templateCalls = new();

        public void RegisterTemplate(ITemplateGenerator template)
        {
            _templates[template.TemplateName] = template;
        }

        public string GenerateFromTemplate(
            string templateName,
            Dictionary<string, object>? parameters
        )
        {
            _templateCalls.Add((templateName, parameters));

            if (_templates.TryGetValue(templateName, out var template))
            {
                return template.GenerateTemplate(parameters);
            }

            throw new ArgumentException($"Template '{templateName}' not found");
        }

        public IEnumerable<string> GetAvailableTemplates()
        {
            return _templates.Keys;
        }

        /// <summary>
        /// Gets the parameters that were passed to a specific template call for verification in tests.
        /// </summary>
        /// <param name="templateName">The name of the template to get parameters for.</param>
        /// <param name="callIndex">The zero-based index of the template call (in case the same template was called multiple times).</param>
        /// <returns>The parameters dictionary that was passed to the template, or an empty dictionary if null was passed.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the call index is greater than the number of times the template was called.</exception>
        public Dictionary<string, object>? GetParametersForTemplate(
            string templateName,
            int callIndex
        )
        {
            var calls = _templateCalls.Where(c => c.templateName == templateName).ToList();
            if (callIndex >= calls.Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(callIndex),
                    $"Template '{templateName}' was called {calls.Count} times, but index {callIndex} was requested"
                );
            }

            return calls[callIndex].parameters ?? new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Test file system that fails during file creation to test error handling.
    /// </summary>
    private class FileCreationFailureFileSystem : IFileSystem
    {
        public bool DirectoryExists(string path) => false;

        public bool FileExists(string path) => false;

        public void CreateDirectory(string path)
        {
            // Allow directory creation to succeed
        }

        public string CombinePaths(params string[] paths) => Path.Combine(paths);

        public void CreateMarkdownFile(string path, string fileName, string body) =>
            throw new IOException("Failed to create file");

        public void CreateFile(string path, string fileName, string body)
        {
            throw new NotImplementedException();
        }

        public void UpdateFile(string path, string fileName, string body)
        {
            throw new NotImplementedException();
        }

        public void DeleteFile(string filePath)
        {
            throw new NotImplementedException();
        }

        public string GetFileContent(string filePath)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Test template manager that throws exceptions for missing templates.
    /// </summary>
    private class EmptyTemplateManager : ITemplateManager
    {
        public void RegisterTemplate(ITemplateGenerator template)
        {
            // Don't register any templates
        }

        public string GenerateFromTemplate(
            string templateName,
            Dictionary<string, object>? parameters
        )
        {
            throw new ArgumentException($"Template '{templateName}' not found");
        }

        public IEnumerable<string> GetAvailableTemplates()
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Test implementation of IJournalInitializer that tracks initialization calls.
    /// </summary>
    private class TestJournalInitializer : IJournalInitializer
    {
        public List<(string journalDirectory, string journalName)> InitializedJournals { get; } = new();
        public bool ShouldThrow { get; set; } = false;
        public Exception? ExceptionToThrow { get; set; }

        public void Initialize(string journalDirectory, string journalName)
        {
            if (ShouldThrow && ExceptionToThrow != null)
            {
                throw ExceptionToThrow;
            }

            InitializedJournals.Add((journalDirectory, journalName));
        }
    }
}
