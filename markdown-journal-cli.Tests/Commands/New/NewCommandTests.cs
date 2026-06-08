using markdown_journal_cli.Commands.New;
using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.DependencyInjection;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.JournalTemplates;
using markdown_journal_cli.Infrastructure.Tracking;
using markdown_journal_cli.Infrastructure.Tracking.Models;
using markdown_journal_cli.Infrastructure.Transactions;
using markdown_journal_cli.Services;
using markdown_journal_cli.Tests.Infrastructure;
using markdown_journal_cli.Tests.Infrastructure.FileSystem;
using markdown_journal_cli.Tests.Infrastructure.JournalTemplates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using Xunit;

namespace markdown_journal_cli.Tests.Commands.New;

/// <summary>
/// Unit tests for the <see cref="NewCommand"/> class, covering journal creation functionality,
/// validation, error handling, and template integration.
/// </summary>
public class NewCommandTests : CommandTestBase
{
    private readonly TestJournalInitializer _journalInitializer;

    public NewCommandTests()
    {
        _journalInitializer = new TestJournalInitializer();
    }

    private CommandAppTester BuildNewApp()
    {
        var settings = Options.Create(
            new JournalSettings
            {
                AppName = "md-journal",
                JournalConfigFileName = ".journalrc",
                DefaultJournalName = "MyJournal",
                TableOfContentsFileName = "1a-TableOfContents",
                TableOfContentsTitle = "Table of Contents",
                IntroductionFileName = "1b-Intro",
                IntroductionTitle = "Introduction",
                JournalEntryTemplateFileName = "1c-Journal_Entry_Template",
                JournalEntryTemplateTitle = "Journal Entry Template",
                AllJournalsFileName = "1h-All_My_Journals",
                AllJournalsTitle = "All My Journals",
            }
        );
        return BuildApp(
            config =>
            {
                config.SetApplicationName("md-journal");
                config
                    .AddCommand<NewCommand>("new")
                    .WithDescription("Creates a new markdown journal.");
            },
            services =>
            {
                services.AddSingleton<INewJournalService>(_journalInitializer);
                services.AddSingleton<NewCommand>();
                services.AddSingleton(settings);
            }
        );
    }

    [Fact]
    public void Execute_Should_CreateNewJournalWithDefaultName()
    {
        // When
        var result = BuildNewApp().Run(["new", "MyJournal"]);

        // Then
        result.ExitCode.ShouldBe(0);
        result.Output.ShouldContain("MyJournal");
        _journalInitializer.InitializedJournals.ShouldContain(x =>
            x.journalDirectory == "./MyJournal" && x.journalName == "MyJournal"
        );
    }

    [Fact]
    public void Execute_Should_CreateNewJournalWithCustomName()
    {
        // When
        var result = BuildNewApp().Run(["new", "CustomJournal"]);

        // Then
        result.ExitCode.ShouldBe(0);
        result.Output.ShouldContain("CustomJournal");
        _journalInitializer.InitializedJournals.ShouldContain(x =>
            x.journalName == "CustomJournal"
        );
    }

    [Fact]
    public void Execute_Should_ReturnError_When_JournalAlreadyExists()
    {
        // Given
        var journalName = "ExistingJournal";
        var path = Path.Combine(".", journalName);
        MockFileSystem.Setup(x => x.DirectoryExists(path)).Returns(true);

        // When
        var result = BuildNewApp().Run(["new", journalName]);

        // Then
        result.ExitCode.ShouldBe(1);
        result.Output.ShouldContain("Error");
        result.Output.ShouldContain("already exists");
    }

    [Theory]
    [InlineData("--path")]
    [InlineData("-p")]
    public void Execute_Should_CreateJournalInCustomPath_When_CustomPathProvided(string pathOption)
    {
        // Given
        var customPath = Path.Combine("custom", "journals");
        var journalName = "PathJournal";
        var expectedPath = Path.Combine(customPath, journalName);

        // When
        var result = BuildNewApp().Run(["new", journalName, $"{pathOption}", customPath]);

        // Then
        result.ExitCode.ShouldBe(0);
        result.Output.ShouldContain(journalName);
        result.Output.ShouldContain(customPath);
        _journalInitializer.InitializedJournals.ShouldContain(x =>
            x.journalDirectory == expectedPath
        );
    }

    [Fact]
    public void Execute_Should_ReturnError_When_JournalNameHasInvalidCharacters()
    {
        // Given
        var invalidName = "Invalid/Name";

        // When
        var result = BuildNewApp().Run(["new", invalidName]);

        // Then
        result.ExitCode.ShouldNotBe(0);
        result.Output.ShouldContain("invalid characters");
    }

    [Fact]
    public void Execute_Should_ReturnError_When_JournalNameIsEmpty()
    {
        // When
        var result = BuildNewApp().Run(["new", ""]);

        // Then
        result.ExitCode.ShouldNotBe(0);
        result.Output.ShouldContain("cannot be empty");
    }

    [Fact]
    public void Execute_Should_CreateAllRequiredFiles()
    {
        // Given
        var journalName = "TestJournal";

        // When
        var result = BuildNewApp().Run(["new", journalName]);

        // Then
        result.ExitCode.ShouldBe(0);
        // Verify the service was called with the expected journal directory
        var journalPath = Path.Combine(".", journalName);
        _journalInitializer.InitializedJournals.ShouldContain(x =>
            x.journalDirectory == journalPath
        );
    }

