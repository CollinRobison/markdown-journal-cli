using markdown_journal_cli.Infrastructure.Transactions;
using Spectre.Console.Cli;

namespace markdown_journal_cli.Commands;

/// <summary>
/// Base class for all journal commands. Handles the <see cref="RollbackCompletedException"/>
/// catch and maps it to the standard exit codes: 2 = fully rolled back, 3 = partial rollback.
/// </summary>
public abstract class JournalCommand<TSettings> : Command<TSettings>
    where TSettings : CommandSettings
{
    protected sealed override int Execute(
        CommandContext context,
        TSettings settings,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return ExecuteCore(context, settings);
        }
        catch (RollbackCompletedException ex)
        {
            return ex.Result.IsFullyRestored ? 2 : 3;
        }
    }

    /// <summary>
    /// Public entry point for direct invocation (e.g., unit tests).
    /// Delegates to <see cref="Execute(CommandContext, TSettings, CancellationToken)"/>
    /// with <see cref="CancellationToken.None"/>.
    /// </summary>
    public int Execute(CommandContext context, TSettings settings) =>
        Execute(context, settings, CancellationToken.None);

    protected abstract int ExecuteCore(CommandContext context, TSettings settings);
}
