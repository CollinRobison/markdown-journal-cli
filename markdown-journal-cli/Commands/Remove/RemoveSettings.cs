using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace markdown_journal_cli.Commands.Remove;

public class RemoveSettings : CommandSettings
{
    [CommandOption("-p|--path")]
    [Description(
        "Specify the path of the journal. If not specified, it will default to the current directory."
    )]
    [DefaultValue(".")]
    public string FilePath { get; set; } = ".";
}

public sealed class RemoveEntrySettings : RemoveSettings
{
    [CommandArgument(0, "<fileName>")]
    [Description("The name of the file to remove (with or without .md extension).")]
    public required string FileName { get; set; }

    [CommandOption("-f|--force")]
    [Description("Skip the confirmation prompt and remove immediately.")]
    public bool Force { get; set; }

    [CommandOption("--clean-refs")]
    [Description(
        "Scan all other entry files and strip inline links pointing to the removed entry."
    )]
    public bool CleanRefs { get; set; }

    public override ValidationResult Validate()
    {
        var stem = FileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            ? FileName[..^3]
            : FileName;

        if (!stem.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.'))
        {
            return ValidationResult.Error(
                "File name may only contain letters, digits, underscores, hyphens, and dots (excluding the .md extension)."
            );
        }

        return ValidationResult.Success();
    }
}
