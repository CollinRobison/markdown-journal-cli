using System.ComponentModel.DataAnnotations;

namespace markdown_journal_cli;

public class JournalSettings
{

    public const string SectionName = "JournalSettings";
    
    [Required]
    public string AppName { get; set; } = "mdjournal";

    [Required]
    public string JournalConfigFileName {get; set;} = ".journalrc";

    [Required]
    public string DefaultJournalName { get; set; } = "MyJournal";

    [Required]
    public string TableOfContentsFileName {get; set;} = "1a-TableOfContents";

    [Required]
    public string TableOfContentsTitle {get; set;} = "Table of Contents";

    [Required]
    public string IntroductionFileName {get; set;} = "1b-Intro";

    [Required]
    public string IntroductionTitle {get; set;} = "Introduction";

    [Required] 
    public string JournalEntryTemplateFileName {get; set;} =  "1c-Journal-Entry-Template";

    [Required]
    public string JournalEntryTemplateTitle {get; set;} = "Journal Entry Template"; 

    [Required] 
    public string AllJournalsFileName = "1h-All-My-Journals";

    [Required]
    public string AllJournalsTitle {get; set;} = "All My Journals";

    [Required]
    public string TitleSpaceSeperator {get; set;} = "-";

    [Required]
    public string HeadingSeperator {get; set;} = "_";

}
