# Quickstart: .NET 10 Upgrade Execution Guide

**Feature**: `001-dotnet10-upgrade`  
**Date**: 2026-04-05  
**SDK Required**: .NET 10.0.201 or any later .NET 10.x SDK

This guide covers the exact execution order for the upgrade tasks. See [research.md](research.md) for rationale behind every decision.

---

## Step 1 — Add `global.json`

Create at the repository root:

```json
{
  "sdk": {
    "version": "10.0.201",
    "rollForward": "latestMinor"
  }
}
```

**Verify**: `dotnet --version` should print `10.0.x`.

---

## Step 2 — Update `markdown-journal-cli.csproj`

Apply in one edit:

| Property | From | To |
|---|---|---|
| `<TargetFramework>` | `net9.0` | `net10.0` |
| `Microsoft.Extensions.Configuration` | `10.0.0` | `10.0.5` |
| `Microsoft.Extensions.Configuration.UserSecrets` | `10.0.0` | `10.0.5` |
| `Microsoft.Extensions.DependencyInjection` | `10.0.0` | `10.0.5` |
| `Microsoft.Extensions.Hosting` | `10.0.0` | `10.0.5` |
| `Microsoft.Extensions.Options.DataAnnotations` | `10.0.0` | `10.0.5` |
| `Spectre.Console` | `0.50.0` | `0.55.0` |
| `Spectre.Console.Cli` | `0.50.0` | `0.55.0` |

---

## Step 3 — Update `markdown-journal-cli.Tests.csproj`

Apply in one edit:

| Property | From | To |
|---|---|---|
| `<TargetFramework>` | `net9.0` | `net10.0` |
| `coverlet.collector` | `6.0.2` | `8.0.1` |
| `Microsoft.Extensions.DependencyInjection` | `10.0.0` | `10.0.5` |
| `Microsoft.NET.Test.Sdk` | `17.12.0` | `18.3.0` |
| `Moq` | `4.20.72` | `4.20.72` (unchanged) |
| `Shouldly` | `4.3.0` | `4.3.0` (unchanged — no update available) |
| `Spectre.Console.Testing` | `0.50.0` | `0.55.0` |
| `xunit` | `2.9.2` | `2.9.3` |
| `xunit.runner.visualstudio` | `2.8.2` | `3.1.5` |

---

## Step 4 — Build Gate (Run First)

```bash
dotnet build markdown-journal-cli/markdown-journal-cli.csproj
```

Expected: exit 0, zero errors, zero warnings.

**If `TestConsole` constructor errors appear**: The parameterless `new TestConsole()` constructor may have changed in Spectre.Console 0.55.0. Update affected test class constructors to use the new factory/options pattern. The affected files all follow this pattern:

```csharp
// Before (may break)
_console = new TestConsole();

// After (if needed — check Spectre.Console 0.55.0 API)
_console = new TestConsole(new TestConsoleOptions { ... });
// or: _console = AnsiConsole.Create(new AnsiConsoleSettings { ... }) as TestConsole;
```

Files containing `new TestConsole()`:
- `markdown-journal-cli.Tests/Commands/Add/AddTableOfContentsCommandTests.cs`
- `markdown-journal-cli.Tests/Commands/Add/AddTableOfContentsRollbackTests.cs`
- `markdown-journal-cli.Tests/Commands/Add/AddJournalrcRollbackTests.cs`
- `markdown-journal-cli.Tests/Commands/Init/InitCommandTests.cs`
- `markdown-journal-cli.Tests/Commands/New/NewCommandTests.cs`
- `markdown-journal-cli.Tests/Commands/Remove/RemoveEntryCommandTests.cs`
- `markdown-journal-cli.Tests/Services/JournalUpdate/JournalUpdateServiceTests.cs`

---

## Step 5 — Test Gate

```bash
dotnet test
```

Expected: all tests pass, zero failures, zero skipped.

---

## Step 6 — Update VS Code Paths

In `.vscode/launch.json` and `.vscode/tasks.json`, replace all occurrences of:

```
net9.0
```

with:

```
net10.0
```

**Verify**: Open VS Code debugger → select any launch config → confirm it attaches without errors.

---

## Step 7 — Update Documentation

| File | Change |
|---|---|
| `README.md` | `.NET 9.0 or later` → `.NET 10.0 or later` (Prerequisites section) |
| `docs/DEVELOPMENT.md` | `.NET 9.0 SDK` → `.NET 10.0 SDK` (Prerequisites section) |
| `.instructions.md` | `A .NET 9 CLI application` → `A .NET 10 CLI application` (Project Overview) |

---

## Step 8 — Final Verification

```bash
# Zero net9.0 references in in-scope files
grep -r "net9\.0\|\.NET 9" \
  --include="*.csproj" \
  --include="*.json" \
  --include="*.md" \
  --include="*.cs" \
  --include=".instructions.md" \
  --exclude-dir=TODO \
  --exclude-dir=bin \
  --exclude-dir=obj \
  .

# Should print nothing (zero matches)
```

---

## Rollback

If the upgrade needs to be reverted before merging:

1. Revert `global.json` (delete it — it did not exist before)
2. Revert both `.csproj` files to `net9.0` and original package versions
3. Revert `.vscode/launch.json` and `.vscode/tasks.json` paths
4. Revert documentation files

All changes are concentrated in configuration files with no logic impact, so rollback is trivial.
