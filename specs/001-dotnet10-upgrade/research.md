# Research: .NET 10 Upgrade

**Feature**: `001-dotnet10-upgrade`  
**Date**: 2026-04-05  
**Status**: Complete — all NEEDS CLARIFICATION resolved

---

## R1: .NET 10 Breaking Changes Relevant to This Codebase

- **Decision**: No breaking changes require source code modifications in this project.
- **Rationale**:
  - `System.Text.Json` mutation enforcement: The project does not use `JsonSerializerOptions` in production source (only scratch notes in `TODO/`). No action required.
  - `FileStream` default buffer change: All file I/O goes through `IFileSystem` / `FileSystem.cs` abstraction, not raw `FileStream` constructors. No direct impact.
  - `Path.GetFullPath` trailing separator: CLI path normalisation is done via standard `Path.Combine` / `Path.GetFullPath` — no edge-case trailing separators.
  - Console / terminal APIs: Spectre.Console wraps terminal via ANSI escape sequences; no `System.Console` API changes affect it.
  - Reflection / type resolution: No dynamic `Type.GetType` by string anywhere in production code.
- **Alternatives considered**: Checked the full .NET 10 breaking changes catalog at `learn.microsoft.com/dotnet/core/compatibility/10.0`. All items requiring action were either .NET library internals or patterns not used in this codebase.
- **Action Required**: None to production source code.

---

## R2: xunit.runner.visualstudio 3.x Migration (2.8.2 → 3.1.5)

- **Decision**: Drop-in upgrade. xunit 2.9.3 and adapter 3.1.5 are fully compatible. No test code changes required.
- **Rationale**:
  - `xunit.runner.visualstudio` 3.x explicitly supports xunit v2 projects (adapter handles both v2 and v3 protocols).
  - xunit 2.9.3 is a maintenance-only release — no test-facing API changes from 2.9.2.
  - Adapter 3.x dropped net462 support only. All test projects target `net10.0` after this upgrade, satisfying the net8.0+ requirement.
  - The adapter no longer references `xunit.runner.console` as a transitive dep in 3.x. The project does not use `xunit.console.exe` directly; `dotnet test` is unaffected.
- **Alternatives considered**: Keeping adapter at 2.8.2. Rejected: version drift accumulates; 3.x provides .NET 10 SDK compatibility without risk.
- **Action Required**: Update version in `markdown-journal-cli.Tests.csproj` to `3.1.5`. No test code changes.

---

## R3: Spectre.Console 0.50.0 → 0.55.0 Impact Assessment

- **Decision**: The `TestConsole` parameterless constructor **must be verified** before upgrading. All other 0.55.0 changes are safe for this project.
- **Rationale**:
  - **`TestConsole` constructor (HIGH RISK)**: Reports indicate a constructor signature change between 0.53 and 0.55. The project uses `new TestConsole()` (parameterless) in 6+ test files. This is the single highest-risk item. Mitigation: after updating the package, run `dotnet build` immediately; if `CS0117` or similar errors appear, update to the new factory pattern.
  - **`ICommandLimiter<T>` removal**: Not used anywhere in this project. No action required.
  - **Strict markup tag parsing**: Production code uses `.EscapeMarkup()` per constitution before all interpolation. No unbalanced literal markup strings found. No action required.
  - **`SelectionPrompt<T>` null display**: Not used in production code yet. No action required.
  - **`CommandApp.SetDefaultCommand<T>()`**: No-argument overload unchanged. No action required.
  - **`IAnsiConsole.Write()` overloads**: Additive change; existing call sites unaffected.
- **Alternatives considered**: Staying at 0.50.0. Rejected: version must match `net10.0` TFM targeting to ensure official support declaration.
- **Action Required**:
  1. Update all three Spectre packages to `0.55.0`.
  2. Run `dotnet build` immediately after the package update.
  3. If `TestConsole` constructor errors appear, update to the new instantiation pattern (factory method or options constructor) across all 6 test class constructors.

---

## R4: global.json — SDK Pin Strategy

- **Decision**: Add `global.json` to repository root with `"version": "10.0.201"` and `"rollForward": "latestMinor"`.
- **Rationale**:
  - Installed SDK: `10.0.201` (confirmed: `dotnet --list-sdks`).
  - `rollForward: latestMinor` uses the highest installed SDK with.NET major `10` and any minor/feature band ≥ `10.0.201`. In practice: **any .NET 10.x.y SDK satisfies the constraint**. Contributors with `10.0.300`, `10.0.401`, etc. use their installed version without friction.
  - `rollForward: latestMinor` does NOT roll forward to .NET 11+. The major version (`10`) is always honoured.
  - `rollForward: disable` (exact pin) was rejected: creates unnecessary friction for contributors with a different patch SDK.
  - `rollForward: latestPatch` was considered: acceptable, but `latestMinor` is more CI-friendly across feature band updates without involving a future major.
- **Alternatives considered**: No `global.json` (rejected: no SDK guard, developer could accidentally build with .NET 9 if it remains installed and no `global.json` signals otherwise).
- **Action Required**: Create `global.json` at repository root.

```json
{
  "sdk": {
    "version": "10.0.201",
    "rollForward": "latestMinor"
  }
}
```

---

## R5: Documentation Files Requiring Updates

In-scope files with `.NET 9` references that must be updated:

| File | Current text | Required change |
|---|---|---|
| `README.md` line 52 | `.NET 9.0 or later` | `.NET 10.0 or later` |
| `docs/DEVELOPMENT.md` line 10 | `.NET 9.0 SDK` | `.NET 10.0 SDK` |
| `.instructions.md` line 8 | `A .NET 9 CLI application` | `A .NET 10 CLI application` |
| `.vscode/launch.json` (14 paths) | `net9.0` in program paths | `net10.0` |
| `.vscode/tasks.json` (1 command) | `net9.0` in rollback-test command | `net10.0` |

Out-of-scope (historical research docs — excluded per spec assumptions):
- `TODO/docs/*` — reference material only
- `docs/research/*` — research archive

---

## Summary of Actions Required

| # | Action | Risk | Files |
|---|---|---|---|
| 1 | Change `TargetFramework` to `net10.0` | Low | Both `.csproj` files |
| 2 | Update Microsoft.Extensions.* to `10.0.5` | Low | Both `.csproj` files |
| 3 | Update Spectre packages to `0.55.0` | Medium (`TestConsole`) | Both `.csproj` files |
| 4 | Update xunit + test packages | Low | `Tests.csproj` |
| 5 | Fix `TestConsole` instantiation if needed | Medium | Test class constructors |
| 6 | Add `global.json` | Low | Repo root |
| 7 | Update VS Code paths (`net9.0` → `net10.0`) | Low | `.vscode/launch.json`, `.vscode/tasks.json` |
| 8 | Update documentation | Low | `README.md`, `docs/DEVELOPMENT.md`, `.instructions.md` |
| 9 | Verify: `dotnet build` passes (exit 0, 0 warnings) | — | Validation gate |
| 10 | Verify: `dotnet test` passes (all green) | — | Validation gate |
