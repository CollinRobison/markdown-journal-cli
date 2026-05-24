# Open-Source Docs Overhaul Plan

## Completed
- `CONTRIBUTING.md` — created at repo root (172 lines, spectre.console/cake-build pattern)

## Files to Create (remaining)

| File | Notes |
|---|---|
| `CODE_OF_CONDUCT.md` | Contributor Covenant v2.1 |
| `SECURITY.md` | Vulnerability reporting policy |
| `CHANGELOG.md` | Keep a Changelog format + maintainer instructions |
| `.github/PULL_REQUEST_TEMPLATE.md` | Checklist-style PR template |
| `.github/ISSUE_TEMPLATE/bug_report.yml` | GitHub issue form |
| `.github/ISSUE_TEMPLATE/feature_request.yml` | GitHub issue form |
| `.github/ISSUE_TEMPLATE/config.yml` | Disables blank issues |
| `.github/CODEOWNERS` | `* @CollinRobison` |
| `.github/workflows/ci.yml` | build + test on push/PR; placeholder note for NuGet secret |
| `docs/COMMANDS.md` | Full command reference moved from README lines 136-465 |
| `docs/TESTING.md` | Test infrastructure guide (base classes, MockFactory, rollback testing) |

## Files to Modify

### README.md (529 lines → ~150 lines)
- Add badge row: NuGet (placeholder note), CI (placeholder note), .NET 10, MIT
- Keep: pitch, quick start (3-4 examples), installation, brief how-it-works
- Remove: full command reference (→ docs/COMMANDS.md), development status, contributing inline
- Add: links to docs/COMMANDS.md, docs/DEVELOPMENT.md, docs/TESTING.md, docs/ARCHITECTURE.md

### docs/DEVELOPMENT.md (1033 lines → ~500 lines)
- KEEP: Getting Started (prereqs, build, run), Project Structure, Release Process, Code Standards, Debugging Tips
- UPDATE: Development Workflow templates — fix stale command/service patterns to match AGENTS.md (primary constructors, JournalCommand base class)
- REMOVE: Testing Guidelines section (lines 368-470) → moved to docs/TESTING.md
- REMOVE: Contribution Guidelines section (lines 941-1033) → already in CONTRIBUTING.md

## Key facts verified against actual code
- Program.cs DI registrations = ground truth for service registration list
- CommandTestBase, ServiceTestBase, JournalIntegrationTestBase, ServiceRollbackTestBase = ground truth for TESTING.md
- MockFactory provides: IFileSystem (with path helpers), IJournalConfiguration, IFileTracking, ITemplateManager, ITableOfContentsService, IEntryFormatterService, IJournalValidator, IJournalTocStructureRepository
- NuGet package ID will be `markdown-journal-cli` (update badge URL when published)
- CI workflow file will be `.github/workflows/ci.yml` (update badge URL when created)
- Copyright: 2026 Collin Robison
