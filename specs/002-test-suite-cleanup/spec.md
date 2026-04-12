# Feature Specification: Test Suite Deep Dive & Cleanup

**Feature Branch**: `002-test-suite-cleanup`  
**Created**: 2026-04-11  
**Status**: Draft  
**Input**: User description: "I need to do a deep dive/clean up of the unit / integration tests of this project. I notice a lot of this project is not using moq for the unit tests and a lot of the commands dont have integration tests. also I need to make the test projects cleaner and more maintainable."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Missing Command Integration Tests (Priority: P1)

A developer working on the `init`, `new`, `update`, or `remove` commands needs to verify end-to-end behavior against real file system interactions. Currently only the `add` command has dedicated integration tests; all other commands rely solely on unit tests. When a regression is introduced in one of these commands, it may not be caught until it reaches production.

**Why this priority**: Integration tests are the most valuable safety net for catching regressions in user-facing behavior. Without them, entire command code paths go unexercised in CI.

**Independent Test**: Can be verified by running the test suite and confirming that dedicated integration test classes exist for all five commands (`Init`, `New`, `Add`, `Update`, and `Remove`), that the `Init`, `New`, `Update`, and `Remove` classes exercise the real CLI pipeline end-to-end, and that the existing `Add` integration tests are migrated onto the same shared `JournalIntegrationTestBase` pattern.

**Acceptance Scenarios**:

1. **Given** the `init` command runs against an existing unmanaged directory, **When** the command completes successfully, **Then** `.journalrc`, `.{AppName}`, and `1a-TableOfContents.md` are created in that directory and the test passes without mocks.
2. **Given** the `new` command runs against an existing parent directory and the named journal does not yet exist, **When** the command completes successfully, **Then** a new journal subdirectory is created containing `.journalrc`, `.{AppName}`, and `1a-TableOfContents.md`, and the test passes without mocks.
3. **Given** the `update journal --toc` command runs against an initialized journal with existing entries, **When** the command completes successfully, **Then** `1a-TableOfContents.md` is refreshed to reflect the current entries and the test passes without mocks.
4. **Given** the `remove entry` command runs against a journal with an existing entry, **When** the entry is removed, **Then** the entry file is absent and `1a-TableOfContents.md` no longer references it, verified without mocks.
5. **Given** an integration test targets a journal path that already represents an initialized journal, **When** the scenario is not explicitly the "already exists / already initialized" case, **Then** the test must use a fresh unique temp root instead of reusing that path.
6. **Given** any integration test completes or aborts after partial setup, **When** teardown runs, **Then** all temporary files created under that test's root are cleaned up from disk.

**Artifact verification matrix**

| Command | Starting state | Minimum observable artifacts |
|---|---|---|
| `init` | Existing unmanaged target directory | `.journalrc`, `.{AppName}`, `1a-TableOfContents.md` created in target directory |
| `new` | Existing parent directory; target journal does not exist | `<JournalName>/` created under parent with `.journalrc`, `.{AppName}`, `1a-TableOfContents.md` |
| `add` | Initialized journal | New entry markdown file created; `.journalrc`, `.{AppName}`, and `1a-TableOfContents.md` updated |
| `update` | Initialized journal with at least one entry | Requested journal artifact change visible on disk; for the required MVP scenario, `update journal --toc` refreshes `1a-TableOfContents.md` |
| `remove` | Initialized journal with target entry present | Target entry file removed and `1a-TableOfContents.md` no longer references it |

---

### User Story 2 - Inconsistent Mock Usage in Unit Tests (Priority: P1)

A developer reading a unit test for a service or command needs to quickly understand which dependencies are real and which are mocked. Currently some test files use Moq while others bypass it using test doubles, null-object patterns, or direct instantiation with no shared convention documented. This inconsistency slows onboarding and makes it harder to extend tests confidently.

**Why this priority**: Consistent mocking patterns reduce cognitive overhead, prevent subtle test design mistakes, and make it straightforward to add new tests following the established convention.

**Independent Test**: Can be verified by inspecting all unit test files that have external dependencies and confirming each follows the same mocking approach, with no test file mixing two different mock strategies.

**Acceptance Scenarios**:

1. **Given** a unit test file for a command or service, **When** the file is opened, **Then** all external dependencies are mocked using the single agreed-upon mocking approach.
2. **Given** a new unit test is added for an existing command, **When** the developer follows the established pattern, **Then** mocks are set up identically to other test files in the same folder with no special-case setup required.
3. **Given** a test uses the `NoOpFileTransactionCoordinator` or `NoOpRollbackReporter` null-object patterns, **When** the test is reviewed, **Then** their use is intentional and documented where the pattern differs from the standard mock approach.

---

### User Story 3 - Test Project Maintainability & Organization (Priority: P2)

