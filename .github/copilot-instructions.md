# markdown-journal-cli Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-04-15

## Active Technologies
- C# 13 / .NET 10 (`net10.0`) + Spectre.Console 0.55.0, Spectre.Console.Cli 0.55.0, Microsoft.Extensions.* 10.0.5 (002-test-suite-cleanup)
- Real `System.IO` file system for integration tests; `TestFileSystem` (in-memory) for unit/rollback tests (002-test-suite-cleanup)
- C# 13 / .NET 10 (`net10.0`) + Spectre.Console 0.55.0, Spectre.Console.Cli 0.55.0, Microsoft.Extensions.* 10.0.5, xUnit + Moq + Shouldly (003-sync-skip-dates)
- Files — `.journalrc` and `.mdjournal` (JSON); markdown `.md` entry files (003-sync-skip-dates)

- C# 13 / .NET 10 (`net10.0`) — upgrading from .NET 9 (`net9.0`) + Spectre.Console 0.55.0, Spectre.Console.Cli 0.55.0, Microsoft.Extensions.* 10.0.5 (001-dotnet10-upgrade)

## Project Structure

```text
src/
tests/
```

## Commands

# Add commands for C# 13 / .NET 10 (`net10.0`) — upgrading from .NET 9 (`net9.0`)

## Code Style

C# 13 / .NET 10 (`net10.0`) — upgrading from .NET 9 (`net9.0`): Follow standard conventions

## Recent Changes
- 003-sync-skip-dates: Added C# 13 / .NET 10 (`net10.0`) + Spectre.Console 0.55.0, Spectre.Console.Cli 0.55.0, Microsoft.Extensions.* 10.0.5, xUnit + Moq + Shouldly
- 003-sync-skip-dates: Added [if applicable, e.g., PostgreSQL, CoreData, files or N/A]
- 002-test-suite-cleanup: Added C# 13 / .NET 10 (`net10.0`) + Spectre.Console 0.55.0, Spectre.Console.Cli 0.55.0, Microsoft.Extensions.* 10.0.5


<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
