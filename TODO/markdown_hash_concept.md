Perfect! Here's a clean, focused implementation for just the hash tracking core functionality:

## Core Hash Tracking Implementation

```csharp
// Models/FileState.cs
namespace MarkdownJournalCli.Models
{
    public class FileState
    {
        public string FilePath { get; set; }
        public string Hash { get; set; }
        public DateTime LastChecked { get; set; }
    }

    public class JournalIndex
    {
        public Dictionary<string, FileState> Files { get; set; } = new();
    }

    public class ChangeDetectionResult
    {
        public List<string> AddedFiles { get; set; } = new();
        public List<string> ModifiedFiles { get; set; } = new();
        public List<string> DeletedFiles { get; set; } = new();

        public bool HasChanges => AddedFiles.Any() || ModifiedFiles.Any() || DeletedFiles.Any();
    }
}
```

```csharp
// Services/HashService.cs
using System. Security.Cryptography;

namespace MarkdownJournalCli.Services
{
    public class HashService
    {
        public string ComputeFileHash(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hash = sha256.ComputeHash(stream);
            return BitConverter. ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }
}
```

```csharp
// Services/ChangeDetectionService.cs
using System.Text. Json;
using MarkdownJournalCli. Models;

namespace MarkdownJournalCli.Services
{
    public class ChangeDetectionService
    {
        private readonly string _indexPath;
        private readonly string _journalRoot;
        private readonly HashService _hashService;

        public ChangeDetectionService(string journalRoot)
        {
            _journalRoot = journalRoot;
            var metadataDir = Path.Combine(journalRoot, ". markdown-journal");
            Directory.CreateDirectory(metadataDir);
            _indexPath = Path.Combine(metadataDir, "index.json");
            _hashService = new HashService();
        }

        /// <summary>
        /// Load the index from disk.  Returns empty index if file doesn't exist.
        /// </summary>
        public JournalIndex LoadIndex()
        {
            if (!File. Exists(_indexPath))
                return new JournalIndex();

            var json = File.ReadAllText(_indexPath);
            return JsonSerializer.Deserialize<JournalIndex>(json) ?? new JournalIndex();
        }

        /// <summary>
        /// Save the index to disk.
        /// </summary>
        public void SaveIndex(JournalIndex index)
        {
            var json = JsonSerializer. Serialize(index, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(_indexPath, json);
        }

        /// <summary>
        /// Get all markdown files in the journal directory (excluding metadata directory).
        /// </summary>
        private HashSet<string> GetCurrentMarkdownFiles()
        {
            return Directory.GetFiles(_journalRoot, "*.md", SearchOption. AllDirectories)
                .Where(f => !f.Contains(". markdown-journal"))
                .Select(f => Path.GetRelativePath(_journalRoot, f))
                .ToHashSet();
        }

        /// <summary>
        /// Detect all changes:  added, modified, and deleted files.
        /// Updates the index automatically.
        /// </summary>
        public ChangeDetectionResult DetectChanges()
        {
            var result = new ChangeDetectionResult();
            var oldIndex = LoadIndex();
            var newIndex = new JournalIndex();

            // Get current files on disk
            var currentFiles = GetCurrentMarkdownFiles();

            // Check all files that exist on disk
            foreach (var relativeFilePath in currentFiles)
            {
                var fullPath = Path.Combine(_journalRoot, relativeFilePath);
                var currentHash = _hashService.ComputeFileHash(fullPath);

                // Add to new index
                newIndex.Files[relativeFilePath] = new FileState
                {
                    FilePath = relativeFilePath,
                    Hash = currentHash,
                    LastChecked = DateTime.UtcNow
                };

                // Determine if added or modified
                if (! oldIndex.Files.ContainsKey(relativeFilePath))
                {
                    // New file
                    result.AddedFiles.Add(relativeFilePath);
                }
                else if (oldIndex.Files[relativeFilePath].Hash != currentHash)
                {
                    // Modified file
                    result.ModifiedFiles.Add(relativeFilePath);
                }
                // else: unchanged file, no action needed
            }

            // Check for deleted files (in old index but not on disk)
            foreach (var trackedFile in oldIndex.Files. Keys)
            {
                if (!currentFiles.Contains(trackedFile))
                {
                    result.DeletedFiles.Add(trackedFile);
                }
            }

            // Save the updated index
            SaveIndex(newIndex);

            return result;
        }

        /// <summary>
        /// Detect changes without updating the index (dry run).
        /// </summary>
        public ChangeDetectionResult DetectChangesWithoutUpdate()
        {
            var result = new ChangeDetectionResult();
            var oldIndex = LoadIndex();
            var currentFiles = GetCurrentMarkdownFiles();

            foreach (var relativeFilePath in currentFiles)
            {
                var fullPath = Path. Combine(_journalRoot, relativeFilePath);
                var currentHash = _hashService.ComputeFileHash(fullPath);

                if (!oldIndex.Files.ContainsKey(relativeFilePath))
                {
                    result.AddedFiles.Add(relativeFilePath);
                }
                else if (oldIndex.Files[relativeFilePath].Hash != currentHash)
                {
                    result.ModifiedFiles.Add(relativeFilePath);
                }
            }

            foreach (var trackedFile in oldIndex.Files.Keys)
            {
                if (!currentFiles.Contains(trackedFile))
                {
                    result.DeletedFiles.Add(trackedFile);
                }
            }

            return result;
        }

        /// <summary>
        /// Update the index with current state of all files without returning changes.
        /// Useful after creating/updating files through the CLI.
        /// </summary>
        public void UpdateIndex()
        {
            var newIndex = new JournalIndex();
            var currentFiles = GetCurrentMarkdownFiles();

            foreach (var relativeFilePath in currentFiles)
            {
                var fullPath = Path.Combine(_journalRoot, relativeFilePath);
                var currentHash = _hashService.ComputeFileHash(fullPath);

                newIndex.Files[relativeFilePath] = new FileState
                {
                    FilePath = relativeFilePath,
                    Hash = currentHash,
                    LastChecked = DateTime.UtcNow
                };
            }

            SaveIndex(newIndex);
        }

        /// <summary>
        /// Update the index for a specific file.
        /// Useful after creating or updating a single file. 
        /// </summary>
        public void UpdateFileInIndex(string relativeFilePath)
        {
            var index = LoadIndex();
            var fullPath = Path.Combine(_journalRoot, relativeFilePath);

            if (File.Exists(fullPath))
            {
                var hash = _hashService.ComputeFileHash(fullPath);
                index.Files[relativeFilePath] = new FileState
                {
                    FilePath = relativeFilePath,
                    Hash = hash,
                    LastChecked = DateTime.UtcNow
                };
            }
            else
            {
                // File doesn't exist, remove from index
                index.Files.Remove(relativeFilePath);
            }

            SaveIndex(index);
        }

        /// <summary>
        /// Remove a file from the index (after deletion).
        /// </summary>
        public void RemoveFileFromIndex(string relativeFilePath)
        {
            var index = LoadIndex();
            index.Files.Remove(relativeFilePath);
            SaveIndex(index);
        }
    }
}
```

