# CI/CD Implementation Plan

> Branch: `ci-cd-implement`  
> Target: release-please + semantic versioning, version in `.csproj`, pre-v1

---

## Confirmed Decisions

| Decision | Choice |
|---|---|
| Version source | `<Version>` in `.csproj` — release-please patches via generic updater + marker comment |
| Starting version | `0.1.0` (pre-v1) |
| Coverage reporting | GitHub Actions job summary (no Codecov) |
| NuGet publish | Skeleton wired; secret instructions left as markdown for manual setup |
| `dotnet format` gate | Deferred — fix locally first, then enable |
| CodeQL | Required blocking status check |

---

## Current State

- ✅ `ci.yml` exists (basic: build + test only, no cache, no coverage)
- ✅ `global.json` (SDK 10.0.201)
- ✅ `PackAsTool`, `PackageId` in `.csproj`
- ✅ `--version` flag
- ❌ `ToolCommandName` missing from `.csproj` (should be `mdjournal`)
- ❌ No `release-please-config.json` / `.release-please-manifest.json`
- ❌ `dependabot.yml` only covers `devcontainers` (needs `github-actions` + `nuget`)
- ❌ No release, security, or lint workflows

---

## release-please Version Strategy

Use `release-type: simple` with `extra-files` generic updater.  
Add a marker comment to `.csproj` so release-please can find and patch `<Version>`:

```xml
<Version>0.1.0</Version> <!-- x-release-please-version -->
```

The generic updater looks for lines with that marker and replaces the version string.  
The `.release-please-manifest.json` tracks the current version (`"0.1.0"` to start).

---

## Phase 0 — Pre-flight (`.csproj` + Dependabot)

**Files to change:**
- `markdown-journal-cli/markdown-journal-cli.csproj`
  - Add `<ToolCommandName>mdjournal</ToolCommandName>` inside the first `<PropertyGroup>`
  - Add `<!-- x-release-please-version -->` marker comment to the `<Version>` line
- `.github/dependabot.yml`
  - Add `github-actions` ecosystem (directory: `/`, weekly)
  - Add `nuget` ecosystem (directory: `/markdown-journal-cli`, weekly)

---

## Phase 1 — Enhance CI Workflow

**File:** `.github/workflows/ci.yml`

Additions to the existing workflow:
1. **NuGet cache** — `actions/cache@v4` keyed on `**/*.csproj` hash
2. **Coverage collection** — `dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage`
3. **Coverage report** — `danielpalme/ReportGenerator-GitHub-Action@v5` → GitHub Actions job summary (no external service)
4. *(Format check deferred — add `dotnet format --verify-no-changes` once codebase is formatted)*

---

## Phase 2 — PR Title Lint

**File:** `.github/workflows/pr-title-lint.yml`

- Trigger: `pull_request` types `[opened, edited, synchronize, reopened]`
- Action: `amannn/action-semantic-pull-request@v5`
- Uses `GITHUB_TOKEN` only (no secrets needed)
- Must be added as a required status check (see Phase 6)

---

## Phase 3 — release-please

**Files to create:**

### `release-please-config.json`
```json
{
  "$schema": "https://raw.githubusercontent.com/googleapis/release-please/main/schemas/config.json",
  "release-type": "simple",
  "packages": {
    ".": {
      "release-type": "simple",
      "extra-files": [
        {
          "type": "generic",
          "path": "markdown-journal-cli/markdown-journal-cli.csproj"
        }
      ]
    }
  },
  "changelog-sections": [
    {"type": "feat", "section": "Features"},
    {"type": "fix", "section": "Bug Fixes"},
    {"type": "perf", "section": "Performance Improvements"},
    {"type": "refactor", "section": "Code Refactoring"},
    {"type": "docs", "section": "Documentation", "hidden": false},
    {"type": "chore", "section": "Miscellaneous", "hidden": true},
    {"type": "ci", "section": "CI/CD", "hidden": true}
  ]
}
```

### `.release-please-manifest.json`
```json
{
  ".": "0.1.0"
}
```

