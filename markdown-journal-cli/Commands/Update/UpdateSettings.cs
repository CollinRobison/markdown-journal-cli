using System;
using System.ComponentModel;
using Spectre.Console.Cli;

namespace markdown_journal_cli.Commands.Update;

public class UpdateSettings : CommandSettings
{
    [CommandOption("-p|--path")]
    [Description(
        "Specify the path of the journal. If not specified, it will defaulted to the current directory."
    )]
    [DefaultValue(".")]
    public string FilePath { get; set; } = ".";
}

public class UpdateJournalSettings : UpdateSettings
{
    [CommandOption("-c|--config")]
    [Description(
        "Flag to update the config."
    )]
    public bool ConfigFlag {get; set;}

    [CommandOption("-d|--dates")]
    [Description(
        "Flag to update the created dates of modified files."
    )]
    public bool DateFlag {get; set;} // make sure the date also updates tracking

    [CommandOption("--toc|--tableofcontents")]
    [Description(
        "Flag to update the table of contents."
    )]
    public bool TocFlag {get; set;}
}