# Data Model: Test Suite Deep Dive & Cleanup

**Feature**: `002-test-suite-cleanup`  
**Phase**: 1 — Design  
**Date**: 2026-04-11

---

## Overview

This feature introduces shared test infrastructure entities in the test project only. No entities or data structures in the production project are changed. All entities below live under `markdown-journal-cli.Tests/Infrastructure/`.

---

## Entity 1: `CommandTestBase` (abstract class)

**Location**: `markdown-journal-cli.Tests/Infrastructure/CommandTestBase.cs`  
**Layer**: Command unit tests  
**Extends**: nothing (plain abstract class, xUnit constructor/dispose lifecycle)

### Responsibilities

- Construct all Moq mocks for command-level dependencies once in the base constructor
- Provide pre-configured "happy path" mock defaults via a `SetupDefaultBehaviors()` call
- Expose a `BuildApp(Action<IConfigurator> configure)` protected helper that produces a fresh `CommandAppTester` with mocks wired into the `TypeRegistrar`

### Fields

| Field | Type | Notes |
|---|---|---|
| `MockFileSystem` | `Mock<IFileSystem>` | Strict behavior |
| `MockJournalConfiguration` | `Mock<IJournalConfiguration>` | Strict behavior |
| `MockFileTracking` | `Mock<IFileTracking>` | Strict behavior |
| `MockTemplateManager` | `Mock<ITemplateManager>` | Strict behavior |
| `MockTableOfContentsService` | `Mock<ITableOfContentsService>` | Strict behavior |
| `MockEntryFormatterService` | `Mock<IEntryFormatterService>` | Strict behavior |
| `JournalSettings` | `IOptions<JournalSettings>` | Concrete Options.Create with test defaults |

### Key Methods

| Method | Return | Description |
|---|---|---|
| `BuildApp(Action<IConfigurator>)` | `CommandAppTester` | New tester instance + fresh output buffer per call |
| `SetupDefaultBehaviors()` | `void` | Protected — sets up common mock returns shared by most tests |

### Relationships
- Subclassed by each command-layer test class (e.g., `AddEntryCommandTests`, `InitCommandTests`)
- Uses `MockFactory` to obtain pre-configured `Mock<T>` instances
- Delegates DI registration to the existing `TypeRegistrar` (production class, already tested)

---

## Entity 2: `ServiceTestBase` (abstract class)

**Location**: `markdown-journal-cli.Tests/Infrastructure/ServiceTestBase.cs`  
**Layer**: Service unit tests  
**Extends**: nothing

### Responsibilities

- Construct all Moq mocks for service-level dependencies in the base constructor
- Provide access to `NoOpFileTransactionCoordinator.Instance` and `NoOpRollbackReporter.Instance` for the common case where transaction/rollback is not under test
- Expose a `MockRepository` for batch `VerifyAll()` after each test

### Fields

| Field | Type | Notes |
|---|---|---|
| `MockFileSystem` | `Mock<IFileSystem>` | Strict behavior |
| `MockJournalConfiguration` | `Mock<IJournalConfiguration>` | Strict behavior |
| `MockFileTracking` | `Mock<IFileTracking>` | Strict behavior |
| `MockTemplateManager` | `Mock<ITemplateManager>` | Strict behavior |
| `MockTableOfContentsService` | `Mock<ITableOfContentsService>` | Strict behavior |
| `MockEntryFormatterService` | `Mock<IEntryFormatterService>` | Strict behavior |
| `JournalSettings` | `IOptions<JournalSettings>` | Test defaults via `Options.Create` |
| `NoOpCoordinator` | `IFileTransactionCoordinator` | `NoOpFileTransactionCoordinator.Instance` |
| `NoOpReporter` | `IRollbackReporter` | `NoOpRollbackReporter.Instance` |

### Relationships
- Subclassed by each service-layer unit test class
- `ServiceRollbackTestBase` (existing) is independent — it wires a real `FileTransactionCoordinator` and `FaultInjectingFileSystem`, and should NOT extend this base class

