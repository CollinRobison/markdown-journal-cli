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
    public sealed override int Execute(CommandContext context, TSettings settings)
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

    protected abstract int ExecuteCore(CommandContext context, TSettings settings);
}
