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

}