---

## Entity 3: `MockFactory` (static class)

**Location**: `markdown-journal-cli.Tests/Infrastructure/MockFactory.cs`  
**Layer**: Shared across command and service tests

### Responsibilities

- Central factory for creating pre-configured `Mock<T>` instances
- Encodes "happy path" defaults so individual tests only need to override specific behaviors
- Returns `Mock<T>` (not `.Object`) so tests can add `Setup()` overrides and call `Verify()`

### Factory Methods

| Method | Return | Pre-configured Default |
|---|---|---|
| `CreateFileSystem()` | `Mock<IFileSystem>` | `.FileExists(It.IsAny<string>())` → `false` |
| `CreateJournalConfiguration()` | `Mock<IJournalConfiguration>` | None (strict, configure per test) |
| `CreateFileTracking()` | `Mock<IFileTracking>` | None (strict) |
| `CreateTemplateManager()` | `Mock<ITemplateManager>` | `.GetTemplate<T>()` returns an empty template |
| `CreateTableOfContentsService()` | `Mock<ITableOfContentsService>` | None (strict) |
| `CreateEntryFormatterService()` | `Mock<IEntryFormatterService>` | `.RemoveSpaceSeparators(s)` → identity |
| `CreateJournalSettings(string? path)` | `IOptions<JournalSettings>` | Concrete `Options.Create` with sensible defaults |

### Design Notes
- All methods are `public static`
- `Mock<T>()` created with `MockBehavior.Strict` for domain services; `MockBehavior.Loose` for loggers
- No state — purely functional factory

---

## Entity 4: `JournalIntegrationTestBase` (abstract class)

**Location**: `markdown-journal-cli.Tests/Infrastructure/JournalIntegrationTestBase.cs`  
**Layer**: Integration tests (command + service)

### Responsibilities

- Create a unique `Guid`-named temp directory under `Path.GetTempPath()` in the constructor
- Instantiate all real service implementations wired against the real `FileSystem`
- Clean up the temp directory in `Dispose()` regardless of test outcome
- Provide a `RunCommand(string[] args)` helper that invokes the full CLI pipeline

### Fields

| Field | Type | Notes |
|---|---|---|
| `JournalRoot` | `string` | Root temp dir: `Path.Combine(GetTempPath(), $"journal-{Guid}")` |
| `JournalPath` | `string` | Specific journal subdirectory within `JournalRoot` |
| `FileSystem` | `IFileSystem` | Real `FileSystem` instance |
| `JournalSettings` | `IOptions<JournalSettings>` | Wired to `JournalPath` |

### Key Methods

| Method | Return | Description |
|---|---|---|
| `InitializeJournal()` | `void` | Seeds `.journalrc`, `.mdjournal`, TOC — matches production `init` output |
| `Dispose()` | `void` | `Directory.Delete(JournalRoot, recursive: true)` |

### Relationships
- Subclassed by `InitCommandIntegrationTests`, `NewCommandIntegrationTests`, `UpdateCommandIntegrationTests`, `RemoveEntryCommandIntegrationTests`
- `AddEntryIntegrationTests` and `AddTableOfContentsIntegrationTests` will be migrated to extend this base class (removing duplicate temp-dir setup code)

---

## Entity 5: `ServiceRollbackTestBase` (existing — preserve, integrate)

**Location**: `markdown-journal-cli.Tests/Services/Rollback/ServiceRollbackTestBase.cs`  
**Status**: EXISTS — do not replace; integrate into shared infrastructure reference, not inheritance

### Current Responsibilities (unchanged)
- Provides `FaultInjectingFileSystem`, real `FileTransactionCoordinator`, real services
- All rollback-specific test infrastructure

### Integration Note
- The new `ServiceTestBase` will NOT inherit from `ServiceRollbackTestBase`
- `ServiceRollbackTestBase` remains the dedicated base for all rollback tests
- A comment reference in `ServiceTestBase` will point developers to `ServiceRollbackTestBase` for rollback scenarios

