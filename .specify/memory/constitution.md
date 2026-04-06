<!--
  SYNC IMPACT REPORT
  ==================
  Version change: (unversioned placeholder template) → 1.0.0
  Added principles (all new — first constitution population):
    - I.   Thin Command Layer
    - II.  Service-Oriented Architecture with Interface Segregation
    - III. File System Abstraction (Repository Pattern)
    - IV.  Transactional Integrity
    - V.   Test Coverage (NON-NEGOTIABLE)
    - VI.  Rich Terminal UI (Spectre.Console)
  Added sections:
    - Technology Stack Constraints
    - Security Requirements
    - Development Workflow
    - Quality Gates / Definition of Done
    - Governance
  Templates reviewed:
    - .specify/templates/plan-template.md  ✅ no update required (Constitution Check section present)
    - .specify/templates/spec-template.md  ✅ no update required
    - .specify/templates/tasks-template.md ✅ no update required
  Deferred TODOs: none
-->

# mdjournal Constitution

## Core Principles

### I. Thin Command Layer

Commands MUST delegate ALL business logic to services. A command:

- MUST contain only input validation, service delegation, and UI output
- MUST NOT exceed 100 lines of code (excluding comments)
- MUST NOT contain static helper methods or embedded domain logic
- MUST inject `IAnsiConsole` for all console output; `AnsiConsole` static members MUST NOT appear in commands

**Rationale**: Commands are the CLI entry point. Fat commands couple UI to domain logic, making both harder to
test and evolve independently. Thin commands unlock independent service testing and clear separation of concerns.

### II. Service-Oriented Architecture with Interface Segregation

All business logic MUST live in services that follow these rules:

- Every service MUST have a corresponding interface (e.g., `IFooService` / `FooService`)
- Services MUST be registered in `Program.cs`
- Services MUST receive all dependencies via constructor injection
- Constructor parameters MUST be validated for null and throw `ArgumentNullException`
- Each service MUST have a single, cohesive responsibility; create a new service rather than expanding an existing one

**Rationale**: Interfaces enable mocking in tests; singletons match the scoped lifetime of a CLI run; null guards
surface misconfigured DI containers immediately at startup rather than deep in a code path.

### III. File System Abstraction (Repository Pattern)

Services and commands MUST NEVER call `System.IO` types directly. All file operations MUST go through `IFileSystem`.

- `FileSystem` (production implementation) wraps `System.IO`
- Test implementations (e.g., `TestFileSystem`, `FaultInjectingFileSystem`) substitute the real one
- Any new file operation added to production code MUST be surfaced on the `IFileSystem` interface first

**Rationale**: Direct `System.IO` calls make unit tests slow, fragile, and environment-dependent. The abstraction
lets the entire business layer run without touching disk, enabling fast, reliable, parallel test execution.

### IV. Transactional Integrity

Any operation that writes to more than one file MUST participate in a `FileTransactionScope`.

- All writes MUST be tracked via `IFileTransactionCoordinator.Begin()` or `BeginOrJoin()`
- On failure, ALL completed writes MUST be compensated (rolled back) before the error surfaces to the caller
- Commands MUST extend `JournalCommand<TSettings>` to map `RollbackCompletedException` to standard exit codes:
  - **Exit code 2** — operation failed; all writes fully rolled back (safe to retry)
  - **Exit code 3** — operation failed; rollback encountered errors (manual inspection required)

**Rationale**: Partial writes corrupt journal state. Atomic, compensating transactions ensure users can
always recover to a consistent state without manual cleanup.

### V. Test Coverage (NON-NEGOTIABLE)

Every service, command, and infrastructure component MUST have a corresponding unit test class.

- **Test stack**: xUnit + Moq + Shouldly — no framework substitutions permitted
- **Mirror structure**: test files MUST mirror source layout under `markdown-journal-cli.Tests/`
- **Naming**: `MethodName_Should_ExpectedBehavior_When_Condition`
- Each test class MUST cover: happy path, edge cases (null, empty, boundary values), and exception scenarios
- Service delegation MUST be verified with `Mock.Verify()` where the observable behavior is a service call

**Rationale**: The file system abstraction and DI design make everything testable. Untested code signals
incomplete design and creates disproportionate maintenance risk in a file-mutation tool.

### VI. Rich Terminal UI (Spectre.Console)

