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
   ├── RemoveEntry from config   (always)
   ├── RemoveFileFromIndex       (always)
   ├── UpdateTableOfContents     (always)
   └── cleanRefs=true → StripLinksInDirectory + re-hash (always when flag set)
```
