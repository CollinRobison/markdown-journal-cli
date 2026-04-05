# Rollback System Planning (No Commit)

## Problem Statement
`markdown-journal-cli` performs multi-file updates across `.mdjournal`, `.journalrc`, entry markdown files, and TOC files. Today these writes are not transactional across the full sync/update flow, so a failure mid-operation can leave inconsistent state. Existing infrastructure includes `IInMemoryFileBuffer` / `InMemoryFileBuffer` with snapshot/stage/restore primitives, but they are not yet orchestrated by update services for end-to-end rollback.

## Current State (Analyzed)
- `IInMemoryFileBuffer` exists and supports `Snapshot`, `Stage`, `Commit`, `Restore`, `Clear`.
- `JournalUpdateService` and `JournalFileUpdateService` execute direct file writes across several steps (tracking, config, TOC, link rewrites), with no transaction coordinator.
- Architecture docs explicitly note rollback as future wiring.
- Research artifacts now exist in `docs/` and `TODO/rollback-system.md` capturing patterns from Cargo, Helm, WAL, rename atomicity, .NET transaction approaches, and CLI error messaging guidance.

## Proposed Approach (Planning-Level)
Use an application-level file transaction boundary (execute-then-compensate) centered on snapshot/restore, with clear start/commit/rollback lifecycle and explicit user-visible rollback reporting.

## Todos
1. Define rollback scope and boundaries
   - Decide which commands/flows are in v1 (`update journal`, `update entry`, `remove entry`, TOC rename/link rewrite path).
   - Define behavior for file create/delete/rename rollback.

2. Design transactional abstraction
   - Decide whether to extend `IInMemoryFileBuffer` directly or add a dedicated coordinator/scope service.
   - Define operation ordering and reverse-order restoration semantics.

3. Map affected write points
   - Enumerate all write calls in `JournalUpdateService`, `JournalFileUpdateService`, and related services.
   - Mark where snapshots must occur before mutation.

4. Failure/notification semantics
   - Define rollback messaging format (what failed, what restored, any non-restorable items).
   - Define stderr/exit-code behavior aligned with current CLI conventions.

5. Test strategy
   - Add/extend tests for induced failures at each critical step and assert full rollback state.
   - Include idempotent rollback tests and singleton buffer state clearing verification.

6. ADR generation
   - Produce an ADR from research sources describing selected rollback strategy and rejected alternatives.

## Notes / Considerations
- No commit should be made as part of this workstream unless explicitly requested.
- Keep rollback behavior deterministic and observable (avoid silent recovery).
- Favor cross-platform patterns (avoid deprecated/Windows-only TxF paths).
