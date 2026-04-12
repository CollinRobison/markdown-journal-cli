# Feature Specification: Test Suite Deep Dive & Cleanup

**Feature Branch**: `002-test-suite-cleanup`  
**Created**: 2026-04-11  
**Status**: Draft  
**Input**: User description: "I need to do a deep dive/clean up of the unit / integration tests of this project. I notice a lot of this project is not using moq for the unit tests and a lot of the commands dont have integration tests. also I need to make the test projects cleaner and more maintainable."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Missing Command Integration Tests (Priority: P1)

A developer working on the `init`, `new`, `update`, or `remove` commands needs to verify end-to-end behavior against real file system interactions. Currently only the `add` command has dedicated integration tests; all other commands rely solely on unit tests. When a regression is introduced in one of these commands, it may not be caught until it reaches production.

**Why this priority**: Integration tests are the most valuable safety net for catching regressions in user-facing behavior. Without them, entire command code paths go unexercised in CI.

**Independent Test**: Can be verified by running the test suite and confirming that `Init`, `New`, `Update`, and `Remove` command integration test classes exist and exercise the real CLI pipeline end-to-end.

**Acceptance Scenarios**:

1. **Given** the `init` command runs against a temporary directory, **When** the command completes successfully, **Then** the expected journal folder structure is present on disk and the test passes without mocks.
2. **Given** the `new` command runs against an initialized journal, **When** a new journal is created, **Then** the expected files are present and the test passes without mocks.
3. **Given** the `update` command runs against a journal with existing entries, **When** an entry is updated, **Then** the entry file reflects the change and the test passes without mocks.
4. **Given** the `remove` command runs against a journal with existing entries, **When** an entry is removed, **Then** the entry is absent and the table of contents is updated, verified without mocks.
5. **Given** any integration test completes (pass or fail), **When** the test teardown runs, **Then** all temporary files created during the test are cleaned up from disk.

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

- What happens when a temporary directory for an integration test cannot be created (e.g., insufficient permissions)? The test should skip or fail with a clear message rather than leaving orphaned files.
- How should integration tests handle partial state left by a previously interrupted test run? Each test must use a unique temporary path (e.g., `Guid`-based) to avoid collisions.
- What if a unit test relies on a `NoOp` null-object where a real mock is needed to verify interaction? The cleanup must identify and flag these cases rather than silently converting them.
- How does the suite handle tests that were previously integration-style but lack proper teardown? Teardown must be implemented via `IDisposable` or `IAsyncLifetime` to guarantee cleanup even on test failure.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Every CLI command (`init`, `new`, `add`, `update`, `remove`) MUST have at least one dedicated integration test class that exercises the full command pipeline without mocks.
- **FR-002**: Any test that exercises a component with a real external dependency MUST mock that dependency using the project-standard mocking approach. Unit tests in particular MUST be as fully mocked as practical — no real file I/O, no real service wiring unless the test is explicitly categorized as an integration test.
- **FR-003**: Integration tests MUST create and clean up all temporary file system resources automatically, regardless of whether the test passes or fails.
- **FR-004**: Shared mock setup MUST be consolidated using separate layer-scoped base classes (`CommandTestBase` for command tests, `ServiceTestBase` for service unit tests) plus a shared `MockFactory` utility for common mock construction. This pattern MUST be used wherever the same dependency configuration is repeated across two or more test files.
- **FR-005**: The test project folder structure MUST mirror the main project's folder structure so that tests for a given component are located in the corresponding subfolder.
- **FR-006**: Each test MUST produce a failure message that identifies the specific assertion that failed and the context, without requiring a debugger.
- **FR-007**: All existing passing tests MUST be migrated to the new shared patterns (builders, base classes) as part of this cleanup; no test may be deleted without equivalent replacement coverage. This migration covers deduplication of mock setup only — it does NOT require replacing `NoOpFileTransactionCoordinator` or `NoOpRollbackReporter` with Moq mocks.
- **FR-011**: The full quality pass MUST include: (a) fixing vacuous assertions and always-passing tests that provide no real coverage signal; (b) renaming misleading or unclear test names to clearly describe the scenario under test; (c) removing tests that duplicate coverage verbatim of another test with no unique scenario. Any test removed under (c) MUST have its coverage verified as redundant before deletion.
- **FR-009**: Integration tests MUST use real disk I/O backed by temporary directories (matching the existing `AddEntryIntegrationTests` pattern), not in-memory file system substitutes.
- **FR-010**: Integration tests MUST use real service implementations and MUST NOT mock any dependency unless there is no real implementation available (e.g., external APIs, hardware). The goal is to exercise the full production wiring end-to-end with zero or minimal mocks.
- **FR-008**: The `Rollback/` test folder pattern (service-level rollback tests using `FaultInjectingFileSystem`) MUST be preserved and integrated into the shared test infrastructure rather than duplicated per test file.