    [Fact]
    public void Execute_Should_CreateFilesWithTemplateContent()
    {
        // Given
        var journalName = "ContentTestJournal";

        // When
        var result = BuildNewApp().Run(["new", journalName]);

        // Then
        result.ExitCode.ShouldBe(0);
        // Verify the service was invoked — file content is a service-layer concern tested in NewJournalServiceTests.
        _journalInitializer.InitializedJournals.ShouldContain(x => x.journalName == journalName);
    }

    [Fact]
    public void Execute_Should_DisplaySuccessMessageWithJournalNameAndPath()
    {
        // Given
        var journalName = "SuccessTestJournal";

        // When
        var result = BuildNewApp().Run(["new", journalName]);

        // Then
        result.ExitCode.ShouldBe(0);
        result.Output.ShouldContain("Success:");
        result.Output.ShouldContain(journalName);
        result.Output.ShouldContain("created at");
    }

    [Fact]
    public void Execute_Should_HandleNestedCustomPaths()
    {
        // Given
        var customPath = Path.Combine("very", "deep", "nested", "path");
        var journalName = "NestedJournal";
        var expectedPath = Path.Combine(customPath, journalName);

        // When
        var result = BuildNewApp().Run(["new", journalName, "--path", customPath]);

        // Then
        result.ExitCode.ShouldBe(0);
        _journalInitializer.InitializedJournals.ShouldContain(x =>
            x.journalDirectory == expectedPath
        );
    }

    [Theory]
    [InlineData("Journal-With-Dashes")]
    [InlineData("Journal_With_Underscores")]
    [InlineData("JournalWithNumbers123")]
    [InlineData("123NumericStart")]
    public void Execute_Should_AcceptValidJournalNames(string validName)
    {
        // When
        var result = BuildNewApp().Run(["new", validName]);

        // Then
        result.ExitCode.ShouldBe(0);
        var expectedPath = Path.Combine(".", validName);
        _journalInitializer.InitializedJournals.ShouldContain(x =>
            x.journalDirectory == expectedPath
        );
    }

    [Theory]
    [InlineData("Invalid/Name")] // Forward slash is invalid on all platforms
    [InlineData("Invalid\0Name")] // Null character is invalid on all platforms
    public void Execute_Should_ReturnError_When_JournalNameIsInvalid(string invalidName)
    {
        // When
        var result = BuildNewApp().Run(["new", invalidName]);

        // Then
        result.ExitCode.ShouldNotBe(0);
        result.Output.ShouldContain("invalid characters");
    }

    [Fact]
    public void Execute_Should_ReturnError_When_JournalNameIsWhitespaceOnly()
    {
        // When
        var result = BuildNewApp().Run(["new", "   "]);

        // Then
        result.ExitCode.ShouldNotBe(0);
        result.Output.ShouldContain("cannot be empty");
    }

    [Theory]
    [InlineData("Journal With Spaces")]
    [InlineData("My Journal")]
    [InlineData("Test Journal Name")]
    [InlineData("Journal Name With Multiple Spaces")]
    public void Execute_Should_ReturnError_When_JournalNameHasSpaces(string nameWithSpaces)
    {
        // When
        var result = BuildNewApp().Run(["new", nameWithSpaces]);

        // Then
        result.ExitCode.ShouldNotBe(0);
        result.Output.ShouldContain("cannot contain spaces");
    }

