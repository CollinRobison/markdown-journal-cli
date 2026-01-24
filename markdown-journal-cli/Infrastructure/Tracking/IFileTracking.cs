using System;
using markdown_journal_cli.Infrastructure.Tracking.Models;

namespace markdown_journal_cli.Infrastructure.Tracking;

public interface IFileTracking
{
    /// <summary>
    /// Load the index from disk.  Returns empty index if file doesn't exist.
    /// </summary>
    /// <param name="path">the journal directory path.</param>
    /// <returns>empty index if file doesn't exist..</returns>
    public JournalIndex LoadIndex(string path);

    /// <summary>
    /// Save the index to disk.
    /// </summary>
    /// <param name="path">the journal directory path.</param>
    /// <param name="index">The Index file to save to.</param>
    public void SaveIndex(JournalIndex index, string path); 

     /// <summary>
    /// Detect all changes:  added, modified, and deleted files.
    /// Updates the index automatically.
    /// </summary>
    /// <param name="path">the journal directory path.</param>
    /// <returns>Added, modified, and deleted files results</returns>
    public ChangeDetectionResult DetectChanges(string path);
    
    /// <summary>
    /// Detect changes without updating the index (dry run).
    /// </summary>
    /// <param name="path">the journal directory path.</param>
    /// <returns>Added, modified, and deleted files results</returns>
    public ChangeDetectionResult DetectChangesWithoutUpdate(string path);

    /// <summary>
    /// Update the index with current state of all files without returning changes.
    /// Useful after creating/updating files through the CLI.
    /// </summary>
    /// <param name="path">the journal directory path.</param>
    public void UpdateIndex(string path);

    /// <summary>
    /// Update the index for a specific file.
    /// Useful after creating or updating a single file. 
    /// </summary>
    /// <param name="path">the journal directory path.</param>
    /// <param name="relativeFilePath">the relative file path of file to update in index.</param>
    public void UpdateFileInIndex(string path, string relativeFilePath);

    /// <summary>
    /// Remove a file from the index (after deletion).
    /// </summary>
    /// <param name="path">the journal directory path.</param>
    /// <param name="relativeFilePath">the relative file path of file to update in index.</param>
    public void RemoveFileFromIndex(string path, string relativeFilePath);
}