## Usage Examples

```csharp
// Example 1: Detect all changes
var service = new ChangeDetectionService("~/my-journal");
var changes = service.DetectChanges();

if (changes.HasChanges)
{
    Console.WriteLine("Added files:");
    foreach (var file in changes.AddedFiles)
        Console.WriteLine($"  + {file}");

    Console.WriteLine("Modified files:");
    foreach (var file in changes.ModifiedFiles)
        Console.WriteLine($"  M {file}");

    Console.WriteLine("Deleted files:");
    foreach (var file in changes.DeletedFiles)
        Console.WriteLine($"  - {file}");
}
else
{
    Console.WriteLine("No changes detected.");
}
```

```csharp
// Example 2: Check without updating (dry run)
var service = new ChangeDetectionService("~/my-journal");
var changes = service.DetectChangesWithoutUpdate();

if (changes.ModifiedFiles.Any())
{
    Console.WriteLine($"Warning: {changes.ModifiedFiles. Count} files have been modified externally.");
}
```

```csharp
// Example 3: Update index after creating a file
var service = new ChangeDetectionService("~/my-journal");
var newFilePath = "2026-01-01.md";

// ...  create the file ...

service.UpdateFileInIndex(newFilePath); // Track the new file
```

```csharp
// Example 4: Update entire index after bulk operations
var service = new ChangeDetectionService("~/my-journal");

// ...  perform multiple file operations ...

service.UpdateIndex(); // Refresh entire index
```

This gives you a clean, simple API to work with: 
- **`DetectChanges()`** - Find and track all changes
- **`DetectChangesWithoutUpdate()`** - Check changes without saving
- **`UpdateIndex()`** - Refresh entire index
- **`UpdateFileInIndex()`** - Update single file
- **`RemoveFileFromIndex()`** - Remove deleted file

All the complexity is hidden, and you just get back what was added, modified, or deleted! 