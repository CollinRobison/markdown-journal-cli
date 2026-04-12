using markdown_journal_cli.Infrastructure.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Spectre.Console.Testing;

/// <summary>
/// Drop-in replacement for the removed Spectre.Console.Testing.CommandAppTester (removed in 0.55.0).
/// Wraps <see cref="CommandApp"/> with a <see cref="TestConsole"/> so that both
/// CLI-framework output and command output are captured in <see cref="CommandAppResult.Output"/>.
/// </summary>
public sealed class CommandAppTester
{
    private readonly CommandApp _app;
    private readonly TestConsole _testConsole;

    /// <summary>The internal TestConsole used by this tester. Use Console.Input to push text before Run().</summary>
    public TestConsole Console => _testConsole;

    public CommandAppTester(ITypeRegistrar registrar)
    {
        _testConsole = new TestConsole();
        // Override IAnsiConsole with our test console so all command output is captured.
        registrar.RegisterInstance(typeof(IAnsiConsole), _testConsole);
        _app = new CommandApp(registrar);
    }

    public void Configure(Action<IConfigurator> configure)
    {
        _app.Configure(config =>
        {
            config.Settings.Console = _testConsole;
            configure(config);
        });
    }

    public CommandAppResult Run(IEnumerable<string> args)
    {
        var exitCode = _app.Run(args, CancellationToken.None);
        return new CommandAppResult(exitCode, _testConsole.Output);
    }
}

public sealed record CommandAppResult(int ExitCode, string Output);