A developer adding a new test for any command or service needs shared builders, base classes, and helpers that avoid repeating setup code across files. Currently each command test file independently wires up 5-7 mock objects and a `TestConsole` in its constructor. When a service constructor changes, every test file that touches that service must be updated individually.

**Why this priority**: Reducing duplicated setup code lowers the maintenance burden when the production codebase evolves, as constructor changes and new dependencies need updating in fewer places.

**Independent Test**: Can be verified by confirming that shared test infrastructure (mock builders, base test classes, shared fixture helpers) exists and is referenced across at least two unrelated test files.

**Acceptance Scenarios**:

1. **Given** a shared test base class or builder exists for command tests, **When** a new command unit test file is created, **Then** the author only needs to configure the specific dependencies relevant to their test, not all 7 mocks.
2. **Given** a service constructor gains a new required dependency, **When** the test suite is updated, **Then** only the shared builder or base class needs updating, not every independent test file.
3. **Given** the test project folder structure, **When** a developer looks for tests for a specific command or service, **Then** the folder mirrors the main project layout and is immediately obvious.
4. **Given** all test files, **When** all tests are run, **Then** the test output groups related tests together so failures are easy to triage.

---

### Edge Cases

- What happens when a temporary directory for an integration test cannot be created (e.g., insufficient permissions or CI temp-path restrictions)? The test must fail clearly with the surfaced environment error; it must not skip, silently redirect to a different location, or leave orphaned files.
- How should integration tests handle partial state left by a previously interrupted test run? Each test must use a unique temporary path (`journal-{Guid:N}` under `Path.GetTempPath()`) to avoid collisions, except when a test is explicitly asserting "already exists" or "already initialized" behavior.
- What if a unit test relies on a `NoOp` null-object where a real mock is needed to verify interaction? The cleanup must identify and flag these cases rather than silently converting them.
- How does the suite handle tests that were previously integration-style but lack proper teardown? Teardown must run automatically even on test failure; if setup fails before the temp root exists, cleanup is a no-op, and if setup created a temp root, that entire root must be deleted recursively.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Every CLI command (`init`, `new`, `add`, `update`, `remove`) MUST have at least one dedicated integration test class that exercises the full command pipeline without mocks. For this feature, "full command pipeline" means CLI argument parsing and binding, the concrete Spectre command type, the real service implementation(s) invoked by that command, any transaction/rollback infrastructure the command normally uses, and persistence through the real file system. The minimum observable artifacts are defined in the artifact verification matrix above.
- **FR-002**: Moq is the project-standard mocking approach. Any non-integration test that replaces an external dependency MUST use Moq for that substitution. This applies to command unit tests, service unit tests, and any other non-integration test that substitutes a real collaborator. It does NOT apply to integration tests governed by FR-009 and FR-010 or rollback tests governed by FR-008. `NoOpFileTransactionCoordinator` and `NoOpRollbackReporter` remain permitted where transaction or rollback behavior is intentionally out of scope.
- **FR-003**: Integration tests MUST create and clean up all temporary file system resources automatically, regardless of whether the test passes or fails. If temp-directory creation fails before the root directory exists, cleanup is a no-op and the test fails with the surfaced environment error. If setup created a temp root and the test later fails, teardown MUST delete that entire root recursively.
- **FR-004**: Shared mock setup MUST be consolidated using separate layer-scoped base classes (`CommandTestBase` for command-layer unit tests, `ServiceTestBase` for service-layer unit tests) plus a shared `MockFactory` utility for common mock construction. Command integration tests MUST use `JournalIntegrationTestBase`, and rollback or fault-injection tests MUST use `ServiceRollbackTestBase` or the existing command rollback pattern. A shared builder or base class is required once the same dependency configuration appears in two or more test files in the same layer. The normative public API for `CommandTestBase`, `ServiceTestBase`, `MockFactory`, and `JournalIntegrationTestBase` is defined in `contracts/test-infrastructure-api.md`.
- **FR-005**: The test project folder structure MUST mirror the main project's folder structure so that tests for a given component are located in the corresponding subfolder.
- **FR-006**: Each test MUST produce a failure message that identifies the specific assertion that failed and the context, without requiring a debugger.
- **FR-007**: All existing passing command unit tests, service unit tests, and command integration tests MUST be migrated to the new shared patterns as part of this cleanup, including the existing `AddEntryIntegrationTests` and `AddTableOfContentsIntegrationTests`. The file-level exemptions are `markdown-journal-cli.Tests/Services/Rollback/**`, `markdown-journal-cli.Tests/Commands/Add/*RollbackTests.cs`, and infrastructure tests outside the command/service layers. This migration covers deduplication of mock setup only — it does NOT require replacing `NoOpFileTransactionCoordinator` or `NoOpRollbackReporter` with Moq mocks.
- **FR-008**: The `Services/Rollback/` folder structure, the `*ServiceRollbackTests` naming pattern, and the existing `Commands/Add/*RollbackTests.cs` rollback pattern MUST be preserved. These rollback and fault-injection suites are excluded from the FR-011 quality-pass renaming, deletion, and vacuous-assertion cleanup unless a change is limited to non-behavioral reference comments or documentation links.
- **FR-009**: Integration tests MUST use real disk I/O backed by temporary directories (matching the existing `AddEntryIntegrationTests` pattern), not in-memory file system substitutes.
- **FR-010**: Integration tests MUST use real service implementations and MUST NOT mock any dependency unless there is no repository-owned real implementation available or the dependency requires an unavailable external system (for example, external APIs or hardware). The default is zero mocks. "Minimal mocks" means only those narrowly justified exceptions, and each exception MUST be documented in the test file or PR description with the reason a real implementation could not be used.
- **FR-011**: The full quality pass MUST include: (a) fixing vacuous assertions and always-passing tests that provide no real coverage signal; (b) renaming misleading or unclear test names to clearly describe the scenario under test using the pattern `{MethodOrScenario}_Should_{ExpectedBehavior}_When_{Condition}`; (c) removing tests that duplicate coverage verbatim of another test with no unique scenario. A "vacuous assertion" is any assertion whose truth does not depend on production behavior (for example `Assert.True(true)`, `Assert.False(false)`, or an assertion that only verifies setup code executed). An "always-passing test" is a test that would still pass if the production method or command body were removed or returned early. "Equivalent replacement coverage" means the surviving test or tests exercise the same production path and assert the same observable behavior for that scenario; line-coverage percentage is not the acceptance metric. Any test removed under (c) MUST be justified in the PR description by naming the deleted test, the surviving replacement test(s), and the redundancy rationale. Coverage-tool output is optional, but reviewer-visible comparison evidence is required. This quality pass applies to command and service tests other than the rollback and fault-injection suites preserved by FR-008.
- **FR-012**: Every mergeable state of this cleanup and every PR update intended for `main` MUST keep the full test suite green. Temporary local experimentation may fail privately, but no failing state may be merged or presented for review as an implementation candidate.
- **FR-013**: Each integration test instance MUST use a unique temp root under `Path.GetTempPath()` using a Guid-derived name and MUST NOT reuse another test's directory, even after interrupted runs. If the target journal directory already exists at test start, the test must either create a fresh unique root or explicitly assert the command's "already exists" or "already initialized" behavior instead of reusing shared state.

