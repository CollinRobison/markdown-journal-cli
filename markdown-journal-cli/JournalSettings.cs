using System.ComponentModel.DataAnnotations;

namespace markdown_journal_cli;

public class JournalSettings
{

    public const string SectionName = "Journal";
    
    [Required]
    public string DefaultJournalName { get; set; } = "MyJournal";

}
