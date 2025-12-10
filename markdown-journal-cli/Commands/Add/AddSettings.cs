using System;
using System.ComponentModel;
using Spectre.Console;
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
    [CommandArgument(0, "<name>")]
    [Description(
        "The name of the file to create."
    )]
    public required string EntryName { get; set; }

    //heading - allow spaces or _ and program to interpret either along.
    [CommandOption("--he|--heading")]
    [Description(
        "The heading that the new entry will fall under in the table of contents. If the subheading option is also specified then the heading will prepend to that."
    )]
    public string? Heading { get; set; }

    //subheadings - force this one to have to be correctly formatted so _ for heading names with multiple words and - to define a child heading. 
    //program subheading to append to heading if defined but take the first match if its not defined. 
    [CommandOption("--sh|--subheading")]
    [Description(
        "The subheadings that the new entry will fall under in the table of contents. Nested subheadings must be seperated by - and if a subheading has multiple words the spaces must be seperated by _. ex: heading_one-heading_two. If the heading option is also specified the subheading will append to that."
    )]
    public string? Subheading {get; set;}

    //template - this one could be introduced late with just the basic entry to start 

    public override ValidationResult Validate()
    {   
        if(!EntryName.All(c => char.IsLetterOrDigit(c) || c == '_' || c == ' '))
        {
            return ValidationResult.Error("Entry name contains a character that is not a letter, digit, underscore, or space.");
        }
        if(!string.IsNullOrEmpty(Heading) && !Heading.All(c => char.IsLetterOrDigit(c) || c == '_' || c == ' '))
        {
            return ValidationResult.Error("Heading name contains a character that is not a letter, digit, underscore, or space.");
        }
        if(!string.IsNullOrEmpty(Subheading) && !Subheading.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '-'))
        {
            return ValidationResult.Error("Subheadings contains a character that is not a letter, digit, underscore, or hyphen.");
        }
        
        return ValidationResult.Success();
    }
}

public class AddTableOfContentsSettings : AddSettings
{
    
}

public class AddJournalrcSettings : AddSettings
{
    
}
