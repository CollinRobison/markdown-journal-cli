# CI/CD Pipeline Plan

> Planning document for GitHub Actions workflows, versioning strategy, distribution, and repository governance.
> Decisions captured from planning session — May 2026.

---

## Table of Contents

1. [Cost & Free Tier Overview](#cost--free-tier-overview)
2. [Versioning Strategy](#versioning-strategy)
3. [Conventional Commits Enforcement](#conventional-commits-enforcement)
4. [Workflows Overview](#workflows-overview)
5. [Workflow 1 — CI (Build & Test)](#workflow-1--ci-build--test)
6. [Workflow 2 — Release (Auto on Merge to Main)](#workflow-2--release-auto-on-merge-to-main)
7. [Workflow 3 — CodeQL Security Analysis](#workflow-3--codeql-security-analysis)
8. [Workflow 4 — Dependency Review](#workflow-4--dependency-review)
9. [Workflow 5 — Stale Issue Management](#workflow-5--stale-issue-management)
10. [Distribution Strategy](#distribution-strategy)
11. [Quality Gates & Branch Protection](#quality-gates--branch-protection)
12. [Required Secrets & Settings](#required-secrets--settings)
13. [Implementation Order](#implementation-order)
14. [Open Questions / Future Work](#open-questions--future-work)

---

## Cost & Free Tier Overview

**Key finding: This project will run entirely within the free tier as long as the repository is public.**

GitHub Actions is **completely free for public repositories** on standard runners. No billing, no quotas.

| Resource         | Public Repo    | Private Repo (Free plan) |
|-----------------|---------------|--------------------------|
| Minutes/month   | Unlimited      | 2,000                    |
| Artifact storage | Unlimited     | 500 MB                   |
| Cache storage   | 10 GB          | 10 GB                    |

### Cost-saving rules to follow regardless

- Always use `ubuntu-latest` for Linux jobs — cheapest runner ($0.006/min) if you ever go private.
- Use matrix builds only where actually needed (we need them for cross-platform binaries in release).
- Set `actions/cache` for NuGet packages and dotnet SDK — reduces restore time significantly.
- Set short retention on artifacts (`retention-days: 7` for CI, `retention-days: 90` for releases).
- macOS runners are 10x more expensive than Linux ($0.062/min) — **never run tests on macOS**; only use macOS runners to cross-compile the osx-x64/osx-arm64 binaries in the release pipeline. Actually, `dotnet publish --runtime osx-x64` runs fine on a Linux runner — we can skip macOS runners entirely and publish all targets from Linux.

---

## Versioning Strategy

**Chosen: Merge to `main` auto-releases using `dotnet-bump` / commit-message-driven semantic versioning.**

### How it works

1. Every commit to `main` (via PR merge) uses a **conventional commit** message (e.g. `feat: add toc command`).
2. The release workflow reads all commits since the last release tag, determines the bump type:
   - `fix:` → patch bump (1.0.0 → 1.0.1)
   - `feat:` → minor bump (1.0.0 → 1.1.0)
   - `feat!:` or `BREAKING CHANGE:` footer → major bump (1.0.0 → 2.0.0)
3. The workflow updates `<Version>` in the `.csproj`, commits it to `main`, creates a tag (e.g. `v1.1.0`), generates a changelog, and publishes the release.

### Tool recommendation: `semantic-release` + `semantic-release-action`

Use the GitHub Action `cycjimmy/semantic-release-action` which wraps the `semantic-release` npm tool. It:
- Requires no npm knowledge — it runs as a container action.
- Reads commits since last tag.
- Creates the GitHub Release with auto-generated notes.
- Can execute custom steps (we hook in the NuGet publish and binary build steps).

**Alternative (simpler but less powerful):** `googleapis/release-please-action` — Google's maintained tool, very similar behavior, slightly simpler config. Either works fine. `release-please` is arguably easier to set up for a solo maintainer.

### Recommended: `release-please`

`release-please` will:
1. Open a "Release PR" after every merge to `main` that has releasable commits.
2. The release PR shows the pending version bump and changelog.
3. When **you merge the release PR**, it creates the tag and GitHub Release automatically.
4. The release publication triggers the actual build/publish workflow via the `release.published` event.

This is slightly better than fully automatic releases because **you control exactly when a release goes out** — you just merge the auto-generated PR.

```
feat commit → main
       ↓
release-please opens PR: "chore: release 1.1.0"
       ↓
You review & merge
       ↓
release.published event fires
       ↓
Release workflow: build → test → publish NuGet → upload binaries
```

### Version source of truth

The `.csproj` `<Version>` property is the source of truth. `release-please` updates it automatically via PR. The `AssemblyInformationalVersionAttribute` picks it up — which feeds the `--version` flag.

---

## Conventional Commits Enforcement

**You asked if commits can be blocked from pushing if they don't follow the convention.**

### What's possible

GitHub does not natively block commits by message format at the repo level. Options:

| Option | How it works | Enforces on |
|---|---|---|
| **commitlint + PR title lint** | GitHub Action runs on PR open/edit, checks PR *title* follows convention | All contributors via CI |
| **Local git hook (Husky/.NET equivalent)** | A `.git/hooks/commit-msg` script rejects local commits | Only your local machine |
| **`commitlint` as required CI check** | Fails the PR status check if the commit message is wrong | Blocks PR merge |

### Recommended approach for an open source solo/small project

**Lint the PR title, not individual commits.** Why:
- When using squash-merge (recommended), the PR title becomes the single commit message on `main`.
- So enforcing the PR title format is equivalent to enforcing all commit messages on `main`.
- Individual commit messages during development don't matter as much — only what lands on `main` counts.

**Implementation:**
- Add a workflow `pr-title-lint.yml` that triggers on `pull_request` events `(types: [opened, edited, synchronize])`.
- Use the action `amannn/action-semantic-pull-request` — widely used, zero config.
- Add this as a **required status check** in branch protection rules.
- Set the repo's merge strategy to **squash only**.

**For local commit linting (optional but nice):**
- Document in `CONTRIBUTING.md` that contributors should use conventional commits.
- Provide a `.commitlintrc.json` config file in the repo that contributors can hook up themselves.
- This is the approach used by Spectre.Console, Angular, and most major OSS projects.

---

## Workflows Overview

| File | Trigger | Purpose |
|---|---|---|
| `ci.yml` | Push to `main`, PR to `main` | Build + test + format check + coverage |
| `release.yml` | `release.published` event | Build binaries (6 targets), pack NuGet, publish, upload |
| `release-please.yml` | Push to `main` | Open/update the release PR, create tag & GitHub Release when merged |
| `codeql.yml` | Push to `main`, PR to `main`, cron weekly | CodeQL static analysis |
| `dependency-review.yml` | PR to `main` | Flag vulnerable NuGet packages in PRs |
| `pr-title-lint.yml` | PR opened/edited/synced | Enforce conventional commit format on PR title |
| `stale.yml` | Cron daily | Auto-close stale issues/PRs |

---

## Workflow 1 — CI (Build & Test)

**File:** `.github/workflows/ci.yml`
**Triggers:** `push` to `main`, `pull_request` targeting `main`
**Runner:** `ubuntu-latest` only — no cross-platform test matrix needed (the code is .NET, portable)

### Steps

1. **Checkout** — `actions/checkout@v4`
2. **Setup .NET** — `actions/setup-dotnet@v4` with the version from `global.json`
3. **Cache NuGet packages** — `actions/cache@v4` keyed on `**/*.csproj` lock files — saves ~30s per run
4. **Restore** — `dotnet restore`
5. **Format check** — `dotnet format --verify-no-changes` — fails if any file needs formatting
6. **Build** — `dotnet build --no-restore -c Release`
7. **Test with coverage** — `dotnet test --no-build -c Release --collect:"XPlat Code Coverage" --results-directory ./coverage`
8. **Coverage threshold** — use `Palmmedia.ReportGenerator` or a simple inline check; fail if coverage < 80%
9. **Upload coverage report** — upload to Codecov (free for open source) OR just store as artifact
10. **Upload test results** — artifact for debugging flaky tests

### Coverage reporting options

- **Codecov.io** — free for public repos, nice PR comments showing coverage diff. Requires adding `CODECOV_TOKEN` secret (free to get).
- **GitHub Actions summary** — simpler, no external service, shows in the workflow run summary. Use `danielpalme/ReportGenerator-GitHub-Action`.

**Recommendation:** Start with GitHub Actions summary (no account needed), add Codecov later if you want PR diff comments.

### Example structure

```yaml
name: CI

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          global-json-file: global.json  # reads version from global.json
      - uses: actions/cache@v4
        with:
          path: ~/.nuget/packages
          key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj') }}
          restore-keys: ${{ runner.os }}-nuget-
      - run: dotnet restore
      - run: dotnet format --verify-no-changes
      - run: dotnet build --no-restore -c Release
      - run: dotnet test --no-build -c Release --collect:"XPlat Code Coverage" --results-directory ./coverage
      # coverage threshold check TBD
```

---

## Workflow 2 — Release (Auto on Merge to Main)

**File:** `.github/workflows/release.yml`
**Triggers:** `release: [published]` — fires when `release-please` merges its PR and creates a GitHub Release
**Runner:** `ubuntu-latest` for all steps (including cross-platform binary builds — dotnet publish handles cross-compilation)

### Steps

1. **Checkout** — full history (`fetch-depth: 0`)
2. **Setup .NET**
3. **Cache NuGet**
4. **Restore**
5. **Run tests** — one final gate before publishing
6. **Build & publish binaries (matrix)** — 6 targets in parallel:
   - `win-x64`, `win-arm64` — self-contained, single file, `.exe`
   - `linux-x64`, `linux-arm64` — self-contained, single file
   - `osx-x64`, `osx-arm64` — self-contained, single file
7. **Zip each binary** — `zip mdj-<version>-<rid>.zip mdj` (or `.exe`)
8. **Pack NuGet** — `dotnet pack -c Release --no-build -o ./nupkg`
9. **Push to NuGet.org** — `dotnet nuget push ./nupkg/*.nupkg --api-key ${{ secrets.NUGET_API_KEY }} --source https://api.nuget.org/v3/index.json`
10. **Upload binaries to GitHub Release** — `softprops/action-gh-release@v2` to attach zips to the already-created release
11. **Upload NuGet package to GitHub Release** — also attach the `.nupkg` file as a release asset (for people who want to download it directly)

### Binary naming convention

```
mdj-1.1.0-win-x64.zip
mdj-1.1.0-win-arm64.zip
mdj-1.1.0-linux-x64.zip
mdj-1.1.0-linux-arm64.zip
mdj-1.1.0-osx-x64.zip
mdj-1.1.0-osx-arm64.zip
mdj-1.1.0.nupkg          ← also attached
```

### dotnet publish flags for self-contained single-file

```bash
dotnet publish src/markdown-journal-cli/markdown-journal-cli.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=true \
  -o ./publish/win-x64
```

> **Note on trimming:** `PublishTrimmed=true` reduces binary size significantly (~30-60%) but can break reflection-heavy code. Spectre.Console has known trimming issues. Test this carefully. If trimming breaks things, drop it and keep `PublishSingleFile=true` only.

### NuGet package dual-install support

The `.nupkg` serves two purposes:
1. `dotnet tool install -g markdown-journal-cli` — install from NuGet.org (requires .NET runtime)
2. Download the `.nupkg` asset from the GitHub Release page — install with `dotnet tool install --add-source ./nupkg -g markdown-journal-cli` (offline/air-gapped)

For people who don't want .NET: they download the zip for their platform, extract the binary, and add it to their PATH. No .NET required.

---

## Workflow 3 — CodeQL Security Analysis

**File:** `.github/workflows/codeql.yml`
**Triggers:** Push to `main`, PR to `main`, cron (`0 3 * * 1` — weekly Monday 3am)
**Runner:** `ubuntu-latest`
**Cost:** Free for public repos. CodeQL is GitHub's own tool.

### What it does

- Static analysis of C# code for security vulnerabilities (SQL injection, path traversal, etc.)
- Runs automatically, results appear in the **Security** tab of the repo
- Can be set as a required check to block PRs with high/critical severity findings

### Setup

Use the official `github/codeql-action` — minimal config needed for C#:

```yaml
- uses: github/codeql-action/init@v3
  with:
    languages: csharp
- uses: github/codeql-action/autobuild@v3
- uses: github/codeql-action/analyze@v3
```

---

## Workflow 4 — Dependency Review

**File:** `.github/workflows/dependency-review.yml`
**Triggers:** PR to `main` only
**Runner:** `ubuntu-latest`

### What it does

- Compares NuGet package versions between the PR branch and `main`
- Flags packages with known CVEs in the diff
- Uses `actions/dependency-review-action@v4` — official GitHub action, free

```yaml
- uses: actions/dependency-review-action@v4
  with:
    fail-on-severity: moderate
```

This blocks merging a PR that introduces a vulnerable NuGet dependency.

---

## Workflow 5 — Stale Issue Management

**File:** `.github/workflows/stale.yml`
**Triggers:** `schedule: cron('0 0 * * *')` — daily at midnight
**Runner:** `ubuntu-latest`

### What it does

- After 60 days of no activity, labels an issue as `stale` and posts a comment.
- After 7 more days with no activity, closes it.
- Standard practice for solo/small OSS maintainers to avoid issue rot.

Uses `actions/stale@v9` — official, zero config.

---

## Distribution Strategy

### Tier 1: NuGet global tool (for .NET developers)

```bash
dotnet tool install -g markdown-journal-cli
mdj --version
```

Requires .NET 10 runtime installed. Published to NuGet.org on every release.

### Tier 2: Self-contained binary download (for non-.NET users)

Download a zip from the GitHub Releases page:
- No .NET required
- Extract and add to PATH
- 6 platform targets: win-x64, win-arm64, linux-x64, linux-arm64, osx-x64, osx-arm64

### Tier 3: NuGet package download from GitHub Release (offline install)

Download the `.nupkg` file from the GitHub Release assets and install locally:

```bash
dotnet tool install --add-source ./path/to/nupkg -g markdown-journal-cli
```

Useful for air-gapped environments or if NuGet.org is unavailable.

### Future: Package managers (not planned yet)

- **Homebrew** — create a tap (`homebrew-mdj`), maintain a formula. Good for macOS/Linux adoption.
- **Winget** — submit to `microsoft/winget-pkgs`. Auto-updatable, good Windows reach.
- **Chocolatey** — requires moderation queue, slightly more friction.
- **Scoop** — create a bucket, simple JSON manifest.

These add maintenance overhead. Revisit after v1.0 is out and there's user demand.

---

## Additional Workflow: PR Title Lint

**File:** `.github/workflows/pr-title-lint.yml`
**Triggers:** `pull_request` events `(types: [opened, edited, synchronize, reopened])`
**Runner:** `ubuntu-latest`

Uses `amannn/action-semantic-pull-request@v5`:

```yaml
name: PR Title Lint
on:
  pull_request:
    types: [opened, edited, synchronize, reopened]
jobs:
  lint:
    runs-on: ubuntu-latest
    steps:
      - uses: amannn/action-semantic-pull-request@v5
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
```

**Required config:** Set this as a required status check in branch protection.
**Set repo to squash-merge only** so the PR title becomes the commit message on `main`.

### Commit types allowed (conventional commits)

| Type | When to use |
|---|---|
| `feat` | New feature or command |
| `fix` | Bug fix |
| `docs` | Documentation only |
| `test` | Adding or fixing tests |
| `refactor` | Code change that's not a fix or feature |
| `chore` | Dependency updates, build changes |
| `ci` | CI/CD changes |
| `perf` | Performance improvement |

Append `!` for breaking changes: `feat!: rename --path flag to --directory`

---

## Workflow: Release Please

**File:** `.github/workflows/release-please.yml`
**Triggers:** Push to `main`
**Runner:** `ubuntu-latest`

Uses `googleapis/release-please-action@v4`:

```yaml
name: Release Please
on:
  push:
    branches: [main]
jobs:
  release-please:
    runs-on: ubuntu-latest
    outputs:
      release_created: ${{ steps.release.outputs.release_created }}
      tag_name: ${{ steps.release.outputs.tag_name }}
    steps:
      - uses: googleapis/release-please-action@v4
        id: release
        with:
          token: ${{ secrets.GITHUB_TOKEN }}
          release-type: simple  # or 'dotnet' if supported
```

**Config file needed:** `release-please-config.json` and `.release-please-manifest.json` in the repo root.

`release-please` will:
- Read commit messages since last release tag.
- Open a PR titled `chore: release 1.1.0` with an updated `CHANGELOG.md` and bumped `<Version>` in `.csproj`.
- When you merge that PR, it creates the GitHub Release → triggers `release.yml`.

**Note:** You need to tell `release-please` where the version lives. For .NET projects, this means configuring it to update the `.csproj` `<Version>` tag. The `simple` release type supports a `extra-files` config to specify the `.csproj`.

---

## Quality Gates & Branch Protection

### Branch Protection Rules for `main`

Configure via **Settings → Branches → Add ruleset**:

| Rule | Setting |
|---|---|
| Require PR before merging | Enabled |
| Required approvals | 0 (solo project — you are the only approver) |
| Dismiss stale reviews | Enabled |
| Require status checks to pass | Enabled (see below) |
| Require branches up to date | Enabled |
| Block force pushes | Enabled |
| Restrict deletions | Enabled |
| Require linear history | Enabled (squash merges only) |

### Required Status Checks

Add these check names to the required list (exact names come from the workflow job names):

1. `build-and-test` — from `ci.yml`
2. `Semantic Pull Request` — from `pr-title-lint.yml`
3. `CodeQL` — from `codeql.yml` (optional: can leave as informational only)
4. `dependency-review` — from `dependency-review.yml`

### Merge strategy

Set repository to **squash merge only**:
- Settings → General → Pull Requests → uncheck "Allow merge commits" and "Allow rebase merging"
- Check only "Allow squash merging"
- Set default commit message to "PR title and commit details"

This is what makes PR title enforcement meaningful.

---

## Required Secrets & Settings

### Repository Secrets (Settings → Secrets → Actions)

| Secret | How to get | Used in |
|---|---|---|
| `NUGET_API_KEY` | nuget.org → Account → API Keys → Create key scoped to `markdown-journal-cli` package | `release.yml` |

### Repository Variables (Settings → Variables → Actions)

| Variable | Value | Used in |
|---|---|---|
| `DOTNET_VERSION` | `10.0.x` | `ci.yml`, `release.yml` — single place to update |

### GitHub Token

`secrets.GITHUB_TOKEN` is automatically available — no setup needed. Used by `release-please` and `softprops/action-gh-release`.

**Important:** `release-please` needs write permissions to open PRs and create releases. The default `GITHUB_TOKEN` permissions may need to be set to "Read and write" in Settings → Actions → General → Workflow permissions.

---

## Implementation Order

Work through these in order — each one builds on the previous.

### Phase 1 — Foundation (do before anything else)

1. [x] Finish `--version` flag (Task 1 from `pre-release-tasks.md`)
2. [x] Add `<PackAsTool>`, `<PackageId>`, `<ToolCommandName>mdj</ToolCommandName>` to `.csproj` (Task 2)
3. [x] Create `global.json` with the target .NET SDK version if not already present
4. [ ] Register on NuGet.org and create an API key → store as `NUGET_API_KEY` secret

### Phase 2 — CI Workflow

5. [ ] Create `.github/workflows/ci.yml` (build + test + format check)
6. [ ] Verify it passes on the current codebase
7. [ ] Create `.github/workflows/pr-title-lint.yml`
8. [ ] Configure branch protection rules on `main`
9. [ ] Update `dependabot.yml` to also scan NuGet packages (add `package-ecosystem: nuget`)

### Phase 3 — Release Workflow

10. [ ] Create `release-please-config.json` and `.release-please-manifest.json`
11. [ ] Create `.github/workflows/release-please.yml`
12. [ ] Create `.github/workflows/release.yml`
13. [ ] Test end-to-end with a `fix:` commit → merge release PR → verify NuGet push and binary uploads

### Phase 4 — Security

14. [ ] Create `.github/workflows/codeql.yml`
15. [ ] Create `.github/workflows/dependency-review.yml`
16. [ ] Create `.github/workflows/stale.yml`

### Phase 5 — Open Source Health Files

17. [ ] Add all community health files (see `open-source-readiness-research.md`)
18. [ ] Update `dependabot.yml` to include `nuget` ecosystem

---

## Open Questions / Future Work

### Pre-release / nightly builds

**Common practice for a simple OSS CLI tool:** Only ship stable releases. No nightly builds. Pre-release is fine for big features — use `feat!` commits to signal a major upcoming release, merge the release-please PR with a `1.0.0-beta.1` version if needed (release-please supports pre-release suffixes via config). For a v1.0 launch, stable-only is the right call.

### Trimming

Test `PublishTrimmed=true` against all commands before enabling. Spectre.Console may need trimming annotations. If it breaks, ship untrimmed — the binary will be ~80MB instead of ~25MB, which is fine.

### Code signing

For Windows, unsigned executables trigger SmartScreen warnings. Options:
- Buy a code signing certificate (~$300-500/year from DigiCert or Sectigo)
- Use Windows EV code signing
- Accept SmartScreen warning for now (common for small OSS tools — document it in README)

For macOS, unsigned binaries from the internet require `xattr -d com.apple.quarantine mdj`. Document this in README.

### Release-please `.csproj` version bumping

`release-please` has a `dotnet` strategy in newer versions, but it may not be fully stable. If it can't update `.csproj` directly, the workaround is:
1. Store the version in a separate file (e.g., `VERSION`) that `release-please` manages.
2. A workflow step reads that file and patches the `.csproj` before building.
3. Or: skip having release-please update `.csproj` — the version is determined at build time from the git tag using `MinVer` or `GitVersion`.

**Alternative versioning tool: MinVer** — a NuGet package that sets `<Version>` at build time from the nearest git tag. No file to update. Just tag → build → correct version. Pairs very well with release-please creating the tags. Strongly worth considering.

### Coverage threshold

Start at 80% given the existing 1130 tests. Adjust after seeing baseline numbers.
