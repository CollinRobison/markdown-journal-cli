# Pre-Release Tasks: markdown-journal-cli

> Deep-research-backed task list for getting `markdown-journal-cli` production-ready and public.
> All findings are sourced from official docs and real open-source .NET CLI tools.
> **This is a .NET 9 tool using Spectre.Console.Cli v0.50.0.**

---

## Table of Contents

1. [Task 1 — Add `--version` flag](#task-1----add---version-flag)
2. [Task 2 — Global Tool Installation](#task-2----global-tool-installation)
3. [Task 3 — CI/CD Release Pipeline](#task-3----cicd-release-pipeline)
4. [Task 4 — Open Source Repo Setup](#task-4----open-source-repo-setup)
5. [Task 5 — Clean Up Docs](#task-5----clean-up-docs)
6. [Sources](#sources)

---

## Task 1 — Add `--version` flag

**Goal:** Running `mdj --version` (or `mdj -v`) prints the current SemVer version and exits.

### What other tools do

Spectre.Console.Cli has **built-in version support** via `config.SetApplicationVersion(string)`.
When configured, it registers a global `--version` / `-v` option automatically.
Real-world examples: `dotnet-outdated`, `dotnet-script`, and `docfx` all use this pattern.

- Source: [spectreconsole.net — Configuring CommandApp and Commands](https://spectreconsole.net/cli/how-to/configuring-commandapp-and-commands)
- Source: [spectreconsole.net — Built-in Command Behaviors](https://spectreconsole.net/cli/reference/built-in-command-behaviors)

### Steps

#### Step 1.1 — Add `<Version>` to the `.csproj`

Open `markdown-journal-cli/markdown-journal-cli.csproj` and add to `<PropertyGroup>`:

```xml
<!-- Version is the single source of truth; overridden by /p:Version= in CI -->
<Version>1.0.0</Version>

<!-- Suppress the git SHA suffix from InformationalVersion (e.g., 1.0.0+abc1234 → 1.0.0) -->
<IncludeSourceRevisionInInformationalVersion>false</IncludeSourceRevisionInInformationalVersion>
```

**Why:** The `<Version>` property flows automatically into `AssemblyInformationalVersionAttribute`,
which is the correct attribute to read for display (it preserves pre-release suffixes like
`1.0.0-beta.1`). Do **not** use `Assembly.GetName().Version` — it strips pre-release labels.

- Source: [learn.microsoft.com — Set attributes from the project file](https://learn.microsoft.com/en-us/dotnet/standard/assembly/set-attributes-project-file)
- Source: [learn.microsoft.com — How to create a .NET tool](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools-how-to-create)

#### Step 1.2 — Read the version in `Program.cs`

At the top of `Program.cs`, before creating the `CommandApp`, read the assembly attribute:

```csharp
using System.Reflection;

var version = Assembly.GetExecutingAssembly()
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
    .InformationalVersion
    ?? "0.0.0";
```

#### Step 1.3 — Call `SetApplicationVersion` in the app configuration

Inside `app.Configure(config => { ... })`, add:

```csharp
config.SetApplicationVersion(version);
```

This single call:
- Adds `--version` / `-v` as a global option
- Adds a hidden `cli version` sub-command
- Outputs the version string and exits when invoked

#### Step 1.4 — Verify

```bash
dotnet run --project markdown-journal-cli -- --version
# Expected output: 1.0.0
```

> **⚠️ Caution:** If any command in your app claims `-v` as a short form for `--verbose`,
> there will be a flag conflict. Audit all `CommandSettings` for `-v` usage.
> If found, remove the `-v` alias from verbose and keep only `--verbose`.

**Built-in behaviors unlocked by `SetApplicationVersion`:**

| Command | Description |
|---------|-------------|
| `mdj --version` | Prints version and exits |
| `mdj -v` | Short alias |
| `mdj cli version` | Hidden sub-command showing version info |
| `mdj cli explain` | Diagnostic tree of all commands |
| `mdj cli xmldoc` | XML doc of all commands (useful for doc generation) |

---

## Task 2 — Global Tool Installation

**Goal:** Users can run `dotnet tool install -g markdown-journal-cli` to install the tool.

### What other tools do

Every major .NET CLI tool uses the same pattern: `<PackAsTool>true</PackAsTool>` + `<ToolCommandName>` +
`<PackageId>`. References: `dotnet-outdated`, `dotnet-script`, `nuke-build/nuke`, `docfx`.

- Source: [github.com/dotnet-outdated — DotNetOutdated.csproj](https://github.com/dotnet-outdated/dotnet-outdated/blob/main/src/DotNetOutdated/DotNetOutdated.csproj)
- Source: [learn.microsoft.com — Create a .NET Tool](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools-how-to-create)
- Source: [learn.microsoft.com — dotnet tool install](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-tool-install)

### Steps

#### Step 2.1 — Add packaging properties to the `.csproj`

Add the following to `<PropertyGroup>` in `markdown-journal-cli/markdown-journal-cli.csproj`:

```xml
<!-- ── Global Tool packaging (REQUIRED) ── -->
<PackAsTool>true</PackAsTool>
<ToolCommandName>mdj</ToolCommandName>          <!-- the command users type in the terminal -->

<!-- ── NuGet package metadata (REQUIRED for NuGet.org) ── -->
<PackageId>markdown-journal-cli</PackageId>
<Authors>Your Name</Authors>
<Description>A CLI tool for creating and managing markdown journal entries.</Description>
<PackageLicenseExpression>MIT</PackageLicenseExpression>
<PackageProjectUrl>https://github.com/YOUR_USERNAME/markdown-journal-cli</PackageProjectUrl>
<RepositoryUrl>https://github.com/YOUR_USERNAME/markdown-journal-cli.git</RepositoryUrl>
<RepositoryType>git</RepositoryType>

<!-- ── Optional but strongly recommended ── -->
<PackageReadmeFile>README.md</PackageReadmeFile>
<PackageTags>journal;markdown;cli;notes</PackageTags>
<PackageOutputPath>./nupkg</PackageOutputPath>
```

**Required properties:**

| Property | Required? | Notes |
|----------|-----------|-------|
| `<PackAsTool>true</PackAsTool>` | **Yes** | Tells `dotnet pack` to emit binary in `tools/net9.0/any/` inside the `.nupkg`. Without this, `dotnet tool install` fails. |
| `<ToolCommandName>` | Recommended | The CLI command (`mdj`). Defaults to assembly name. No file extensions. |
| `<PackageId>` | **Yes** | Unique NuGet ID. Must be globally unique on NuGet.org. |
| `<Version>` | **Yes** | Already added in Task 1. |
| `<PackageLicenseExpression>` | Required for NuGet.org | SPDX identifier: `MIT`, `Apache-2.0`, etc. |

#### Step 2.2 — Include the README in the NuGet package

Add an `<ItemGroup>` to the `.csproj`:

```xml
<ItemGroup>
  <None Include="..\..\README.md" Pack="true" PackagePath="\"/>
</ItemGroup>
```

This makes the README display on the NuGet.org package page.

#### Step 2.3 — Test the pack locally

```bash
# Pack the .nupkg
dotnet pack markdown-journal-cli/markdown-journal-cli.csproj \
  --configuration Release \
  --output ./nupkg

# Verify the nupkg was created
ls ./nupkg/

# Test install locally (from the local nupkg folder)
dotnet tool install -g markdown-journal-cli \
  --add-source ./nupkg \
  --version 1.0.0

# Test the installed tool
mdj --version

# Uninstall when done testing
dotnet tool uninstall -g markdown-journal-cli
```

#### Step 2.4 — (When ready) Publish to NuGet.org

1. Create a NuGet.org account at [nuget.org](https://www.nuget.org)
2. Generate an API key at https://www.nuget.org/account/apikeys
3. Push:

```bash
dotnet nuget push ./nupkg/markdown-journal-cli.1.0.0.nupkg \
  --api-key YOUR_API_KEY \
  --source https://api.nuget.org/v3/index.json
```

> **Note on `--add-source` limitations:** `dotnet tool install --add-source <URL>` expects a
> **NuGet v3 feed** (`index.json`), not a raw file URL. GitHub Releases are raw file downloads —
> users must download the `.nupkg` first, or you set up a GitHub Packages feed. NuGet.org is
> the strongly recommended primary distribution channel.

> **Note on `ToolCommandName` naming:** Prefix with `dotnet-` (e.g., `dotnet-mdjournal`) if you want
> users to invoke it as `dotnet mdjournal` (the .NET CLI tool resolver auto-strips the `dotnet-` prefix).
> For a standalone tool, a short name like `mdj` or `mdjournal` works well.

---

## Task 3 — CI/CD Release Pipeline

**Goal:** Pushing a `v*` tag triggers a pipeline that: runs tests, builds self-contained
single-file binaries for 5 platforms, packs a `.nupkg`, and creates a GitHub Release with all
assets attached.

### What other tools do

The gold-standard references:
- **[dotnet/docfx](https://github.com/dotnet/docfx/blob/main/.github/workflows/release.yml)** — self-contained multi-platform binaries + GitHub Release
- **[JustArchiNET/ArchiSteamFarm](https://github.com/JustArchiNET/ArchiSteamFarm/blob/main/.github/workflows/publish.yml)** — matrix publish → artifact handoff → release job pattern
- **[dotnet-outdated/dotnet-outdated](https://github.com/dotnet-outdated/dotnet-outdated/blob/main/.github/workflows/release.yml)** — minimal NuGet-only release
- **[softprops/action-gh-release](https://github.com/softprops/action-gh-release)** — the standard GitHub Release action

### Steps

#### Step 3.1 — Create `.github/workflows/ci.yml`

Create a CI workflow that runs on every push and PR:

```yaml
# .github/workflows/ci.yml
name: CI

on:
  push:
    branches: ["**"]
  pull_request:
    branches: ["main"]

env:
  DOTNET_SKIP_FIRST_TIME_EXPERIENCE: true
  DOTNET_CLI_TELEMETRY_OPTOUT: true
  DOTNET_NOLOGO: true

jobs:
  build-and-test:
    name: Build & Test
    runs-on: ubuntu-latest

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 9.0.x

      - name: Restore
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore --configuration Release

      - name: Test
        run: dotnet test --no-build --configuration Release --logger "console;verbosity=normal"
```

#### Step 3.2 — Create `.github/workflows/release.yml`

This is the full release pipeline. Create `.github/workflows/release.yml`:

```yaml
# .github/workflows/release.yml
name: Release

on:
  push:
    tags: ["v*.*.*"]

env:
  DOTNET_SKIP_FIRST_TIME_EXPERIENCE: true
  DOTNET_CLI_TELEMETRY_OPTOUT: true
  DOTNET_NOLOGO: true
  PROJECT: markdown-journal-cli/markdown-journal-cli.csproj

# ─────────────────────────────────────────────────────────────────
# JOB 1: Build & Test
# ─────────────────────────────────────────────────────────────────
jobs:
  ci:
    name: Build & Test
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "9.0.x"
      - run: dotnet restore
      - run: dotnet build --no-restore -c Release
      - run: dotnet test --no-build -c Release --logger "console;verbosity=normal"

# ─────────────────────────────────────────────────────────────────
# JOB 2: Publish platform binaries (matrix, parallel)
# ─────────────────────────────────────────────────────────────────
  publish:
    name: Publish / ${{ matrix.rid }}
    needs: ci
    strategy:
      fail-fast: false
      matrix:
        include:
          - rid: win-x64
            os: windows-latest
            binary: mdj-win-x64.exe
            output_binary: mdj.exe

          - rid: osx-x64
            os: macos-13
            binary: mdj-osx-x64
            output_binary: mdj

          - rid: osx-arm64
            os: macos-latest
            binary: mdj-osx-arm64
            output_binary: mdj

          - rid: linux-x64
            os: ubuntu-latest
            binary: mdj-linux-x64
            output_binary: mdj

          - rid: linux-arm64
            os: ubuntu-latest
            binary: mdj-linux-arm64
            output_binary: mdj

    runs-on: ${{ matrix.os }}
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "9.0.x"

      - name: Extract version
        id: version
        shell: bash
        run: echo "version=${GITHUB_REF_NAME#v}" >> "$GITHUB_OUTPUT"

      - name: Publish self-contained single-file binary
        shell: bash
        run: |
          dotnet publish "${{ env.PROJECT }}" \
            -c Release \
            -r ${{ matrix.rid }} \
            --self-contained true \
            -p:PublishSingleFile=true \
            -p:EnableCompressionInSingleFile=true \
            -p:DebugType=none \
            -p:DebugSymbols=false \
            -p:Version=${{ steps.version.outputs.version }} \
            -o ./publish/${{ matrix.rid }}

      - name: Stage artifact (Unix)
        if: runner.os != 'Windows'
        run: |
          cp ./publish/${{ matrix.rid }}/${{ matrix.output_binary }} \
             ./publish/${{ matrix.binary }}
          chmod +x ./publish/${{ matrix.binary }}

      - name: Stage artifact (Windows)
        if: runner.os == 'Windows'
        shell: pwsh
        run: |
          Copy-Item `
            "./publish/${{ matrix.rid }}/${{ matrix.output_binary }}" `
            "./publish/${{ matrix.binary }}"

      - name: Upload artifact
        uses: actions/upload-artifact@v4
        with:
          name: binary-${{ matrix.rid }}
          path: ./publish/${{ matrix.binary }}
          if-no-files-found: error
          retention-days: 1

# ─────────────────────────────────────────────────────────────────
# JOB 3: Pack .nupkg + Create GitHub Release
# ─────────────────────────────────────────────────────────────────
  release:
    name: Create GitHub Release
    needs: publish
    runs-on: ubuntu-latest
    permissions:
      contents: write   # REQUIRED by softprops/action-gh-release
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "9.0.x"

      - name: Extract version
        id: version
        run: echo "version=${GITHUB_REF_NAME#v}" >> "$GITHUB_OUTPUT"

      - name: Download all platform artifacts
        uses: actions/download-artifact@v4
        with:
          pattern: binary-*
          merge-multiple: true
          path: ./artifacts/binaries

      - name: Pack NuGet global tool
        run: |
          dotnet pack "${{ env.PROJECT }}" \
            -c Release \
            /p:Version=${{ steps.version.outputs.version }} \
            --output ./artifacts/nupkg

      - name: List artifacts
        run: ls -lh ./artifacts/binaries ./artifacts/nupkg

      - name: Create GitHub Release
        uses: softprops/action-gh-release@v2
        with:
          name: "v${{ steps.version.outputs.version }}"
          generate_release_notes: true
          fail_on_unmatched_files: true
          make_latest: true
          files: |
            ./artifacts/binaries/mdj-win-x64.exe
            ./artifacts/binaries/mdj-osx-x64
            ./artifacts/binaries/mdj-osx-arm64
            ./artifacts/binaries/mdj-linux-x64
            ./artifacts/binaries/mdj-linux-arm64
            ./artifacts/nupkg/*.nupkg
```

#### Step 3.3 — (Optional but recommended) Add NuGet.org push

In the `release` job, add after the GitHub Release step:

```yaml
      - name: Push to NuGet.org
        env:
          NUGET_API_KEY: ${{ secrets.NUGET_API_KEY }}
        run: |
          dotnet nuget push ./artifacts/nupkg/*.nupkg \
            --api-key "$NUGET_API_KEY" \
            --source https://api.nuget.org/v3/index.json \
            --skip-duplicate
```

Then add `NUGET_API_KEY` to **Settings → Secrets → Actions** in GitHub.

#### Step 3.4 — (Optional) Pre-release detection

Automatically mark tags like `v1.0.0-beta.1` as GitHub pre-releases:

```yaml
      - name: Detect pre-release
        id: prerelease
        run: |
          if [[ "${GITHUB_REF_NAME}" =~ -[a-zA-Z] ]]; then
            echo "is_prerelease=true" >> "$GITHUB_OUTPUT"
          else
            echo "is_prerelease=false" >> "$GITHUB_OUTPUT"
          fi
```

Then set `prerelease: ${{ steps.prerelease.outputs.is_prerelease }}` on the release action.

#### Step 3.5 — Trigger a release

```bash
git tag v1.0.0
git push origin v1.0.0
# → The release.yml workflow fires automatically
```

#### Important notes on `dotnet publish` flags

| Flag | Purpose |
|------|---------|
| `--self-contained true` | Bundles .NET runtime — no SDK needed on user machine |
| `-p:PublishSingleFile=true` | Bundles all DLLs into one executable |
| `-p:EnableCompressionInSingleFile=true` | Compresses embedded assemblies (~30% smaller) |
| `-p:DebugType=none -p:DebugSymbols=false` | Strips debug info from release binary |
| `/p:Version=...` | Overrides the version from the git tag |

> **⚠️ Do NOT add `-p:PublishTrimmed=true` without testing.** Spectre.Console.Cli and
> `Microsoft.Extensions.DependencyInjection` both use reflection. Trimming will break
> the app unless you add `<TrimmerRootDescriptor>` entries for every affected assembly.

#### macOS runner selection

| Target RID | Runner to use | Reason |
|-----------|---------------|--------|
| `osx-x64` | `macos-13` | Last GitHub-hosted Intel macOS runner |
| `osx-arm64` | `macos-latest` | Points to macOS 15 (Apple Silicon) as of 2024 |

- Source: [github.com/softprops/action-gh-release](https://github.com/softprops/action-gh-release)
- Source: [github.com/dotnet/docfx — release.yml](https://github.com/dotnet/docfx/blob/main/.github/workflows/release.yml)
- Source: [github.com/JustArchiNET/ArchiSteamFarm — publish.yml](https://github.com/JustArchiNET/ArchiSteamFarm/blob/main/.github/workflows/publish.yml)

---

## Task 4 — Open Source Repo Setup

**Goal:** Make the repo public with the correct license, and prepare it for collaborators with
community health files, issue templates, and branch protection.

### What other tools do

Every major .NET open source CLI tool uses **MIT**:
spectre.console, cake-build/cake, nuke-build/nuke, dotnet/sdk, dotnet-outdated, dotnet-script.

Community health files are checked by GitHub's [Community Standards page](https://github.com/YOUR_USERNAME/markdown-journal-cli/community).

- Source: [docs.github.com — About community profiles for public repositories](https://docs.github.com/en/communities/setting-up-your-project-for-healthy-contributions/about-community-profiles-for-public-repositories)
- Source: [github.com/spectreconsole/spectre.console — LICENSE.md](https://github.com/spectreconsole/spectre.console/blob/main/LICENSE.md)

### Steps

#### Step 4.1 — Create `LICENSE` (MIT) at repo root

Create `LICENSE` (no extension) with:

```
MIT License

Copyright (c) [YEAR] [YOUR NAME OR ORG]

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

> **Why MIT?** Spectre.Console, BenchmarkDotNet, Newtonsoft.Json, and the entire .NET
> Foundation ecosystem use MIT. It is maximally permissive, NuGet.org compatible, and
> the universal expectation for contributors in the .NET space.

#### Step 4.2 — Create `CONTRIBUTING.md`

Based on [Spectre.Console's CONTRIBUTING.md](https://github.com/spectreconsole/spectre.console/blob/main/CONTRIBUTING.md):

```markdown
# Contributing to markdown-journal-cli

Thank you for your interest in contributing!

## Prerequisites

- .NET 9 SDK ([download](https://dotnet.microsoft.com/download))
- A GitHub account

## Code of Conduct

By participating, you agree to the [Code of Conduct](CODE_OF_CONDUCT.md).

## Getting Started

1. Fork the repository and clone your fork
2. Create a branch: `git checkout -b feature/my-feature`
3. Make your changes
4. Run tests: `dotnet test`
5. Push and open a Pull Request against `main`

## Code Style

- Follow standard .NET coding conventions
- Run `dotnet format` before committing

## Tests

- All new code should have unit test coverage
- Run `dotnet test` to verify all tests pass

## Commit Messages

Use [Conventional Commits](https://www.conventionalcommits.org/):
- `feat: add --output option to new command`
- `fix: handle missing config file gracefully`
- `docs: update README installation instructions`

## Pull Request Process

1. Open an issue first for non-trivial changes
2. Reference the issue in your PR: `Fixes #123`
3. Keep PRs focused — one feature or fix per PR
4. A maintainer will review within approximately 2 weeks

## Licensing

By contributing, you assert the contribution is your own original work and
you license it under the [MIT License](LICENSE).
```

#### Step 4.3 — Create `CODE_OF_CONDUCT.md`

The Contributor Covenant v2.1 is the standard in the .NET ecosystem.
Get the full text at: https://www.contributor-covenant.org/version/2/1/code_of_conduct/

Replace `[INSERT CONTACT METHOD]` with your email or GitHub Discussions link.

- Source: [contributor-covenant.org](https://www.contributor-covenant.org/version/2/1/code_of_conduct.html)
- Source: [github.com/spectreconsole/spectre.console — CODE_OF_CONDUCT.md](https://github.com/spectreconsole/spectre.console/blob/main/CODE_OF_CONDUCT.md)

#### Step 4.4 — Create `SECURITY.md`

```markdown
# Security Policy

## Supported Versions

| Version | Supported |
|---------|-----------|
| 1.x     | ✅         |
| < 1.0   | ❌         |

## Reporting a Vulnerability

**Do not** open a public GitHub issue for security vulnerabilities.

Report via GitHub's [private security advisory](../../security/advisories/new) feature.

We will respond within 48 hours and aim to release a patch within 7 days.
```

#### Step 4.5 — Create `.github/ISSUE_TEMPLATE/bug_report.yml`

Based on [Spectre.Console's bug_report template](https://github.com/spectreconsole/spectre.console/blob/main/.github/ISSUE_TEMPLATE/bug_report.md):

```yaml
name: Bug report
description: Create a report to help us improve
labels: ["bug", "needs triage"]
body:
  - type: markdown
    attributes:
      value: Please upvote 👍 this issue if you are affected.

  - type: dropdown
    id: version
    attributes:
      label: Tool version
      description: Which version of markdown-journal-cli are you using?
      options:
        - Latest release
        - Built from source
    validations:
      required: true

  - type: input
    id: os
    attributes:
      label: Operating system
      placeholder: "e.g. macOS 14.5 / Windows 11 / Ubuntu 22.04"
    validations:
      required: true

  - type: textarea
    id: description
    attributes:
      label: Describe the bug
    validations:
      required: true

  - type: textarea
    id: repro
    attributes:
      label: Steps to reproduce
      placeholder: |
        mdj new "My Entry" --date 2025-01-01
    validations:
      required: true

  - type: textarea
    id: expected
    attributes:
      label: Expected behavior
    validations:
      required: true

  - type: textarea
    id: actual
    attributes:
      label: Actual behavior (include error messages / stack traces)
    validations:
      required: true
```

#### Step 4.6 — Create `.github/ISSUE_TEMPLATE/feature_request.yml`

```yaml
name: Feature request
description: Suggest an idea for this project
labels: ["enhancement", "needs triage"]
body:
  - type: markdown
    attributes:
      value: Please upvote 👍 this issue if you are interested in it.

  - type: textarea
    id: problem
    attributes:
      label: Is your feature request related to a problem?
      placeholder: "I'm always frustrated when..."

  - type: textarea
    id: solution
    attributes:
      label: Describe the solution you'd like
    validations:
      required: true

  - type: textarea
    id: alternatives
    attributes:
      label: Alternatives you've considered

  - type: textarea
    id: context
    attributes:
      label: Additional context
```

#### Step 4.7 — Create `.github/ISSUE_TEMPLATE/config.yml`

```yaml
blank_issues_enabled: false
contact_links:
  - name: Question / Discussion
    url: https://github.com/YOUR_USERNAME/markdown-journal-cli/discussions
    about: Ask questions and discuss the project
```

#### Step 4.8 — Create `.github/pull_request_template.md`

Based on [Spectre.Console's PR template](https://github.com/spectreconsole/spectre.console/blob/main/.github/pull_request_template.md):

```markdown
<!--
Do NOT open a PR without discussing the changes on an open issue first.
Issue number: #
-->
Fixes #

- [ ] I have read the [Contributing Guidelines](CONTRIBUTING.md)
- [ ] There is no existing PR solving this issue
- [ ] All new code has test coverage
- [ ] All existing tests pass (`dotnet test`)

## Changes

<!-- Describe your changes here -->

---
Please upvote 👍 this PR if you are interested in it.
```

#### Step 4.9 — Create `.github/CODEOWNERS`

```
# Default owners for everything in the repo
*   @YOUR_GITHUB_USERNAME

# Docs owners
/docs/   @YOUR_GITHUB_USERNAME
README.md @YOUR_GITHUB_USERNAME
```

#### Step 4.10 — Set up GitHub Labels

Create these labels via **Settings → Labels** on GitHub:

| Label | Color | Purpose |
|-------|-------|---------|
| `bug` | `#d73a4a` | Something is broken |
| `enhancement` | `#a2eeef` | New feature or improvement |
| `needs triage` | `#e4e669` | New issue, needs review |
| `good first issue` | `#7057ff` | Good for new contributors |
| `help wanted` | `#008672` | Extra attention needed |
| `documentation` | `#0075ca` | Doc improvements |
| `duplicate` | `#cfd3d7` | Duplicate issue |
| `wontfix` | `#ffffff` | Won't be addressed |
| `question` | `#d876e3` | General question |

#### Step 4.11 — Configure Branch Protection for `main`

Go to **Settings → Branches → Add branch protection rule** for `main`:

- ✅ Require a pull request before merging
  - ✅ Require approvals: 1
  - ✅ Dismiss stale pull request approvals when new commits are pushed
- ✅ Require status checks to pass before merging
  - Add your CI job name: `Build & Test`
- ✅ Require conversation resolution before merging
- ✅ Do not allow bypassing the above settings

#### Step 4.12 — Enable GitHub Discussions

Go to **Settings → Features → Discussions** and enable it. Update `.github/ISSUE_TEMPLATE/config.yml`
with the Discussions URL.

#### Step 4.13 — Make the repo public

Go to **Settings → Danger Zone → Change repository visibility → Make public**.

> **Checklist before going public:**
> - [ ] No secrets or credentials in git history
> - [ ] `LICENSE` file present
> - [ ] `README.md` explains what the tool does
> - [ ] `.gitignore` is complete (check for `/bin`, `/obj`, `*.user`, `appsettings.*.json`)
> - [ ] No sensitive paths hardcoded in config files

---

## Task 5 — Clean Up Docs

**Goal:** The README and `/docs` folder are clean, accurate, and welcoming to new users and contributors.

### What other tools do

Best-in-class .NET CLI READMEs follow this pattern:
title + badges → description → demo/screenshot → install → quick start → commands → contributing → license.

References:
- [github.com/spectreconsole/spectre.console — README.md](https://github.com/spectreconsole/spectre.console/blob/main/README.md)
- [github.com/dotnet-script/dotnet-script — README.md](https://github.com/dotnet-script/dotnet-script/blob/master/README.md)
- [shields.io/badges](https://shields.io/badges)

### Steps

#### Step 5.1 — Add badges to the top of `README.md`

Replace or update the header of `README.md` with:

```markdown
# markdown-journal-cli

![CI](https://github.com/YOUR_USERNAME/markdown-journal-cli/actions/workflows/ci.yml/badge.svg)
[![NuGet](https://img.shields.io/nuget/v/markdown-journal-cli)](https://www.nuget.org/packages/markdown-journal-cli)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/download/dotnet/9.0)

> A command-line tool for creating and managing markdown journal entries.
```

**Badge URL patterns:**

| Badge | URL Pattern |
|-------|-------------|
| GitHub Actions CI | `https://github.com/{OWNER}/{REPO}/actions/workflows/{FILE}.yml/badge.svg` |
| NuGet version | `https://img.shields.io/nuget/v/{PACKAGE_ID}` |
| NuGet downloads | `https://img.shields.io/nuget/dt/{PACKAGE_ID}` |
| License (static) | `https://img.shields.io/badge/License-MIT-yellow.svg` |
| .NET version (static) | `https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet` |

> **Note:** The NuGet badge won't resolve until the package is published to NuGet.org.
> Use the static badge format until then.

#### Step 5.2 — Ensure README covers these sections (in order)

```markdown
# markdown-journal-cli
[badges]

> one-sentence description

## Installation
### Global Tool (requires .NET 9)
### Direct Download (links to GitHub Releases)

## Quick Start
[real command examples with expected output]

## Commands
[table or section per top-level command: new, init, add, update, remove]

## Configuration
[brief explanation of appsettings.json / .journalrc]

## Contributing
See [CONTRIBUTING.md](CONTRIBUTING.md)

## License
MIT — see [LICENSE](LICENSE)
```

#### Step 5.3 — Audit the `/docs` folder

Review all files in `/docs/` and:
- Remove or archive any research/draft files that shouldn't be public
- Keep: `ARCHITECTURE.md`, `DEVELOPMENT.md`, `configuration.md` (if it exists)
- Move the research files to a `/docs/research/` subfolder or delete them before going public

Suggested final `/docs` structure:

```
docs/
├── ARCHITECTURE.md         ← system design / architecture decisions
├── DEVELOPMENT.md          ← local dev setup, build instructions, testing guide
└── research/               ← optional: background research (can be private)
```

#### Step 5.4 — Add a demo screenshot or GIF

Run the tool and capture a screenshot of typical output (e.g., `mdj new`, `mdj --help`).
Save to `docs/demo.png` and reference in README:

```markdown
## Demo

![demo](docs/demo.png)
```

Tools for recording terminal sessions: [VHS](https://github.com/charmbracelet/vhs), [asciinema](https://asciinema.org).

#### Step 5.5 — Update `DEVELOPMENT.md`

Ensure `/docs/DEVELOPMENT.md` covers:
- Prerequisites (`.NET 9 SDK` version)
- Clone + build steps (`git clone`, `dotnet restore`, `dotnet build`)
- How to run tests (`dotnet test`)
- How to run the tool locally (`dotnet run --project markdown-journal-cli -- new`)
- How to pack and test the global tool locally (see Task 2, Step 2.3)

#### Step 5.6 — Verify all links in README work

After making the repo public, visit each link in the README to confirm they resolve correctly.
Key links to check:
- GitHub Actions badge URL
- NuGet badge URL (after publishing)
- GitHub Releases download links
- Links to `CONTRIBUTING.md`, `LICENSE`, `CODE_OF_CONDUCT.md`

---

## Sources

### Official Documentation

| Source | Used For |
|--------|----------|
| [spectreconsole.net — Configuring CommandApp and Commands](https://spectreconsole.net/cli/how-to/configuring-commandapp-and-commands) | `SetApplicationVersion` API |
| [spectreconsole.net — Built-in Command Behaviors](https://spectreconsole.net/cli/reference/built-in-command-behaviors) | `--version`, `cli version`, `cli xmldoc` behavior |
| [learn.microsoft.com — How to create a .NET tool](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools-how-to-create) | `PackAsTool`, `ToolCommandName`, AssemblyInformationalVersion |
| [learn.microsoft.com — dotnet tool install](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-tool-install) | `--add-source` limitations, install workflow |
| [learn.microsoft.com — Set assembly attributes from project file](https://learn.microsoft.com/en-us/dotnet/standard/assembly/set-attributes-project-file) | `<Version>` → `AssemblyInformationalVersionAttribute` mapping |
| [docs.github.com — About community profiles](https://docs.github.com/en/communities/setting-up-your-project-for-healthy-contributions/about-community-profiles-for-public-repositories) | GitHub community health file requirements |
| [contributor-covenant.org v2.1](https://www.contributor-covenant.org/version/2/1/code_of_conduct.html) | Full CODE_OF_CONDUCT.md text |
| [shields.io/badges](https://shields.io/badges) | README badge URL format |

### Open Source Reference Projects

| Project | Used For |
|---------|----------|
| [spectreconsole/spectre.console](https://github.com/spectreconsole/spectre.console) | Community files (CONTRIBUTING, CODE_OF_CONDUCT, issue templates, PR template), README structure |
| [dotnet-outdated/dotnet-outdated](https://github.com/dotnet-outdated/dotnet-outdated) | Real `.csproj` global tool packaging, release workflow |
| [dotnet/docfx](https://github.com/dotnet/docfx/blob/main/.github/workflows/release.yml) | Multi-platform self-contained binary release workflow |
| [JustArchiNET/ArchiSteamFarm](https://github.com/JustArchiNET/ArchiSteamFarm/blob/main/.github/workflows/publish.yml) | Matrix publish → artifact handoff → release job pattern |
| [dotnet-script/dotnet-script](https://github.com/dotnet-script/dotnet-script) | Global tool packaging, README structure |
| [softprops/action-gh-release](https://github.com/softprops/action-gh-release) | GitHub Release action reference (all inputs) |
| [nuke-build/nuke](https://github.com/nuke-build/nuke) | MIT license, global tool pattern |
| [cake-build/cake](https://github.com/cake-build/cake) | MIT license, community files pattern |
