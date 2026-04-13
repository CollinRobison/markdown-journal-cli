# Research: Test Suite Deep Dive & Cleanup

**Feature**: `002-test-suite-cleanup`  
**Phase**: 0 — Pre-design context gathering  
**Date**: 2026-04-11

---

## 1. Shared Test Infrastructure Patterns (xUnit + .NET 10)

**Decision:** Abstract base classes for shared mock-wiring behavior; constructor/`IDisposable` lifecycle for unit tests (fresh per test by default); `IAsyncLifetime` for integration tests that need async setup/teardown.

**Rationale:**  
xUnit's constructor/dispose model creates a new instance per test method — exactly what unit tests need (clean mocks each time). Abstract base classes layer on top of this to share `new Mock<T>()` construction and helper methods without any xUnit ceremony. `IAsyncLifetime` (`InitializeAsync` / `DisposeAsync`) is the correct async hook for integration tests that create temp directories and seed files; it avoids the `.GetAwaiter().GetResult()` deadlock risk of trying to do async work from synchronous `Dispose`.

**Alternatives considered:**
- `IClassFixture<T>`: correct for expensive per-class-shared resources, but overkill for mocks (cheap to construct); would share a single mock instance across all tests in a class, preventing independent per-test setup.
- `ICollectionFixture<T>`: appropriate only when a resource must span multiple test classes; adds `[Collection]` serialization overhead and is only needed if tests truly share state.
- AutoFixture.AutoMoq (`[AutoMoqData]`): auto-wires all ctor dependencies as mocks; powerful but opaque. Evaluated but rejected — explicit `MockFactory` is more readable for this project's size, and the team has no existing AutoFixture familiarity.

---

## 2. CommandTestBase Pattern (Spectre.Console)

**Decision:** All mocks created in the base constructor; `BuildApp()` is a protected helper called per test to get a fresh `CommandAppTester` (and therefore a clean `TestConsole` output buffer).

**Rationale:**  
`CommandAppTester` captures `TestConsole` output inside the instance. If shared across tests, earlier test output leaks into later assertions. Constructing mocks once in the base constructor, then producing a new `CommandAppTester` per test via `BuildApp()`, gives isolated output while still sharing the mock instances. This means mock `Setup()` calls from the constructor remain active, but the console buffer resets.

**Concrete pattern for this project:**

```csharp
public abstract class CommandTestBase
{
    protected readonly Mock<IJournalEntryService> MockJournalEntryService;
    protected readonly Mock<IFileSystem> MockFileSystem;
    protected readonly IOptions<JournalSettings> JournalSettings;

    protected CommandTestBase()
    {
        MockJournalEntryService = new Mock<IJournalEntryService>(MockBehavior.Strict);
        MockFileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
        JournalSettings = Options.Create(new JournalSettings { /* defaults */ });
    }

    protected CommandAppTester BuildApp()
    {
        var services = new ServiceCollection();
        services.AddSingleton(MockJournalEntryService.Object);
        services.AddSingleton(MockFileSystem.Object);
        services.AddSingleton(JournalSettings);

        var registrar = new TypeRegistrar(services);
        var appTester = new CommandAppTester(registrar);
        appTester.Configure(config => { /* register commands */ });
        return appTester;
    }
}
```

**Alternatives considered:**
- Putting `CommandAppTester` in constructor: rejected — shared output buffer pollutes assertions.
- `TestConsole` as a field with `app.Configure(c => c.Settings.Console = _testConsole)`: works for output, but doesn't give the same isolation as a fresh tester.

---

## 3. MockFactory Patterns (Moq)

**Decision:** A static `MockFactory` class returning `Mock<T>` (not `.Object`), pre-configured with "happy path" defaults, supplemented by Moq's built-in `MockRepository` for batch `VerifyAll()` in base classes.

**Rationale:**  
Returning `Mock<T>` (not `.Object`) lets tests add overrides with `.Setup()` and verify calls with `.Verify()` / `.VerifyAll()` after construction. Pre-configuring happy-path defaults minimizes per-test noise — each test only needs to override the specific behavior it's testing. `MockRepository` (Moq built-in, not to be confused with a custom factory) provides a `VerifyAll()` that covers every `Mock<T>` created through it simultaneously; useful in `MockBehavior.Strict` base classes.

**Pattern:**

```csharp
public static class MockFactory
{
    public static Mock<IFileSystem> CreateFileSystem()
    {
        var mock = new Mock<IFileSystem>(MockBehavior.Strict);
        // Pre-configure typical happy-path defaults here
        return mock;
    }

    public static Mock<IJournalConfiguration> CreateJournalConfiguration()
    {
        var mock = new Mock<IJournalConfiguration>(MockBehavior.Strict);
        return mock;
    }
}
```

**`MockBehavior.Strict` policy:**  
Domain / service mocks → `Strict` (unexpected calls are bugs). Infrastructure mocks (`ILogger`, console) → `Loose` (calling them is not an assertion violation).

