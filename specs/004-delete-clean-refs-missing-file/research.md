# Research: Delete Entry --clean-refs Tolerates Already-Deleted Files

## Decision 1: Where to relax the file-existence guard

**Decision**: Relax the check inside `ResolveAndValidate` by threading `cleanRefs` through as a parameter; skip step 5 (file-existence assert) when `cleanRefs` is `true`.

**Rationale**: `ResolveAndValidate` is a private helper used by both `ValidatePreconditions` and `RemoveEntry`. A single parameter threads the intent cleanly without duplication, and without introducing a new method or branching at the call site.

**Alternatives considered**:
- Introduce a separate `CleanRefsOnly` service method — rejected; too much duplication of the orchestration logic.
- Catch `FileNotFoundException` at the `RemoveEntry` level and retry — rejected; swallowing and re-entering is fragile and harder to test.

---

## Decision 2: Return type change in `ResolveAndValidate`

**Decision**: Change return type from `(string resolvedFileName, string absoluteEntryPath)` to `(string resolvedFileName, string absoluteEntryPath, bool fileExists)` so the caller can skip the delete step without a second `FileExists` check.

**Rationale**: Avoids a redundant `IFileSystem.FileExists` call after validation, and makes the intent explicit — the tuple encodes whether deletion is required.

**Alternatives considered**:
- Return `absoluteEntryPath` as `null` when the file is absent — rejected; callers would need null checks everywhere and the semantics are less clear.
- Re-check `FileExists` in `RemoveEntry` — rejected; duplicates the work already done in `ResolveAndValidate`.

---

## Decision 3: Skip `tx.TrackDelete` when the file is already absent

**Decision**: Only call `tx.TrackDelete(absoluteEntryPath)` when `fileExists` is `true`. When `false`, no rollback entry is registered for the (already absent) file.

**Rationale**: `TrackDelete` reads the file content via `IFileSystem.GetFileContent` to take a pre-delete snapshot. If the file is absent, this call throws. Skipping the call when the file is gone is both correct and safe — there is nothing to restore on rollback.

**Alternatives considered**:
- Guard inside `TrackDelete` to silently no-op on missing files — rejected; changes the semantics of the transaction infrastructure in a way that is harder to reason about globally.

---

## Decision 4: `ValidatePreconditions` interface signature change

**Decision**: Add `bool cleanRefs = false` as a default parameter to `IRemoveEntryService.ValidatePreconditions`.

**Rationale**: The pre-flight check before the confirmation prompt must mirror the relaxed guard so the user is not shown an error before they can confirm. A default of `false` preserves backwards-compatible behaviour for all existing call sites.

**Alternatives considered**:
- Add a new overload — rejected; C# default parameters achieve the same without duplicating the interface.

---

## No External Dependencies Required

All changes are internal to `RemoveEntryService` and its collaborators. No new packages, algorithms, or infrastructure components are introduced.