### `.github/workflows/release-please.yml`
- Trigger: `push` to `main`
- Action: `googleapis/release-please-action@v4`
- Outputs: `release_created`, `tag_name` (used to chain into `release.yml`)
- Needs **write permissions** for PRs and contents (see Phase 6 — workflow permissions setting)

---

## Phase 4 — Release Workflow

**File:** `.github/workflows/release.yml`

- Trigger: `release: [published]`
- Runner: `ubuntu-latest` for all steps (dotnet cross-compiles to all platforms)

### Steps
1. Checkout (full history: `fetch-depth: 0`)
2. Setup .NET (from `global.json`)
3. NuGet cache
4. Restore
5. Test (final gate before publish)
6. Build binaries — **matrix** across 6 RIDs:
   - `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`
   - Flags: `--self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true`
   - Version sourced from release tag: `-p:Version=${{ github.ref_name }}` (strips `v` prefix in workflow)
7. Zip each binary: `mdjournal-<version>-<rid>.zip`
8. Pack NuGet: `dotnet pack -c Release -p:Version=<tag>`
9. **NuGet publish skeleton** — step commented out with instructions
10. Upload all zips + `.nupkg` to the GitHub Release via `softprops/action-gh-release@v2`

### Artifact naming
```
mdjournal-0.2.0-win-x64.zip
mdjournal-0.2.0-linux-x64.zip
mdjournal-0.2.0-osx-arm64.zip
... (6 total)
CollinRobison.mdjournal.0.2.0.nupkg
```

---

## Phase 5 — Security Workflows

### `codeql.yml`
- Triggers: push to `main`, PR to `main`, cron (`0 3 * * 1` — weekly Monday)
- Language: `csharp`
- Actions: `github/codeql-action/init@v3` → `autobuild` → `analyze`
- Must be added as **required blocking** status check (see Phase 6)

### `dependency-review.yml`
- Trigger: PR to `main` only
- Action: `actions/dependency-review-action@v4`
- Config: `fail-on-severity: moderate`

### `stale.yml`
- Trigger: cron `0 0 * * *` (daily)
- Action: `actions/stale@v9`
- Stale after 60 days → close after 7 more days

---

## Phase 6 — Manual GitHub Settings (post-implementation)

These cannot be done via code — must be configured in the GitHub UI.

### Workflow Permissions
`Settings → Actions → General → Workflow permissions`  
→ Set to **"Read and write permissions"**  
→ This allows `release-please` to open PRs and create releases.

### Merge Strategy
`Settings → General → Pull Requests`
- ❌ Uncheck "Allow merge commits"
- ❌ Uncheck "Allow rebase merging"
- ✅ Check "Allow squash merging"
- Set default commit message: **"PR title and commit details"**

### Branch Protection for `main`
`Settings → Branches → Add ruleset` (or edit existing):

| Rule | Setting |
|---|---|
| Require PR before merging | ✅ |
| Required approvals | 0 |
| Dismiss stale reviews on new commits | ✅ |
| Require status checks to pass | ✅ |
| Require branches to be up to date | ✅ |
| Block force pushes | ✅ |
| Restrict deletions | ✅ |
| Require linear history (squash only) | ✅ |

**Required status checks to add** (exact job names):
- `build-and-test` ← from `ci.yml`
- `Semantic Pull Request` ← from `pr-title-lint.yml`
- `CodeQL` ← from `codeql.yml`
- `dependency-review` ← from `dependency-review.yml`

### NuGet API Key (deferred)
`Settings → Secrets and variables → Actions → New repository secret`
- Name: `NUGET_API_KEY`
- Value: key from nuget.org → Account → API Keys → Create (scope to `CollinRobison.mdjournal`)

---

## Implementation Order

1. Phase 0 — `.csproj` fixes + `dependabot.yml`
2. Phase 1 — Update `ci.yml`
3. Phase 2 — `pr-title-lint.yml`
4. Phase 3 — `release-please-config.json`, manifest, `release-please.yml`
5. Phase 4 — `release.yml`
6. Phase 5 — `codeql.yml`, `dependency-review.yml`, `stale.yml`
7. Phase 6 — Manual GitHub settings (do last, after workflows pass)

