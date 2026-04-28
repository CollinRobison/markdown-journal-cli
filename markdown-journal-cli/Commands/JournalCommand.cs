using markdown_journal_cli.Infrastructure.Transactions;
using markdown_journal_cli.Infrastructure.Validation;
using Spectre.Console;
using Spectre.Console.Cli;

namespace markdown_journal_cli.Commands;

/// <summary>
/// Base class for all journal commands. Handles the <see cref="RollbackCompletedException"/>
/// catch and maps it to the standard exit codes: 2 = fully rolled back, 3 = partial rollback.
/// Before delegating to <see cref="ExecuteCore"/>, validates that the journal metadata directory
/// is present unless <see cref="SkipMetadataValidation"/> is overridden to <c>true</c>.
/// </summary>
public abstract class JournalCommand<TSettings> : Command<TSettings>
    where TSettings : CommandSettings
{
    private readonly IJournalValidator? _validator;
    private readonly IAnsiConsole? _console;

    /// <summary>Parameterless constructor for commands that do not require validation.</summary>
    protected JournalCommand() { }

    /// <summary>
    /// Constructor for commands that participate in metadata-directory validation.
    /// Subclasses that override <see cref="GetJournalDirectory"/> should use this constructor.
    /// </summary>
    protected JournalCommand(IJournalValidator validator, IAnsiConsole console)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _console = console ?? throw new ArgumentNullException(nameof(console));
    }

    /// <summary>
    /// When <c>true</c>, skips metadata directory validation before executing.
    /// Override to <c>true</c> in commands that create the journal (e.g. <c>new</c>, <c>init</c>).
    /// </summary>
    protected virtual bool SkipMetadataValidation => false;

    /// <summary>
    /// Returns the journal root directory for this command's settings, or <c>null</c>
    /// to opt out of pre-execution metadata validation.
    /// </summary>
    protected virtual string? GetJournalDirectory(TSettings settings) => null;

    protected sealed override int Execute(
        CommandContext context,
        TSettings settings,
        CancellationToken cancellationToken
    )
    {
        if (!SkipMetadataValidation)
        {
            var journalDir = GetJournalDirectory(settings);
            if (journalDir is not null)
            {
                // Commands that provide a journal directory must wire (IJournalValidator, IAnsiConsole)
                // into the base constructor. A null validator here is a developer error.
                if (_validator is null || _console is null)
                    throw new InvalidOperationException(
                        $"{GetType().Name} overrides GetJournalDirectory but was constructed without IJournalValidator/IAnsiConsole. Pass both to the JournalCommand base constructor, or set SkipMetadataValidation = true."
                    );

                var missing = _validator.ValidateMetadataDirectory(journalDir);
                if (missing.Count > 0)
                {
                    // A single entry with no path separator means the metadata
                    // directory itself is absent rather than just missing contents.
                    bool isDirMissing =
                        missing.Count == 1
                        && !missing[0].Contains('/')
                        && !missing[0].Contains('\\');

                    if (isDirMissing)
                    {
                        _console.MarkupLine(
                            $"[red]Error:[/] No journal found at '{journalDir.EscapeMarkup()}'. Run 'mdjournal init' or 'mdjournal new' to create one."
                        );
                    }
                    else
                    {
                        var list = string.Join(", ", missing.Select(m => m.EscapeMarkup()));
                        _console.MarkupLine(
                            $"[red]Error:[/] The journal metadata directory is missing required files: {list}. Run 'mdjournal init' to reinitialize."
                        );
                    }

                    return 1;
                }
            }
        }

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
