using markdown_journal_cli.Infrastructure.Configuration;
using markdown_journal_cli.Infrastructure.DependencyInjection;
using markdown_journal_cli.Infrastructure.FileSystem;
using markdown_journal_cli.Infrastructure.JournalTemplates;
using markdown_journal_cli.Infrastructure.Tracking;
using markdown_journal_cli.Infrastructure.Transactions;
using markdown_journal_cli.Infrastructure.Validation;
using markdown_journal_cli.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;

namespace markdown_journal_cli.Tests.Infrastructure;

/// <summary>
/// Abstract base class for command-layer unit tests.
/// Provides pre-constructed Moq mocks for all standard command dependencies.
/// Override SetupDefaultBehaviors() to change defaults for the entire test class.
/// Call BuildApp(configure) in each test or in a helper to get a fresh CommandAppTester.
/// </summary>
public abstract class CommandTestBase
{
    // ── Mocks (created once; set up defaults in SetupDefaultBehaviors) ──
    protected readonly Mock<IFileSystem> MockFileSystem;
    protected readonly Mock<IJournalConfiguration> MockJournalConfiguration;
    protected readonly Mock<IFileTracking> MockFileTracking;
    protected readonly Mock<ITemplateManager> MockTemplateManager;
    protected readonly Mock<ITableOfContentsService> MockTableOfContentsService;
    protected readonly Mock<IEntryFormatterService> MockEntryFormatterService;
    protected readonly Mock<IJournalValidator> MockJournalValidator;
    protected readonly IOptions<JournalSettings> JournalSettings;

    protected CommandTestBase()
    {
        MockFileSystem = MockFactory.CreateFileSystem();
        MockJournalConfiguration = MockFactory.CreateJournalConfiguration();
        MockFileTracking = MockFactory.CreateFileTracking();
        MockTemplateManager = MockFactory.CreateTemplateManager();
        MockTableOfContentsService = MockFactory.CreateTableOfContentsService();
        MockEntryFormatterService = MockFactory.CreateEntryFormatterService();
        MockJournalValidator = MockFactory.CreateJournalValidator();
        JournalSettings = MockFactory.CreateJournalSettings();

        SetupDefaultBehaviors();
    }

    /// <summary>
    /// Override to configure mock defaults for the whole test class.
    /// Called by the base constructor; subclass can call base.SetupDefaultBehaviors().
    /// </summary>
    protected virtual void SetupDefaultBehaviors() { }

    /// <summary>
    /// Creates a fresh CommandAppTester with all current mock objects registered.
    /// Must be called per-test (NOT shared across tests) to get a clean output buffer.
    /// </summary>
    /// <param name="configure">Configure the Spectre.Console command tree (add commands/branches).</param>
    /// <param name="addServices">Optional callback to register additional services (e.g. IFileTransactionCoordinator, concrete command types).</param>
    protected CommandAppTester BuildApp(
        Action<IConfigurator> configure,
        Action<IServiceCollection>? addServices = null
    )
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(new TestConsole());
        services.AddSingleton(MockFileSystem.Object);
        services.AddSingleton(MockJournalConfiguration.Object);
        services.AddSingleton(MockFileTracking.Object);
        services.AddSingleton(MockTemplateManager.Object);
        services.AddSingleton(MockTableOfContentsService.Object);
        services.AddSingleton(MockEntryFormatterService.Object);
        services.AddSingleton(MockJournalValidator.Object);
        services.AddSingleton(JournalSettings);
        services.AddSingleton<IFileTransactionCoordinator>(NoOpFileTransactionCoordinator.Instance);
        services.AddSingleton<IRollbackReporter>(NoOpRollbackReporter.Instance);

        addServices?.Invoke(services);

        var registrar = new TypeRegistrar();
        foreach (var sd in services)
        {
            if (sd.ImplementationInstance != null)
                registrar.RegisterInstance(sd.ServiceType, sd.ImplementationInstance);
            else if (sd.ImplementationType != null)
                registrar.Register(sd.ServiceType, sd.ImplementationType);
        }

        var app = new CommandAppTester(registrar);
        app.Configure(configure);
        return app;
    }
}
