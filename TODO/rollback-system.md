# Rollback System — Implementation Ideas

**Context:** When a multi-file sync operation (`update journal`, `update entry`, `remove entry`, etc.) partially fails, the journal ends up in an inconsistent state — e.g. `.journalrc` updated but the TOC not regenerated, or a file renamed but backlinks not rewritten. The `IInMemoryFileBuffer` already has `Snapshot()` / `Restore()` stubs explicitly noted as "future wiring" in ARCHITECTURE.md. This document captures design options discovered through deep research so the team can evaluate and implement the best approach.

---

## Background: How Other Tools Handle This

### Cargo (`rust-lang/cargo`)
Cargo uses a **RAII `Transaction` guard** for `cargo install`. A staging directory receives all files first; on `Drop` (without explicit commit), it cleans up the partial install. Binaries only move to `~/.cargo/bin/` in one atomic rename step at the end. For downloads, a **zero-length sentinel** on `.crate` files marks an incomplete download — if the file exists but is 0 bytes, it is re-downloaded.
- Source: [`src/cargo/ops/cargo_install.rs`](https://github.com/rust-lang/cargo/blob/master/src/cargo/ops/cargo_install.rs)
- Source: [`src/cargo/sources/registry/download.rs`](https://github.com/rust-lang/cargo/blob/master/src/cargo/sources/registry/download.rs)

### Helm
Helm stores every release revision as an immutable Kubernetes Secret (`sh.helm.release.v1.<name>.v<N>`). Rollback creates a *new* revision whose chart/config/manifest are copied from the target historical revision — it never mutates existing records. This is the **append-only / immutable history** pattern.
- Source: [`pkg/action/rollback.go`](https://github.com/helm/helm/blob/main/pkg/action/rollback.go)
- Source: [`pkg/storage/storage.go`](https://github.com/helm/helm/blob/main/pkg/storage/storage.go)

### TxFileManager (ChinhDo, .NET)
The canonical .NET answer for transactional file I/O. Implements `IEnlistmentNotification` so file operations participate in `System.Transactions` 2-phase commit alongside databases. Each operation (write, copy, delete, move) is **executed immediately**, but a backup copy is taken first. On `Rollback()`, operations are replayed in **reverse order** from backups. `SnapshotOperation` physically copies the file to `%TEMP%\TxFileMgr-*/`.
- NuGet: `TxFileManager`
- Source: [github.com/chinhdo/txFileManager](https://github.com/chinhdo/txFileManager)

### SQLite WAL / Write-Ahead Logging
Changes are written to a separate WAL file before touching the main database. Readers see a consistent snapshot of the main file while a writer commits to the WAL. On crash, the WAL is either replayed (if committed) or discarded (if uncommitted).
- Source: [sqlite.org/wal.html](https://www.sqlite.org/wal.html)

### POSIX `rename(2)` Atomic Swap
Write to a temp file → `fsync()` → `rename()` to the final path. Because `rename()` is atomic on the same filesystem, no reader ever sees a partial file. This is the bedrock pattern for single-file atomicity.
- Source: [man7.org/linux/man-pages/man2/rename.2.html](https://man7.org/linux/man-pages/man2/rename.2.html)

### rsync Crash Recovery
`rsync` writes to a temporary `.tmp` file in the destination, then renames it. On interruption, the partial `.tmp` file is left behind. On the next run, rsync detects the `.tmp` and resumes or replaces it.
- Source: [rsync.samba.org](https://rsync.samba.org/how-rsync-works.html)

### Microsoft's Official Guidance (TxF deprecated)
Transactional NTFS (TxF) was deprecated in Windows 10. Microsoft's recommended replacement for single-file updates is `File.Replace()` (write-to-temp + atomic rename-with-backup). For multi-file atomic updates, they recommend Windows Installer or a database.
- Source: [learn.microsoft.com/windows/win32/fileio/deprecation-of-txf](https://learn.microsoft.com/en-us/windows/win32/fileio/deprecation-of-txf)

---

## Recommended Approach: Execute-Then-Compensate Coordinator

The existing `IInMemoryFileBuffer` already implements the **Memento pattern** (GoF, 1994) with `Snapshot()` / `Restore()`. The missing piece is a **coordinator** that:

1. Snapshots all files before any operation begins
2. Executes each operation step
3. On failure, restores all snapshotted files in **reverse order**
4. Handles the "new file" case (file didn't exist before → rollback = delete)
5. Handles the "renamed file" case (rename → rollback = rename back)
6. Notifies the user with a clear, actionable message listing what was restored

This maps directly to the **Unit of Work (execute-then-compensate)** pattern used by `TxFileManager`, without requiring any external dependencies or OS-specific kernel transactions.

---

## Design Options

### Option A — Extend `IInMemoryFileBuffer` Directly (Minimal)

Add two capabilities to the existing buffer:
- `SnapshotNewFile(path)` — records that this path did not exist before (rollback = delete)
- `RestoreAll()` — restores all snapshots in registration order (or reverse for safety)

Wire it into the update services: snapshot before each write, wrap the sync loop in `try/catch`, call `RestoreAll()` in the catch block.

**Pros:** Least new code, reuses existing infrastructure.  
**Cons:** `IInMemoryFileBuffer` grows beyond its single-responsibility. No explicit "transaction" boundary visible to callers.

---

### Option B — New `IFileTransactionScope` / `FileRollbackCoordinator` (Recommended)

Introduce a dedicated coordinator service:

```csharp
public interface IFileTransactionScope : IDisposable
{
    /// <summary>Snapshot a file that will be modified. Call before each write.</summary>
    void Track(string absolutePath);

    /// <summary>Record a new file that will be created. Rollback will delete it.</summary>
    void TrackNew(string absolutePath);

    /// <summary>Record a rename. Rollback will rename back.</summary>
    void TrackRename(string oldPath, string newPath);

    /// <summary>Mark the transaction as successful. Clears all snapshots.</summary>
    void Commit();

    /// <summary>Rollback all tracked changes. Called automatically on Dispose if not committed.</summary>
    void Rollback();
}
```

Usage pattern in `JournalUpdateService`:

```csharp
using var tx = _fileTransaction.Begin();
try
{
    tx.Track(mdjournalPath);
    UpdateTrackingIndex(...);   // step 1

    tx.Track(entryFilePath);
    StampLastEdited(...);        // step 2

    tx.Track(journalrcPath);
    SyncConfig(...);             // step 3

    tx.Track(tocFilePath);
    RegenerateToc(...);          // step 4

    tx.Commit();
}
catch (Exception ex)
{
    tx.Rollback();
    _console.MarkupLine($"[red]Error:[/] {ex.Message}");
    _console.MarkupLine("[yellow]All changes have been rolled back to their previous state.[/]");
    throw;
}
```

**Pros:** Clear transaction boundary, RAII-style cleanup via `Dispose`, composable, testable, matches patterns from Cargo and TxFileManager.  
**Cons:** More new types, requires wiring into all 5+ update paths.

---

### Option C — `IInMemoryFileBuffer.CommitAll()` + RAII wrapper (Middle ground)

Extend the buffer with:
- `CommitAll()` — writes all staged content to disk in one pass
- `RestoreAll()` — restores all snapshots in reverse order
- `TrackNewFile(path)` — records a "new file" creation for deletion on rollback

Add a thin RAII wrapper (`FileBufferTransaction`) that calls `RestoreAll()` on `Dispose` if `Commit()` was not called. No new interface needed.

**Pros:** Builds on what exists. Relatively small surface area increase.  
**Cons:** Less explicit than Option B. The buffer still mixes staging (dry-run) and transaction (rollback) concerns.

---

### Option D — WAL-Inspired Sentinel File

Before beginning a multi-step operation, write a sentinel file (e.g., `.mdjournal.txn`) containing the list of files to be modified and their pre-operation hashes. On startup, detect an abandoned sentinel and offer to roll back or continue.

**Pros:** Survives process crashes (not just in-memory failures). Inspired by SQLite WAL, Cargo's `.cargo-ok`, and rsync `.tmp`.  
**Cons:** Significantly more complex. Requires startup-time sentinel detection. Overkill for a single-user CLI where crashes are rare.

---

## Files That Need Rollback Coverage

| Service | Operation | Files Modified |
|---|---|---|
| `JournalUpdateService.UpdateLastEditedDatesAndTracking` | `update journal` | `.mdjournal`, `*.md` entry files |
| `JournalUpdateService.UpdateJournalConfig` | `update journal` | `.journalrc` |
| `JournalUpdateService.UpdateTableOfContents` | `update journal` | TOC `.md` file, `.mdjournal` |
| `JournalUpdateService.RenameToc` | `update journal --rename-toc` | TOC `.md` file, `.journalrc`, `.mdjournal`, backlinked `.md` files |
| `JournalFileUpdateService.UpdateEntry` | `update entry` | entry `.md` (rename), `.journalrc`, TOC `.md` |
| `RemoveEntryService` | `remove entry` | `.journalrc`, `.mdjournal`, TOC `.md` |

---

## User Notification Pattern

Based on CLI style guide research ([clig.dev](https://clig.dev), GNU Coding Standards):

- Errors go to `stderr` (Spectre `IAnsiConsole` already handles this)
- Message should name the **original error** and **what was rolled back**
- List every file restored so the user can verify
- Suggest next steps (e.g., re-run the command or file a bug)

Example output:
```
[red]Error:[/] Failed to regenerate Table of Contents: file 'toc.md' is read-only.

[yellow]Rolling back changes...[/]
  Restored: .journalrc
  Restored: my-entry.md
  Deleted:  toc.md.tmp

[yellow]All changes have been rolled back. Your journal is unchanged.[/]
To retry, fix the error above and run the command again.
```

---

## Implementation Notes

1. **New file handling:** `IInMemoryFileBuffer.Snapshot()` calls `_fileSystem.GetFileContent(path)` which will throw if the file doesn't exist. Need a `SnapshotNew(path)` variant that records the path with a null/sentinel content, so rollback can call `_fileSystem.DeleteFile(path)`.

2. **Rename handling:** A rename is not reversible through content restoration alone — need to record `(oldPath, newPath)` pairs and call `_fileSystem.RenameFile(newPath, oldPath)` on rollback.

3. **Order of rollback:** Restore in **reverse order** of the sync loop steps (TOC → `.journalrc` → entry files → `.mdjournal`) to avoid intermediate inconsistency during the rollback itself.

4. **Idempotency:** Rollback should be safe to call multiple times (no-op if already restored).

5. **Testing:** Use the existing `TestFileSystem` mock to inject failures at each step and assert that all prior steps were reversed.

6. **`IInMemoryFileBuffer` is registered as a singleton** — it must be `Clear()`-ed at the end of every transaction (commit or rollback) so it doesn't carry state across commands.

---

## References & Sources

| Source | URL | Relevance |
|---|---|---|
| TxFileManager (ChinhDo) | https://github.com/chinhdo/txFileManager | .NET execute-then-compensate implementation |
| Cargo `cargo_install.rs` | https://github.com/rust-lang/cargo/blob/master/src/cargo/ops/cargo_install.rs | RAII Transaction guard + staging rename |
| Cargo `download.rs` | https://github.com/rust-lang/cargo/blob/master/src/cargo/sources/registry/download.rs | Zero-length sentinel pattern |
| Helm `rollback.go` | https://github.com/helm/helm/blob/main/pkg/action/rollback.go | Immutable history / append-only pattern |
| Martin Fowler — Unit of Work | https://martinfowler.com/eaaCatalog/unitOfWork.html | UoW pattern reference |
| Martin Fowler — WAL | https://martinfowler.com/articles/patterns-of-distributed-systems/wal.html | Write-ahead log pattern |
| SQLite WAL | https://www.sqlite.org/wal.html | WAL-mode atomicity |
| Pillai et al. OSDI 2014 | https://www.usenix.org/system/files/conference/osdi14/osdi14-paper-pillai.pdf | Crash consistency vulnerabilities |
| POSIX rename(2) | https://man7.org/linux/man-pages/man2/rename.2.html | Atomic single-file swap |
| Microsoft TxF Deprecation | https://learn.microsoft.com/en-us/windows/win32/fileio/deprecation-of-txf | Why kernel transactions aren't the answer |
| IEnlistmentNotification | https://learn.microsoft.com/en-us/dotnet/api/system.transactions.ienlistmentnotification | .NET 2PC callbacks |
| clig.dev error guidelines | https://clig.dev/#errors | CLI error message best practices |
| Dan Luu — File Consistency | https://danluu.com/file-consistency/ | Crash-consistent file write patterns |
| Candea & Fox (HotOS 2003) | https://www.usenix.org/legacy/events/hotos03/tech/candea.html | Crash-only software design |
| rsync how it works | https://rsync.samba.org/how-rsync-works.html | Temp-file + rename crash recovery |

