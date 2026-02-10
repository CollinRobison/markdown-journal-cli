using System;
using System.ComponentModel;
using Spectre.Console.Cli;

namespace markdown_journal_cli.Commands.Update;

[Description("Updates configuration, table of contents, and file created dates. All items are updated by default unless specific flags are provided")]
public sealed class UpdateCommand
() : Command<UpdateJournalSettings>
{
    public override int Execute(CommandContext context, UpdateJournalSettings settings)
    {
        throw new NotImplementedException();
    }
}