---

## Open Items / Deferred

- `dotnet format --verify-no-changes` gate: add to `ci.yml` after running `dotnet format` locally
- Trimming validation: test all commands — Spectre.Console has known trim issues; fall back to `PublishSingleFile=true` only if broken
- Package manager distribution (Homebrew, Winget, Scoop): post-v1.0 concern
- Code signing (SmartScreen / Gatekeeper): document workarounds in README for now

---

## Phase 7 — Documentation Updates

> Principle: only update things that are **wrong today** or will **definitely become wrong** once CI/CD lands. No command-output examples, no version numbers, no feature lists.

---

### README.md — Bug fixes (PackageId is wrong)

The NuGet badge and `dotnet tool install` commands reference package ID `markdown-journal-cli`, but the `.csproj` `PackageId` is `CollinRobison.mdjournal`. This will silently break the badge and confuse anyone trying to install.

**Changes:**
- Line 6: Fix NuGet badge URL: `nuget/v/markdown-journal-cli` → `nuget/v/CollinRobison.mdjournal` and badge link
- Line 9: Remove the badge note comment (it's wrong; delete the whole `> Badge notes:` block)
- Lines 117/124/130: `dotnet tool install/update/uninstall -g markdown-journal-cli` → `CollinRobison.mdjournal`

**Command name changes (pending ToolCommandName being set to `mdjournal`):**
- All Quick Start examples: `mdjournal` → `mdjournal`
- All binary asset filenames in Installation section: `mdjournal-osx-arm64.tar.gz` → `mdjournal-<version>-osx-arm64.zip` (also `.tar.gz` → `.zip` per the release plan)
- All `chmod +x mdjournal`, `sudo mv mdjournal`, `xattr ... mdjournal`, `rm mdjournal` → `mdjournal`
- All `mdjournal.exe` → `mdjournal.exe` in Windows section
- `mdjournal --version` → `mdjournal --version` everywhere

**Note:** Do these in Phase 0 at the same time `ToolCommandName` is added to `.csproj` — they're the same logical change.

---

### CONTRIBUTING.md — Add conventional commits section

This is stable architecture, not implementation detail. Contributors need to know this before their first PR once PR title lint is enforced.

**Add a new section before "Pull Request Checklist":**

```markdown
## Commit Message Convention

This project uses [Conventional Commits](https://www.conventionalcommits.org/).
PR titles must follow the format because squash merges use the PR title as
the commit message on `main`, and release-please reads those commits to
determine the next version bump.

Format: `<type>: <description>` (e.g., `feat: add export command`)

| Type | Version bump | When to use |
|---|---|---|
| `feat` | minor | New feature or command |
| `fix` | patch | Bug fix |
| `docs` | none | Documentation only |
| `test` | none | Adding or fixing tests |
| `refactor` | none | Code change, not a fix or feature |
| `chore` | none | Dependencies, build tooling |
| `ci` | none | CI/CD changes |
| `perf` | patch | Performance improvement |

For breaking changes, append `!`: `feat!: rename --path flag to --directory`

PR title format is enforced by CI — the PR will fail the lint check if
the title doesn't match the convention.
```

**Update PR checklist — add one item:**
- `[ ] PR title follows conventional commits format (`feat:`, `fix:`, etc.)`

---

### DEVELOPMENT.md — Replace Release Process section

The current "Release Process" section describes a fully manual process. Once CI/CD lands it becomes actively misleading. Replace the entire section with a description of the automated flow — conceptual, not implementation-specific (won't go stale).

**Replace the `## Release Process` section with:**

```markdown
## Release Process

Releases are fully automated via [release-please](https://github.com/googleapis/release-please).

**How it works:**

1. Merge PRs to `main` using conventional commit titles (`feat:`, `fix:`, etc.).
2. `release-please` reads those commits and opens a "Release PR" that bumps
   `<Version>` in `.csproj` and updates `CHANGELOG.md`.
3. When you merge the Release PR, a GitHub Release is created automatically.
4. The release triggers the build pipeline: binaries for 6 platforms are built
   and attached to the Release, and the NuGet package is published.

**Version bump rules:**
- `fix:` → patch bump (0.1.0 → 0.1.1)
- `feat:` → minor bump (0.1.0 → 0.2.0)
- `feat!:` or `BREAKING CHANGE:` footer → major bump (0.1.0 → 1.0.0)

**To test packaging locally** (without triggering a release):

```bash
dotnet pack markdown-journal-cli --configuration Release
dotnet tool install -g --add-source ./markdown-journal-cli/nupkg CollinRobison.mdjournal
mdjournal --version
```

To reinstall a rebuilt version:

```bash
dotnet tool uninstall -g CollinRobison.mdjournal
dotnet tool install -g --add-source ./markdown-journal-cli/nupkg CollinRobison.mdjournal
```
```

**Remove from that section:**
- The manual `dotnet publish` matrix for self-contained binaries (the release workflow does this)
- "Publish release notes in `CHANGELOG.md`" (automated)
- "Run full test suite before pushing a release tag" (CI does this)

---

### Nothing to change in

- `docs/TESTING.md` — thorough, well-structured, stable
- `docs/ARCHITECTURE.md` — describes the model, not implementation details; stable
- `CHANGELOG.md` — will be maintained by release-please going forward; no changes needed now
- `SECURITY.md` / `CODE_OF_CONDUCT.md` — stable by nature


---

## ✅ Implementation Complete — Manual Steps Required

Everything automatable has been committed to `ci-cd-implement`. Below are the steps you need to perform manually in the GitHub UI before merging.

### 1. Workflow Permissions (required for release-please to open PRs)

`Settings → Actions → General → Workflow permissions`
→ Select **"Read and write permissions"**
→ Check **"Allow GitHub Actions to create and approve pull requests"**

### 2. Merge Strategy

`Settings → General → Pull Requests`
- ❌ Uncheck "Allow merge commits"
- ❌ Uncheck "Allow rebase merging"
- ✅ Check "Allow squash merging"
- Set default commit message: **"PR title and commit details"**

### 3. Branch Protection Ruleset for `main`

`Settings → Branches → Add ruleset` (name it e.g. "main protection"):

| Rule | Setting |
|---|---|
| Target branches | `main` |
| Require PR before merging | ✅ |
| Required approvals | 0 |
| Dismiss stale reviews on new commits | ✅ |
| Require status checks to pass | ✅ |
| Require branches to be up to date before merging | ✅ |
| Block force pushes | ✅ |
| Restrict deletions | ✅ |
| Require linear history | ✅ |

**Required status checks** (add these exact job names after the first CI run so GitHub can discover them):
- `build-and-test` ← ci.yml
- `Semantic Pull Request` ← pr-title-lint.yml
- `CodeQL` ← codeql.yml
- `dependency-review` ← dependency-review.yml

> **Tip:** Run this PR through CI first so GitHub registers the job names, then come back and add them as required checks.

### 4. NuGet API Key (defer until ready to publish)

`Settings → Secrets and variables → Actions → New repository secret`
- Name: `NUGET_API_KEY`
- Value: create at [nuget.org → Account → API Keys](https://www.nuget.org/account/apikeys), scoped to `CollinRobison.mdjournal`

Then uncomment the publish step in `.github/workflows/release.yml` (the `# - name: Publish to NuGet.org` block).

### 5. Run `dotnet format` locally before enabling format gate

The format check is intentionally deferred. Once you've run `dotnet format` and committed the result, uncomment the format gate in `ci.yml`:

```yaml
- name: Format check
  run: dotnet format --verify-no-changes
```

### 6. Push and open a PR

```bash
git push origin ci-cd-implement
```

Open a PR with a conventional commit title, e.g.: `ci: implement full CI/CD pipeline with release-please`

The PR title lint check will validate itself on this first PR.
