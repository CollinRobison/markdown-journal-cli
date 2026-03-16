using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace markdown_journal_cli.Commands.Init;

public sealed class InitSettings : CommandSettings
{
    [CommandArgument(0, "[name]")]
    [Description(
        "The name of the journal. If not specified, the directory name will be used."
    )]
    public string? JournalName { get; set; }

    [CommandOption("-p|--path <filePath>")]
    [Description(
        "The path to the existing directory to initialise as a journal. Defaults to the current directory."
    )]
    [DefaultValue(".")]
    public string? FilePath { get; set; }

    [CommandOption("-n|--toc|--tableofcontents <tocFileName>")]
    [Description(
        "The name for the Table of Contents file. Defaults to the configured TableOfContentsFileName."
    )]
    public string? TableOfContentsName { get; set; }

    public override ValidationResult Validate()
    {
        if (JournalName != null && string.IsNullOrWhiteSpace(JournalName))
            return ValidationResult.Error("Journal name cannot be empty or whitespace");

        if (
            !string.IsNullOrWhiteSpace(JournalName)
            && JournalName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
        )
            return ValidationResult.Error("Journal name contains invalid characters");

        if (!string.IsNullOrWhiteSpace(JournalName) && JournalName.Contains(' '))
            return ValidationResult.Error("Journal name cannot contain spaces");

        return ValidationResult.Success();
    }
}
