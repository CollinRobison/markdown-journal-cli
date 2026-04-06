# Implementation Plan: .NET 10 Upgrade

**Branch**: `001-dotnet10-upgrade` | **Date**: 2026-04-05 | **Spec**: [spec.md](spec.md)  
**Input**: Feature specification from `/specs/001-dotnet10-upgrade/spec.md`

## Summary

Upgrade the project from .NET 9 to .NET 10 by updating `TargetFramework` in both `.csproj` files, bumping all NuGet packages to their latest stable versions, adding a `global.json` SDK pin (`10.0.201`, `rollForward: latestMinor`), updating VS Code tooling paths, and refreshing all user-facing documentation. No new services, interfaces, or domain logic are introduced. The upgrade is a pure tooling and configuration change that must leave all existing tests green.

## Technical Context

**Language/Version**: C# 13 / .NET 10 (`net10.0`) — upgrading from .NET 9 (`net9.0`)  
**Primary Dependencies**: Spectre.Console 0.55.0, Spectre.Console.Cli 0.55.0, Microsoft.Extensions.* 10.0.5  
**Storage**: File system (markdown files, `.journalrc`, `.mdjournal`) — unchanged by this upgrade  
**Testing**: xUnit 2.9.3 + xunit.runner.visualstudio 3.1.5 + Moq 4.20.72 + Shouldly 4.3.0 + Spectre.Console.Testing 0.55.0 — `dotnet test`  
**Target Platform**: macOS / Linux / Windows — .NET 10 cross-platform CLI  
**Project Type**: CLI application (global dotnet tool, Spectre.Console.Cli)  
**Performance Goals**: N/A — build time and test runtime unchanged  
**Constraints**: Single TFM `net10.0` only; multi-targeting is out of scope per constitution  
**Scale/Scope**: 2 project files, 1 `global.json`, 4 VS Code config files, 3 documentation files to update

### Package Version Delta

| Package | Current | Target | Notes |
|---|---|---|---|
| `TargetFramework` (both projects) | `net9.0` | `net10.0` | Core change |
| `Microsoft.Extensions.*` (all 5) | `10.0.0` | `10.0.5` | Patch bump |
| `Spectre.Console` | `0.50.0` | `0.55.0` | Minor bump — see research |
| `Spectre.Console.Cli` | `0.50.0` | `0.55.0` | Minor bump |
| `Spectre.Console.Testing` | `0.50.0` | `0.55.0` | Minor bump — `TestConsole` constructor risk |
| `xunit` | `2.9.2` | `2.9.3` | Patch |
| `xunit.runner.visualstudio` | `2.8.2` | `3.1.5` | Major bump — v2 compat confirmed |
| `Microsoft.NET.Test.Sdk` | `17.12.0` | `18.3.0` | Minor bump |
| `coverlet.collector` | `6.0.2` | `8.0.1` | Major bump (no API changes for tests) |
| `Moq` | `4.20.72` | `4.20.72` | No update available; .NET 10 compatible |

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Thin Command Layer | ✅ PASS | No new commands; existing commands unchanged |
| II. Service-Oriented Architecture | ✅ PASS | No new services; existing services unchanged |
| III. File System Abstraction | ✅ PASS | No `System.IO` calls introduced |
| IV. Transactional Integrity | ✅ PASS | No multi-file write paths added |
| V. Test Coverage | ✅ PASS | All existing tests must remain green; no new business logic added |
| VI. Rich Terminal UI | ✅ PASS | No new console output introduced; `TestConsole` constructor usage requires verification against Spectre.Console 0.55.0 (see research) |
| Technology Stack | ✅ PASS | This change satisfies the constitution: "Runtime: .NET 10 — downgrade requires an explicit ADR" |
| Security | ✅ PASS | No new dependencies without vetting; no secrets introduced |

**Post-design re-check**: No new entities, interfaces, or patterns introduced. Constitution gates all green.

## Project Structure

### Documentation (this feature)

```text
specs/001-dotnet10-upgrade/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output (N/A note — no new entities)
├── quickstart.md        # Phase 1 output
└── tasks.md             # Phase 2 output (/speckit.tasks — not created here)
```

### Files Changed (repository root)

```text
# Configuration / SDK
global.json                                             # NEW — SDK pin 10.0.201, rollForward: latestMinor

# Project files
markdown-journal-cli/markdown-journal-cli.csproj        # MODIFIED — TargetFramework + package versions
markdown-journal-cli.Tests/markdown-journal-cli.Tests.csproj  # MODIFIED — TargetFramework + package versions

# VS Code tooling
.vscode/launch.json                                     # MODIFIED — net9.0 → net10.0 in all program paths
.vscode/tasks.json                                      # MODIFIED — net9.0 → net10.0 in task commands

# User-facing documentation
README.md                                               # MODIFIED — prerequisites: .NET 9 → .NET 10
docs/DEVELOPMENT.md                                     # MODIFIED — prerequisites: .NET 9.0 SDK → .NET 10.0 SDK
.instructions.md                                        # MODIFIED — project description: .NET 9 → .NET 10
```

**Structure Decision**: Single-project CLI. No new directories. All changes are in-place edits to existing files plus one new `global.json` at the repo root.

## Complexity Tracking

> No constitution violations to justify.
