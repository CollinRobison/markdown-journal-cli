using System;
using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace markdown_journal_cli.Commands.Update;

public class UpdateSettings : CommandSettings
{
    [CommandOption("-p|--path")]
    [Description(
        "Specify the path of the journal. If not specified, it will default to the current directory."
    )]
    [DefaultValue(".")]
    public string FilePath { get; set; } = ".";
}

public class UpdateJournalSettings : UpdateSettings
{
    [CommandOption("-c|--config")]
    [Description("Flag to update the config.")]
    public bool ConfigFlag { get; set; }

    [CommandOption("-d|--date")]
    [Description(
        "Flag to update the markdown \"Last Edited:\" metadata and related tracking for modified files."
    )]
    public bool DateFlag { get; set; } // make sure the date also updates tracking

    [CommandOption("-t|--tracking")]
    [Description(
        "Updates file tracking independently without touching \"Last Edited:\" metadata. "
            + "This overrides --date if both are specified, leaving file metadata unchanged."
    )]
    public bool Tracking { get; set; }

    [CommandOption("--toc|--tableofcontents")]
    [Description("Flag to update the table of contents.")]
    public bool TocFlag { get; set; }

    [CommandOption("--rename-toc")]
    [Description(
        "Rename the table-of-contents file to <name> (stem only, no extension). "
            + "Updates .journalrc, rewrites all markdown inline link references, and stamps "
            + "Last Edited on modified files."
    )]
    public string? RenameToc { get; set; }

    [CommandOption("--dry-run|--check")]
    [Description(
        "Preview what would change without applying any updates. "
            + "Shows added, modified, and removed files for each requested section. "
            + "Respects all other flags for scoping (e.g. --dry-run --config shows only config drift). "
            + "--check is an alias."
    )]
    public bool DryRun { get; set; }

    public override ValidationResult Validate()
    {
        if (RenameToc is not null && RenameToc.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            return ValidationResult.Error(
                "--rename-toc expects a stem only (no extension). "
                    + "The .md extension is appended automatically."
            );
        }

        return ValidationResult.Success();
    }
}

public class UpdateEntrySettings : UpdateSettings
{
    [CommandArgument(0, "<fileName>")]
    [Description("The name of the file you want to update. this can be with or without the .md file extension.")]
    public required string FileName {get; set;}

    [CommandOption("-n|--name")]
    [Description(
        "The new name for the entry (last filename segment only). "
        + "Updates both the file name and TOC title when they currently match, unless --title is also specified. "
        + "To change the heading location use -h|--headings."
    )]
    public string? EntryName { get; set; }

    [CommandOption("-t|--title")]
    [Description("The title of the journal entry. This is the name that will show on the TOC.")]
    public string? EntryTitle { get; set; }

    [CommandOption("--he|--headings")]
    [Description(
        "The new location in the TOC hierarchy. Use - to separate heading levels and _ for spaces within heading names. "
            + "Example: 'Projects-2024_Goals' creates nested headings. If you use spaces without - separators, "
            + "the entire string is treated as a single heading. Recommended: use _ for clarity."
    )]
    public string? Headings { get; set; }

    [CommandOption("--ignore")]
    [Description("Add this entry to the ignore list so it won't appear in the table of contents.")]
    public bool IgnoreFile { get; set; }

    [CommandOption("--unignore")]
    [Description("Remove this entry from the ignore list so it will appear in the table of contents.")]
    public bool UnignoreFile { get; set; }

    [CommandOption("--nb|--no-backlinks")]
    [Description("Skip updating inline link references in other entry files after a rename. Backlink updates are enabled by default.")]
    public bool NoBacklinks { get; set; }

    public override ValidationResult Validate()
    {
        if (IgnoreFile && UnignoreFile)
        {
            return ValidationResult.Error(
                "Cannot specify both --ignore and --unignore flags."
            );
        }

        if (!string.IsNullOrEmpty(EntryName) && !EntryName.All(c => char.IsLetterOrDigit(c) || c == '_' || c == ' '))
        {
            return ValidationResult.Error(
                "Entry name may only contain letters, digits, underscores, and spaces. Use -h|--headings to set the heading location."
            );
        }
        if (
            !string.IsNullOrEmpty(Headings)
            && !Headings.All(c => char.IsLetterOrDigit(c) || c == '_' || c == ' ' || c == '-')
        )
        {
            return ValidationResult.Error(
                "Headings contains a character that is not a letter, digit, underscore, space, or hyphen."
            );
        }

        return ValidationResult.Success();
    }
}