All terminal output and user interaction MUST use Spectre.Console idioms.

- `IAnsiConsole` MUST be injected via DI; `AnsiConsole` static members MUST NOT appear in production code
- ALL user-supplied strings MUST be passed through `.EscapeMarkup()` before markup interpolation
- Use `AnsiConsole.Status()` for short operations; `AnsiConsole.Progress()` for long, multi-step operations
- Use `SelectionPrompt<T>` / `MultiSelectionPrompt<T>` for interactive choices; `Console.ReadLine()` MUST NOT be used

**Rationale**: Injecting `IAnsiConsole` keeps commands testable via `TestConsole`; escaping prevents markup
injection from user-supplied content breaking terminal rendering or producing confusing output.

## Technology Stack Constraints

| Layer | Constraint |
|---|---|
| Runtime | .NET 10 — downgrade requires an explicit ADR |
| CLI framework | Spectre.Console.Cli — all commands extend `JournalCommand<TSettings>` |
| Testing | xUnit + Moq + Shouldly — no framework substitutions |
| Serialization | System.Text.Json for `.journalrc` and `.mdjournal` |
| File hashing | SHA256 via `HashService` — alternative algorithms require an ADR |

New runtime dependencies MUST be evaluated for security advisories, maintenance status, and license
compatibility before adoption.

## Security Requirements

- **Path traversal**: All user-supplied paths MUST be resolved and validated to reside within the journal
  directory before any file operation is performed
- **Markup injection**: ALL user-supplied strings rendered in Spectre.Console markup MUST call `.EscapeMarkup()`
  — no exceptions
- **Dependency vetting**: Third-party packages MUST be reviewed for known CVEs before adoption
- **No secrets in source**: Credentials, tokens, and secrets MUST NOT appear in source files or config files
  committed to version control

## Development Workflow

### Adding a Command
1. Create command class in `Commands/{Group}/` extending `JournalCommand<TSettings>`
2. Create settings class extending `CommandSettings` in the same folder
3. Register command in `Program.cs` via `config.AddCommand<T>()` or `config.AddBranch<T>()`
4. Add singleton registration for the command class in `Program.cs`
5. Create mirror test class in `markdown-journal-cli.Tests/Commands/{Group}/`

### Adding a Service
1. Define the interface in the appropriate `Services/` or `Infrastructure/` subfolder
2. Implement the class (constructor MUST null-check all parameters)
3. Register in `Program.cs`
4. Create mirror test class with mocked dependencies

### Adding a Template
1. Implement `ITemplateGenerator` in `Infrastructure/JournalTemplates/Templates/`
2. Register in `TemplateManager.RegisterDefaultTemplates()`

## Quality Gates / Definition of Done

A change is **done** only when ALL of the following are true:

- [ ] All new/modified services and commands have corresponding unit tests
- [ ] No `System.IO` calls outside of the `FileSystem` implementation class
- [ ] Multi-file write operations are wrapped in a `FileTransactionScope`
- [ ] User-supplied strings are `.EscapeMarkup()`-escaped before console output
- [ ] `IAnsiConsole` is injected; no `AnsiConsole` static calls in production code
- [ ] Commands are ≤ 100 lines; no static helpers embedded in command classes
- [ ] All new services have interfaces and are registered in `Program.cs`
- [ ] Build passes: `dotnet build` with no errors or warnings
- [ ] All tests pass: `dotnet test`
- [ ] PR reviewed and approved by the repository author

## Governance

This constitution supersedes all other development guidance. When documents conflict, the constitution wins.

**Amendment process:**
1. Any contributor may propose an amendment via pull request
2. The PR description MUST state the change, version bump rationale (MAJOR/MINOR/PATCH), and any affected principles
3. The repository author reviews and approves before merge
4. MAJOR changes (principle removal or fundamental redefinition) MUST include a migration note describing
   how existing non-compliant code is brought into compliance

**Versioning policy** (semantic):
- **MAJOR** — backward-incompatible governance changes: principle removed or fundamentally redefined
- **MINOR** — new principle or section added, or materially expanded guidance
- **PATCH** — wording clarifications, typo fixes, non-semantic refinements

All PRs MUST include a constitution compliance review. Non-compliant code MUST NOT be merged.

**Version**: 1.0.0 | **Ratified**: 2025-08-05 | **Last Amended**: 2026-04-05