### Key Entities

- **Command Integration Test**: An end-to-end test that invokes a CLI command through the full production pipeline (argument parsing/binding → command type → real service implementation(s) → transaction/rollback infrastructure → real file system) against real disk state, with no mocked dependencies unless FR-010 explicitly allows a narrowly justified exception.
- **Unit Test**: A test that isolates a single component under test by mocking its external dependencies with Moq and by avoiding real disk I/O or real end-to-end service wiring.
- **Shared Test Infrastructure**: Layer-scoped base classes (`CommandTestBase`, `ServiceTestBase`, `JournalIntegrationTestBase`) and the shared `MockFactory` utility residing in the test project's `Infrastructure/` folder, reused across multiple test files within each applicable layer and governed by `contracts/test-infrastructure-api.md`.
- **Fault-Injecting Test**: A rollback-focused test that uses `FaultInjectingFileSystem` to simulate I/O failures at specific operation points in order to verify rollback correctness; these tests remain governed by FR-008 rather than FR-011.
- **Vacuous Assertion**: An assertion whose truth is detached from the production behavior under test, such as `Assert.True(true)` or any equivalent assertion that would still succeed if the production path were never exercised.
- **Always-Passing Test**: A test whose pass/fail outcome does not materially depend on the production code path under test because the assertion is trivial, disconnected, or only validates setup.
- **Equivalent Replacement Coverage**: Reviewer-verifiable evidence that a surviving test or tests still exercise the same scenario and observable outcomes as a removed duplicate test.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All 5 CLI commands (init, new, add, update, remove) have at least one integration test class by the end of this feature.
- **SC-002**: Across migrated command and service unit tests, zero files retain duplicate declarations or constructor wiring for the six standard shared dependency mocks — every shared setup lives in `CommandTestBase`, `ServiceTestBase`, or `MockFactory`. Reviewers verify this by confirming the applicable tests inherit the shared base classes and do not redeclare those shared mocks locally.
- **SC-003**: All tests pass in CI with zero new test failures introduced by the cleanup, and no mergeable review state for this feature leaves the suite red.
- **SC-004**: A developer adding a new command or service unit test can do so by inheriting the appropriate shared base class without locally constructing the shared `ServiceCollection` / `TestConsole` wiring or redeclaring the six standard shared dependency mocks; only scenario-specific setup and verification code is added in the test file.
- **SC-005**: Test project folder layout maps 1:1 to the main project layout — no test file lives in a folder that does not correspond to a main project folder.
- **SC-006**: Every integration test cleans up its temporary resources in 100% of test runs, including runs where the test fails partway through.
- **SC-007**: No vacuous assertions (for example `Assert.True(true)`) or always-passing tests remain in the command and service test files covered by FR-011 after the cleanup. Review scope is bounded to `markdown-journal-cli.Tests/Commands/` and `markdown-journal-cli.Tests/Services/`, excluding rollback and fault-injection suites preserved by FR-008.

