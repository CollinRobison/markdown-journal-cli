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

### User Story 3 - Fully-Cleaned State: File Gone From Everywhere (Priority: P2)

The file is already absent from disk **and** is no longer present in the config or tracking index (either because a previous `--clean-refs` run completed, or the user cleaned things up manually). The user re-runs `remove entry <file> --clean-refs` just to be sure. The command should confirm that nothing needed doing — it must not report a deletion that never happened or leave the user confused.

**Why this priority**: Without this, repeated runs of `--clean-refs` produce misleading "Removed" output even when nothing was actually removed, eroding trust in the command's output.

**Independent Test**: Run `remove entry <file> --clean-refs --force` twice. The second run must exit 0 and output nothing about removing from config/tracking (since those entries are gone), and must report 0 stripped links.

**Acceptance Scenarios**:

1. **Given** a journal entry file is absent from disk, config, and tracking index, **When** the user runs `remove entry <file> --clean-refs --force`, **Then** the command exits 0, reports that the file was already absent from config and tracking (or simply skips those steps silently), reports 0 stripped links, and does not print misleading "Removed from config/tracking" lines.

2. **Given** the fully-cleaned state above, **When** the command completes, **Then** the user can infer from the output that (A) the file no longer exists anywhere in the journal and (B) there are no remaining dead links.

---

### Edge Cases

- What happens when the file doesn't exist AND `--clean-refs` is **not** set? → Must still fail with an error (existing behaviour preserved).
- What happens when the file doesn't exist AND neither config nor tracking contain a reference to it? → Exits 0 with a "nothing to clean" summary; does not claim a deletion occurred.
- What happens when `--clean-refs` is true but the file still exists on disk? → Normal flow unchanged; file is deleted then refs are cleaned.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: When `--clean-refs` is set and the target entry file does **not** exist on disk, the `remove entry` command MUST skip the file-deletion step and proceed with all remaining cleanup steps (config removal, tracking removal, TOC regeneration, reference stripping).
- **FR-002**: When `--clean-refs` is set and the target file is missing, the `ValidatePreconditions` check MUST NOT throw `FileNotFoundException`; the confirmation prompt MUST still be presented to the user (unless `--force` is also set).
- **FR-003**: When `--clean-refs` is **not** set and the target file is missing, the command MUST continue to fail with a `FileNotFoundException` error (existing behaviour preserved).
- **FR-004**: The command MUST exit 0 after a successful `--clean-refs` run on an already-deleted file, and MUST report the number of files whose dead links were stripped.
- **FR-005**: Config removal and tracking-index removal MUST be attempted even when the file was already absent from disk; the operations MUST be idempotent (no error if already removed).
- **FR-006**: The transaction rollback mechanism MUST NOT attempt to restore a file that was not present at the start of the operation.
- **FR-007**: When `--clean-refs` is set and the target entry is absent from **both** config and tracking at the time the command runs, the command MUST NOT output lines claiming the entry was "removed from config" or "removed from tracking". It MUST instead indicate the entry was already absent from those stores (or omit those lines entirely).
- **FR-008**: When `--clean-refs` is set, the command output MUST always provide a definitive signal that dead-link cleanup has completed — either by reporting the count of modified files or an equivalent message such as "No dead references found." when none were stripped. This gives the user confidence that no dead links remain and that the journal is fully consistent. This requirement applies only to `--clean-refs` runs; non-`--clean-refs` invocations MUST NOT print any dead-link status.

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
- **SC-006**: Running `remove entry <file> --clean-refs --force` a second time (fully-cleaned state) exits 0 and does not print "removed from config" or "removed from tracking" — it reports 0 stripped links, giving the user confidence the journal is clean.

## Assumptions

- The `.journalrc` config file and the `.mdjournal` tracking index are assumed to be present; the command already requires these and that requirement is unchanged.
- Config and tracking removal operations are assumed to handle gracefully the case where the entry is no longer present in them (idempotent by convention in the existing codebase).
- Only inline markdown links (`[text](url)`) are in scope for dead-link stripping; reference-style links are out of scope (existing limitation of `MarkdownLinkRewriter`).
- The `--clean-refs` flag is the only flag that changes the file-existence requirement; all other validation guards (protected files, journalrc existence, tracking index existence) remain mandatory.
