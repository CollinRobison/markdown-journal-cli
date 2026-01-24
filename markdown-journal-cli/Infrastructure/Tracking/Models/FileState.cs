using System;

namespace markdown_journal_cli.Infrastructure.Tracking.Models;

public class FileState
{
    public required string FilePath { get; set; }
    public required string Hash { get; set; }
    public DateTime LastChecked { get; set; }
}

public class JournalIndex
{
    public Dictionary<string, FileState> Files {get; set;} = [];
}

public class ChangeDetectionResult
{
    public List<string> AddedFiles {get; set;} = []; 
    public List<string> ModifiedFiles {get; set;} = []; 
    public List<string> DeletedFiles {get; set;} = []; 

    public bool HasChanges => AddedFiles.Count != 0 || ModifiedFiles.Count != 0 || DeletedFiles.Count != 0;
}