---

## Entity 6: New Integration Test Classes (to be created)

| Class | File Location | Tests |
|---|---|---|
| `InitCommandIntegrationTests` | `Commands/Init/InitCommandIntegrationTests.cs` | FR-001: full pipeline, real disk |
| `NewCommandIntegrationTests` | `Commands/New/NewCommandIntegrationTests.cs` | FR-001: full pipeline, real disk |
| `UpdateCommandIntegrationTests` | `Commands/Update/UpdateCommandIntegrationTests.cs` | FR-001: full pipeline, real disk |
| `RemoveEntryCommandIntegrationTests` | `Commands/Remove/RemoveEntryCommandIntegrationTests.cs` | FR-001: full pipeline, real disk |

---

## Entity 7: Test Name Convention (not a class — a rule)

**Pattern**: `MethodName_Should_ExpectedBehavior_When_Condition`

**Examples (correct)**:
- `Execute_Should_CreateJournalFolder_When_PathIsValid`
- `Execute_Should_ReturnExitCode1_When_JournalAlreadyExists`
- `AddEntry_Should_ThrowJournalrcNotFoundException_When_JournalrcMissing`

**Anti-patterns (to fix during quality pass)**:
- `Test_AddEntry` (no scenario)
- `AddEntry_Works` (not descriptive)
- `Should_Pass` (vacuous)

---

## Entity 8: Vacuous / Always-Passing Tests (to remove)

**Criteria for removal**:
- Assertion is `Assert.True(true)` or equivalent
- Test would pass even if the method under test were deleted
- Test duplicates another verbatim with no unique scenario
- Before removal: verify alternative test covers the same code path

**Replacement rule**: Any deleted test must have its coverage confirmed as redundant; tests with unique coverage must be rewritten correctly before the original is removed.

---

## Folder Structure (post-cleanup)

```text
markdown-journal-cli.Tests/
├── Infrastructure/
│   ├── CommandAppTester.cs         (existing — unchanged)
│   ├── CommandTestBase.cs          (NEW)
│   ├── ServiceTestBase.cs          (NEW)
│   ├── MockFactory.cs              (NEW)
│   ├── JournalIntegrationTestBase.cs  (NEW)
│   ├── Configuration/              (existing — unchanged)
│   ├── DependencyInjection/        (existing — unchanged)
│   ├── FileSystem/                 (existing — unchanged)
│   ├── JournalTemplates/           (existing — unchanged)
│   ├── Tracking/                   (existing — unchanged)
│   └── Transactions/               (existing — unchanged)
├── Commands/
│   ├── Add/                        (existing — migrate to CommandTestBase)
│   ├── Init/
│   │   ├── InitCommandTests.cs              (existing — migrate)
│   │   └── InitCommandIntegrationTests.cs   (NEW)
│   ├── New/
│   │   ├── NewCommandTests.cs               (existing — migrate)
│   │   └── NewCommandIntegrationTests.cs    (NEW)
│   ├── Remove/
│   │   ├── RemoveEntryCommandTests.cs       (existing — migrate)
│   │   └── RemoveEntryCommandIntegrationTests.cs (NEW)
│   └── Update/
│       ├── UpdateCommandTests.cs            (existing — migrate)
│       ├── UpdateEntryCommandTests.cs       (existing — migrate + dedup audit)
│       └── UpdateCommandIntegrationTests.cs (NEW)
├── Services/
│   ├── EntryFormatter/             (existing — migrate to ServiceTestBase)
│   ├── InitJournal/                (existing — migrate)
│   ├── JournalEntry/               (existing — migrate)
│   ├── JournalFileUpdate/          (existing — migrate)
│   ├── JournalUpdate/              (existing — migrate)
│   ├── NewJournal/                 (existing — migrate)
│   ├── RemoveEntry/                (existing — migrate)
│   ├── Rollback/                   (existing — preserve, no changes)
│   └── TableOfContents/            (existing — migrate)
└── Exceptions/                     (existing — unchanged)
```
