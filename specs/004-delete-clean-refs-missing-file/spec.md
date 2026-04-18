# Feature Specification: Delete Entry --clean-refs Tolerates Already-Deleted Files

**Feature Branch**: `004-delete-clean-refs-missing-file`  
**Created**: 2026-04-18  
**Status**: Draft  
**Input**: User description: "update the delete entry command when using the --clean-refs flag, after a file has already been deleted, to allow it to still clean up the references to that file link in other files."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Clean Refs After Manual File Deletion (Priority: P1)

A user manually deleted a journal entry file from disk (e.g., via the OS file manager or `rm`), leaving the journal config, tracking index, TOC, and other entry files still containing references to it. The user runs `remove entry <filename> --clean-refs` to clean up those orphaned references. The command should present a confirmation prompt (or skip it with `--force`) and succeed, stripping all dead links across the journal.

**Why this priority**: This is the core use case described in the feature request. Without this fix, the command fails with a `FileNotFoundException` before any cleanup occurs, forcing users to manually find and strip all orphaned links.

**Independent Test**: Can be fully tested by creating a journal with entries that link to one another, manually deleting the target file, then running `remove entry <deleted-file> --clean-refs` (confirming the prompt) and verifying: exit code 0, links stripped in other files, config/tracking updated, TOC regenerated.

**Acceptance Scenarios**:

1. **Given** a journal entry `target.md` has been deleted from disk (but still referenced from the config, tracking index, and other entries), **When** the user runs `remove entry target.md --clean-refs` and confirms the prompt, **Then** the command exits 0, strips all inline links to `target.md` from other files, removes it from the config and tracking index, and regenerates the TOC.

2. **Given** `target.md` was already deleted and `--clean-refs` is set, **When** no other file contains a link to `target.md`, **Then** the command exits 0, updates config and tracking, regenerates the TOC, and reports 0 files modified.

3. **Given** `target.md` was already deleted and `--clean-refs` is set with `--force`, **When** the user runs `remove entry target.md --clean-refs --force`, **Then** the confirmation prompt is skipped and the cleanup proceeds immediately.

---

### User Story 2 - Partial Cleanup After Interrupted Removal (Priority: P2)

A previous `remove entry` command was interrupted after the file was deleted but before config/tracking/TOC/refs were updated. The user re-runs the command with `--clean-refs` to complete the cleanup.

**Why this priority**: Interrupted operations are a realistic failure mode. Without this fix the second run fails immediately, leaving the journal in a permanently inconsistent state.

**Independent Test**: Can be tested by simulating a partial deletion (file deleted, config/tracking intact), then running `remove entry <file> --clean-refs` (confirming the prompt) and verifying full cleanup completes.

**Acceptance Scenarios**:

1. **Given** a journal entry file was deleted by a previous interrupted command (config/tracking still reference it), **When** the user re-runs `remove entry <file> --clean-refs` and confirms the prompt, **Then** the command exits 0, completes the partial cleanup (config, tracking, TOC, refs).

---

### Edge Cases

- What happens when the file doesn't exist AND `--clean-refs` is **not** set? → Must still fail with an error (existing behaviour preserved).
- What happens when the file doesn't exist AND neither config nor tracking contain a reference to it? → Should succeed gracefully with no-ops.
- What happens when `--clean-refs` is true but the file still exists on disk? → Normal flow unchanged; file is deleted then refs are cleaned.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: When `--clean-refs` is set and the target entry file does **not** exist on disk, the `remove entry` command MUST skip the file-deletion step and proceed with all remaining cleanup steps (config removal, tracking removal, TOC regeneration, reference stripping).
- **FR-002**: When `--clean-refs` is set and the target file is missing, the `ValidatePreconditions` check MUST NOT throw `FileNotFoundException`; the confirmation prompt MUST still be presented to the user (unless `--force` is also set).
- **FR-003**: When `--clean-refs` is **not** set and the target file is missing, the command MUST continue to fail with a `FileNotFoundException` error (existing behaviour preserved).
- **FR-004**: The command MUST exit 0 after a successful `--clean-refs` run on an already-deleted file, and MUST report the number of files whose dead links were stripped.
- **FR-005**: Config removal and tracking-index removal MUST be attempted even when the file was already absent from disk; the operations MUST be idempotent (no error if already removed).
- **FR-006**: The transaction rollback mechanism MUST NOT attempt to restore a file that was not present at the start of the operation.

### Key Entities

- **Journal Entry File**: A `.md` file tracked by the journal; may be absent from disk while its metadata remains in the config and tracking index.
- **`--clean-refs` flag**: Instructs the remove command to scan all other journal entries and strip inline links pointing to the removed file.
- **ValidatePreconditions**: Pre-flight guard called before the interactive confirmation prompt; MUST reflect the relaxed file-existence check when `--clean-refs` is active.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Running `remove entry <deleted-file> --clean-refs` on a journal where the file is already absent exits 0 (after confirmation or with `--force`) in under 2 seconds.
- **SC-002**: All inline links to the deleted file are stripped from other journal entries — 0 dead links remain after the command completes.
- **SC-003**: Config and tracking index no longer reference the deleted file after the command completes.
- **SC-004**: Existing behaviour is fully preserved: without `--clean-refs`, missing files still return exit code 1 with a `FileNotFoundException` error message.
- **SC-005**: All existing tests continue to pass; new tests cover the already-deleted-file scenario for both `cleanRefs = true` and `cleanRefs = false`.

## Assumptions

- The `.journalrc` config file and the `.mdjournal` tracking index are assumed to be present; the command already requires these and that requirement is unchanged.
- Config and tracking removal operations are assumed to handle gracefully the case where the entry is no longer present in them (idempotent by convention in the existing codebase).
- Only inline markdown links (`[text](url)`) are in scope for dead-link stripping; reference-style links are out of scope (existing limitation of `MarkdownLinkRewriter`).
- The `--clean-refs` flag is the only flag that changes the file-existence requirement; all other validation guards (protected files, journalrc existence, tracking index existence) remain mandatory.