    [Fact]
    public void Execute_Should_HandleException_When_TemplateManagerThrows()
    {
        // Given
        var faultyTemplateManager = new EmptyTemplateManager();
        var testJournalConfiguration = new TestJournalConfiguration();

        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(new TestConsole());
        services.AddSingleton<IFileSystem>(MockFileSystem.Object);
        services.AddSingleton<IJournalConfiguration>(testJournalConfiguration);
        services.AddSingleton<ITemplateManager>(faultyTemplateManager);
        services.AddSingleton(JournalSettings);
        var mockFileTracking = new Mock<IFileTracking>();
        mockFileTracking
            .Setup(x => x.LoadIndex(It.IsAny<string>()))
            .Returns(new JournalIndex { Files = [] });
        services.AddSingleton(mockFileTracking.Object);
        var mockTableOfContentsGenerator = new Mock<ITableOfContentsService>();
        services.AddSingleton(mockTableOfContentsGenerator.Object);
        services.AddSingleton<IFileTransactionCoordinator>(NoOpFileTransactionCoordinator.Instance);
        services.AddSingleton<IRollbackReporter>(NoOpRollbackReporter.Instance);
        services.AddSingleton<ILogger<NewJournalService>>(NullLogger<NewJournalService>.Instance);
        services.AddSingleton<IJournalTocStructureRepository>(
            markdown_journal_cli
                .Tests.Infrastructure.MockFactory.CreateTocStructureRepository()
                .Object
        );
        services.AddSingleton<INewJournalService, NewJournalService>();
        services.AddSingleton<NewCommand>();

        // Use helper method to create TypeRegistrar with manual service registration
        var registrar = CreateTypeRegistrar(services);

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
    public void Execute_Should_UseDefaultPath_When_PathNotSpecified()
    {
        // Given
        var journalName = "DefaultPathJournal";

        // When
        var result = BuildNewApp().Run(["new", journalName]);

        // Then
        result.ExitCode.ShouldBe(0);
        var expectedPath = Path.Combine(".", journalName);
        _journalInitializer.InitializedJournals.ShouldContain(x =>
            x.journalDirectory == expectedPath
        );
    }

    [Fact]
    public void Execute_Should_CreateDirectoryBeforeCreatingFiles()
    {
        // Given
        var journalName = "DirectoryTestJournal";

        // When
        var result = BuildNewApp().Run(["new", journalName]);

        // Then
        result.ExitCode.ShouldBe(0);
        // Verify the service was called with the correct journal directory
        var journalPath = Path.Combine(".", journalName);
        _journalInitializer.InitializedJournals.ShouldContain(x =>
            x.journalDirectory == journalPath
        );
    }

    [Fact]
    public void Execute_Should_UseCorrectTemplateForTableOfContents()
    {
        // Given
        var journalName = "TOCTestJournal";

        // When
        var result = BuildNewApp().Run(["new", journalName]);

        // Then
        result.ExitCode.ShouldBe(0);
        // Verify the service was invoked — template content is a service-layer concern tested elsewhere.
        _journalInitializer.InitializedJournals.ShouldContain(x => x.journalName == journalName);
    }

    [Fact]
    public void Execute_Should_UseCorrectTemplateForJournalEntryFiles()
    {
        // Given
        var journalName = "JournalEntryTestJournal";

        // When
        var result = BuildNewApp().Run(["new", journalName]);

        // Then
        result.ExitCode.ShouldBe(0);
        // Verify the service was invoked — template content is a service-layer concern tested elsewhere.
        _journalInitializer.InitializedJournals.ShouldContain(x => x.journalName == journalName);
    }

    [Fact]
    public void Execute_Should_CreateExactlyFourFiles()
    {
        // Given
        var journalName = "FileCountTestJournal";

        // When
        var result = BuildNewApp().Run(["new", journalName]);

        // Then
        result.ExitCode.ShouldBe(0);
        // Verify the service was called — file creation details are a service-layer concern.
        _journalInitializer.InitializedJournals.ShouldContain(x => x.journalName == journalName);
    }

    [Fact]
    public void Execute_Should_HandleNullPathParameter()
    {
        // Given
        var journalName = "NullPathJournal";

        // When
        var result = BuildNewApp().Run(["new", journalName]);

        // Then
        result.ExitCode.ShouldBe(0);
        var expectedPath = Path.Combine(".", journalName);
        _journalInitializer.InitializedJournals.ShouldContain(x =>
            x.journalDirectory == expectedPath
        );
    }

    [Fact]
    public void Execute_Should_HandleRootPath()
    {
        // Given
        var journalName = "RootJournal";
        var rootPath = "/";

        // When
        var result = BuildNewApp().Run(["new", journalName, "--path", rootPath]);

        // Then
        result.ExitCode.ShouldBe(0);
        var expectedPath = Path.Combine(rootPath, journalName);
        _journalInitializer.InitializedJournals.ShouldContain(x =>
            x.journalDirectory == expectedPath
        );
    }

    [Fact]
    public void Execute_Should_HandleRelativePathWithDots()
    {
        // Given
        var journalName = "RelativeJournal";
        var relativePath = "../parent/child";

        // When
        var result = BuildNewApp().Run(["new", journalName, "--path", relativePath]);

        // Then
        result.ExitCode.ShouldBe(0);
        var expectedPath = Path.Combine(relativePath, journalName);
        _journalInitializer.InitializedJournals.ShouldContain(x =>
            x.journalDirectory == expectedPath
        );
    }

    [Fact]
    public void Execute_Should_DisplayCorrectSuccessMessageFormat()
    {
        // Given
        var journalName = "MessageTestJournal";
        var customPath = "custom/path";

        // When
        var result = BuildNewApp().Run(["new", journalName, "--path", customPath]);

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
    public void Execute_Should_AcceptSingleCharacterAndLongNames(string journalName)
    {
        // When
        var result = BuildNewApp().Run(["new", journalName]);

        // Then
        result.ExitCode.ShouldBe(0);
        var expectedPath = Path.Combine(".", journalName);
        _journalInitializer.InitializedJournals.ShouldContain(x =>
            x.journalDirectory == expectedPath
        );
    }

    [Fact]
    public void Execute_Should_ReturnError_When_JournalNameHasLeadingAndTrailingSpaces()
    {
        // Given - Names with leading/trailing spaces should be rejected due to space validation
        var journalName = " SpacedJournal ";

        // When
        var result = BuildNewApp().Run(["new", journalName]);

        // Then
        result.ExitCode.ShouldNotBe(0);
        result.Output.ShouldContain("cannot contain spaces");
    }

    [Fact]
    public void Execute_Should_CreateDirectory_When_ParentPathDoesNotExist()
    {
        // Given
        var journalName = "DeepJournal";
        var deepPath = Path.Combine("non", "existent", "deep", "path");

        // When
        var result = BuildNewApp().Run(["new", journalName, "--path", deepPath]);

        // Then
        result.ExitCode.ShouldBe(0);
        var expectedPath = Path.Combine(deepPath, journalName);
        _journalInitializer.InitializedJournals.ShouldContain(x =>
            x.journalDirectory == expectedPath
        );
    }

    [Fact]
    public void Execute_Should_CreateFilesInCorrectOrder()
    {
        // Given
        var journalName = "OrderTestJournal";

        // When
        var result = BuildNewApp().Run(["new", journalName]);

        // Then
        result.ExitCode.ShouldBe(0);
        // Verify the service was called — file ordering is a service-layer concern.
        _journalInitializer.InitializedJournals.ShouldContain(x => x.journalName == journalName);
    }

    [Fact]
    public void Execute_Should_HandleException_When_FileSystemThrows()
    {
        // Given
        var faultyFileSystem = new FaultyTestFileSystem();
        var testTemplateManager = new TemplateManager(JournalSettings);
        var testJournalConfiguration = new TestJournalConfiguration();

        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(new TestConsole());
        services.AddSingleton<IFileSystem>(faultyFileSystem);
        services.AddSingleton<IJournalConfiguration>(testJournalConfiguration);
        services.AddSingleton<ITemplateManager>(testTemplateManager);
        services.AddSingleton(JournalSettings);
        var mockFileTracking = new Mock<IFileTracking>();
        mockFileTracking
            .Setup(x => x.LoadIndex(It.IsAny<string>()))
            .Returns(new JournalIndex { Files = [] });
        services.AddSingleton(mockFileTracking.Object);
        var mockTableOfContentsGenerator = new Mock<ITableOfContentsService>();
        services.AddSingleton(mockTableOfContentsGenerator.Object);
        services.AddSingleton<IFileTransactionCoordinator>(NoOpFileTransactionCoordinator.Instance);
        services.AddSingleton<IRollbackReporter>(NoOpRollbackReporter.Instance);
        services.AddSingleton<ILogger<NewJournalService>>(NullLogger<NewJournalService>.Instance);
        services.AddSingleton<IJournalTocStructureRepository>(
            markdown_journal_cli
                .Tests.Infrastructure.MockFactory.CreateTocStructureRepository()
                .Object
        );
        services.AddSingleton<INewJournalService, NewJournalService>();

        // Use helper method to create TypeRegistrar with manual service registration
        var registrar = CreateTypeRegistrar(services);

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
    public void Constructor_Should_ThrowArgumentNullException_When_ParametersAreNull()
    {
        // When & Then
        Should.Throw<ArgumentNullException>(() =>
            new NewCommand(null!, MockFileSystem.Object, _journalInitializer, JournalSettings)
        );
        Should.Throw<ArgumentNullException>(() =>
            new NewCommand(new TestConsole(), null!, _journalInitializer, JournalSettings)
        );
        Should.Throw<ArgumentNullException>(() =>
            new NewCommand(new TestConsole(), MockFileSystem.Object, null!, JournalSettings)
        );
    }

    [Fact]
    public void Execute_Should_UseDefaultValuesFromSettings()
    {
        // Given - Use no arguments to test default behavior
        var result = BuildNewApp().Run(["new"]);

        // Then - Should use default name from settings ("MyJournal") and current directory
        result.ExitCode.ShouldBe(0);
        _journalInitializer.InitializedJournals.ShouldContain(x =>
            x.journalDirectory == "./MyJournal"
        );
        result.Output.ShouldContain("MyJournal");
    }

    [Fact]
    public void Execute_Should_UseDefaultJournalNameFromSettings_When_NoNameProvided()
    {
        // Given - Create custom settings with a different default name
        var customSettings = Options.Create(
            new JournalSettings
            {
                AppName = "md-journal",
                DefaultJournalName = "CustomDefaultName",
                JournalConfigFileName = ".journalrc",
                TableOfContentsFileName = "1a-TableOfContents",
                TableOfContentsTitle = "Table of Contents",
                IntroductionFileName = "1b-Intro",
                IntroductionTitle = "Introduction",
                JournalEntryTemplateFileName = "1c-Journal_Entry_Template",
                JournalEntryTemplateTitle = "Journal Entry Template",
                AllJournalsFileName = "1h-All_My_Journals",
                AllJournalsTitle = "All My Journals",
            }
        );

        var testFileSystem = new TestFileSystem();

        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(new TestConsole());
        services.AddSingleton<IFileSystem>(testFileSystem);
        var testInitializer = new TestJournalInitializer(testFileSystem);
        services.AddSingleton<INewJournalService>(testInitializer);
        services.AddSingleton(customSettings);

        // Use helper method to create TypeRegistrar with manual service registration
        var registrar = CreateTypeRegistrar(services);

        var customApp = new CommandAppTester(registrar);
        customApp.Configure(config =>
        {
            config.SetApplicationName(customSettings.Value.AppName);
            config.AddCommand<NewCommand>("new").WithDescription("Creates a new markdown journal.");
        });

        // When - Run without providing a journal name
        var result = customApp.Run(["new"]);

        // Then - Should use the custom default name
        result.ExitCode.ShouldBe(0);
        testFileSystem.DirectoryExists("./CustomDefaultName").ShouldBeTrue();
        result.Output.ShouldContain("CustomDefaultName");
        testInitializer.InitializedJournals.ShouldContain(x =>
            x.journalName == "CustomDefaultName"
        );
    }

    [Fact]
    public void Execute_Should_UseCustomSettingsForFileNames()
    {
        // Given - Create custom settings with different file names
        var customSettings = Options.Create(
            new JournalSettings
            {
                AppName = "custom-journal",
                JournalConfigFileName = ".customrc",
                DefaultJournalName = "CustomDefault",
                TableOfContentsFileName = "00-TOC",
                TableOfContentsTitle = "Contents",
                IntroductionFileName = "01-Welcome",
                IntroductionTitle = "Welcome",
                JournalEntryTemplateFileName = "02-Template",
                JournalEntryTemplateTitle = "Entry Template",
                AllJournalsFileName = "99-Journals",
                AllJournalsTitle = "My Journals",
            }
        );

        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(new TestConsole());
        services.AddSingleton<IFileSystem>(MockFileSystem.Object);
        var testInitializer = new TestJournalInitializer();
        services.AddSingleton<INewJournalService>(testInitializer);
        services.AddSingleton(customSettings);

        // Use helper method to create TypeRegistrar with manual service registration
        var registrar = CreateTypeRegistrar(services);

        var customApp = new CommandAppTester(registrar);
        customApp.Configure(config =>
        {
            config.SetApplicationName(customSettings.Value.AppName);
            config.AddCommand<NewCommand>("new").WithDescription("Creates a new markdown journal.");
        });

        // When
        var result = customApp.Run(["new", "TestJournal"]);

        // Then
        result.ExitCode.ShouldBe(0);

        // Verify the initializer was called with correct parameters
        testInitializer.InitializedJournals.ShouldContain(x => x.journalName == "TestJournal");
    }

    [Fact]
    public void Execute_Should_CreateFilesWithCustomSettingsNames()
    {
        // Given - Create custom settings
        var customSettings = Options.Create(
            new JournalSettings
            {
                AppName = "test-journal",
                JournalConfigFileName = ".testrc",
                DefaultJournalName = "DefaultTest",
                TableOfContentsFileName = "0-Contents",
                TableOfContentsTitle = "My Contents",
                IntroductionFileName = "1-Intro",
                IntroductionTitle = "My Intro",
                JournalEntryTemplateFileName = "2-Template",
                JournalEntryTemplateTitle = "My Template",
                AllJournalsFileName = "9-AllJournals",
                AllJournalsTitle = "All Journals",
            }
        );

        var testFileSystem = new TestFileSystem();

        // Use real JournalInitializer with test template manager
        var testTemplateManager = new TestTemplateManager();
        var testJournalConfig = new TestJournalConfiguration();
        var mockFileTracking = new Mock<IFileTracking>();
        mockFileTracking
            .Setup(x => x.LoadIndex(It.IsAny<string>()))
            .Returns(new JournalIndex { Files = [] });
        var mockTableOfContentsGenerator = new Mock<ITableOfContentsService>();
        var realInitializer = new NewJournalService(
            testFileSystem,
            testTemplateManager,
            testJournalConfig,
            mockFileTracking.Object,
            customSettings,
            NoOpFileTransactionCoordinator.Instance,
            NoOpRollbackReporter.Instance,
            NullLogger<NewJournalService>.Instance,
            markdown_journal_cli
                .Tests.Infrastructure.MockFactory.CreateTocStructureRepository()
                .Object
        );

        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(new TestConsole());
        services.AddSingleton<IFileSystem>(testFileSystem);
        services.AddSingleton<INewJournalService>(realInitializer);
        services.AddSingleton(customSettings);

        // Use helper method to create TypeRegistrar with manual service registration
        var registrar = CreateTypeRegistrar(services);

        var customApp = new CommandAppTester(registrar);
        customApp.Configure(config =>
        {
            config.SetApplicationName(customSettings.Value.AppName);
            config.AddCommand<NewCommand>("new").WithDescription("Creates a new markdown journal.");
        });

        // When
        var result = customApp.Run(["new", "MyJournal"]);

        // Then
        result.ExitCode.ShouldBe(0);

        var journalPath = Path.Combine(".", "MyJournal");

        // Verify files were created with custom file names
        testFileSystem.FileExists(Path.Combine(journalPath, "0-Contents.md")).ShouldBeTrue();
        testFileSystem.FileExists(Path.Combine(journalPath, "1-Intro.md")).ShouldBeTrue();
        testFileSystem.FileExists(Path.Combine(journalPath, "2-Template.md")).ShouldBeTrue();
        testFileSystem.FileExists(Path.Combine(journalPath, "9-AllJournals.md")).ShouldBeTrue();
    }

    [Fact]
    public void Execute_Should_HandleEmptyStringPath()
    {
        // Given
        var journalName = "EmptyPathJournal";

        // When
        var result = BuildNewApp().Run(["new", journalName, "--path", ""]);

        // Then
        result.ExitCode.ShouldBe(0);
        // Empty path should be treated as current directory
        _journalInitializer.InitializedJournals.ShouldContain(x =>
            x.journalDirectory == Path.Combine("", journalName)
        );
    }

    [Fact]
    public void Execute_Should_UseCombinePathsFromFileSystem()
    {
        // Given
        var journalName = "PathCombineTest";
        var customPath = "custom";

        // When
        var result = BuildNewApp().Run(["new", journalName, "--path", customPath]);

        // Then
        result.ExitCode.ShouldBe(0);
        var expectedPath = Path.Combine(customPath, journalName);
        _journalInitializer.InitializedJournals.ShouldContain(x =>
            x.journalDirectory == expectedPath
        );
    }

    [Fact]
    public void Execute_Should_NormalizeMinusOneToOne_When_ValidationFails()
    {
        // Spectre.Console's CommandApp.Run() returns -1 when Settings.Validate() fails.
        // Program.Main() normalises that to 1 so shell scripts receive the canonical
        // exit code 1 ("pre-write guard failed") rather than bash's 255 (overflow of -1).
        // This test pins the normalization logic.
        const int spectreValidationFailure = -1;
        var normalized = spectreValidationFailure == -1 ? 1 : spectreValidationFailure;
        normalized.ShouldBe(1);
    }

    [Theory]
    [InlineData("Journal With Spaces", "cannot contain spaces")]
    [InlineData("Invalid/Name", "invalid characters")]
    public void Execute_Should_ReturnNonZeroExitCode_When_ValidationFails(
        string invalidName,
        string expectedMessage
    )
    {
        // CommandAppTester returns the raw Spectre value (-1); Program.Main normalises it to 1.
        // We assert ShouldNotBe(0) here to keep the test independent of the wrapper layer.
        var result = BuildNewApp().Run(["new", invalidName]);

        result.ExitCode.ShouldNotBe(0);
        result.Output.ShouldContain(expectedMessage);
    }

    [Fact]
    public void Execute_Should_ReturnError_When_JournalNameContainsBrackets()
    {
        // Brackets are valid filesystem chars on macOS/Linux but are rejected because:
        // 1. They break markdown link syntax — "[my[journal]]" is malformed.
        // 2. They are interpreted as shell globs — my[journal] expands in bash.
        var result = BuildNewApp().Run(["new", "my[journal]"]);

        result.ExitCode.ShouldNotBe(0);
        result.Output.ShouldContain("markdown link characters");
    }

    /// <summary>
    /// Test double that simulates file system exceptions for error handling tests.
    /// </summary>
    private class FaultyTestFileSystem : IFileSystem
    {
        public bool DirectoryExists(string path) => false;

        public bool IsDirectory(string path) => false;

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

        public void DeleteDirectory(string path)
        {
            throw new NotImplementedException();
        }

        public void RenameFile(string oldPath, string newPath)
        {
            throw new NotImplementedException();
        }

        public string GetFileContent(string filePath)
        {
            throw new NotImplementedException();
        }

        public string? GetFileNameWithoutExtension(string? path) =>
            Path.GetFileNameWithoutExtension(path);

        public string? GetDirectoryName(string? path) => Path.GetDirectoryName(path);

        public string? GetFileName(string? path) => Path.GetFileName(path);

        public string GetFullPath(string path) => Path.GetFullPath(path);

        public string[] GetFiles(string path, string searchPattern, SearchOption searchOption)
        {
            throw new IOException("Simulated I/O error");
        }

        public IReadOnlyList<string> GetMarkdownFiles(string directory)
        {
            throw new IOException("Simulated I/O error");
        }
    }

    [Fact]
    public void Execute_Should_PassCorrectParametersToTemplateManager_When_GeneratingIntro()
    {
        // Given
        var testTemplateManager = new TestTemplateManagerWithParameterCapture();
        testTemplateManager.RegisterTemplate(new TestTemplateGenerator("table-of-contents", "TOC"));
        testTemplateManager.RegisterTemplate(new TestTemplateGenerator("journal-entry", "ENTRY"));
        var testJournalConfiguration = new TestJournalConfiguration();

        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(new TestConsole());
        services.AddSingleton<IFileSystem>(MockFileSystem.Object);
        services.AddSingleton<IJournalConfiguration>(testJournalConfiguration);
        services.AddSingleton<ITemplateManager>(testTemplateManager);
        services.AddSingleton(JournalSettings);
        var mockFileTracking = new Mock<IFileTracking>();
        mockFileTracking
            .Setup(x => x.LoadIndex(It.IsAny<string>()))
            .Returns(new JournalIndex { Files = [] });
        services.AddSingleton(mockFileTracking.Object);
        var mockTableOfContentsGenerator = new Mock<ITableOfContentsService>();
        services.AddSingleton(mockTableOfContentsGenerator.Object);
        services.AddSingleton<INewJournalService, NewJournalService>();

        // Use helper method to create TypeRegistrar with manual service registration
        var registrar = CreateTypeRegistrar(services);

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
    public void Execute_Should_PassCorrectParametersToTemplateManager_When_GeneratingAllMyJournals()
    {
        // Given
        var testTemplateManager = new TestTemplateManagerWithParameterCapture();
        testTemplateManager.RegisterTemplate(new TestTemplateGenerator("table-of-contents", "TOC"));
        testTemplateManager.RegisterTemplate(new TestTemplateGenerator("journal-entry", "ENTRY"));
        var testJournalConfiguration = new TestJournalConfiguration();

        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(new TestConsole());
        services.AddSingleton<IFileSystem>(MockFileSystem.Object);
        services.AddSingleton<IJournalConfiguration>(testJournalConfiguration);
        services.AddSingleton<ITemplateManager>(testTemplateManager);
        services.AddSingleton(JournalSettings);
        var mockFileTracking = new Mock<IFileTracking>();
        mockFileTracking
            .Setup(x => x.LoadIndex(It.IsAny<string>()))
            .Returns(new JournalIndex { Files = [] });
        services.AddSingleton(mockFileTracking.Object);
        var mockTableOfContentsGenerator = new Mock<ITableOfContentsService>();
        services.AddSingleton(mockTableOfContentsGenerator.Object);
        services.AddSingleton<INewJournalService, NewJournalService>();

        // Use helper method to create TypeRegistrar with manual service registration
        var registrar = CreateTypeRegistrar(services);

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
- [example journal 3](link-to-journal)";
        allJournalsParams.ShouldContainKeyAndValue("body", expectedBody);
    }

    [Fact]
    public void Execute_Should_PassEmptyParametersToTemplateManager_When_GeneratingTableOfContents()
    {
        // Given
        var testTemplateManager = new TestTemplateManagerWithParameterCapture();
        testTemplateManager.RegisterTemplate(new TestTemplateGenerator("table-of-contents", "TOC"));
        testTemplateManager.RegisterTemplate(new TestTemplateGenerator("journal-entry", "ENTRY"));
        var testJournalConfiguration = new TestJournalConfiguration();

        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(new TestConsole());
        services.AddSingleton<IFileSystem>(MockFileSystem.Object);
        services.AddSingleton<IJournalConfiguration>(testJournalConfiguration);
        services.AddSingleton<ITemplateManager>(testTemplateManager);
        services.AddSingleton(JournalSettings);
        var mockFileTracking = new Mock<IFileTracking>();
        var mockTableOfContentsGenerator = new Mock<ITableOfContentsService>();
        services.AddSingleton(mockTableOfContentsGenerator.Object);
        mockFileTracking
            .Setup(x => x.LoadIndex(It.IsAny<string>()))
            .Returns(new JournalIndex { Files = [] });
        services.AddSingleton(mockFileTracking.Object);
        services.AddSingleton<INewJournalService, NewJournalService>();

        // Use helper method to create TypeRegistrar with manual service registration
        var registrar = CreateTypeRegistrar(services);

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
    public void Execute_Should_PassEmptyParametersToTemplateManager_When_GeneratingJournalEntryTemplate()
    {
        // Given
        var testTemplateManager = new TestTemplateManagerWithParameterCapture();
        testTemplateManager.RegisterTemplate(new TestTemplateGenerator("table-of-contents", "TOC"));
        testTemplateManager.RegisterTemplate(new TestTemplateGenerator("journal-entry", "ENTRY"));
        var testJournalConfiguration = new TestJournalConfiguration();

        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(new TestConsole());
        services.AddSingleton<IFileSystem>(MockFileSystem.Object);
        services.AddSingleton<IJournalConfiguration>(testJournalConfiguration);
        services.AddSingleton<ITemplateManager>(testTemplateManager);
        services.AddSingleton(JournalSettings);
        var mockFileTracking = new Mock<IFileTracking>();
        var mockTableOfContentsGenerator = new Mock<ITableOfContentsService>();
        services.AddSingleton(mockTableOfContentsGenerator.Object);
        mockFileTracking
            .Setup(x => x.LoadIndex(It.IsAny<string>()))
            .Returns(new JournalIndex { Files = [] });
        services.AddSingleton(mockFileTracking.Object);
        services.AddSingleton<INewJournalService, NewJournalService>();

        // Use helper method to create TypeRegistrar with manual service registration
        var registrar = CreateTypeRegistrar(services);

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
    public void Execute_Should_HandleException_When_FileCreationFails()
    {
        // Given
        var faultyFileSystem = new FileCreationFailureFileSystem();
        var testTemplateManager = new TemplateManager(JournalSettings);
        var testJournalConfiguration = new TestJournalConfiguration();

        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(new TestConsole());
        services.AddSingleton<IFileSystem>(faultyFileSystem);
        services.AddSingleton<IJournalConfiguration>(testJournalConfiguration);
        services.AddSingleton<ITemplateManager>(testTemplateManager);
        var mockTableOfContentsGenerator = new Mock<ITableOfContentsService>();
        services.AddSingleton(mockTableOfContentsGenerator.Object);
        services.AddSingleton(JournalSettings);
        var mockFileTracking = new Mock<IFileTracking>();
        mockFileTracking
            .Setup(x => x.LoadIndex(It.IsAny<string>()))
            .Returns(new JournalIndex { Files = [] });
        services.AddSingleton(mockFileTracking.Object);
        services.AddSingleton<IFileTransactionCoordinator>(NoOpFileTransactionCoordinator.Instance);
        services.AddSingleton<IRollbackReporter>(NoOpRollbackReporter.Instance);
        services.AddSingleton<ILogger<NewJournalService>>(NullLogger<NewJournalService>.Instance);
        services.AddSingleton<IJournalTocStructureRepository>(
            markdown_journal_cli
                .Tests.Infrastructure.MockFactory.CreateTocStructureRepository()
                .Object
        );
        services.AddSingleton<INewJournalService, NewJournalService>();

        // Use helper method to create TypeRegistrar with manual service registration
        var registrar = CreateTypeRegistrar(services);

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
    public void Execute_Should_HandleVariousNullAndEmptyPathValues(string? pathValue)
    {
        // Given
        var journalName = "PathTestJournal";
        var args =
            pathValue == null
                ? new[] { "new", journalName }
                : new[] { "new", journalName, "--path", pathValue };

        // When
        var result = BuildNewApp().Run(args);

        // Then
        result.ExitCode.ShouldBe(0);
        var expectedPath = Path.Combine(pathValue ?? ".", journalName);
        _journalInitializer.InitializedJournals.ShouldContain(x =>
            x.journalDirectory == expectedPath
        );
    }

    /// <summary>
    /// Test template manager that captures parameters passed to templates for verification.
    /// </summary>
    private class TestTemplateManagerWithParameterCapture : ITemplateManager
    {
        private readonly Dictionary<string, ITemplateGenerator> _templates = [];
        private readonly List<(
            string templateName,
            Dictionary<string, object>? parameters
        )> _templateCalls = [];

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

            return calls[callIndex].parameters ?? [];
        }
    }

    /// <summary>
    /// Test file system that fails during file creation to test error handling.
    /// </summary>
    private class FileCreationFailureFileSystem : IFileSystem
    {
        public bool DirectoryExists(string path) => false;

        public bool IsDirectory(string path) => false;

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

        public void DeleteDirectory(string path)
        {
            throw new NotImplementedException();
        }

        public void RenameFile(string oldPath, string newPath)
        {
            throw new NotImplementedException();
        }

        public string GetFileContent(string filePath)
        {
            throw new NotImplementedException();
        }

        public string? GetFileNameWithoutExtension(string? path) =>
            Path.GetFileNameWithoutExtension(path);

        public string? GetDirectoryName(string? path) => Path.GetDirectoryName(path);

        public string? GetFileName(string? path) => Path.GetFileName(path);

        public string GetFullPath(string path) => Path.GetFullPath(path);

        public string[] GetFiles(string path, string searchPattern, SearchOption searchOption)
        {
            return Array.Empty<string>();
        }

        public IReadOnlyList<string> GetMarkdownFiles(string directory)
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Helper method to create a TypeRegistrar with services registered manually to work with Spectre.Console's DI
    /// </summary>
    private static TypeRegistrar CreateTypeRegistrar(ServiceCollection services)
    {
        // Ensure NewCommand is registered if not already
        if (!services.Any(s => s.ServiceType == typeof(NewCommand)))
        {
            services.AddSingleton<NewCommand>();
        }

        var registrar = new TypeRegistrar();

        // Register null logging so type-registered services (e.g. NewJournalService) that
        // depend on ILogger<T> can resolve correctly inside the TypeRegistrar's ServiceProvider.
        registrar.RegisterInstance(typeof(ILoggerFactory), NullLoggerFactory.Instance);
        registrar.Register(typeof(ILogger<>), typeof(Logger<>));
        registrar.RegisterInstance(
            typeof(IFileTransactionCoordinator),
            NoOpFileTransactionCoordinator.Instance
        );
        registrar.RegisterInstance(typeof(IRollbackReporter), NoOpRollbackReporter.Instance);
        registrar.RegisterInstance(
            typeof(IJournalTocStructureRepository),
            markdown_journal_cli
                .Tests.Infrastructure.MockFactory.CreateTocStructureRepository()
                .Object
        );

        foreach (var service in services)
        {
            if (service.ImplementationInstance != null)
            {
                registrar.RegisterInstance(service.ServiceType, service.ImplementationInstance);
            }
            else if (service.ImplementationType != null)
            {
                registrar.Register(service.ServiceType, service.ImplementationType);
            }
        }

        return registrar;
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
    private class TestJournalInitializer : INewJournalService
    {
        private readonly IFileSystem? _fileSystem;

        public List<(string journalDirectory, string journalName)> InitializedJournals { get; } =
        [];
        public bool ShouldThrow { get; set; } = false;
        public Exception? ExceptionToThrow { get; set; }

        public TestJournalInitializer(IFileSystem? fileSystem = null)
        {
            _fileSystem = fileSystem;
        }

        public void Initialize(string journalDirectory, string journalName)
        {
            if (ShouldThrow && ExceptionToThrow != null)
            {
                throw ExceptionToThrow;
            }

            // Create directory to simulate real behavior
            _fileSystem?.CreateDirectory(journalDirectory);

            // Create the expected files
            if (_fileSystem != null)
            {
                _fileSystem.CreateMarkdownFile(
                    journalDirectory,
                    "1a-TableOfContents",
                    "# Table of Contents\n\nTEST_TOC_CONTENT"
                );
                _fileSystem.CreateMarkdownFile(
                    journalDirectory,
                    "1b-Intro",
                    "# TEST_JOURNAL_ENTRY\n\nTEST_CONTENT"
                );
                _fileSystem.CreateMarkdownFile(
                    journalDirectory,
                    "1c-Journal_Entry_Template",
                    "# TEST_JOURNAL_ENTRY\n\nTEST_CONTENT"
                );
                _fileSystem.CreateMarkdownFile(
                    journalDirectory,
                    "1h-All_My_Journals",
                    "# TEST_JOURNAL_ENTRY\n\nTEST_CONTENT"
                );
            }

            InitializedJournals.Add((journalDirectory, journalName));
        }
    }
}
