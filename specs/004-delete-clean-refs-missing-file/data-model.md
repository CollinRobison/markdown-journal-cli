# Data Model: Delete Entry --clean-refs Tolerates Already-Deleted Files

No new persistent entities or schema changes. This feature modifies service orchestration logic only.

## Affected Method Signatures

### `IRemoveEntryService` (interface)

```csharp
// Before
void ValidatePreconditions(string journalPath, string fileName);

// After
void ValidatePreconditions(string journalPath, string fileName, bool cleanRefs = false);
```

### `RemoveEntryService` (implementation)

```csharp
// Private helper — before
private (string resolvedFileName, string absoluteEntryPath) ResolveAndValidate(
    string journalPath, string fileName)

// Private helper — after
private (string resolvedFileName, string absoluteEntryPath, bool fileExists) ResolveAndValidate(
    string journalPath, string fileName, bool cleanRefs = false)
```

### `IRemoveEntryService` — `RemoveEntry` return type (US3)

```csharp
// Before (returns stripped-link file list directly)
IReadOnlyList<string> RemoveEntry(string journalPath, string fileName, bool cleanRefs);

// After (returns structured result; enables honest command output per FR-007/FR-008)
RemoveEntryResult RemoveEntry(string journalPath, string fileName, bool cleanRefs);
```

### `RemoveEntryResult` (new record — US3)

Carries per-operation outcome flags so the command layer can produce honest output (FR-007) and always show the stripped-link count (FR-008) without false "removed" claims.

```csharp
record RemoveEntryResult(
    bool FileExistedOnDisk,                  // true if the .md file was present when the command ran
    bool RemovedFromConfig,                  // true if the entry was in .journalrc and was removed
    bool RemovedFromTracking,                // true if the entry was in .mdjournal and was removed
    IReadOnlyList<string> StrippedLinkFiles  // paths of files whose dead links were stripped
);
```

## State Transitions

```
Call: remove entry <file> --clean-refs
        │
        ▼
ResolveAndValidate(cleanRefs: true)
   ├── .journalrc missing?  → throw JournalrcNotFoundException
   ├── tracking index missing? → throw TrackingIndexNotFoundException
   ├── protected file? → throw ProtectedJournalFileException
   └── entry file missing?
         ├── cleanRefs=false → throw FileNotFoundException   (existing behaviour)
         └── cleanRefs=true  → continue; fileExists = false  (NEW behaviour)

        │
        ▼
RemoveEntry orchestration
   ├── fileExists=true  → tx.TrackDelete + DeleteFile
   └── fileExists=false → skip (no-op; file already gone)
   ├── removedFromConfig   = _journalConfiguration.RemoveEntry(...)   (captures bool return)
   ├── index               = _fileTracking.LoadIndex(...)             (pre-read; required for tracking check)
   ├── removedFromTracking = index.Files.ContainsKey(resolvedFileName) before RemoveFileFromIndex
   ├── _fileTracking.RemoveFileFromIndex(...)
   ├── UpdateTableOfContents     (only when removedFromConfig = true; skip if entry was already absent from config)
   ├── cleanRefs=true → StripLinksInDirectory + re-hash
   └── return new RemoveEntryResult(fileExists, removedFromConfig, removedFromTracking, strippedFiles)
```