**Alternatives considered:**
- Returning `.Object`: rejected — prevents `.Setup()`/`.Verify()` in test bodies.
- AutoFixture.AutoMoq: evaluated in Topic 1 above; rejected for this project size.
- Moq's `MockRepository` alone (without custom factory): useful companion but doesn't handle pre-configured defaults; keep both.

---

## 4. Integration Test Cleanup: `IDisposable` vs `IAsyncLifetime`

**Decision:** Use `IDisposable` for integration test cleanup (synchronous `Directory.Delete(recursive: true)`) since file I/O is synchronous and there is no async cleanup path required. Use `Path.Combine(Path.GetTempPath(), $"journal-{Guid.NewGuid():N}")` for unique temp directory names.

**Rationale:**  
The existing `AddEntryIntegrationTests` pattern already uses `IDisposable` successfully. Upgrading to `IAsyncLifetime` would add complexity without benefit — .NET's `Directory.Delete` is synchronous and reliable. The `Guid.NewGuid():N` format (32 hex chars, no dashes) produces collision-free names that are also easy to identify in `ls /tmp`. The pattern `Directory.CreateTempSubdirectory()` (.NET 7+) would be marginally more atomic but is stylistically inconsistent with the existing pattern; manual `Path.Combine + CreateDirectory` is retained for consistency.

**Base class pattern for this project:**

```csharp
public abstract class JournalIntegrationTestBase : IDisposable
{
    protected readonly string JournalRoot;
    protected readonly string JournalPath;

    protected JournalIntegrationTestBase(string journalName = "TestJournal")
    {
        JournalRoot = Path.Combine(Path.GetTempPath(), $"journal-{Guid.NewGuid():N}");
        JournalPath = Path.Combine(JournalRoot, journalName);
        Directory.CreateDirectory(JournalPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(JournalRoot))
            Directory.Delete(JournalRoot, recursive: true);
    }
}
```

**Alternatives considered:**
- `IAsyncLifetime`: appropriate for async file I/O or DB teardown; unnecessary here.
- `[CollectionDefinition]` + `ICollectionFixture<T>` for shared journal state: rejected — each command integration test should get an independent temp directory to prevent cross-test interference.

---

## 5. Integration Test DI Wiring (No TestServer)

**Decision:** Wire real services directly via `IServiceCollection` in each integration test's constructor, matching the registration pattern from `Program.cs`. No `WebApplicationFactory`, no full `IHost`.

**Rationale:**  
This is a CLI app, not an ASP.NET app. `ServiceCollection` is all that's needed. The existing `AddEntryIntegrationTests` pattern already does this inline. The cleanup will keep this approach but extract common service registration into a protected `RegisterServices(IServiceCollection, JournalSettings)` helper on the integration base class, so each integration test class calls it once rather than repeating 7–10 registration lines.

**Key practices:**
- `Options.Create(new JournalSettings { ... })` for `IOptions<T>` wiring in tests.
- `NullLogger<T>.Instance` for logger dependencies in services that log but where logging is not under test.
- Services always registered as singletons in CLI context (matches production lifetime).

**Alternatives considered:**
- `Host.CreateApplicationBuilder()` + `host.StartAsync()`: overkill; services don't need `IHostedService`.
- Extracting registrations from `Program.cs` into a shared `AddJournalServices(IServiceCollection)` extension: ideal long-term, but that would require modifying production code. The test suite cleanup is scoped to the test project only. Instead, registrations are consolidated within the integration test base class helper.

---

## 6. Current State Audit Summary

Based on codebase exploration:

| Area | Finding |
|---|---|
| Moq usage: commands | `AddEntryCommandTests` — Moq used correctly; ~7 mocks per constructor; repeated verbatim across similar tests |
| Moq usage: services | Update/Init service tests mix `TestFileSystem` (real test double) with `Mock<>` for some services |
| NoOp usage | `NoOpFileTransactionCoordinator.Instance` + `NoOpRollbackReporter.Instance` used in all unit tests — intentional, must be preserved |
| Integration tests | Only `Add` command has integration tests (2 files); `Init`, `New`, `Update`, `Remove` have none |
| Rollback tests | `Services/Rollback/` has solid base class + 6 service-specific files; `Commands/Add/` has 3 rollback files |
| Vacuous assertions | Need audit — not yet quantified |
| Duplicate coverage | Need audit — esp. `UpdateCommandTests.cs` duplicating `UpdateEntryCommandTests.cs` scenarios |
| Folder structure | Mostly mirrors source; `Services/` subdirectories need verification |

---

## 7. Technology Choices (No New Dependencies Required)

All required tools are already present in `markdown-journal-cli.Tests.csproj`:

| Tool | Version | Role |
|---|---|---|
| xUnit | 2.9.3 | Test framework |
| Moq | 4.20.72 | Mocking (`Mock<T>`, `MockRepository`) |
| Shouldly | 4.3.0 | Fluent assertions |
| Spectre.Console.Testing | 0.55.0 | `TestConsole` + existing `CommandAppTester` |
| Microsoft.Extensions.DependencyInjection | 10.0.5 | `ServiceCollection` for integration DI |

**No new NuGet packages are required for this feature.**
