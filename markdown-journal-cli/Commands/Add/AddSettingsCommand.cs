using System;
using System.ComponentModel;
using Spectre.Console.Cli;

namespace markdown_journal_cli.Commands.Add;

public class AddSettings : CommandSettings
{
    [CommandOption("-p|--path")]
    [Description(
            "Specify the path where the journal will be created. If not specified, it will be created in the current directory."
    )]
    [DefaultValue(".")]
    public string FilePath { get; set; } = ".";
}

// lets make a file be composed like this heading_1-heading_2-this_is_the_title
// so if a title or a heading has a space the space is defined as an underscore _
// the seperators to tell where a file is located logically is defined as a dash - 
//with the title of the file entry always being listed last
// ex: heading 1 -> heading 2 -> this is the title = heading_1-heading_2-this_is_the_title

public class AddEntrySettings : AddSettings
{
    
    //title - allow spaces or _ and program to interpret either along. set filename and title to this

    //heading - allow spaces or _ and program to interpret either along.

    //subheadings - force this one to have to be correctly formatted so _ for heading names with multiple words and - to define a child heading. 
    //program subheading to append to heading if defined but take the first match if its not defined. 

    //template - this one could be introduced late with just the basic entry to start 
}

public class AddTableOfContentsSettings : AddSettings
{
    
}

public class AddJournalrcSettings : AddSettings
{
    
}