### Key Entities

- **Command Integration Test**: An end-to-end test that invokes a CLI command through the full pipeline (command → service → file system) against real or in-memory disk state, with no mocked dependencies.
- **Unit Test**: A test that isolates a single component under test by mocking all its external dependencies.
- **Shared Test Infrastructure**: Layer-scoped base classes (`CommandTestBase`, `ServiceTestBase`) and a shared `MockFactory` utility residing in the test project's `Infrastructure/` folder, reused across multiple test files within each layer.
- **Fault-Injecting Test**: A test that uses `FaultInjectingFileSystem` to simulate I/O failures at specific operation points in order to verify rollback correctness.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All 5 CLI commands (init, new, add, update, remove) have at least one integration test class by the end of this feature.
- **SC-002**: The number of repeated mock setup blocks (identical constructor-level mock configurations) across test files is reduced to zero — every shared setup lives in one place.
- **SC-003**: All tests pass in CI with zero new test failures introduced by the cleanup.
- **SC-004**: A developer adding a new command test can do so by extending a shared base class or using a builder, configuring only the dependencies specific to their scenario.
- **SC-005**: Test project folder layout maps 1:1 to the main project layout — no test file lives in a folder that does not correspond to a main project folder.
- **SC-006**: Every integration test cleans up its temporary resources in 100% of test runs, including runs where the test fails partway through.
- **SC-007**: No vacuous assertions (e.g., `Assert.True(true)`) or always-passing tests (tests that pass regardless of production code behavior) remain in the suite after the cleanup.

## Assumptions

- Moq is the established and preferred mocking library for this project (already in `.csproj`). It will be used wherever an external dependency is being replaced — meaning in any test (unit or otherwise) that substitutes a real collaborator. Unit tests in particular must maximize the use of mocks rather than real implementations.
- The `FaultInjectingFileSystem` / `TestFileSystem` infrastructure already in place will be preserved and extended, not replaced. These are not substitutes for Moq but complementary tools for testing transaction and rollback behavior specifically.
- Integration tests will use real disk I/O with temporary directories under `Path.GetTempPath()` using `Guid`-based names, matching the pattern established by `AddEntryIntegrationTests`. They will wire up real service implementations with zero or minimal mocks — the intent is full end-to-end coverage of the production dependency graph.
- The `NoOpFileTransactionCoordinator` and `NoOpRollbackReporter` null-object patterns are intentional for simple unit tests that do not need to verify transaction or rollback behavior. They will remain in place and are explicitly out of scope for the Moq migration — converting them would add setup noise with no validation benefit.
- The full refactor applies to ALL existing tests — every test file should be migrated to use shared builders and base classes where applicable, except that NoOp usage is not subject to replacement. No other test file is exempt.
- The scope of this feature is limited to the test project; no changes to production source code are required unless a refactor is needed to make a component testable.
- Existing rollback tests under `Services/Rollback/` and the `Commands/Add/` rollback test files are considered correct and serve as the reference pattern for new rollback tests.

## Clarifications

### Session 2026-04-11

- Q: Should `NoOpFileTransactionCoordinator` / `NoOpRollbackReporter` usages be migrated to Moq as part of the FR-007 test migration? → A: No — keep NoOp patterns for transaction/rollback opt-out; FR-007 migration covers mock setup deduplication (builders/base classes) only, not NoOp replacement.
- Q: What form should the shared test infrastructure take — single base class, layer-scoped base classes, or static factories only? → A: Separate base classes per layer (`CommandTestBase`, `ServiceTestBase`) plus a shared `MockFactory`; commands and services have different wiring needs that make a single root base class unwieldy.
- Q: What is the scope of the cleanup for existing test content — structural fixes only, or full quality pass? → A: Full quality pass — fix vacuous assertions and always-pass tests, rename misleading test names, and remove verified duplicate coverage; all three categories are in scope.

