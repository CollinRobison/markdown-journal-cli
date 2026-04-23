# Implementation Plan: Journal Metadata Directory

**Branch**: `005-journal-metadata-dir` | **Date**: 2026-04-22 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/005-journal-metadata-dir/spec.md`

## Summary

Reorganize the internal file layout of every journal directory: extract TOC structure from `.journalrc` into a new `.journaltoc` file; rename and relocate the `.mdjournal` flat tracking file to `.mdjournal/.journalindex`; create a hidden metadata directory (`.mdjournal/`) to house both internal files. A new `add toc --structure-only / --md-only` command surface is introduced as an additive, non-breaking extension. No migration path is required — there are no existing users with old-layout journals.

## Technical Context

**Language/Version**: C# 13 / .NET 10 (`net10.0`)  
**Primary Dependencies**: Spectre.Console 0.55.0, Spectre.Console.Cli 0.55.0, Microsoft.Extensions.* 10.0.5  
**Storage**: Files — `.journalrc` (JSON config), `.mdjournal/` directory containing `.journalindex` (JSON tracking) and `.journaltoc` (JSON TOC structure), `.md` entry files  
**Testing**: xUnit + Moq + Shouldly; real `System.IO` for integration tests; `TestFileSystem` (in-memory) for unit/rollback tests  
**Target Platform**: macOS / Linux (`.` prefix hidden-directory convention; Windows in scope)  
**Project Type**: CLI tool  
**Performance Goals**: N/A — local file system operations; no throughput requirements  
**Constraints**: No migration path required. `.journalrc` retains its root-level position and name. No command flag, argument, or exit code changes. `add toc` new flags (`--structure-only`, `--md-only`) are additive.  
**Scale/Scope**: Single-user local CLI; journals are personal directories of markdown files.

## Constitution Check

*GATE: Must pass before implementation. Re-checked after design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Thin Command Layer | ✅ | All new logic in services; commands only delegate and output |
| II. Service-Oriented | ✅ | `AddTocService`, `JournalValidator`, `JournalTocStructureRepository` each backed by an interface |
| III. File System Abstraction | ✅ | `IsDirectory` added to `IFileSystem`; no direct `System.IO` in services or commands |
| IV. Transactional Integrity | ✅ | All multi-file writes (`new`, `init`, `add toc`) use `FileTransactionScope` |
| V. Test Coverage | ✅ | Mirror test classes for every new service, command, and infrastructure component |
| VI. Rich Terminal UI | ✅ | `IAnsiConsole` injected for error and warning output; `.EscapeMarkup()` on user-supplied paths |

## Project Structure

### Documentation (this feature)

```text
specs/005-journal-metadata-dir/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
└── tasks.md
```

### Source Code (repository root)

```text
markdown-journal-cli/
├── JournalSettings.cs                              ← add MetadataDirName, TrackingFileName, TocStructureFileName
├── appsettings.json                                ← add new setting defaults
├── Program.cs                                      ← register new services
├── Commands/
│   ├── JournalCommand.cs                           ← add SkipMetadataValidation virtual + validator call
│   ├── New/NewCommand.cs                           ← override SkipMetadataValidation => true
│   ├── Init/InitCommand.cs                         ← override SkipMetadataValidation => true
│   └── Add/
│       └── AddTableOfContentsCommand.cs            ← add --structure-only / --md-only flags
├── Infrastructure/
│   ├── Configuration/
│   │   ├── Models/
│   │   │   ├── TableOfContents.cs                  ← remove Structure + RootEntries
│   │   │   └── JournalTocStructure.cs              ← NEW (with Empty() factory)
│   │   ├── JournalConfiguration.cs                 ← omit structure/rootEntries from .journalrc writes
│   │   ├── JournalConfigGenerator.cs               ← load/save via IJournalTocStructureRepository
│   │   ├── IJournalTocStructureRepository.cs       ← NEW
│   │   └── JournalTocStructureRepository.cs        ← NEW
│   ├── FileSystem/
│   │   ├── IFileSystem.cs                          ← add IsDirectory
│   │   └── FileSystem.cs                           ← implement IsDirectory
│   ├── Tracking/
│   │   └── FileTracking.cs                         ← update path construction + exclusion filter
│   └── Validation/
│       ├── IJournalValidator.cs                    ← NEW
│       └── JournalValidator.cs                     ← NEW
└── Services/
    ├── AddToc/
    │   ├── IAddTocService.cs                       ← NEW
    │   └── AddTocService.cs                        ← NEW
    ├── NewJournal/NewJournalService.cs             ← create metadata dir + split files
    ├── InitJournal/InitJournalService.cs           ← create metadata dir + split files
    ├── TableOfContents/TableOfContentsService.cs   ← write to .journaltoc via repository
    ├── JournalUpdate/JournalUpdateService.cs       ← read TOC structure from repository
    ├── JournalEntry/JournalEntryService.cs         ← resolve index + TOC from metadata dir
    ├── RemoveEntry/RemoveEntryService.cs           ← resolve paths from metadata dir
    └── JournalFileUpdate/JournalFileUpdateService.cs ← resolve metadata paths from .mdjournal/

markdown-journal-cli.Tests/
├── Infrastructure/
│   ├── FileSystem/FileSystemTests.cs               ← IsDirectory tests
│   ├── Configuration/
│   │   └── JournalTocStructureRepositoryTests.cs   ← NEW
│   ├── Validation/
│   │   └── JournalValidatorTests.cs                ← NEW
│   ├── JournalIntegrationTestBase.cs               ← update to new layout
│   └── QuickstartValidationTests.cs                ← update to new layout
└── Commands/
    ├── Add/
    │   ├── AddEntryIntegrationTests.cs             ← verify metadata dir updates
    │   └── AddTableOfContentsIntegrationTests.cs   ← verify add toc flags
    ├── Update/UpdateCommandIntegrationTests.cs     ← verify split-file reads/writes
    ├── Remove/RemoveEntryCommandIntegrationTests.cs ← verify metadata dir cleanup
    └── Init/InitCommandIntegrationTests.cs         ← FR-007a validation test
```

**Structure Decision**: Single-project CLI tool. All new types follow existing conventions (`Services/`, `Infrastructure/`, `Commands/`). A new `Validation/` subfolder is introduced under `Infrastructure/` for `IJournalValidator` / `JournalValidator`.

## Key Technical Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Validator exemption pattern | `protected virtual bool SkipMetadataValidation => false;` on `JournalCommand<TSettings>`; overridden to `true` in `NewCommand` and `InitCommand` | Single base class; opt-out is explicit and minimal surface area change |
| TOC structure file format | JSON (`System.Text.Json`, `WriteIndented = true`) | Consistent with `.journalrc` and `.journalindex`; no new parser required |
| Layout detection | File-vs-directory check on `.mdjournal` path | Simple; `IsDirectory()` added to `IFileSystem`; no content parsing |
| Migration scope | None | No existing users; all commands target new layout only |
| `JournalTocStructure` initialization | Static `Empty()` factory for absent-file case | `required` properties cannot be omitted; empty arrays are safe defaults |