## Non-Functional Requirements

- **NFR-001**: The full `dotnet test markdown-journal-cli.sln` run MUST complete in under 60 seconds on the repository's standard CI runner under normal load.
- **NFR-002**: Integration tests depend on a writable system temp directory. If the environment cannot create directories under `Path.GetTempPath()`, the run MUST fail clearly rather than skipping affected tests or silently redirecting them elsewhere.

## Reviewer Verification Notes

- **SC-002 verification proxy**: reviewers confirm that migrated command and service unit tests inherit `CommandTestBase` or `ServiceTestBase` and do not redeclare the shared dependency mocks locally.
- **SC-004 verification proxy**: reviewers confirm that representative new unit tests add only scenario-specific `Setup()` / `Verify()` calls and do not rebuild the shared DI or console wiring.
- **SC-007 verification scope**: automated searches for obvious vacuous patterns may assist review, but manual inspection is bounded to `Commands/` and `Services/` test files excluding rollback and fault-injection suites preserved by FR-008.

## Success Criteria Traceability

| Success Criterion | Backing requirements |
|---|---|
| **SC-001** | FR-001, FR-009, FR-010 |
| **SC-002** | FR-004, FR-007 |
| **SC-003** | FR-003, FR-012 |
| **SC-004** | FR-004 |
| **SC-005** | FR-005 |
| **SC-006** | FR-003, FR-013 |
| **SC-007** | FR-011 |

## Assumptions

- Moq is already present in the test project dependencies, so adopting FR-002 does not require a package change.
- The `FaultInjectingFileSystem` / `TestFileSystem` infrastructure already in place will be preserved and extended, not replaced. These are not substitutes for Moq but complementary tools for testing transaction and rollback behavior specifically.
- The repository's standard local and CI environments provide a writable system temp directory, which is required by FR-013 and NFR-002.
- The `NoOpFileTransactionCoordinator` and `NoOpRollbackReporter` null-object patterns are intentional for simple unit tests that do not need to verify transaction or rollback behavior. They remain in place and are explicitly out of scope for replacement under FR-002 and FR-007.
- The migration scope covers command unit tests, service unit tests, and command integration tests. Rollback/fault-injection suites and infrastructure tests retain their specialized patterns per FR-007 and FR-008.
- The scope of this feature is limited to the test project; no changes to production source code are required unless a refactor is needed to make a component testable.
- Existing rollback tests under `Services/Rollback/` and the `Commands/Add/` rollback test files are considered correct and serve as the reference pattern for new rollback tests.

## Clarifications

### Session 2026-04-11

- Q: Should `NoOpFileTransactionCoordinator` / `NoOpRollbackReporter` usages be migrated to Moq as part of the FR-007 test migration? → A: No — keep NoOp patterns for transaction/rollback opt-out; FR-007 migration covers mock setup deduplication (builders/base classes) only, not NoOp replacement.
- Q: What form should the shared test infrastructure take — single base class, layer-scoped base classes, or static factories only? → A: Separate base classes per layer (`CommandTestBase`, `ServiceTestBase`) plus a shared `MockFactory`; commands and services have different wiring needs that make a single root base class unwieldy.
- Q: What is the scope of the cleanup for existing test content — structural fixes only, or full quality pass? → A: Full quality pass — fix vacuous assertions and always-pass tests, rename misleading test names, and remove verified duplicate coverage; all three categories are in scope.

### Session 2026-04-12

- Q: What counts as "minimal mocks" in FR-010? → A: Only collaborators with no repository-owned real implementation or collaborators that require unavailable external systems; each exception must be explicitly justified in the test file or PR description.
- Q: Are the existing `add` command integration tests exempt from the shared integration-test base migration? → A: No — `AddEntryIntegrationTests` and `AddTableOfContentsIntegrationTests` are explicitly in scope under FR-007.
- Q: Are rollback and fault-injection suites part of the FR-011 quality pass? → A: No — they are preserved under FR-008 and excluded from the renaming / deletion / vacuous-assertion audit unless only documentation-only comments are touched.
- Q: What evidence is required before deleting a duplicate test? → A: The PR description must name the deleted test, the surviving replacement test(s), and the redundancy rationale; coverage-tool output is optional, but reviewer-visible comparison evidence is required.
