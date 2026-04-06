# Feature Specification: .NET 10 Upgrade

**Feature Branch**: `001-dotnet10-upgrade`  
**Created**: 2026-04-05  
**Status**: Draft  
**Input**: User description: "I want to do a full upgrade of this project from .net9 to .net10"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Project Targets .NET 10 and Builds Clean (Priority: P1)

As a developer working on the project, I need both the main CLI project and the test project to target .NET 10 so that the codebase runs on the current supported runtime and satisfies the constitution's technology stack constraint.

**Why this priority**: This is the core deliverable of the upgrade. Without a successful .NET 10 build and green test suite, nothing else matters.

**Independent Test**: Run `dotnet build` and `dotnet test` against both project files and confirm both succeed with no errors or warnings.

**Acceptance Scenarios**:

1. **Given** the main CLI project targets `net9.0`, **When** `TargetFramework` is changed to `net10.0`, **Then** `dotnet build` completes with no errors or warnings
2. **Given** the test project targets `net9.0`, **When** `TargetFramework` is changed to `net10.0`, **Then** `dotnet test` completes with all existing tests passing and zero new failures
3. **Given** the project targets `net10.0`, **When** a developer compares the running runtime to the constitution's Technology Stack Constraints, **Then** no discrepancy exists between the declared constraint and the actual project target

---

### User Story 2 - NuGet Dependencies Are Compatible With .NET 10 (Priority: P2)

As a developer, I need all NuGet packages to be at versions that officially support .NET 10 so that no runtime compatibility warnings appear and the project benefits from the latest stable library releases.

**Why this priority**: Package compatibility is required for a clean, warning-free build. The `Microsoft.Extensions.*` packages are already at `10.0.0`; other packages (Spectre.Console, xunit, Moq, Shouldly, coverlet) need to be verified and updated.

**Independent Test**: Review updated `.csproj` files for `PackageReference` entries and confirm `dotnet build` produces no NuGet compatibility or deprecation warnings.

**Acceptance Scenarios**:

1. **Given** the project targets `net10.0`, **When** `dotnet build` is run, **Then** no "package is not compatible" or "targets a lower framework" warnings appear
2. **Given** test packages reference Spectre.Console.Testing, Moq, Shouldly, and xunit, **When** the test project targets `net10.0`, **Then** all test dependencies resolve without compatibility warnings
3. **Given** all packages have been updated, **When** `dotnet test` is run, **Then** all previously passing tests continue to pass

---

### User Story 3 - Developer Tooling and Documentation Reflect .NET 10 (Priority: P3)

As a developer cloning or working in the project, I need all VS Code configuration files and project documentation to reference the correct .NET 10 output paths and runtime requirement so that debugging, integrated tasks, and README instructions work out-of-the-box.

**Why this priority**: Stale `net9.0` paths in `.vscode/tasks.json` and `.vscode/launch.json` break the debugger and rollback-test tasks; stale README prerequisites mislead new contributors.

**Independent Test**: Confirm no `net9.0` path references remain in `.vscode/tasks.json`, `.vscode/launch.json`, `README.md`, and `.instructions.md`, and verify the VS Code debugger attaches without errors.

**Acceptance Scenarios**:

1. **Given** `.vscode/launch.json` contains `net9.0` in all program paths, **When** all paths are updated to `net10.0`, **Then** the VS Code debugger successfully attaches to the CLI application
2. **Given** `.vscode/tasks.json` references `net9.0` in rollback-test task commands, **When** paths are updated to `net10.0`, **Then** the `rollback-test: create-journal` task executes successfully
3. **Given** `README.md` states ".NET 9.0 or later" and `.instructions.md` describes "a .NET 9 CLI application", **When** both are updated to reference .NET 10, **Then** no remaining `net9` text appears in these user-facing files

---

### Edge Cases

- What happens if the .NET 10 SDK is not installed? The project should fail with a clear SDK-not-found error rather than silently using the wrong runtime.
- What if a NuGet package has no .NET 10-compatible version published? The build must surface a compatibility warning so the package can be assessed or replaced.
- What if a .NET 10 breaking change affects an existing API (e.g., `System.Text.Json`, reflection, or `System.IO`)? All existing tests must continue to pass; any breaking change must be resolved before the upgrade is complete.
- What if `global.json` pins an SDK version not available in a contributor's environment? The `rollForward: latestMinor` policy allows any .NET 10.x.y SDK to satisfy the constraint, so contributors only need any .NET 10 SDK installed — not a specific patch.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `markdown-journal-cli.csproj` MUST have `TargetFramework` set to `net10.0`
- **FR-002**: `markdown-journal-cli.Tests.csproj` MUST have `TargetFramework` set to `net10.0`
- **FR-003**: All `Microsoft.Extensions.*` packages MUST remain at version `10.x.x` or later with no downgrade
- **FR-004**: All third-party packages (Spectre.Console, Spectre.Console.Cli, Spectre.Console.Testing, xunit, xunit.runner.visualstudio, Moq, Shouldly, coverlet.collector, Microsoft.NET.Test.Sdk) MUST be updated to their latest stable versions that declare .NET 10 support
- **FR-005**: `dotnet build` MUST succeed with zero errors and zero warnings
- **FR-006**: `dotnet test` MUST pass with all existing tests green and zero new failures
- **FR-007**: All `net9.0` output path references in `.vscode/tasks.json` MUST be replaced with `net10.0`
- **FR-008**: All `net9.0` output path references in `.vscode/launch.json` MUST be replaced with `net10.0`
- **FR-009**: `README.md` prerequisites MUST state `.NET 10.0 or later`
- **FR-010**: `.instructions.md` project description MUST reference `.NET 10` instead of `.NET 9`
- **FR-011**: A top-level `global.json` MUST be added to the repository root, specifying `sdk.version` as the current stable .NET 10 SDK and `rollForward: latestMinor`
- **FR-012**: After the upgrade, a search for `net9.0` or `.NET 9` across all source, config, and user-facing documentation files MUST return zero matches

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: `dotnet build` completes with exit code 0 and zero diagnostics (errors or warnings)
- **SC-002**: `dotnet test` completes with exit code 0, zero failures, and zero skipped tests
- **SC-003**: A search for `net9.0` across all non-binary project, config, and user-facing documentation files returns zero matches
- **SC-004**: The VS Code debugger attaches and the CLI executes its first command successfully under .NET 10 with no runtime errors

## Assumptions

- No `global.json` currently exists in the repository; one may be created as an optional deliverable pending clarification
- The `TODO/docs/` research folder contains reference material only and does not need updating as part of this upgrade
- The `Microsoft.Extensions.*` packages are already at `10.0.0` and do not require version changes; only other packages and the `TargetFramework` properties need attention
- CI/CD pipelines are not yet configured; this upgrade affects only local developer and build tooling
- Any .NET 10 breaking changes that require code modifications MUST be resolved within the scope of this feature — no code is carved out as a follow-up
- The upgrade targets a single TFM (`net10.0` only); multi-targeting is out of scope per the constitution's technology stack constraint
