# PRD: File Rollback System
**Status:** APPROVED — All design decisions resolved  
**Author:** GitHub Copilot  
**Date:** 2026-03-28  
**For:** Multi-Agent Autopilot Implementation

---

## Table of Contents
1. [Overview](#1-overview)
2. [Problem Statement](#2-problem-statement)
3. [Goals and Non-Goals](#3-goals-and-non-goals)
4. [Architectural Decision Record](#4-architectural-decision-record)
5. [New Types — Full C# Specifications](#5-new-types)
6. [Modified Types](#6-modified-types)
7. [Service Integration Map](#7-service-integration-map)
8. [Spectre.Console Output Specification](#8-spectreconsole-output-specification)
9. [Logging Strategy](#9-logging-strategy)
10. [DI Registration Changes](#10-di-registration-changes)
11. [Test Strategy](#11-test-strategy)
12. [Implementation Checklist](#12-implementation-checklist-ordered-for-autopilot)
13. [Resolved Decisions](#13-resolved-decisions)

---

## 1. Overview

`markdown-journal-cli` performs multi-file write operations across `.mdjournal`, `.journalrc`,
entry `.md` files, and the Table of Contents when commands run. These operations are currently
**not transactional** — a failure mid-way leaves no automatic recovery path.

This PRD specifies the design, interface contracts, service integration plan, test strategy, and
user output format for a **file rollback system** that brings execute-then-compensate atomicity
to all multi-file write paths, without external dependencies or OS-specific kernel transactions.

The infrastructure foundation (`IInMemoryFileBuffer` with `Snapshot`/`Restore`) already exists.
The missing pieces are:
1. A **coordinator** that orchestrates snapshots, tracks renames/creations/deletions, and drives
   rollback on failure.
2. An **ambient transaction pattern** so `update journal`'s four sub-operations share one outer
   scope that rolls everything back together on failure.
3. A `TrackNewDirectory` mechanism so `new journal` cleans up a partially created directory,
   leaving no trace.

---

## 2. Problem Statement

### Current Failure Scenarios

| Command | Failure Point | Inconsistent State Left Behind |
|---|---|---|
| `update journal` | TOC write fails after config sync | `.journalrc` updated, TOC stale |
| `update journal` | Config sync fails after entry files stamped | Entries stamped but config out of sync |
| `update journal --rename-toc` | Backlink rewrite fails | TOC renamed, `.journalrc` stale, some `.md` files broken |
| `update entry` | TOC fails after rename+config | Entry renamed, config updated, TOC stale |
| `remove entry` | TOC fails after deletion | Entry gone, TOC stale, tracking inconsistent |
| `remove entry --clean-refs` | `StripLinksInDirectory` fails mid-scan | Some files stripped, others not |
| `add entry` | Config update fails after file creation | Orphaned `.md` not in `.journalrc` |
| `add toc` | TOC generation fails after `.journalrc` update | `.journalrc` references missing TOC file |
| `init journal` | TOC creation fails after config generation | `.journalrc` exists, TOC missing |
| `new journal` | Any step fails part-way | Partial directory left on disk |

### What Already Exists
- `IInMemoryFileBuffer.Snapshot(path)` / `Restore(path)` / `Clear()` — ready, unused for rollback
- Architecture docs mark rollback as "future wiring"
- `IMarkdownLinkRewriter.FindFilesWithLinkTo(directory, fileName)` — pre-scan API exists

---

## 3. Goals and Non-Goals

### Goals (this implementation)
- Rollback all write operations for `update journal` (all sub-paths); entire command rolls back
  as one transaction via ambient scope pattern
- Rollback `update entry`, `remove entry` (with/without `--clean-refs`)
- Rollback `add entry` (orphaned file + partial config/tracking/TOC writes)
- Rollback `add toc` (TOC file + `.journalrc` update)
- Rollback `add journalrc` (partially-written `.journalrc` cleaned up on failure)
- Rollback `add file-tracking` (partially-written `.mdjournal` cleaned up on failure)
- Rollback `init journal` (`.mdjournal`, `.journalrc`, TOC file)
- Rollback `new journal` (all created files + the journal directory itself)
- Rollback file renames, deletions, and new-file creations in reverse order
- User-visible rollback report with paths relative to journal root
- `ILogger` trace at every rollback step
- `IInMemoryFileBuffer.Clear()` called at end of every transaction
- Comprehensive test coverage via failure injection at each write step
- Idempotent rollback (safe to call multiple times)
- Cross-platform (no OS-specific APIs)
- Exit codes: `0` success, `1` generic failure, `2` failed+fully rolled back, `3` failed+rollback
  had errors; documented in `ARCHITECTURE.md`
- Extensible deletion strategy via `IDeletionRollbackStrategy` interface

### Non-Goals (future work)
- WAL/sentinel file for crash recovery (process kill, power loss)
- Concurrency / multi-process journal access

---

## 4. Architectural Decision Record

### Option B — Dedicated `IFileTransactionScope` / `FileTransactionCoordinator`

Selected over:
- Option A (extend `IInMemoryFileBuffer` directly) — violates SRP; contamination risk
- Option C (RAII wrapper around buffer) — same singleton lifetime concern
- Option D (WAL sentinel file) — overly complex for single-user CLI

**Rationale:** Single responsibility, explicit RAII boundary, testable, no singleton state
cross-contamination, composable for future commands.

### 4.1 Ambient Transaction Pattern (Q1: one outer transaction for `update journal`)

`IFileTransactionCoordinator` exposes:
- `Begin()` — creates a new root scope, sets it as `_current` (thread-local)
- `BeginOrJoin()` — if `_current` is active, returns a `JoinedTransactionScope` (lightweight
  wrapper that delegates all `Track*` and `Rollback()` to the root scope, with no-op `Commit()`);
  otherwise behaves identically to `Begin()`

`UpdateCommand` calls `Begin()` to own the outer scope. Each `JournalUpdateService` method
calls `BeginOrJoin()`. If any service fails, `Rollback()` on the joined scope delegates to root —
rolling back ALL prior sub-operations.

All other services call `Begin()` directly (standalone transactions).

`JoinedTransactionScope` is an internal class, not DI-registered:
- `Track*` calls delegate to root
- `Commit()` sets `_committed = true` and `_sealed = true` (no-op on root)
- `Rollback()` sets `_sealed = true` and delegates to root
- `Dispose()`: if `!_sealed`, calls `Rollback()` (fail-safe)
- `IsCommitted` returns `_committed` — only `true` on the commit path, NOT after rollback

### 4.2 `IDeletionRollbackStrategy` Abstraction (Q2: flexible deletion rollback)

```csharp
public interface IDeletionRollbackStrategy
{
    void Capture(string absolutePath, string content); // called BEFORE deletion
    void Restore(IFileSystem fileSystem, string absolutePath); // called DURING rollback
    void Release(string absolutePath); // called on commit
}
```

Default: `InMemoryDeletionRollbackStrategy` (Dictionary<string, string>).
To swap to soft-delete: change only the DI registration.

### 4.3 Directory Rollback (`new journal`) — `TrackNewDirectory` + `IFileSystem.DeleteDirectory`

`IFileTransactionScope` gains `TrackNewDirectory(string absolutePath)`.
`RollbackEntryKind` gains `NewDirectory`.
On rollback, `IFileSystem.DeleteDirectory(path)` is called after all file entries are reversed.

`TrackNewDirectory` is registered **before** any `TrackNew` calls for files inside the directory
so that reverse-order rollback processes the directory deletion LAST.

**⚠ Ordering constraint is load-bearing:** if any `TrackNew` call is placed before
`TrackNewDirectory`, rollback will attempt `DeleteDirectory` while files are still inside it,
fail with a non-empty-directory error, and surface as a `RollbackResult.Failed` entry.
The test `Should_Rollback_NewDirectory_After_All_New_Files_Inside_Are_Deleted` (§11.1)
mechanically enforces this invariant.

### 4.4 Exit Code Contract — `RollbackCompletedException`

After rollback, services throw `RollbackCompletedException(RollbackResult, Exception)` instead
of re-throwing the original. Commands catch this before `Exception` and return:
- `2` if `ex.Result.IsFullyRestored` — fully rolled back, safe to retry
- `3` if `!ex.Result.IsFullyRestored` — partial rollback, manual inspection needed

---

## 5. New Types

### 5.1 File Layout

```
markdown-journal-cli/
  Infrastructure/
    Transactions/
      IFileTransactionScope.cs             NEW
      FileTransactionScope.cs              NEW  (internal root scope)
      JoinedTransactionScope.cs            NEW  (internal, not DI-registered)
      IFileTransactionCoordinator.cs       NEW
      FileTransactionCoordinator.cs        NEW
      IDeletionRollbackStrategy.cs         NEW
      InMemoryDeletionRollbackStrategy.cs  NEW
      IRollbackReporter.cs                 NEW
      RollbackReporter.cs                  NEW
      RollbackCompletedException.cs        NEW
      Models/
        RollbackEntry.cs                   NEW
        RollbackEntryKind.cs               NEW
        RollbackFailure.cs                 NEW
        RollbackResult.cs                  NEW

markdown-journal-cli.Tests/
  Infrastructure/
    Transactions/
      FileTransactionScopeTests.cs         NEW
      JoinedTransactionScopeTests.cs       NEW
      FileTransactionCoordinatorTests.cs   NEW
      RollbackReporterTests.cs             NEW
  Services/
    JournalEntry/
      JournalEntryServiceRollbackTests.cs  NEW
    InitJournal/
      InitJournalServiceRollbackTests.cs   NEW
    NewJournal/
      NewJournalServiceRollbackTests.cs    NEW
  Commands/
    Add/
      AddTableOfContentsRollbackTests.cs   NEW
      AddJournalrcRollbackTests.cs         NEW
      AddFileTrackingRollbackTests.cs      NEW
```

### 5.2 `RollbackEntryKind` (Enum)

```csharp
namespace markdown_journal_cli.Infrastructure.Transactions.Models;

public enum RollbackEntryKind
{
    Modify,        // existed + modified. Rollback: restore snapshot content
    New,           // created new file. Rollback: delete it
    Rename,        // renamed OldPath -> NewPath. Rollback: rename NewPath back
    Delete,        // deleted. Rollback: re-create from snapshotted content
    NewDirectory,  // directory created. Rollback: DeleteDirectory (after all files inside deleted)
}
```

### 5.3 `RollbackEntry` (Record)

```csharp
namespace markdown_journal_cli.Infrastructure.Transactions.Models;

public record RollbackEntry(
    string AbsolutePath,       // for Rename: original (pre-rename) path
    RollbackEntryKind Kind,
    string? NewPath = null     // for Rename only: the post-rename path
);
```

### 5.4 `RollbackFailure` (Record) + `RollbackResult` (Record)

```csharp
namespace markdown_journal_cli.Infrastructure.Transactions.Models;

/// <summary>
/// A single rollback step that could not be reversed.
/// Using a named record (instead of an anonymous tuple) keeps test assertions readable
/// and prevents element-name drift if the field set ever needs to grow.
/// </summary>
public record RollbackFailure(RollbackEntry Entry, Exception Error);

public record RollbackResult(
    IReadOnlyList<RollbackEntry> Restored,
    IReadOnlyList<RollbackFailure> Failed
)
{
    public bool IsFullyRestored => Failed.Count == 0;
}
```

**File:** `Infrastructure/Transactions/Models/RollbackFailure.cs` (NEW — see §5.1 layout)

### 5.5 `RollbackCompletedException`

```csharp
namespace markdown_journal_cli.Infrastructure.Transactions;

public sealed class RollbackCompletedException : Exception
{
    public RollbackResult Result { get; }

    public RollbackCompletedException(RollbackResult result, Exception originalCause)
        : base(originalCause.Message, originalCause) { Result = result; }
}
```

### 5.6 `IDeletionRollbackStrategy` + `InMemoryDeletionRollbackStrategy`

```csharp
public interface IDeletionRollbackStrategy
{
    void Capture(string absolutePath, string content);
    void Restore(IFileSystem fileSystem, string absolutePath);
    void Release(string absolutePath); // called on Commit() to free snapshot memory
}

// Default implementation:
internal sealed class InMemoryDeletionRollbackStrategy : IDeletionRollbackStrategy
{
    // INVARIANT: _snapshots must be empty at the start of every transaction.
    // This is safe because the CLI is single-threaded and transactions never overlap.
    // If this assumption ever changes, switch to a scope-owned instance instead of singleton.
    private readonly Dictionary<string, string> _snapshots
        = new(StringComparer.OrdinalIgnoreCase);

    public void Capture(string path, string content)
    {
        Debug.Assert(!_snapshots.ContainsKey(path),
            $"DeletionRollbackStrategy already has a snapshot for '{path}'. " +
            "This indicates overlapping transactions, which is not supported.");
        _snapshots[path] = content;
    }

    public void Restore(IFileSystem fs, string path)
    {
        var content = _snapshots[path];
        fs.CreateFile(Path.GetDirectoryName(path)!, Path.GetFileName(path)!, content);
    }

    public void Release(string path) => _snapshots.Remove(path);
}
```

**Singleton safety contract:** `InMemoryDeletionRollbackStrategy` is registered as a singleton
because the CLI processes one command at a time (single-threaded, no concurrent transactions).
The `Debug.Assert` in `Capture` will surface violations in test runs immediately.

### 5.7 `IFileTransactionScope` (Interface)

```csharp
namespace markdown_journal_cli.Infrastructure.Transactions;

public interface IFileTransactionScope : IDisposable
{
    // Call BEFORE each write. Track* variants:
    void Track(string absolutePath);                    // existing file, will be modified
    void TrackNew(string absolutePath);                 // new file, will be created
    void TrackRename(string oldPath, string newPath);   // file will be renamed
    void TrackDelete(string absolutePath);              // file will be deleted
    void TrackNewDirectory(string absolutePath);        // directory will be created

    void Commit();
    RollbackResult Rollback();    // idempotent; subsequent calls return empty result

    bool IsCommitted { get; }
    bool IsRolledBack { get; }
}
```

**`FileTransactionScope` Implementation Notes:**

Constructor (internal, created by `FileTransactionCoordinator.Begin()` only):
```csharp
internal FileTransactionScope(
    IFileSystem fileSystem,
    IInMemoryFileBuffer buffer,  // only used for Clear() — see note below
    IDeletionRollbackStrategy deletionStrategy,
    ILogger<FileTransactionScope> logger,
    Action onDisposed   // clears coordinator._current
)
```

**Note on `IInMemoryFileBuffer` dependency:** `FileTransactionScope` takes the full
`IInMemoryFileBuffer` but only ever calls `Clear()` on it. An `Action onClear` callback
passed from the coordinator would be a narrower coupling and easier to test in isolation.
This is a low-priority refactor — implement as `Action onClear` if the buffer interface
ever gains methods that would conflict with the scope's semantics.

`Track(path)`: first-write-wins — guard against _committed/_rolledBack, snapshot content only if
not already in `_modifySnapshots`, append `RollbackEntry(path, Modify)`.

**Pre-condition:** `path` must exist on disk — `GetFileContent` will throw if it does not.
Callers are responsible for ensuring tracked paths exist; pre-flight validation handles this.
If `GetFileContent` throws (e.g., file not found), the exception propagates through the
try/catch → rollback returns an empty result → `RollbackCompletedException` is thrown with
`IsFullyRestored=true` → exit code 2. This is acceptable; the correct fix-and-retry guidance
surfaced to the user is the same regardless of which code (1 or 2) is returned in this case.

`TrackDelete(path)`: calls `_deletionStrategy.Capture(path, content)`, appends Delete entry.

`TrackNewDirectory(path)`: appends `NewDirectory` entry only (no snapshot needed).

`Commit()`: sets `_committed = true`, releases all deletion snapshots, clears `_entries` and
`_modifySnapshots`, calls `_buffer.Clear()`, calls `_onDisposed()`.

`Rollback()`:
- If `_committed` or `_rolledBack`: return empty `RollbackResult`
- Set `_rolledBack = true`
- Iterate `_entries` in REVERSE ORDER:
  - `Modify`: `_fileSystem.UpdateFile(dir, file, _modifySnapshots[path])`
  - `New`: if file exists, `_fileSystem.DeleteFile(path)`
  - `Rename`: if `entry.NewPath` exists, `_fileSystem.RenameFile(entry.NewPath, path)`
  - `Delete`: `_deletionStrategy.Restore(_fileSystem, path)`
  - `NewDirectory`: if directory exists, `_fileSystem.DeleteDirectory(path)`
  - On exception: add to `failed` list; log Error
- Call `_buffer.Clear()`, call `_onDisposed()`
- Return `new RollbackResult(restored, failed)`

`Dispose()`: if `!_committed && !_rolledBack`, call `Rollback()`.

### 5.8 `JoinedTransactionScope` (Internal Class)

```csharp
internal sealed class JoinedTransactionScope(IFileTransactionScope root) : IFileTransactionScope
{
    private bool _sealed    = false;  // true once Commit() OR Rollback() called; guards Dispose()
    private bool _committed = false;  // true only on the Commit() path

    public void Track(string p)               => root.Track(p);
    public void TrackNew(string p)            => root.TrackNew(p);
    public void TrackRename(string o, string n) => root.TrackRename(o, n);
    public void TrackDelete(string p)         => root.TrackDelete(p);
    public void TrackNewDirectory(string p)   => root.TrackNewDirectory(p);

    public void Commit()        { _committed = true; _sealed = true; }  // no-op on root
    public RollbackResult Rollback()
    {
        _sealed = true;
        return root.Rollback();   // delegates — rolls back entire outer transaction
    }

    public bool IsCommitted  => _committed;        // false after rollback — semantically correct
    public bool IsRolledBack => root.IsRolledBack;

    public void Dispose() { if (!_sealed) Rollback(); }
}
```

A service using `BeginOrJoin()` that exits its `using` block without `Commit()` will trigger
`Dispose()` → `Rollback()` on the entire outer scope. This is the intended fail-safe.

### 5.9 `IFileTransactionCoordinator` (Interface)

```csharp
namespace markdown_journal_cli.Infrastructure.Transactions;

public interface IFileTransactionCoordinator
{
    /// Creates a new root scope. Sets it as the ambient Current for this thread.
    /// Use in command-level handlers and standalone services.
    IFileTransactionScope Begin();

    /// If an active ambient scope (Current) exists, returns a JoinedTransactionScope
    /// that delegates Track* and Rollback() to it (Commit() is a no-op on the root).
    /// Otherwise behaves identically to Begin().
    /// Use in services that may be called inside an outer command-level transaction.
    IFileTransactionScope BeginOrJoin();

    /// The currently active ambient scope for this thread. Null if none in progress.
    IFileTransactionScope? Current { get; }
}
```

**`FileTransactionCoordinator` Implementation Notes:**

```csharp
// Thread-local ambient scope
[ThreadStatic] private static IFileTransactionScope? _current;
public IFileTransactionScope? Current => _current;

// Constructor:
public FileTransactionCoordinator(
    IFileSystem fileSystem,
    IInMemoryFileBuffer buffer,
    IDeletionRollbackStrategy deletionStrategy,
    ILoggerFactory loggerFactory
)

public IFileTransactionScope Begin()
{
    if (_current is { IsCommitted: false, IsRolledBack: false })
        throw new InvalidOperationException(
            "A transaction scope is already active on this thread. " +
            "Use BeginOrJoin() to participate in the existing scope.");
    var scope = new FileTransactionScope(
        _fileSystem, _buffer, _deletionStrategy,
        _loggerFactory.CreateLogger<FileTransactionScope>(),
        onDisposed: () => _current = null
    );
    _current = scope;
    return scope;
}

public IFileTransactionScope BeginOrJoin()
{
    var current = _current;
    if (current is { IsCommitted: false, IsRolledBack: false })
        return new JoinedTransactionScope(current);
    return Begin();
}
```

### 5.10 `IRollbackReporter` + `RollbackReporter`

```csharp
public interface IRollbackReporter
{
    void ReportRollbackStarting(string operationDescription, Exception cause);
    void ReportRollbackComplete(RollbackResult result, string journalRoot);
}
```

Constructor: `RollbackReporter(IAnsiConsole console, ILogger<RollbackReporter> logger)`

See section 8 for complete output specification.

---

## 6. Modified Types

### 6.1 `IFileSystem` — Add `DeleteDirectory`

```csharp
/// Deletes the specified directory. Throws if not empty or does not exist.
void DeleteDirectory(string path);
```

Implementation in `FileSystem.cs`:
```csharp
public void DeleteDirectory(string path) => Directory.Delete(path);  // non-recursive
```

Implementation in `TestFileSystem`:
```csharp
public List<string> DeletedDirectories { get; } = new();
public void DeleteDirectory(string path) => DeletedDirectories.Add(path);
```

### 6.2 Service Constructor Changes

Add `IFileTransactionCoordinator txCoordinator, IRollbackReporter rollbackReporter` to:
- `JournalUpdateService`
- `JournalFileUpdateService`
- `RemoveEntryService`
- `JournalEntryService`
- `InitJournalService`
- `NewJournalService`

### 6.3 Command Constructor Changes

Add `IFileTransactionCoordinator txCoordinator, IRollbackReporter rollbackReporter` to:
- `AddTableOfContents`
- `AddJournalrc`
- `AddFileTracking`

Add `IFileTransactionCoordinator txCoordinator` (no reporter — services handle it) to:
- `UpdateCommand`

### 6.4 Command `catch` Block Changes

All affected commands add before the generic `Exception` catch:

```csharp
catch (RollbackCompletedException ex)
{
    // Reporter already called by service; just return the right exit code
    return ex.Result.IsFullyRestored ? 2 : 3;
}
```

Affected: `UpdateCommand`, `UpdateEntryCommand`, `RemoveEntryCommand`,
`AddEntryCommand`, `InitCommand`, `NewCommand`, `AddTableOfContents`,
`AddJournalrc`, `AddFileTracking`.

---

## 7. Service Integration Map

### 7.1 Pattern — Standalone Service (own scope)

```csharp
using var tx = _txCoordinator.Begin();
try
{
    // tx.Track*(path) for every file BEFORE writing it
    // ... perform writes ...
    tx.Commit();
}
catch (Exception ex)
{
    _rollbackReporter.ReportRollbackStarting("[description]", ex);
    var result = tx.Rollback();
    _rollbackReporter.ReportRollbackComplete(result, journalPath);
    throw new RollbackCompletedException(result, ex);
}
```

### 7.2 Pattern — Service Participating in Outer Scope (joined)

```csharp
using var tx = _txCoordinator.BeginOrJoin();  // only difference from above
// ... same body ...
```

---

### 7.3 `UpdateCommand.Execute` — Outer Ambient Scope

```
using var outerTx = _txCoordinator.Begin()
try:
    // services internally call BeginOrJoin() and join this scope
    _journalUpdateService.UpdateLastEditedDatesAndTracking(...)
    _journalUpdateService.UpdateJournalConfig(...)
    _journalUpdateService.UpdateTableOfContents(...)
    if settings.RenameToc != null: _journalUpdateService.RenameToc(...)
    outerTx.Commit()
    return 0
catch (RollbackCompletedException ex):
    return ex.Result.IsFullyRestored ? 2 : 3
catch (Exception ex):
    _console.MarkupLine("[red]Error:[/] {msg}")
    return 1
```

---

### 7.4 `JournalUpdateService.UpdateLastEditedDatesAndTracking` (BeginOrJoin)

```
using var tx = _txCoordinator.BeginOrJoin()
try:
    tx.Track(mdjournalPath)                         BEFORE first tracking mutation
    foreach modifiedFile: tx.Track(absolutePath)    BEFORE each UpdateFile
    // existing write logic unchanged
    tx.Commit()
catch (Exception ex): report -> rollback -> throw RollbackCompletedException
```

---

### 7.5 `JournalUpdateService.UpdateJournalConfig` (BeginOrJoin)

```
using var tx = _txCoordinator.BeginOrJoin()
try:
    tx.Track(journalrcPath)                         BEFORE any AddEntry/RemoveEntry
    // existing write logic unchanged
    tx.Commit()
catch: report -> rollback -> throw
```

---

### 7.6 `JournalUpdateService.UpdateTableOfContents` (BeginOrJoin)

```
using var tx = _txCoordinator.BeginOrJoin()
try:
    if fileExists(tocAbsPath): tx.Track(tocAbsPath)
    else:                      tx.TrackNew(tocAbsPath)   // first-time TOC creation
    tx.Track(mdjournalPath)
    // existing write logic unchanged
    tx.Commit()
catch: report -> rollback -> throw
```

---

### 7.7 `JournalUpdateService.RenameToc` (BeginOrJoin)

```
using var tx = _txCoordinator.BeginOrJoin()
try:
    // 1. Pre-scan backlinks BEFORE any writes
    foreach relative in FindFilesWithLinkTo(journalPath, currentTocFile):
        tx.Track(CombinePaths(journalPath, relative))
    // 2. Track write targets
    tx.TrackRename(oldTocAbsPath, newTocAbsPath)
    tx.Track(journalrcPath)
    tx.Track(mdjournalPath)
    // 3. Execute existing writes unchanged
    tx.Commit()
catch: report -> rollback -> throw
```

---

### 7.8 `JournalFileUpdateService.UpdateEntry` (Begin — standalone)

```
using var tx = _txCoordinator.Begin()
try:
    // Pre-scan backlinks (only if rename + updateBacklinks)
    if isRenaming and updateBacklinks:
        foreach relative in FindFilesWithLinkTo(directory, currentFile):
            tx.Track(CombinePaths(directory, relative))
    if isRenaming: tx.TrackRename(currentAbsPath, targetAbsPath)
    tx.Track(journalrcPath)
    tx.Track(tocAbsPath)
    // Execute existing write steps unchanged
    tx.Commit()
catch: report -> rollback -> throw
```

---

### 7.9 `RemoveEntryService.RemoveEntry` (Begin — standalone)

Validation steps 1-5 stay outside the transaction boundary.

```
[steps 1-5: existing validation, outside tx]

using var tx = _txCoordinator.Begin()
try:
    if cleanRefs:
        foreach relative in FindFilesWithLinkTo(journalPath, resolvedFileName):
            tx.Track(CombinePaths(journalPath, relative))
    tx.Track(journalrcPath)
    tx.Track(mdjournalPath)
    tx.Track(tocAbsPath)
    tx.TrackDelete(absoluteEntryPath)          snapshot BEFORE DeleteFile
    // Execute steps 6-10 unchanged
    tx.Commit()
catch: report -> rollback -> throw
```

Delete rollback: scope Rollback() sees `Delete` entry, calls
`_deletionStrategy.Restore(_fileSystem, path)` to re-create the entry file.

---

### 7.10 `JournalEntryService.AddEntry` (Begin — standalone)

Pre-flight checks stay outside tx (`JournalrcNotFoundException`, `TrackingIndexNotFoundException`,
`JournalEntryAlreadyExistsException`).

```
[existing pre-flight checks, outside tx]

using var tx = _txCoordinator.Begin()
try:
    tx.TrackNew(entryAbsPath)
    tx.Track(journalrcPath)
    tx.Track(mdjournalPath)
    if not ignoreFile: tx.Track(tocAbsPath)
    // Execute existing write steps unchanged
    tx.Commit()
catch: report -> rollback -> throw
```

---

### 7.11 `InitJournalService.Initialize` (Begin — standalone)

`TocFileAlreadyExistsException` check stays outside tx.

```
[TocFileAlreadyExistsException check, outside tx]

using var tx = _txCoordinator.Begin()
try:
    tx.TrackNew(mdjournalPath)
    tx.TrackNew(journalrcPath)
    tx.TrackNew(tocPath)
    // Execute existing initialization steps unchanged
    tx.Commit()
catch: report -> rollback(journalDirectory) -> throw
```

Rollback (reverse): delete tocPath, delete journalrcPath, delete mdjournalPath.

---

### 7.12 `NewJournalService.Initialize` (Begin + TrackNewDirectory)

```
var directoryAlreadyExisted = _fileSystem.DirectoryExists(journalDirectory)

using var tx = _txCoordinator.Begin()
try:
    // Register directory FIRST so it is deleted LAST in reverse-order rollback
    if not directoryAlreadyExisted:
        tx.TrackNewDirectory(journalDirectory)

    tx.TrackNew(tocAbsPath)                  // _journalSettings.TableOfContentsFileName + .md
    tx.TrackNew(introAbsPath)               // _journalSettings.IntroductionFileName + .md
    tx.TrackNew(templateAbsPath)            // _journalSettings.JournalEntryTemplateFileName + .md
    tx.TrackNew(allJournalsAbsPath)         // _journalSettings.AllJournalsFileName + .md  (e.g. 1h-All-My-Journals.md)
    tx.TrackNew(journalrcAbsPath)           // _journalSettings.JournalConfigFileName  (e.g. .journalrc)
    tx.TrackNew(mdjournalAbsPath)           // "." + _journalSettings.AppName  (e.g. .mdjournal)

    // Execute existing creation steps unchanged
    _fileSystem.CreateDirectory(journalDirectory)
    CreateTableOfContents(...)
    CreateIntroduction(...)
    CreateJournalEntryTemplate(...)
    CreateAllMyJournals(...)
    CreateJournalConfiguration(...)
    CreateFileTrackingIndex(...)

    tx.Commit()
catch: report -> rollback(journalDirectory) -> throw
```

Rollback (reverse): delete mdjournal, journalrc, allJournals, template, intro, toc,
then DeleteDirectory(journalDirectory).

---

### 7.13 `AddTableOfContents` Command — Command-Level Transaction

```
[pre-flight: JournalrcNotFoundException, InvalidOperationException, early return if toc exists]

using var tx = _txCoordinator.Begin()
try:
    if configCurrent.TableOfContents.File != tocFile:
        tx.Track(journalrcPath)         BEFORE Update
        _journalConfiguration.Update(...)
    tx.TrackNew(tocPath)               BEFORE UpdateTableOfContents
    _tableOfContentsGenerator.UpdateTableOfContents(...)
    tx.Commit()
    _console.MarkupLine("[green]Success:[/]...")
    return 0
catch (Exception ex):
    _rollbackReporter.ReportRollbackStarting("add table of contents", ex)
    var result = tx.Rollback()
    _rollbackReporter.ReportRollbackComplete(result, settings.FilePath)
    throw new RollbackCompletedException(result, ex)

// Outer catches:
catch (RollbackCompletedException ex): return ex.Result.IsFullyRestored ? 2 : 3
catch (JournalrcNotFoundException ex): ...; return 1
catch (Exception ex): ...; return 1
```

---

### 7.14 `AddJournalrc` Command — Command-Level Transaction

This command creates exactly one file (`.journalrc`). No multi-file consistency risk, but a
partially-written config file would confuse subsequent commands. `TrackNew` ensures cleanup.

```
[pre-flight: if .journalrc already exists, return 1 — NO transaction started]

using var tx = _txCoordinator.Begin()
try:
    tx.TrackNew(journalrcPath)             BEFORE any Generate* call
    _configGenerator.GenerateFrom*(...)    (writes .journalrc)
    tx.Commit()
    _console.MarkupLine("[green]✓[/]...")
    return 0
catch (Exception ex):
    _rollbackReporter.ReportRollbackStarting("add journal configuration", ex)
    var result = tx.Rollback()
    _rollbackReporter.ReportRollbackComplete(result, settings.FilePath)
    throw new RollbackCompletedException(result, ex)

// Outer catches:
catch (RollbackCompletedException ex): return ex.Result.IsFullyRestored ? 2 : 3
catch (ArgumentException ex): ...; return 1
catch (Exception ex): ...; return 1
```

---

### 7.15 `AddFileTracking` Command — Command-Level Transaction

This command creates exactly one file (`.mdjournal` tracking index). Same rationale as 7.14.

```
[pre-flight: if .journalrc missing and !ignoreConfig, throw JournalrcNotFoundException — NO tx]
[pre-flight: if tracking file already exists, return 0 with warning — NO tx]

using var tx = _txCoordinator.Begin()
try:
    tx.TrackNew(trackingFilePath)          BEFORE LoadIndex/UpdateIndex
    _fileTracking.LoadIndex(settings.FilePath)
    _fileTracking.UpdateIndex(settings.FilePath)
    tx.Commit()
    _console.MarkupLine("[green]Success:[/]...")
    return 0
catch (Exception ex):
    _rollbackReporter.ReportRollbackStarting("add file tracking", ex)
    var result = tx.Rollback()
    _rollbackReporter.ReportRollbackComplete(result, settings.FilePath)
    throw new RollbackCompletedException(result, ex)

// Outer catches:
catch (RollbackCompletedException ex): return ex.Result.IsFullyRestored ? 2 : 3
catch (JournalrcNotFoundException ex): ...; return 1
catch (Exception ex): ...; return 1
```

---

## 8. Spectre.Console Output Specification

### 8.1 `ReportRollbackStarting`

```
[red]Error:[/] Failed to {operationDescription}: {cause.Message}

[yellow]Rolling back changes...[/]
```

### 8.2 `ReportRollbackComplete` — Success Path

**Path format (Q6=B): relative to journal root.**
Use `Path.GetRelativePath(journalRoot, entry.AbsolutePath)` for the file column.

**Escaping rule:** Escape ONLY the path portions with `.EscapeMarkup()` before embedding
them in markup templates. Do NOT call `.EscapeMarkup()` on the fully-built `detail` string —
that would escape the intentional `[dim]...[/]` markup.

```csharp
var table = new Table()
    .Title("[bold]Rollback Summary[/]")
    .Border(TableBorder.Rounded)
    .BorderColor(Color.Yellow)
    .AddColumn(new TableColumn("[bold]Action[/]").Centered())
    .AddColumn(new TableColumn("[bold]File[/]"));

foreach (var entry in result.Restored)
{
    // Escape path portions individually so bracket chars in file names don't break markup.
    var rel    = Path.GetRelativePath(journalRoot, entry.AbsolutePath).EscapeMarkup();
    var relNew = entry.NewPath is not null
        ? Path.GetRelativePath(journalRoot, entry.NewPath).EscapeMarkup()
        : string.Empty;

    var (action, detail) = entry.Kind switch
    {
        RollbackEntryKind.Modify       => ("Restored",     rel),
        RollbackEntryKind.Delete       => ("Restored",     rel),
        RollbackEntryKind.New          => ("Deleted",      $"{rel} [dim](never committed)[/]"),
        RollbackEntryKind.NewDirectory => ("Dir Deleted",  $"{rel} [dim](empty dir removed)[/]"),
        RollbackEntryKind.Rename       => ("Renamed back", $"{relNew} [dim]-> {rel}[/]"),
        _                              => ("Restored",     rel),
    };
    // detail already contains only safe markup — do NOT call EscapeMarkup() here.
    table.AddRow($"[yellow]{action}[/]", detail);
}

_console.Write(table);
_console.MarkupLine("[green]All changes have been rolled back. Your journal is unchanged.[/]");
_console.MarkupLine("[dim]To retry, fix the error above and run the command again.[/]");
```

### 8.3 `ReportRollbackComplete` — Partial Failure Path

Add below the table:
```
[red]  x  Notes/could-not-restore.md: Access denied.[/]
[red]WARNING: Some files could not be restored. Manual inspection recommended.[/]
```

### 8.4 No-Op Case

```
[yellow]No changes to roll back.[/]
```

### 8.5 Exit Code Reference (document in `ARCHITECTURE.md`)

| Code | Meaning |
|---|---|
| `0` | Command succeeded |
| `1` | Command failed — pre-flight check or unexpected error; no writes started |
| `2` | Command failed mid-write; all writes fully rolled back (safe to retry) |
| `3` | Command failed mid-write; rollback had errors (manual inspection required) |

---

## 9. Logging Strategy

| Event | Level | Properties |
|---|---|---|
| `Track(path)` called | `Trace` | `{AbsolutePath}` |
| `TrackNew(path)` called | `Trace` | `{AbsolutePath}` |
| `TrackRename(old, new)` called | `Trace` | `{OldPath}`, `{NewPath}` |
| `TrackDelete(path)` called | `Trace` | `{AbsolutePath}` |
| `TrackNewDirectory(path)` called | `Trace` | `{AbsolutePath}` |
| `BeginOrJoin()` returns joined scope | `Debug` | — |
| Transaction committed | `Debug` | `{EntryCount}` |
| Rollback started | `Warning` | `{EntryCount}`, `{OperationDescription}` |
| Single entry rolled back (success) | `Information` | `{Kind}`, `{AbsolutePath}` |
| Single entry rollback failed | `Error` | `{Kind}`, `{AbsolutePath}`, `{Exception}` |
| Rollback complete | `Warning` | `{RestoredCount}`, `{FailedCount}` |
| `Rollback()` called when already rolled back | `Debug` | (no-op message) |

---

## 10. DI Registration Changes

**File:** `markdown-journal-cli/Program.cs`

```csharp
// Rollback infrastructure
host.Services.AddSingleton<IDeletionRollbackStrategy, InMemoryDeletionRollbackStrategy>();
host.Services.AddSingleton<IFileTransactionCoordinator, FileTransactionCoordinator>();
host.Services.AddSingleton<IRollbackReporter, RollbackReporter>();
```

`IDeletionRollbackStrategy`: singleton (safe for single-threaded CLI, one scope at a time).  
`IFileTransactionCoordinator`: singleton (stateless factory + thread-local ambient `_current`).  
`IRollbackReporter`: singleton (stateless renderer).  
`FileTransactionScope` / `JoinedTransactionScope`: NOT DI-registered — created by coordinator.

**ARCHITECTURE.md updates:**
1. Add `IFileTransactionCoordinator`, `IRollbackReporter`, `IDeletionRollbackStrategy` to DI table
2. Add new **Exit Codes** section with the table from section 8.5
3. Update `IInMemoryFileBuffer` description: remove "future rollback" marker (rollback is now
   implemented via the coordinator; the buffer remains dry-run-only)

**DEVELOPMENT.md updates:**
1. Update the file tree — add `Transactions/` folder under `Infrastructure/`
2. Update the service descriptions section — add entries for `IFileTransactionCoordinator`,
   `IRollbackReporter`, and `IDeletionRollbackStrategy`; update `IInMemoryFileBuffer` description
   to remove "future rollback" marker

**Dry-run / transaction buffer invariant:** The dry-run path in `UpdateCommand` (and any other
command) must always return before any `Begin()` call is made. Staging dry-run content in the
buffer while a transaction scope is active is unsupported — the scope's `Commit()` and
`Rollback()` both call `_buffer.Clear()`, which would silently discard staged preview data.

---

## 11. Test Strategy

### 11.1 `FileTransactionScopeTests` — Required Test Cases

```
// Track / Modify
Should_Snapshot_File_Content_When_Tracked
Should_Not_Overwrite_Snapshot_On_Duplicate_Track                 (first-write-wins)
Should_Restore_File_Content_On_Rollback_For_Modify
Should_Throw_When_Track_Called_After_Commit
Should_Throw_When_Track_Called_After_Rollback

// TrackNew
Should_Delete_Created_File_On_Rollback_For_New
Should_Not_Throw_When_New_File_Already_Gone_On_Rollback          (idempotency)

// TrackRename
Should_Rename_Back_On_Rollback_For_Rename
Should_Not_Throw_When_Renamed_File_Not_Found_On_Rollback

// TrackDelete
Should_Delegate_Snapshot_To_DeletionStrategy_On_TrackDelete
Should_Restore_Via_DeletionStrategy_On_Rollback_For_Delete

// TrackNewDirectory
Should_Delete_Directory_On_Rollback_For_NewDirectory
Should_Add_To_Failed_When_DeleteDirectory_Throws

// Commit
Should_Clear_All_Tracked_Entries_On_Commit
Should_Clear_InMemoryFileBuffer_On_Commit
Should_Release_All_Deletion_Snapshots_On_Commit
Should_Return_Empty_RollbackResult_When_Rollback_Called_After_Commit
Should_Clear_Ambient_Current_On_Commit

// Rollback
Should_Execute_Rollback_Entries_In_Reverse_Order
Should_Return_Fully_Restored_Result_When_All_Entries_Succeed
Should_Return_Partially_Restored_Result_When_Some_Entries_Fail
Should_Clear_InMemoryFileBuffer_On_Rollback
Should_Be_Idempotent_On_Multiple_Rollback_Calls
Should_Auto_Rollback_Via_Dispose_When_No_Commit
Should_Clear_Ambient_Current_On_Rollback

// Full sequence
Should_Rollback_Mixed_Operations_In_Correct_Reverse_Order
Should_Rollback_NewDirectory_After_All_New_Files_Inside_Are_Deleted
```

### 11.2 `JoinedTransactionScopeTests`

```
Should_Delegate_Track_Calls_To_Root_Scope
Should_Not_Commit_Root_On_Joined_Commit
Should_Delegate_Rollback_To_Root_When_Rollback_Called
Should_Auto_Rollback_Root_Via_Dispose_When_Not_Committed
Should_Not_Auto_Rollback_Root_Via_Dispose_When_Committed
Should_Return_Root_IsRolledBack_State
```

### 11.3 `FileTransactionCoordinatorTests`

```
Should_Return_New_Root_Scope_On_Begin
Should_Set_Current_When_Begin_Called
Should_Clear_Current_When_Scope_Committed
Should_Clear_Current_When_Scope_Rolled_Back
Should_Return_Joined_Scope_When_Active_Root_Exists_On_BeginOrJoin
Should_Return_New_Root_Scope_When_No_Active_Root_On_BeginOrJoin
Should_Return_Null_Current_Before_Begin
Should_Throw_When_Begin_Called_While_Scope_Is_Active          (Begin() guard)
```

**Test isolation note (`[ThreadStatic]`):** xUnit reuses thread pool threads between tests.
Each `FileTransactionCoordinatorTests` test must use a **fresh `FileTransactionCoordinator`
instance** (constructor injection, not a shared static). Assert `coordinator.Current == null`
at the start of any test that calls `Begin()`, to detect leftover state from a prior test
on the same thread.

Implement a `TransactionCoordinatorTestBase` base class (or `IAsyncLifetime` fixture) that
asserts `Current == null` in both `InitializeAsync` and `DisposeAsync` — this enforces the
constraint mechanically across all coordinator test cases rather than relying on per-test
prose reminders:

```csharp
public abstract class TransactionCoordinatorTestBase : IDisposable
{
    protected readonly FileTransactionCoordinator Coordinator;

    protected TransactionCoordinatorTestBase()
    {
        // Each test gets a fresh coordinator instance with its own [ThreadStatic] slot.
        Coordinator = new FileTransactionCoordinator(/* fakes */);
        Assert.Null(Coordinator.Current); // fail fast if leftover state from prior test
    }

    public void Dispose()
    {
        // Clean up any uncommitted scope in case the test threw mid-way
        Coordinator.Current?.Rollback();
        Assert.Null(Coordinator.Current);
    }
}
```

**Test isolation note (`IDeletionRollbackStrategy` singleton):** Service rollback tests that
construct services directly (not via DI) must instantiate a **fresh `InMemoryDeletionRollbackStrategy`**
per test class, not share the application-registered singleton. The strategy's internal dictionary
persists across calls — a test that leaves a `Capture` without a matching `Release` or `Rollback`
will corrupt the next test's deletion snapshot state. Pass the fresh instance to both the service
constructor and `FileTransactionCoordinator` constructor in test setup.

```csharp
// example — in each service rollback test class:
private readonly InMemoryDeletionRollbackStrategy _deletionStrategy = new();
private readonly FileTransactionCoordinator _txCoordinator;

public MyServiceRollbackTests()
{
    _txCoordinator = new FileTransactionCoordinator(
        new TestFileSystem(), new InMemoryFileBuffer(new TestFileSystem()),
        _deletionStrategy, NullLoggerFactory.Instance);
}
```

### 11.4 Service Rollback Tests — Failure Injection Matrix

Use `FaultInjectingFileSystem` that throws at a controlled write step N.

#### `JournalUpdateService` (with UpdateCommand outer scope)

```
Should_Rollback_All_Steps_When_UpdateTableOfContents_Fails_After_ConfigSync
Should_Rollback_All_Steps_When_UpdateJournalConfig_Fails_After_TrackingSync
Should_Rollback_Toc_Rename_And_Config_When_JournalRc_Update_Throws
Should_Rollback_All_Backlinked_Files_When_LinkRewriter_Throws
```

#### `JournalFileUpdateService`

```
Should_Rollback_Rename_When_Config_Update_Throws
Should_Rollback_Rename_And_Backlinks_When_Toc_Regeneration_Throws
Should_Rollback_Config_When_DisplayName_Change_Followed_By_Toc_Failure
```

#### `RemoveEntryService`

```
Should_Restore_Deleted_Entry_When_Config_RemoveEntry_Throws
Should_Restore_Deleted_Entry_And_Config_When_Tracking_Update_Throws
Should_Restore_All_Files_When_Toc_Regeneration_Throws
Should_Restore_Partially_Cleaned_Ref_Files_When_StripLinks_Throws
```

#### `JournalEntryService` (new)

```
Should_Delete_Created_Entry_When_Config_AddEntry_Throws
Should_Delete_Created_Entry_And_Restore_Config_When_Tracking_Update_Throws
Should_Delete_Entry_Restore_Config_And_Tracking_When_Toc_Throws
Should_Not_Start_Transaction_For_PreFlight_Exceptions
```

#### `InitJournalService` (new)

```
Should_Delete_Created_Mdjournal_And_Journalrc_When_Toc_Creation_Throws
Should_Delete_All_Created_Files_When_Second_TrackingUpdate_Throws
Should_Not_Start_Transaction_For_TocAlreadyExistsException
```

#### `NewJournalService` (new)

```
Should_Delete_All_Created_Files_And_Directory_When_TrackingInit_Throws
Should_Delete_Created_Files_And_Directory_When_Config_Write_Throws
Should_Not_Delete_Directory_When_It_Existed_Before_Command
Should_Add_Directory_To_Failed_When_DeleteDirectory_Throws
```

#### `AddTableOfContents` Command (new)

```
Should_Delete_Created_Toc_When_FileWrite_Fails
Should_Restore_Journalrc_When_Toc_Creation_Fails_After_Config_Update
Should_Not_Start_Transaction_When_Toc_Already_Exists
```

#### `AddJournalrc` Command (new)

```
Should_Delete_Created_Journalrc_When_Write_Fails_Midway
Should_Not_Start_Transaction_When_Journalrc_Already_Exists
```

#### `AddFileTracking` Command (new)

```
Should_Delete_Created_Tracking_File_When_UpdateIndex_Throws
Should_Not_Start_Transaction_When_Tracking_File_Already_Exists
Should_Not_Start_Transaction_When_JournalrcNotFound
```

### 11.5 Edge Case Tests

```
Should_Use_Original_Snapshot_When_File_Tracked_Twice
Should_Return_Empty_Result_On_Second_Rollback_Call
Should_Return_Empty_RollbackResult_When_Nothing_Tracked
Should_Clear_Buffer_After_Commit_So_Next_Command_Has_Clean_State
Should_Clear_Buffer_After_Rollback_So_Next_Command_Has_Clean_State
Should_Track_All_Files_Returned_By_FindFilesWithLinkTo_Before_Writes
Should_Add_Failed_Entry_When_RenameBack_Target_Already_Exists
Should_Return_ExitCode2_When_RollbackCompletedException_IsFullyRestored
Should_Return_ExitCode3_When_RollbackCompletedException_IsNotFullyRestored
```

### 11.6 `RollbackReporterTests` (use `Spectre.Console.Testing.TestConsole`)

```
Should_Render_Error_Message_With_Cause_For_Starting
Should_Render_Rolling_Back_Message_For_Starting
Should_Render_Table_With_Restored_Entries_Using_Relative_Paths
Should_Render_Success_Line_When_Fully_Restored
Should_Render_Warning_And_Failed_Files_When_Partial
Should_Render_NoOp_Message_When_Entry_Lists_Empty
```

### 11.7 Test Helper Requirements

`FaultInjectingFileSystem` — extend `TestFileSystem`:
```csharp
public void InjectFaultOn(string methodName, int onCallNumber, Exception ex);
// Throws ex on the Nth invocation of the named method
```

 Replace `string methodName` with a dedicated enum
(`FaultInjectPoint.UpdateFile`, `FaultInjectPoint.CreateFile`, etc.) to make injections
refactor-safe — a method rename won't silently break the fault injection. Low priority;
implement if the string-based API proves fragile during Phase 12.

### 11.8 Coverage Targets

| Area | Target |
|---|---|
| `FileTransactionScope` | 100% line coverage |
| `JoinedTransactionScope` | 100% line coverage |
| `RollbackReporter` | 95%+ |
| Service rollback paths | At least one failure injection test per write step |

---

## 12. Implementation Checklist (Ordered for Autopilot)

### Phase 1 — Models (no dependencies)
- [ ] Create `Infrastructure/Transactions/Models/` folder
- [ ] Implement `RollbackEntryKind.cs` (include `NewDirectory`)
- [ ] Implement `RollbackEntry.cs`
- [ ] Implement `RollbackFailure.cs` (named record — see §5.4; must precede `RollbackResult.cs`)
- [ ] Implement `RollbackResult.cs`
- [ ] Implement `RollbackCompletedException.cs`

### Phase 2 — Core Infrastructure
- [ ] Add `DeleteDirectory(string path)` to `IFileSystem` + `FileSystem` + `TestFileSystem`
- [ ] Implement `IDeletionRollbackStrategy.cs`
- [ ] Implement `InMemoryDeletionRollbackStrategy.cs`
- [ ] Implement `IFileTransactionScope.cs`
- [ ] Implement `FileTransactionScope.cs` (internal, with `onDisposed` callback)
- [ ] Implement `JoinedTransactionScope.cs` (internal)
- [ ] Implement `IFileTransactionCoordinator.cs`
- [ ] Implement `FileTransactionCoordinator.cs` (thread-local ambient via `[ThreadStatic]`)
- [ ] Implement `IRollbackReporter.cs`
- [ ] Implement `RollbackReporter.cs`

### Phase 3 — Infrastructure Tests
- [ ] Implement `FileTransactionScopeTests.cs`
- [ ] Implement `JoinedTransactionScopeTests.cs`
- [ ] Implement `FileTransactionCoordinatorTests.cs`
- [ ] Implement `RollbackReporterTests.cs`
- [ ] Run tests — all must pass before proceeding

### Phase 4 — DI + Documentation
- [ ] Add three registrations to `Program.cs` (section 10)
- [ ] Update `ARCHITECTURE.md`: DI table, Exit Codes section, IInMemoryFileBuffer note
- [ ] Update `DEVELOPMENT.md`: DI table entry for `IInMemoryFileBuffer` (remove "future rollback" marker), add `IFileTransactionCoordinator`/`IRollbackReporter`/`IDeletionRollbackStrategy` to service descriptions section
- [ ] Update `DEVELOPMENT.md`: file tree (line ~72) — add `Transactions/` folder under `Infrastructure/`
- [ ] Update `README.md` if it documents exit codes or command behaviour
- [ ] `dotnet build` with zero warnings

### Phase 5 — `RemoveEntryService` Integration
- [ ] Add constructor parameters
- [ ] Wire per section 7.9
- [ ] Implement `RemoveEntryServiceRollbackTests.cs`
- [ ] Run all tests

### Phase 6 — `JournalUpdateService` + `UpdateCommand` Ambient Integration
- [ ] Add constructor parameters to `JournalUpdateService`
- [ ] Wire all four methods (sections 7.4–7.7) with `BeginOrJoin`
- [ ] Add `IFileTransactionCoordinator` to `UpdateCommand`, wire outer scope (section 7.3)
- [ ] Update `UpdateCommand` catch blocks (section 6.4)
- [ ] Implement `JournalUpdateServiceRollbackTests.cs`
- [ ] Run all tests

### Phase 7 — `JournalFileUpdateService` Integration
- [ ] Add constructor parameters
- [ ] Wire per section 7.8
- [ ] Update `UpdateEntryCommand` catch blocks
- [ ] Implement `JournalFileUpdateServiceRollbackTests.cs`
- [ ] Run all tests

### Phase 8 — `JournalEntryService` + `AddEntryCommand` Integration
- [ ] Add constructor parameters
- [ ] Wire per section 7.10
- [ ] Update `AddEntryCommand` catch blocks
- [ ] Implement `JournalEntryServiceRollbackTests.cs`
- [ ] Run all tests

### Phase 9 — `InitJournalService` + `InitCommand` Integration
- [ ] Add constructor parameters
- [ ] Wire per section 7.11
- [ ] Update `InitCommand` catch blocks
- [ ] Implement `InitJournalServiceRollbackTests.cs`
- [ ] Run all tests

### Phase 10 — `NewJournalService` + `NewCommand` Integration
- [ ] Add constructor parameters
- [ ] Wire per section 7.12 (includes `TrackNewDirectory`)
- [ ] Update `NewCommand` catch blocks
- [ ] Implement `NewJournalServiceRollbackTests.cs`
- [ ] Run all tests

### Phase 11 — `AddTableOfContents`, `AddJournalrc`, `AddFileTracking` Integration
- [ ] Add `IFileTransactionCoordinator` + `IRollbackReporter` to `AddTableOfContents` constructor
- [ ] Wire `AddTableOfContents` per section 7.13
- [ ] Implement `AddTableOfContentsRollbackTests.cs`
- [ ] Add `IFileTransactionCoordinator` + `IRollbackReporter` to `AddJournalrc` constructor
- [ ] Wire `AddJournalrc` per section 7.14
- [ ] Implement `AddJournalrcRollbackTests.cs`
- [ ] Add `IFileTransactionCoordinator` + `IRollbackReporter` to `AddFileTracking` constructor
- [ ] Wire `AddFileTracking` per section 7.15
- [ ] Implement `AddFileTrackingRollbackTests.cs`
- [ ] Update `AddJournalrc` and `AddFileTracking` catch blocks (section 6.4)
- [ ] Run all tests

### Phase 12 — Edge Cases + Final Verification
- [ ] Implement `FaultInjectingFileSystem` helper (section 11.7)
- [ ] Implement edge case tests from section 11.5
- [ ] Run full test suite — zero regressions
- [ ] `dotnet build` with zero warnings
- [ ] `dotnet test` — all tests pass
- [ ] Manual smoke test: each in-scope command on a real journal
- [ ] Verify rollback output matches section 8 spec visually

---

## 13. Resolved Decisions

All design decisions are resolved. This section serves as the authoritative reference for
implementors encountering ambiguity in the checklist or integration specs.

| ID | Decision | Resolution | Rationale |
|---|---|---|---|
| Q1 | Transaction boundary for `update journal` | One outer transaction for entire command via ambient `BeginOrJoin` pattern | Failure in step 3 should restore step 2. Per-sub-op commits leave partial state. |
| Q2 | Delete restore mechanism | In-memory (`InMemoryDeletionRollbackStrategy`); interface allows future swap | Simple and sufficient for single-user CLI. |
| Q3 | Exit codes | `0`/`1`/`2`/`3` scheme, documented in `ARCHITECTURE.md` | Maps failure modes for scripting; code `3` is critical for "inspect manually" signal. |
| Q4 | Reporter location | In services (co-located with try/catch and Rollback) | Same rollback UX regardless of which command calls the service. |
| Q5 | `TrackNew` usage | Actively used; all create-commands included | Add/init/new commands confirmed in scope. |
| Q6 | Path format in rollback table | Relative to journal root (`Path.GetRelativePath`) | Informative without verbosity of absolute paths. |
| Scope-A | Which create-commands | `add entry`, `add toc`, `add journalrc`, `add file-tracking`, `init journal` all included | `add journalrc` and `add file-tracking` are single-file writes — rollback cleans up any partially-written file, consistent with the rest of the system. |
| Scope-C | Why `add journalrc` + `add file-tracking` included | Both create one file each (no multi-file consistency risk), but in-flight write failures CAN leave a partial file. `TrackNew` is trivially cheap and keeps the rollback system complete. |
| Scope-B | `new journal` failure cleanup | Full rollback including directory deletion | "New journal should be a complete journal" — no partial traces. |
| Ambient | Ambient transaction pattern | Approved — `BeginOrJoin()` + `[ThreadStatic] _current` + `JoinedTransactionScope` | Services work standalone OR inside outer scope without coupling changes. |
| Dir | Directory rollback mechanism | Option A — `DeleteDirectory` on `IFileSystem` + `TrackNewDirectory` on scope | Full cleanup; fail-safe if files could not be deleted (surfaces in `RollbackResult.Failed`). |
| Terminology | "v1/v2" scope labels | Replaced with "this implementation" / "future work" throughout | Clearer for implementors; avoids version confusion. |
| RollbackFailure | `RollbackResult.Failed` tuple type | Promoted to named `public record RollbackFailure(RollbackEntry Entry, Exception Error)` in `Models/` | Named record keeps test assertions readable (`ex.Result.Failed[0].Entry` vs `.Item1`), prevents silent breakage on field additions, and is consistent with the rest of the model layer. |
| EscapeMarkup | Path escaping in `RollbackReporter` | Escape path portions individually with `.EscapeMarkup()` before building the `detail` string; never call `.EscapeMarkup()` on the assembled string | Calling `.EscapeMarkup()` on a string that already contains `[dim]...[/]` markup escapes the brackets, breaking the rendering. |
| Buffer coupling | `FileTransactionScope` depends on `IInMemoryFileBuffer` | Keep `IInMemoryFileBuffer` for now; noted as low-priority refactor to `Action onClear` if the interface grows | Narrower coupling preferred but not worth the churn until the buffer interface stabilises. |
| DeletionStrategy singleton | `InMemoryDeletionRollbackStrategy` registered as singleton with mutable state | Add `Debug.Assert` in `Capture` to detect overlapping transactions immediately in tests | Safe because CLI is single-threaded; assertion surfaces assumption violations in CI without runtime overhead. |
