# GitHub Actions Release Pipeline for a .NET CLI Tool

> Research into building a complete CI/CD pipeline with multi-platform self-contained
> single-file binaries, NuGet packaging, and GitHub Releases.

---

## Executive Summary

Building a production-quality release pipeline for a .NET CLI tool requires three
cooperating parts: a **CI job** that runs on every push, a **matrix publish job** that
compiles self-contained single-file binaries for five target RIDs (Windows x64, macOS
x64, macOS arm64, Linux x64, Linux arm64), and a **release job** that fires only on
`v*` tags, downloads all platform artifacts, packs the `.nupkg`, and uses
`softprops/action-gh-release` to attach everything as GitHub Release assets.

The primary real-world reference is
[ArchiSteamFarm's publish.yml](https://github.com/JustArchiNET/ArchiSteamFarm/blob/main/.github/workflows/publish.yml),
which demonstrates the exact artifact-handoff pattern between matrix jobs and a
downstream release job. For NuGet-only releases (no platform binaries),
[dotnet-outdated's release.yml](https://github.com/dotnet-outdated/dotnet-outdated/blob/main/.github/workflows/release.yml)
is a clean, minimal reference.

---

## Project Setup: `csproj` Changes Required First

The `markdown-journal-cli.csproj` currently has no packaging metadata. Before the
workflow will produce useful artifacts, several properties need to be added.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <RootNamespace>markdown_journal_cli</RootNamespace>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>

    <!-- ── Packaging identity ── -->
    <PackageId>markdown-journal</PackageId>
    <Version>1.0.0</Version>          <!-- overridden by /p:Version= in CI -->
    <Authors>Your Name</Authors>
    <Description>A markdown-based journaling CLI tool.</Description>
    <PackageTags>journal;markdown;cli</PackageTags>

    <!-- ── Global tool support ── -->
    <PackAsTool>true</PackAsTool>
    <ToolCommandName>mdjournal</ToolCommandName>

    <!-- ── Publish quality-of-life ── -->
    <!-- Suppresses the first-run setup delay when the binary launches -->
    <RuntimeIdentifiers>win-x64;osx-x64;osx-arm64;linux-x64;linux-arm64</RuntimeIdentifiers>
  </PropertyGroup>
  <!-- ... existing ItemGroups ... -->
</Project>
```

> **Why `PackAsTool`?**  
> Setting `PackAsTool=true` makes `dotnet pack` emit the binary inside the
> `tools/net9.0/any/` folder within the `.nupkg`, conforming to the global tool
> specification. Without it the pack output is a plain library/exe NuGet package that
> **cannot** be installed with `dotnet tool install`.

---

## Architecture of the Workflow

```
┌─────────────────────────────────────────────────────────────────────────┐
│  on: push (any branch)          on: push (v* tag)                       │
│                                                                         │
│  ┌──────────┐                   ┌──────────┐                           │
│  │  ci job  │                   │  ci job  │  (same, runs first)        │
│  │ build+   │                   │ build+   │                           │
│  │ test     │                   │ test     │                           │
│  └──────────┘                   └────┬─────┘                           │
│                                      │ needs: ci                        │
│                              ┌───────┴──────────────────────┐          │
│                              │  publish matrix (5 jobs)     │          │
│                              │  win-x64  (windows-latest)   │          │
│                              │  osx-x64  (macos-13)         │          │
│                              │  osx-arm64 (macos-latest)    │          │
│                              │  linux-x64 (ubuntu-latest)   │          │
│                              │  linux-arm64 (ubuntu-latest) │          │
│                              │  → upload-artifact per job   │          │
│                              └───────┬──────────────────────┘          │
│                                      │ needs: publish                   │
│                              ┌───────┴──────────────────────┐          │
│                              │  release job (ubuntu-latest) │          │
│                              │  download-artifact (×5)      │          │
│                              │  dotnet pack  → .nupkg       │          │
│                              │  softprops/action-gh-release │          │
│                              └──────────────────────────────┘          │
└─────────────────────────────────────────────────────────────────────────┘
```

**Why separate jobs instead of one big job?**

- Each platform job runs on its **native runner** — macOS arm64 binaries need a macOS
  runner for macOS-specific post-processing (chmod +x, notarization hooks, etc.).
- GitHub parallelises the matrix jobs, cutting wall-clock time roughly 5×.
- The release job is a clean aggregation point with a single call to
  `softprops/action-gh-release`.
- If a single platform job fails, GitHub cancels or marks the release job as failed
  without uploading partial assets.

---

## The Key `dotnet publish` Flags

### Self-Contained Single-File

```bash
dotnet publish ./markdown-journal-cli/markdown-journal-cli.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:EnableCompressionInSingleFile=true \
  -p:DebugType=none \
  -p:DebugSymbols=false \
  -o ./publish/win-x64
```

| Flag | Purpose |
|------|---------|
| `-c Release` | Optimised build; no debug overhead |
| `-r win-x64` | Target Runtime Identifier (RID) |
| `--self-contained true` | Bundle the .NET runtime; no host SDK needed on target |
| `-p:PublishSingleFile=true` | Bundle all assemblies into one `.exe`/binary |
| `-p:EnableCompressionInSingleFile=true` | Compress embedded assemblies (~30% smaller) |
| `-p:DebugType=none` | Don't embed or emit PDB data |
| `-p:DebugSymbols=false` | Don't generate `.pdb` sidecar file |

> **Optional: `-p:PublishTrimmed=true`**  
> Trims unused IL from the binary. Can reduce size by 40-60% but **breaks apps that
> use reflection** (e.g. `System.Text.Json` source-gen, Spectre.Console's CLI binder,
> Microsoft.Extensions.DependencyInjection). If trimming is desired, add
> `<TrimmerRootDescriptor>` entries for any assemblies that rely on reflection. Start
> without trimming; add it later if binary size is a concern.

### Framework-Dependent (for `--no-self-contained`)

```bash
dotnet publish ./markdown-journal-cli/markdown-journal-cli.csproj \
  -c Release \
  --no-self-contained \
  -p:PublishSingleFile=false \
  -o ./publish/framework-dependent
```

This is **smaller** (~100 KB vs ~60 MB) but requires the target machine to have the
correct .NET SDK/Runtime installed. Useful as a "developer" distribution alongside the
nupkg global tool artifact.

### NuGet Global Tool Pack

```bash
dotnet pack ./markdown-journal-cli/markdown-journal-cli.csproj \
  -c Release \
  /p:Version=${VERSION} \
  --output ./artifacts/nupkg
```

Requires `<PackAsTool>true</PackAsTool>` in the csproj (see above). The output is a
`.nupkg` that can be installed with:

```bash
dotnet tool install --global markdown-journal --version 1.2.3
```

---

## `softprops/action-gh-release` — Key Inputs

Source: [`softprops/action-gh-release` action.yml](https://github.com/softprops/action-gh-release/blob/master/action.yml)

| Input | Default | Notes |
|-------|---------|-------|
| `files` | — | Newline-delimited glob patterns for assets to upload |
| `name` | tag name | Human-readable release title |
| `body` | — | Markdown release notes inline |
| `body_path` | — | Path to a markdown file for release notes |
| `generate_release_notes` | false | Auto-generate notes from merged PRs since last tag |
| `draft` | false | Keep as draft (don't publish immediately) |
| `prerelease` | false | Mark as pre-release |
| `token` | `github.token` | Override default token; needed for cross-repo releases |
| `fail_on_unmatched_files` | false | Fail if a glob matches nothing |
| `overwrite_files` | true | Overwrite existing assets with same name |
| `make_latest` | — | `true`/`false`/`legacy` to control "Latest" badge |
| `tag_name` | `github.ref_name` | Explicit tag name override |

**Minimal release step:**

```yaml
- name: Create GitHub Release
  uses: softprops/action-gh-release@v2
  with:
    generate_release_notes: true
    fail_on_unmatched_files: true
    files: |
      ./artifacts/**/*
```

---

## Complete Workflow YAML

Save this file to `.github/workflows/release.yml`.

```yaml
# .github/workflows/release.yml
# ─────────────────────────────────────────────────────────────────────────────
# CI + Release pipeline for markdown-journal-cli
#
# Triggers:
#   • Every push / PR  → build + test only
#   • Push of v* tag   → build + test + publish platform binaries + GitHub Release
#
# Release assets created on a v* tag:
#   markdown-journal-win-x64.exe
#   markdown-journal-osx-x64
#   markdown-journal-osx-arm64
#   markdown-journal-linux-x64
#   markdown-journal-linux-arm64
#   markdown-journal.<version>.nupkg   (dotnet global tool)
# ─────────────────────────────────────────────────────────────────────────────

name: CI & Release

on:
  push:
    branches: ["**"]
    tags: ["v*"]
  pull_request:
    branches: ["main"]

env:
  DOTNET_SKIP_FIRST_TIME_EXPERIENCE: true
  DOTNET_CLI_TELEMETRY_OPTOUT: true
  DOTNET_NOLOGO: true
  PROJECT: markdown-journal-cli/markdown-journal-cli.csproj
  TEST_PROJECT: markdown-journal-cli.Tests/markdown-journal-cli.Tests.csproj

# ─────────────────────────────────────────────────────────────────────────────
# JOB 1: Build & Test (runs on every push/PR)
# ─────────────────────────────────────────────────────────────────────────────
jobs:
  ci:
    name: Build & Test
    runs-on: ubuntu-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "9.0.x"

      - name: Restore dependencies
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore -c Release

      - name: Test
        run: dotnet test --no-build -c Release --logger "console;verbosity=normal"

# ─────────────────────────────────────────────────────────────────────────────
# JOB 2: Publish platform binaries (only on v* tags)
# ─────────────────────────────────────────────────────────────────────────────
  publish:
    name: Publish / ${{ matrix.rid }}
    if: startsWith(github.ref, 'refs/tags/v')
    needs: ci
    strategy:
      fail-fast: false
      matrix:
        include:
          # Windows x64 – must run on Windows to produce a valid .exe
          - rid: win-x64
            os: windows-latest
            binary: markdown-journal-win-x64.exe
            output_binary: markdown-journal-cli.exe

          # macOS Intel – macos-13 is the last x64 runner
          - rid: osx-x64
            os: macos-13
            binary: markdown-journal-osx-x64
            output_binary: markdown-journal-cli

          # macOS Apple Silicon – macos-latest is arm64 as of macOS 14+
          - rid: osx-arm64
            os: macos-latest
            binary: markdown-journal-osx-arm64
            output_binary: markdown-journal-cli

          # Linux x64
          - rid: linux-x64
            os: ubuntu-latest
            binary: markdown-journal-linux-x64
            output_binary: markdown-journal-cli

          # Linux arm64 – cross-compiled from x64; dotnet supports this natively
          - rid: linux-arm64
            os: ubuntu-latest
            binary: markdown-journal-linux-arm64
            output_binary: markdown-journal-cli

    runs-on: ${{ matrix.os }}
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "9.0.x"

      # Extract semver from the tag (strips the leading 'v')
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

      # Rename the binary to the platform-specific asset name
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
          retention-days: 1   # only needed for the release job; don't clutter storage

# ─────────────────────────────────────────────────────────────────────────────
# JOB 3: Pack .nupkg + Create GitHub Release (only on v* tags)
# ─────────────────────────────────────────────────────────────────────────────
  release:
    name: Create GitHub Release
    if: startsWith(github.ref, 'refs/tags/v')
    needs: publish
    runs-on: ubuntu-latest
    permissions:
      contents: write   # required by softprops/action-gh-release
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "9.0.x"

      - name: Extract version
        id: version
        run: echo "version=${GITHUB_REF_NAME#v}" >> "$GITHUB_OUTPUT"

      # ── Download all platform binaries produced by the matrix ──
      - name: Download all platform artifacts
        uses: actions/download-artifact@v4
        with:
          pattern: binary-*
          merge-multiple: true
          path: ./artifacts/binaries

      # ── Pack NuGet global tool ──
      - name: Pack NuGet global tool (.nupkg)
        run: |
          dotnet pack "${{ env.PROJECT }}" \
            -c Release \
            /p:Version=${{ steps.version.outputs.version }} \
            --output ./artifacts/nupkg

      # ── List what we're about to upload (useful in run logs) ──
      - name: List artifacts
        run: ls -lh ./artifacts/binaries ./artifacts/nupkg

      # ── Create Release and upload all assets ──
      - name: Create GitHub Release
        uses: softprops/action-gh-release@v2
        with:
          name: "v${{ steps.version.outputs.version }}"
          generate_release_notes: true   # auto-generates notes from PR titles since last tag
          fail_on_unmatched_files: true
          make_latest: true
          files: |
            ./artifacts/binaries/markdown-journal-win-x64.exe
            ./artifacts/binaries/markdown-journal-osx-x64
            ./artifacts/binaries/markdown-journal-osx-arm64
            ./artifacts/binaries/markdown-journal-linux-x64
            ./artifacts/binaries/markdown-journal-linux-arm64
            ./artifacts/nupkg/*.nupkg
```

---

## Workflow Walkthrough

### Trigger Strategy

```yaml
on:
  push:
    branches: ["**"]   # CI runs on all pushes
    tags: ["v*"]       # Release runs on v1.2.3, v2.0.0-beta.1, etc.
  pull_request:
    branches: ["main"]
```

The `if: startsWith(github.ref, 'refs/tags/v')` guard on the `publish` and `release`
jobs ensures those expensive jobs **only run on tagged releases**, not on every
branch push.

### Tag-based versioning

The version is extracted from the git tag with:

```bash
echo "version=${GITHUB_REF_NAME#v}" >> "$GITHUB_OUTPUT"
# e.g. tag "v1.2.3" → version "1.2.3"
```

This is then passed to `dotnet publish` and `dotnet pack` via `/p:Version=`.
No need to hard-code versions in the `.csproj`.

### The artifact handoff pattern

The matrix `publish` jobs each produce **one file** and upload it with a unique
artifact name (`binary-win-x64`, `binary-linux-arm64`, etc.).

The `release` job uses `actions/download-artifact@v4` with `pattern: binary-*` and
`merge-multiple: true` to download all five files into a single flat directory.
This pattern is used in
[ArchiSteamFarm](https://github.com/JustArchiNET/ArchiSteamFarm/blob/main/.github/workflows/publish.yml)
and avoids the complexity of `needs` output variable passing.

### macOS Runner Selection

| RID | Runner | Reason |
|-----|--------|--------|
| `osx-x64` | `macos-13` | The last GitHub-hosted Intel macOS runner |
| `osx-arm64` | `macos-latest` | Points to `macos-15` (Apple Silicon) as of 2024 |

Both can _cross-compile_ for the other architecture with
`dotnet publish -r osx-x64` / `dotnet publish -r osx-arm64`. Running on the native
arch avoids any runner-to-RID mismatch surprises.

### Linux arm64 Cross-Compilation

Linux arm64 publishes from `ubuntu-latest` (x64) via:

```bash
dotnet publish -r linux-arm64 --self-contained true
```

The .NET SDK bundles the arm64 runtime pack and cross-compiles without any extra
QEMU/cross-compiler setup. This is well-tested and reliable as of .NET 6+.

---

## Global Tool vs Self-Contained: When to Use Which

| Distribution method | Install command | Requires .NET? | Binary size |
|--------------------|----------------|---------------|-------------|
| Self-contained single-file | Download from Releases | ❌ No | ~60–80 MB |
| `dotnet tool install -g` | `dotnet tool install -g markdown-journal` | ✅ Yes | ~100 KB .nupkg |
| Framework-dependent binary | Download from Releases | ✅ Yes | ~1–3 MB |

**Recommendation for `markdown-journal-cli`:** Ship all three. The nupkg is for
developers who already have .NET installed and want `dotnet tool update` support.
The self-contained binaries are for CI/CD environments or users who just want to
download and run.

---

## Optional Enhancements

### Add NuGet.org Publishing

Add to the `release` job after the GitHub Release step:

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

Store `NUGET_API_KEY` in **Settings → Secrets → Actions**.

### Add SHA-256 checksums (security best practice)

```yaml
      - name: Generate SHA256 checksums
        working-directory: ./artifacts/binaries
        run: |
          sha256sum * > SHA256SUMS.txt
          cat SHA256SUMS.txt

      # Then add SHA256SUMS.txt to the `files:` list in action-gh-release
```

### Pre-release detection

Detect pre-release tags (e.g. `v1.0.0-beta.1`) automatically:

```yaml
      - name: Detect pre-release
        id: prerelease
        run: |
          if [[ "${GITHUB_REF_NAME}" =~ -[a-zA-Z] ]]; then
            echo "is_prerelease=true" >> "$GITHUB_OUTPUT"
          else
            echo "is_prerelease=false" >> "$GITHUB_OUTPUT"
          fi

      - name: Create GitHub Release
        uses: softprops/action-gh-release@v2
        with:
          prerelease: ${{ steps.prerelease.outputs.is_prerelease }}
          # ... rest of inputs
```

### Separate CI and Release workflows

If the combined file gets unwieldy, split into two files:

- `.github/workflows/ci.yml` — triggers on all pushes, runs build+test
- `.github/workflows/release.yml` — triggers on `v*` tags only, runs full pipeline

---

## Real-World Reference Workflows

| Project | Workflow | Pattern used |
|---------|----------|-------------|
| [JustArchiNET/ArchiSteamFarm](https://github.com/JustArchiNET/ArchiSteamFarm) | [publish.yml](https://github.com/JustArchiNET/ArchiSteamFarm/blob/main/.github/workflows/publish.yml) | Matrix publish → artifact handoff → release job; `ncipollo/release-action` |
| [dotnet-outdated/dotnet-outdated](https://github.com/dotnet-outdated/dotnet-outdated) | [release.yml](https://github.com/dotnet-outdated/dotnet-outdated/blob/main/.github/workflows/release.yml) | `workflow_dispatch`, `dotnet pack`, `dotnet nuget push`, `actions/create-release` |
| [filipw/dotnet-script](https://github.com/filipw/dotnet-script) | [main.yml](https://github.com/filipw/dotnet-script/blob/main/.github/workflows/main.yml) | C# build scripts, matrix OS runners, GitHub release via Octokit |
| [spectreconsole/spectre.console](https://github.com/spectreconsole/spectre.console) | [publish.yaml](https://github.com/spectreconsole/spectre.console/blob/main/.github/workflows/publish.yaml) | `dotnet make publish` via Cake, tag + main branch triggers |
| [softprops/action-gh-release](https://github.com/softprops/action-gh-release) | [action.yml](https://github.com/softprops/action-gh-release/blob/master/action.yml) | Reference for all available inputs |

---

## `permissions` Block Reference

The `contents: write` permission is required by `softprops/action-gh-release` to
create the release and upload assets. GitHub's default permissions are read-only if
the repo has `Settings → Actions → Workflow permissions` set to "Read repository
contents and packages permissions."

```yaml
permissions:
  contents: write   # create releases, upload assets
```

Set this on the **`release` job** only, not globally, to follow least-privilege.

---

## Confidence Assessment

| Finding | Confidence | Basis |
|---------|-----------|-------|
| `dotnet publish` flags for self-contained single-file | **High** | Official .NET docs + ArchiSteamFarm production workflow |
| `softprops/action-gh-release` input names | **High** | Fetched from `action.yml` source directly |
| artifact handoff pattern (upload/download between jobs) | **High** | Used verbatim in ArchiSteamFarm |
| macOS runner names (`macos-13` for x64, `macos-latest` for arm64) | **High** | GitHub-hosted runner documentation (confirmed as of 2025) |
| Linux arm64 cross-compilation working from x64 runner | **High** | .NET 6+ SDK documented cross-compile support |
| `PublishTrimmed` warning for Spectre.Console | **Medium** | Known pattern; specific root descriptor requirements depend on app |
| `EnableCompressionInSingleFile` compression ratio estimates | **Medium** | Approximate from community benchmarks |

---

## Footnotes

[^1]: ArchiSteamFarm `publish.yml` — matrix publish with `dotnet publish -p:PublishSingleFile=true -p:PublishTrimmed=true -r $VARIANT --self-contained`: [`JustArchiNET/ArchiSteamFarm/.github/workflows/publish.yml`](https://github.com/JustArchiNET/ArchiSteamFarm/blob/main/.github/workflows/publish.yml)

[^2]: dotnet-outdated `release.yml` — `dotnet pack /p:Version=$VERSION`, `dotnet nuget push`, `actions/create-release`, `actions/upload-release-asset`: [`dotnet-outdated/dotnet-outdated/.github/workflows/release.yml`](https://github.com/dotnet-outdated/dotnet-outdated/blob/main/.github/workflows/release.yml)

[^3]: dotnet-script `main.yml` — multi-OS matrix CI (`ubuntu-latest`, `macos-26`, `windows-latest`): [`dotnet-script/dotnet-script/.github/workflows/main.yml`](https://github.com/dotnet-script/dotnet-script/blob/main/.github/workflows/main.yml)

[^4]: dotnet-script `Build.csx` — shows `DotNet.Publish()`, `CreateNuGetPackages()`, `CreateGlobalToolPackage()` (patching `PackAsTool`, `PackageId`), and `ReleaseManagerFor().CreateRelease()` with zip asset: [`dotnet-script/dotnet-script/build/Build.csx`](https://github.com/filipw/dotnet-script/blob/main/build/Build.csx)

[^5]: spectre.console `publish.yaml` — tag + main branch trigger, `dotnet make publish --nuget-key=`: [`spectreconsole/spectre.console/.github/workflows/publish.yaml`](https://github.com/spectreconsole/spectre.console/blob/main/.github/workflows/publish.yaml)

[^6]: `softprops/action-gh-release` action.yml — all supported inputs (`files`, `generate_release_notes`, `fail_on_unmatched_files`, `make_latest`, `prerelease`, etc.): [`softprops/action-gh-release/action.yml`](https://github.com/softprops/action-gh-release/blob/master/action.yml)

[^7]: `markdown-journal-cli.csproj` — current state (no `PackAsTool`, no `Version`, no `PackageId`): [`markdown-journal-cli/markdown-journal-cli.csproj`](/Users/collinrobison/Repos/markdown-journal-cli/markdown-journal-cli/markdown-journal-cli.csproj)
