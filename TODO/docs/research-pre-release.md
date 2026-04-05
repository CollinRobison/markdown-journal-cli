# Pre-Release Research: markdown-journal-cli

> Research for a .NET 9 CLI tool using Spectre.Console.Cli (v0.50.0) with `CommandApp` + `TypeRegistrar` DI.
> Topics: version flag, global tool packaging, CI/CD pipeline, open-source repo setup, and README/docs best practices.

---

## Table of Contents

1. [Task 1 — `--version` flag in Spectre.Console.Cli](#task-1----version-flag-in-spectreconsolecli)
2. [Task 2 — .NET Global Tool Packaging](#task-2----net-global-tool-packaging)
3. [Task 3 — GitHub Actions CI/CD Release Pipeline](#task-3----github-actions-cicd-release-pipeline)
4. [Task 4 — Open Source Repository Setup](#task-4----open-source-repository-setup)
5. [Task 5 — README and Documentation Best Practices](#task-5----readme-and-documentation-best-practices)

---

## Task 1 — `--version` flag in Spectre.Console.Cli

### Executive Summary

Spectre.Console.Cli has **built-in version support** via `config.SetApplicationVersion(string)` on the `IConfigurator`. When set, users can invoke `--version` or `-v` at the top-level to get the application version. The version string should be sourced from `AssemblyInformationalVersionAttribute`, which is automatically populated from `<Version>` in the `.csproj` (including any SemVer pre-release suffixes). There is also a built-in `cli version` sub-command. **Do not** use `Assembly.GetName().Version` for display — it strips pre-release suffixes.

### Key Finding 1: The `SetApplicationVersion` API

`SetApplicationVersion` is a method on `IConfigurator`, the object passed into `app.Configure(config => { })`:

```csharp
var app = new CommandApp();

app.Configure(config =>
{
    config.SetApplicationName("markdown-journal");
    config.SetApplicationVersion("1.2.3");   // ← this is the API

    config.AddCommand<NewEntryCommand>("new")
        .WithDescription("Create a new journal entry");
});

return await app.RunAsync(args);
```

**What it does:**
- Registers a global `--version` / `-v` option at the top level.
- Registers a hidden `cli version` sub-command.
- Outputs the version string you provide when `--version` is passed.

**Source:** [spectreconsole.net — Configuring CommandApp and Commands](https://spectreconsole.net/cli/how-to/configuring-commandapp-and-commands)[^1]  
**Built-in behaviors reference:** [spectreconsole.net — Built-in Command Behaviors](https://spectreconsole.net/cli/reference/built-in-command-behaviors)[^2]

### Key Finding 2: Built-in `cli version` Behavior

When `SetApplicationVersion` is configured, Spectre.Console.Cli also exposes:

| Access Pattern | Description |
|---|---|
| `myapp --version` | Global option — displays the configured version and exits |
| `myapp -v` | Short alias for `--version` |
| `myapp cli version` | Hidden sub-command that shows library/app versions |
| `myapp cli explain` | Diagnostic tree view of all registered commands |
| `myapp cli xmldoc` | Generates XML documentation from CLI config |

> **Note:** The `-v` short form only works for version if no command has claimed `-v` as a verbose flag. If you define `-v|--verbose` on any command or a root interceptor, there will be a conflict. Use a different short form for verbose (`--verbose` only) if you want `--version` + `-v`.

**Source:** [spectreconsole.net — Built-in Command Behaviors](https://spectreconsole.net/cli/reference/built-in-command-behaviors)[^2]

### Key Finding 3: Reading the Version from Assembly Attributes

**Never** hardcode the version string. Instead, read it at startup from `AssemblyInformationalVersionAttribute`:

```csharp
using System.Reflection;

var version = Assembly.GetExecutingAssembly()
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
    .InformationalVersion
    ?? "unknown";

var app = new CommandApp();
app.Configure(config =>
{
    config.SetApplicationName("markdown-journal");
    config.SetApplicationVersion(version);
    // ... register commands
});

return await app.RunAsync(args);
```

**Why `AssemblyInformationalVersionAttribute` over `Assembly.GetName().Version`:**

| Attribute | Source | Example Value |
|---|---|---|
| `Assembly.GetName().Version` | `<AssemblyVersion>` in csproj | `1.2.3.0` (strips pre-release) |
| `AssemblyFileVersionAttribute` | `<FileVersion>` in csproj | `1.2.3.0` (strips pre-release) |
| `AssemblyInformationalVersionAttribute` | `<Version>` / `<InformationalVersion>` | `1.2.3-beta.1+abc123` ✅ |

`AssemblyVersion` and `FileVersion` default to the numeric portion of `$(Version)` without suffix. `InformationalVersion` defaults to the full `$(Version)` value, including pre-release identifiers and, in .NET 8+, the Source Link commit SHA suffix.[^3]

**Microsoft official docs example:**
```csharp
// From Microsoft's official .NET global tool how-to guide
var versionString = Assembly.GetEntryAssembly()?
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
    .InformationalVersion
    .ToString();

Console.WriteLine($"botsay v{versionString}");
```

**Source:** [learn.microsoft.com — How to create a .NET tool](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools-how-to-create)[^4]

### Key Finding 4: The `<Version>` → `AssemblyInformationalVersionAttribute` Pipeline

```
.csproj:
  <Version>1.2.3-beta.1</Version>
       │
       ▼ (GenerateAssemblyInfo = true, the default)
auto-generated AssemblyInfo.cs:
  [assembly: AssemblyInformationalVersion("1.2.3-beta.1+<git-sha>")]
  [assembly: AssemblyVersion("1.2.3")]
  [assembly: AssemblyFileVersion("1.2.3")]
       │
       ▼
Assembly.GetExecutingAssembly()
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
    .InformationalVersion
  → "1.2.3-beta.1+<git-sha>"
```

**MSBuild property → Assembly attribute mapping:**

| MSBuild Property | Assembly Attribute | Notes |
|---|---|---|
| `<Version>` / `<InformationalVersion>` | `AssemblyInformationalVersionAttribute` | Full SemVer with pre-release suffix. In .NET 8+ SDK: also appends git commit SHA via Source Link. |
| `<Version>` | `AssemblyVersion` | Numeric part only (`1.2.3.0`). |
| `<FileVersion>` | `AssemblyFileVersionAttribute` | Numeric part only (`1.2.3.0`). |

**Source:** [learn.microsoft.com — Set attributes from the project file](https://learn.microsoft.com/en-us/dotnet/standard/assembly/set-attributes-project-file)[^3]

> **Tip:** To suppress the automatic git SHA suffix in .NET 8+ while keeping SemVer pre-release labels, add to your `.csproj`:
> ```xml
> <IncludeSourceRevisionInInformationalVersion>false</IncludeSourceRevisionInInformationalVersion>
> ```

### Recommended Approach

```csharp
// Program.cs
using System.Reflection;
using Spectre.Console.Cli;

var version = Assembly.GetExecutingAssembly()
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
    .InformationalVersion
    ?? "0.0.0";

var registrar = new TypeRegistrar(/* your DI container */);
var app = new CommandApp(registrar);

app.Configure(config =>
{
    config.SetApplicationName("mdj");
    config.SetApplicationVersion(version);

    config.AddCommand<NewEntryCommand>("new")
        .WithDescription("Create a new journal entry");
    // ...

#if DEBUG
    config.PropagateExceptions();
    config.ValidateExamples();
#endif
});

return await app.RunAsync(args);
```

And in `.csproj`:
```xml
<PropertyGroup>
  <Version>1.0.0</Version>
  <!-- Optional: suppress git-sha suffix from InformationalVersion -->
  <IncludeSourceRevisionInInformationalVersion>false</IncludeSourceRevisionInInformationalVersion>
</PropertyGroup>
```

---

## Task 2 — .NET Global Tool Packaging

### Executive Summary

A .NET global tool requires three mandatory `.csproj` properties: `<PackAsTool>true</PackAsTool>`, `<ToolCommandName>`, and `<PackageId>`. The workflow is `dotnet pack` → upload `.nupkg` → users install with `dotnet tool install -g`. Tools can be installed directly from a local directory or a URL (not a raw GitHub URL — but GitHub Releases `.nupkg` works via `--add-source`). Publishing to NuGet.org is strongly recommended for discoverability.

### Required `.csproj` Properties

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>

    <!-- ─── Global Tool packaging ─── -->
    <PackAsTool>true</PackAsTool>
    <ToolCommandName>mdj</ToolCommandName>          <!-- the CLI command users invoke -->

    <!-- ─── NuGet package metadata ─── -->
    <PackageId>markdown-journal-cli</PackageId>     <!-- unique NuGet package ID -->
    <Version>1.0.0</Version>
    <Authors>Your Name</Authors>
    <Description>A markdown journal CLI tool.</Description>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageProjectUrl>https://github.com/you/markdown-journal-cli</PackageProjectUrl>
    <RepositoryUrl>https://github.com/you/markdown-journal-cli.git</RepositoryUrl>
    <RepositoryType>git</RepositoryType>

    <!-- ─── Optional but recommended ─── -->
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <PackageTags>journal;markdown;cli;notes</PackageTags>
    <PackageOutputPath>./nupkg</PackageOutputPath>
  </PropertyGroup>

  <!-- Include README in package -->
  <ItemGroup>
    <None Include="..\..\README.md" Pack="true" PackagePath="\"/>
  </ItemGroup>
</Project>
```

**Real-world reference:** [`dotnet-outdated`'s `.csproj`](https://github.com/dotnet-outdated/dotnet-outdated/blob/main/src/DotNetOutdated/DotNetOutdated.csproj)[^5]

### Property Reference

| Property | Required? | Description |
|---|---|---|
| `<PackAsTool>true</PackAsTool>` | **Yes** | Signals this is a .NET tool package |
| `<ToolCommandName>` | Recommended | Command users type (e.g., `mdj`). Defaults to assembly name. No file extensions. |
| `<PackageId>` | **Yes** | Unique NuGet ID. Convention: `dotnet-{name}` or kebab-case project name |
| `<Version>` | **Yes** | SemVer string, also sets `AssemblyInformationalVersion` |
| `<Authors>` | Yes for NuGet | Shows on nuget.org author listing |
| `<Description>` | Strongly recommended | Shown in NuGet search and `dotnet tool search` |
| `<PackageLicenseExpression>` | Required for NuGet.org | SPDX identifier: `MIT`, `Apache-2.0`, etc. |
| `<RepositoryUrl>` | Strongly recommended | Links NuGet page to GitHub repo |
| `<PackageReadmeFile>` | Optional | Shows README on nuget.org package page |

**Source:** [Microsoft Docs — Create a .NET Tool](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools-how-to-create)[^4]

### `<ToolCommandName>` Naming Best Practices

- **Use lowercase kebab-case**: `mdj`, `markdown-journal`, `dotnet-outdated`
- **Avoid `.exe`, `.cmd` extensions** — tools are installed as app host shims
- **Avoid conflicts with system commands**: check `which <name>` on Linux/macOS
- **Convention for tools meant to supplement `dotnet`**: prefix with `dotnet-` (e.g., `dotnet-outdated`, `dotnet-format`). This allows invocation as `dotnet outdated` (without the prefix) because the .NET CLI tool resolver finds `dotnet-*` executables on `PATH`.

### The Pack + Install Workflow

```bash
# 1. Pack the NuGet package
dotnet pack --configuration Release --output ./nupkg

# 2a. Install globally from NuGet.org (after publishing)
dotnet tool install -g markdown-journal-cli

# 2b. Install from local .nupkg file (for testing)
dotnet tool install -g markdown-journal-cli \
  --add-source ./nupkg \
  --version 1.0.0

# 2c. Install from a GitHub Releases .nupkg via --add-source
#     GitHub Releases serve .nupkg as static files — NOT a NuGet feed
#     You must download the file first, or host a proper NuGet feed.
#
#     Alternative: use the GitHub Packages NuGet feed
dotnet tool install -g markdown-journal-cli \
  --add-source https://nuget.pkg.github.com/YOUR_ORG/index.json \
  --version 1.0.0

# 3. Update an installed tool
dotnet tool update -g markdown-journal-cli

# 4. Uninstall
dotnet tool uninstall -g markdown-journal-cli

# 5. List installed global tools
dotnet tool list -g
```

**Source:** [learn.microsoft.com — dotnet tool install](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-tool-install)[^6]

### Distribution Options Comparison

| Distribution Method | Discoverability | Auth Required | Best For |
|---|---|---|---|
| **NuGet.org** | High (public) | API key | Public open-source tools |
| **GitHub Packages** | Medium (org-scoped) | GitHub PAT | Organization-internal tools |
| **Local `.nupkg`** | None | None | Testing, CI/CD artifact |
| **GitHub Releases `.nupkg`** | Requires manual download | None | Direct download users |
| **Custom NuGet server** | Low | Configurable | Enterprise / self-hosted |

> **Key constraint:** `dotnet tool install --add-source <URL>` expects a **NuGet v3 feed** (`index.json`), **not** a raw file URL. GitHub Releases are raw file downloads — users must either download the `.nupkg` and install from the local path, or you must set up a proper NuGet feed (GitHub Packages, BaGet, etc.).

### Installing from GitHub Releases (for users without NuGet.org access)

```bash
# Option A: Download and install locally
curl -L -o mdj.nupkg \
  https://github.com/you/markdown-journal-cli/releases/download/v1.0.0/markdown-journal-cli.1.0.0.nupkg

dotnet tool install -g markdown-journal-cli \
  --add-source /path/to/folder-containing-nupkg \
  --version 1.0.0

# Option B: Host via GitHub Packages (requires authentication)
dotnet nuget add source \
  --username YOUR_GITHUB_USERNAME \
  --password YOUR_GITHUB_PAT \
  --store-password-in-clear-text \
  --name github \
  "https://nuget.pkg.github.com/YOUR_ORG/index.json"

dotnet tool install -g markdown-journal-cli --add-source github
```

### Real-World Examples

| Tool | NuGet ID | `ToolCommandName` | Source |
|---|---|---|---|
| `dotnet-outdated` | `dotnet-outdated-tool` | `dotnet-outdated` | [GitHub](https://github.com/dotnet-outdated/dotnet-outdated) |
| `dotnet-script` | `dotnet-script` | `dotnet-script` | [GitHub](https://github.com/dotnet-script/dotnet-script) |
| `docfx` | `docfx` | `docfx` | [GitHub](https://github.com/dotnet/docfx) |

---

## Task 3 — GitHub Actions CI/CD Release Pipeline

### Executive Summary

The recommended approach uses two separate workflows: (1) a CI workflow triggered on every push/PR that builds and tests, and (2) a release workflow triggered on tag push (`v*`) that packs, publishes self-contained binaries, and creates a GitHub Release using `softprops/action-gh-release`. The best real-world reference for a .NET CLI tool with both NuGet and self-contained binaries is `dotnet/docfx`.

### Workflow Architecture

```
┌─────────────────────────────────────────────────────┐
│  .github/workflows/                                 │
│                                                     │
│  ci.yml        ─── Trigger: push, pull_request      │
│  │                 Jobs: build, test                 │
│                                                     │
│  release.yml   ─── Trigger: push tags v*            │
│                    Jobs:                            │
│                      1. build-and-pack (nupkg)      │
│                      2. publish-binaries             │
│                         (win-x64, osx-x64,          │
│                          osx-arm64, linux-x64,       │
│                          linux-arm64)                │
│                      3. create-release               │
│                         (softprops/action-gh-release)│
└─────────────────────────────────────────────────────┘
```

### CI Workflow (`.github/workflows/ci.yml`)

```yaml
name: CI

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]

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
        run: dotnet test --no-build --configuration Release --verbosity normal
```

### Release Workflow (`.github/workflows/release.yml`)

This is the critical workflow. Based on the real `dotnet/docfx` release workflow[^7] and `softprops/action-gh-release`[^8]:

```yaml
name: Release

on:
  push:
    tags:
      - 'v*.*.*'

permissions:
  contents: write   # Required to create GitHub Releases

env:
  DOTNET_SKIP_FIRST_TIME_EXPERIENCE: true
  DOTNET_CLI_TELEMETRY_OPTOUT: true
  DOTNET_NOLOGO: true

jobs:
  release:
    name: Build & Release
    runs-on: ubuntu-latest

    steps:
      - name: Checkout
        uses: actions/checkout@v4
        with:
          fetch-depth: 0   # Needed for git history / version stamping

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 9.0.x

      - name: Restore
        run: dotnet restore

      - name: Test
        run: dotnet test --configuration Release --verbosity normal

      # ─── Extract version from tag (strips leading 'v') ───
      - name: Get version
        id: version
        run: echo "VERSION=${GITHUB_REF_NAME#v}" >> $GITHUB_OUTPUT

      # ─── Pack NuGet package (for global tool install) ───
      - name: Pack NuGet
        run: |
          dotnet pack \
            --configuration Release \
            /p:Version=${{ steps.version.outputs.VERSION }} \
            --output ./drop/nupkg

      # ─── Publish self-contained single-file binaries ───
      - name: Publish win-x64
        run: |
          dotnet publish src/markdown-journal-cli \
            --configuration Release \
            --runtime win-x64 \
            --self-contained true \
            -p:PublishSingleFile=true \
            -p:Version=${{ steps.version.outputs.VERSION }} \
            --output ./drop/publish/win-x64

      - name: Publish osx-x64
        run: |
          dotnet publish src/markdown-journal-cli \
            --configuration Release \
            --runtime osx-x64 \
            --self-contained true \
            -p:PublishSingleFile=true \
            -p:Version=${{ steps.version.outputs.VERSION }} \
            --output ./drop/publish/osx-x64

      - name: Publish osx-arm64
        run: |
          dotnet publish src/markdown-journal-cli \
            --configuration Release \
            --runtime osx-arm64 \
            --self-contained true \
            -p:PublishSingleFile=true \
            -p:Version=${{ steps.version.outputs.VERSION }} \
            --output ./drop/publish/osx-arm64

      - name: Publish linux-x64
        run: |
          dotnet publish src/markdown-journal-cli \
            --configuration Release \
            --runtime linux-x64 \
            --self-contained true \
            -p:PublishSingleFile=true \
            -p:Version=${{ steps.version.outputs.VERSION }} \
            --output ./drop/publish/linux-x64

      - name: Publish linux-arm64
        run: |
          dotnet publish src/markdown-journal-cli \
            --configuration Release \
            --runtime linux-arm64 \
            --self-contained true \
            -p:PublishSingleFile=true \
            -p:Version=${{ steps.version.outputs.VERSION }} \
            --output ./drop/publish/linux-arm64

      # ─── Zip platform binaries ───
      - name: Zip binaries
        run: |
          mkdir -p drop/bin
          TAG=${{ github.ref_name }}
          zip -j drop/bin/mdj-win-x64-${TAG}.zip drop/publish/win-x64/mdj.exe
          zip -j drop/bin/mdj-osx-x64-${TAG}.zip drop/publish/osx-x64/mdj
          zip -j drop/bin/mdj-osx-arm64-${TAG}.zip drop/publish/osx-arm64/mdj
          zip -j drop/bin/mdj-linux-x64-${TAG}.zip drop/publish/linux-x64/mdj
          zip -j drop/bin/mdj-linux-arm64-${TAG}.zip drop/publish/linux-arm64/mdj

      # ─── Publish to NuGet.org ───
      - name: Push to NuGet.org
        run: |
          dotnet nuget push drop/nupkg/*.nupkg \
            --api-key ${{ secrets.NUGET_API_KEY }} \
            --source https://api.nuget.org/v3/index.json \
            --skip-duplicate

      # ─── Create GitHub Release + upload all assets ───
      - name: Create GitHub Release
        uses: softprops/action-gh-release@v2
        with:
          generate_release_notes: true
          files: |
            drop/nupkg/*.nupkg
            drop/bin/*.zip
```

### `dotnet publish` Flags Reference

| Flag | Purpose |
|---|---|
| `--runtime <RID>` | Target runtime identifier (win-x64, osx-x64, osx-arm64, linux-x64, linux-arm64) |
| `--self-contained true` | Bundle .NET runtime in binary (no .NET SDK required on user machine) |
| `-p:PublishSingleFile=true` | Bundle all DLLs into one executable |
| `-p:PublishTrimmed=true` | *(Optional)* Tree-shake unused assemblies (reduces size) |
| `-p:EnableCompressionInSingleFile=true` | *(Optional)* Compress embedded files (slower startup, smaller binary) |
| `-p:Version=...` | Override the version at build time |
| `-c Release` | Release build configuration |

### Real-World Reference: `dotnet/docfx` release.yml

The `dotnet/docfx` project provides a near-perfect example[^7]:

```yaml
# Actual docfx release.yml excerpt
- name: dotnet publish
  run: |
    dotnet publish src/docfx -f net10.0 -c Release \
      /p:Version=${GITHUB_REF_NAME#v} \
      --self-contained -r win-x64 -o drop/publish/win-x64
    dotnet publish src/docfx -f net10.0 -c Release \
      /p:Version=${GITHUB_REF_NAME#v} \
      --self-contained -r linux-x64 -o drop/publish/linux-x64
    dotnet publish src/docfx -f net10.0 -c Release \
      /p:Version=${GITHUB_REF_NAME#v} \
      --self-contained -r osx-x64 -o drop/publish/osx-x64
```

### `softprops/action-gh-release` Key Parameters

```yaml
- uses: softprops/action-gh-release@v2
  with:
    # Release body/notes
    body: "Release notes here"            # inline text
    body_path: CHANGELOG.md              # OR from file
    generate_release_notes: true         # OR auto-generate from git log

    # Asset upload
    files: |
      drop/nupkg/*.nupkg
      drop/bin/*.zip

    # Metadata
    name: "v${{ steps.version.outputs.VERSION }}"  # release title
    draft: false
    prerelease: false
    make_latest: true

    # Permission: needs 'permissions: contents: write' at job level
```

**Permissions required:**
```yaml
permissions:
  contents: write  # To create releases and upload assets
```

**Source:** [github.com/softprops/action-gh-release](https://github.com/softprops/action-gh-release)[^8]

### Workflow for Tag-Triggered Releases

```bash
# Developer workflow to trigger a release:
git tag v1.0.0
git push origin v1.0.0
# → release.yml triggers automatically
```

### Version Extraction from Tag

```bash
# In GitHub Actions, GITHUB_REF_NAME = "v1.2.3" for tag pushes
# Strip the leading 'v':
VERSION=${GITHUB_REF_NAME#v}    # → "1.2.3"

# Or using GitHub output:
echo "VERSION=${GITHUB_REF_NAME#v}" >> $GITHUB_OUTPUT
```

### `--no-self-contained` Alternative (Framework-Dependent)

For users who prefer the smaller NuGet global tool approach (they install once, works everywhere with .NET installed):

```bash
# Framework-dependent (requires .NET 9 on user machine, much smaller binary)
dotnet publish --configuration Release \
  --runtime win-x64 \
  --no-self-contained \
  -p:PublishSingleFile=true
```

> **Recommendation:** Provide **both**: the NuGet global tool package (framework-dependent, small, works via `dotnet tool install`) AND self-contained binaries for users who don't have .NET installed.

---

## Task 4 — Open Source Repository Setup

### Executive Summary

Most .NET open-source tools (including Spectre.Console) use the **MIT license**. GitHub's community health score checks for: README, LICENSE, CODE_OF_CONDUCT, CONTRIBUTING, ISSUE_TEMPLATE(s), and PULL_REQUEST_TEMPLATE. The Contributor Covenant v2.1 is the de-facto standard for CODE_OF_CONDUCT in the .NET ecosystem. Branch protection with required reviews + CODEOWNERS is the standard setup for collaborator readiness.

### License Choice

| License | Copyleft | Commercial Use | Patent Grant | .NET Ecosystem Usage |
|---|---|---|---|---|
| **MIT** | ❌ None | ✅ Free | ❌ None | **Most common** (Spectre.Console, BenchmarkDotNet, Newtonsoft.Json) |
| **Apache 2.0** | ❌ None | ✅ Free | ✅ Explicit grant | Common (ASP.NET Core, .NET SDK, docfx) |
| **GPL v3** | ✅ Strong | Restricted | ❌ None | Rare in .NET tools |

**Recommendation for `markdown-journal-cli`: MIT**

MIT is the default for .NET CLI tools and libraries. It is maximally permissive, compatible with NuGet.org, and familiar to contributors.

**Evidence:** Spectre.Console uses MIT[^9]. `dotnet-outdated` uses MIT[^10]. `dotnet-script` uses MIT[^11].

### Community Health Files

GitHub's community standards checklist (visible at `github.com/OWNER/REPO/community`) checks for:

| File | Location | Status |
|---|---|---|
| `README.md` | repo root | Required |
| `LICENSE` | repo root | Required |
| `CONTRIBUTING.md` | repo root or `.github/` | Required |
| `CODE_OF_CONDUCT.md` | repo root or `.github/` | Required |
| `.github/ISSUE_TEMPLATE/` | `.github/ISSUE_TEMPLATE/` | Required (at least one template) |
| `.github/pull_request_template.md` | `.github/` | Recommended |
| `SECURITY.md` | repo root or `.github/` | Recommended |
| `.github/CODEOWNERS` | `.github/` | Optional but enables auto-review assignment |

**Source:** [docs.github.com — About community profiles for public repositories](https://docs.github.com/en/communities/setting-up-your-project-for-healthy-contributions/about-community-profiles-for-public-repositories)[^12]

### `LICENSE` (MIT)

```
MIT License

Copyright (c) [YEAR] [AUTHOR NAME]

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

### `CONTRIBUTING.md` Template (for a .NET CLI Tool)

Based on the Spectre.Console CONTRIBUTING.md[^13]:

```markdown
# Contributing to markdown-journal-cli

Thank you for your interest in contributing! Here's how to get started.

## Prerequisites

- .NET 9 SDK
- A GitHub account

By contributing, you assert that:
- The contribution is your own original work.
- You agree to the [Code of Conduct](CODE_OF_CONDUCT.md).
- You license your contribution under the [MIT License](LICENSE).

## Getting Started

1. Fork the repository and clone your fork.
2. Create a branch: `git checkout -b feature/my-feature`
3. Make your changes.
4. Run tests: `dotnet test`
5. Push and open a Pull Request against `main`.

## Code Style

- Follow standard .NET coding conventions.
- See the [Framework Design Guidelines](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/).
- Run `dotnet format` before committing.

## Tests

- All new code should have unit test coverage.
- Run `dotnet test` to verify all tests pass before submitting.

## Pull Request Process

1. Open an issue first for non-trivial changes.
2. Reference the issue in your PR: `Fixes #123`.
3. Keep PRs focused — one feature or fix per PR.
4. A maintainer will review within ~2 weeks.

## Commit Messages

Use [Conventional Commits](https://www.conventionalcommits.org/):
- `feat: add --output option to new command`
- `fix: handle missing config file gracefully`
- `docs: update README installation instructions`
```

### `CODE_OF_CONDUCT.md` (Contributor Covenant v2.1)

The Spectre.Console project uses the Contributor Covenant v2.1 verbatim[^14]. Key sections:

```markdown
# Contributor Covenant Code of Conduct

## Our Pledge
We pledge to make participation in our community a harassment-free experience
for everyone, regardless of age, body size, disability, ethnicity, sex 
characteristics, gender identity, level of experience, education, nationality, 
personal appearance, race, religion, or sexual identity and orientation.

## Our Standards
Positive behavior includes: empathy, respect, constructive feedback, 
accepting responsibility for mistakes.

Unacceptable behavior: harassment, trolling, personal attacks, publishing 
others' private info.

## Enforcement
[Contact email or link to reporting form]

## Attribution
Contributor Covenant v2.1: https://www.contributor-covenant.org/version/2/1/
```

Full text: [contributor-covenant.org/version/2/1/code_of_conduct](https://www.contributor-covenant.org/version/2/1/code_of_conduct.html)

### `SECURITY.md` Template

```markdown
# Security Policy

## Supported Versions

| Version | Supported |
|---------|-----------|
| 1.x     | ✅         |
| < 1.0   | ❌         |

## Reporting a Vulnerability

Please **do not** open a public GitHub issue for security vulnerabilities.

Instead, report via:
- GitHub's [private security advisory](../../security/advisories/new) feature
- Email: security@example.com

We will respond within 48 hours and aim to release a patch within 7 days.
```

### `.github/ISSUE_TEMPLATE/bug_report.md`

Based on Spectre.Console's template[^15]:

```markdown
---
name: Bug report
about: Create a report to help us improve
title: ''
labels: ["bug", "needs triage"]
assignees: ''
---

**Environment**
- OS: [e.g. Windows 11 / macOS 14 / Ubuntu 22.04]
- Tool version: [e.g. 1.2.3]
- .NET version: [e.g. 9.0.2]

**Describe the bug**
A clear and concise description of the bug.

**To Reproduce**
```
mdj new --date 2025-01-01
```

**Expected behavior**
What you expected to happen.

**Actual behavior**
What actually happened (include any error messages or stack traces).

**Additional context**
Any other context, screenshots, etc.

---
Please upvote 👍 this issue if you are also affected.
```

### `.github/ISSUE_TEMPLATE/feature_request.md`

Based on Spectre.Console's template[^16]:

```markdown
---
name: Feature request
about: Suggest an idea
title: ''
labels: ["enhancement", "needs triage"]
assignees: ''
---

**Is your feature request related to a problem?**
A clear and concise description of the problem. E.g. "I'm always frustrated when..."

**Describe the solution you'd like**
What you want to happen.

**Alternatives considered**
Any alternative solutions you've considered.

**Additional context**
Any other context, screenshots, or mockups.

---
Please upvote 👍 this issue if you are interested in it.
```

### `.github/pull_request_template.md`

Directly based on Spectre.Console's template[^17]:

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

### `.github/CODEOWNERS`

```
# Default owners for everything
*   @your-github-username

# Documentation owners
/docs/   @your-github-username
README.md @your-github-username
```

### Branch Protection Rules

Set via GitHub UI → Settings → Branches → Branch protection rules for `main`:

- ✅ **Require a pull request before merging**
  - ✅ Require approvals: 1
  - ✅ Dismiss stale reviews
- ✅ **Require status checks to pass** (add your CI workflow job name)
- ✅ **Require conversation resolution before merging**
- ✅ **Do not allow bypassing the above settings**

### GitHub Labels

Recommended labels for a .NET CLI tool (matches Spectre.Console's convention):

| Label | Color | Purpose |
|---|---|---|
| `bug` | `#d73a4a` | Something is broken |
| `enhancement` | `#a2eeef` | New feature or improvement |
| `needs triage` | `#e4e669` | New issue, needs review |
| `good first issue` | `#7057ff` | Good for new contributors |
| `help wanted` | `#008672` | Extra attention needed |
| `documentation` | `#0075ca` | Doc improvements |
| `duplicate` | `#cfd3d7` | Duplicate issue |
| `wontfix` | `#ffffff` | Won't be addressed |
| `question` | `#d876e3` | General question |

---

## Task 5 — README and Documentation Best Practices

### Executive Summary

A best-in-class README for a .NET CLI tool should include: title + badges, 1-sentence description, demo screenshot/GIF, installation (global tool + direct download), quick usage examples, full command reference, contributing section, and license. The shields.io badge service provides badges for NuGet version, build status, and license. NuGet badges use `https://img.shields.io/nuget/v/{packageId}`. GitHub Actions workflow status badges use the built-in `![badge](https://github.com/{owner}/{repo}/actions/workflows/{file}/badge.svg)` syntax.

### README Section Structure

```markdown
# Tool Name

![CI](https://github.com/owner/repo/actions/workflows/ci.yml/badge.svg)
[![NuGet](https://img.shields.io/nuget/v/my-package-id)](https://www.nuget.org/packages/my-package-id)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/download)

> One-line description of what the tool does.

## Installation

## Quick Start

## Commands

## Contributing

## License
```

### Badges Reference

#### NuGet Version Badge (shields.io)
```markdown
[![NuGet](https://img.shields.io/nuget/v/{PACKAGE_ID})](https://www.nuget.org/packages/{PACKAGE_ID})

<!-- Example -->
[![NuGet](https://img.shields.io/nuget/v/markdown-journal-cli)](https://www.nuget.org/packages/markdown-journal-cli)
```

#### NuGet Downloads Badge
```markdown
[![NuGet Downloads](https://img.shields.io/nuget/dt/{PACKAGE_ID})](https://www.nuget.org/packages/{PACKAGE_ID})
```

#### GitHub Actions Build Status (GitHub native)
```markdown
![CI Status](https://github.com/{OWNER}/{REPO}/actions/workflows/{WORKFLOW_FILE}.yml/badge.svg)

<!-- Example -->
![CI](https://github.com/collinrobison/markdown-journal-cli/actions/workflows/ci.yml/badge.svg)
```

#### License Badge (shields.io static)
```markdown
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
```

#### .NET Version Badge (shields.io static)
```markdown
[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/download)
```

#### Shields.io URL Pattern
```
https://img.shields.io/nuget/v/{packageName}
https://img.shields.io/nuget/vpre/{packageName}   ← includes pre-releases
https://img.shields.io/nuget/dt/{packageName}      ← download count
https://img.shields.io/badge/{label}-{message}-{color}  ← static badge
```

**Shields.io format rules:**
- `_` or `%20` → space
- `--` → dash `-`
- `__` → underscore `_`

**Source:** [shields.io/badges](https://shields.io/badges)[^18]

### Full README Template for markdown-journal-cli

```markdown
# markdown-journal-cli

![CI](https://github.com/OWNER/markdown-journal-cli/actions/workflows/ci.yml/badge.svg)
[![NuGet](https://img.shields.io/nuget/v/markdown-journal-cli)](https://www.nuget.org/packages/markdown-journal-cli)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/download/dotnet/9.0)

> A command-line tool for creating and managing markdown journal entries.

## Installation

### Global Tool (requires .NET 9 SDK)

```bash
dotnet tool install -g markdown-journal-cli
```

### Direct Download

Download the latest self-contained binary for your platform from
[Releases](https://github.com/OWNER/markdown-journal-cli/releases/latest):

| Platform | Download |
|----------|----------|
| Windows x64 | `mdj-win-x64-vX.X.X.zip` |
| macOS (Intel) | `mdj-osx-x64-vX.X.X.zip` |
| macOS (Apple Silicon) | `mdj-osx-arm64-vX.X.X.zip` |
| Linux x64 | `mdj-linux-x64-vX.X.X.zip` |

## Quick Start

```bash
# Create a new journal entry for today
mdj new

# Create an entry with a specific title
mdj new "My first entry"

# List recent entries
mdj list

# Show version
mdj --version
```

## Commands

### `mdj new [title]`

Creates a new journal entry.

| Option | Description |
|--------|-------------|
| `--date <DATE>` | Entry date (default: today) |
| `--open` | Open in default editor after creating |

### `mdj list`

Lists journal entries.

| Option | Description |
|--------|-------------|
| `--limit <N>` | Number of entries to show (default: 10) |

## Configuration

The tool looks for a configuration file at `~/.config/mdj/config.json`.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for details on our development process.

## License

MIT — see [LICENSE](LICENSE).
```

### What Spectre.Console's README Does Well

The [Spectre.Console README](https://github.com/spectreconsole/spectre.console/blob/main/README.md)[^19] exemplifies:
1. Large logo/header image above the fold
2. Multiple language README links (global audience)
3. Badges: build, NuGet, GitHub Stars
4. Short, one-paragraph description
5. Screenshot/demo showing rich output
6. Links to documentation site (not inline walls of text)
7. Feature list

### `/docs` Folder Structure

For a CLI tool, a simple `/docs` folder is sufficient (avoid over-engineering with mkdocs unless traffic warrants it):

```
docs/
├── research-pre-release.md   ← this file
├── configuration.md          ← advanced config options
└── changelog.md              ← version history (or CHANGELOG.md at root)
```

> For larger tools with many commands, consider a GitHub wiki or a simple mkdocs site hosted on GitHub Pages. Tools like `nuke`, `cake`, and `spectre.console` use custom static-site generators. For a small CLI tool, a well-structured README + `/docs` folder is sufficient.

### Auto-Generating CLI Help Docs from Spectre.Console.Cli

Spectre.Console.Cli includes a **built-in XML documentation command** (`cli xmldoc`) that outputs a machine-readable XML description of all commands and parameters. This can be piped into a doc-generation script:

```bash
# Generate XML doc from your CLI
mdj cli xmldoc > docs/commands.xml

# Or redirect to a file during CI for doc generation
```

**Output format:**
```xml
<?xml version="1.0" encoding="utf-8"?>
<Model>
  <Command Name="new" ClrType="NewEntryCommand" Settings="NewEntrySettings">
    <Description>Create a new journal entry</Description>
    <Parameters>
      <Argument Name="title" Position="0" Required="false" Kind="Scalar" />
      <Option Short="d" Long="date" Required="false" Kind="Scalar" />
    </Parameters>
  </Command>
</Model>
```

**Source:** [spectreconsole.net — Built-in Command Behaviors](https://spectreconsole.net/cli/reference/built-in-command-behaviors)[^2]

### dotnet-script README Pattern

The [dotnet-script README](https://github.com/dotnet-script/dotnet-script/blob/master/README.md)[^20] demonstrates a well-structured CLI tool README:
1. Tool logo + description
2. NuGet badge + CI badge
3. Installation section (global tool + local tool + Docker)
4. Usage examples with real command output
5. Feature table
6. Configuration section
7. Contributing and license

---

## Key Repositories Summary

| Repository | Purpose | Key Files Referenced |
|---|---|---|
| [spectreconsole/spectre.console](https://github.com/spectreconsole/spectre.console) | Spectre.Console library — version API, community files | `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `LICENSE.md`, `.github/` |
| [dotnet-outdated/dotnet-outdated](https://github.com/dotnet-outdated/dotnet-outdated) | Real .NET global tool example | `DotNetOutdated.csproj`, `.github/workflows/release.yml` |
| [dotnet/docfx](https://github.com/dotnet/docfx) | Real release workflow with self-contained binaries | `.github/workflows/release.yml` |
| [dotnet-script/dotnet-script](https://github.com/dotnet-script/dotnet-script) | Real global tool + README example | `README.md`, `.github/workflows/main.yml` |
| [softprops/action-gh-release](https://github.com/softprops/action-gh-release) | GitHub Action for creating releases | Action inputs reference |

---

## Confidence Assessment

| Topic | Confidence | Notes |
|---|---|---|
| `SetApplicationVersion` API | **High** | Confirmed from official spectreconsole.net docs with exact code examples |
| `--version` / `-v` behavior | **High** | Confirmed from built-in command behaviors reference page |
| `AssemblyInformationalVersionAttribute` pipeline | **High** | Confirmed from official Microsoft docs |
| `.csproj` global tool properties | **High** | Confirmed from Microsoft docs + real `dotnet-outdated` csproj |
| `dotnet tool install --add-source` limitations | **High** | Confirmed from Microsoft dotnet-tool-install docs |
| GitHub Actions release workflow | **High** | Based on real `dotnet/docfx` release.yml |
| `softprops/action-gh-release` inputs | **High** | Confirmed from action README |
| License choice (MIT) | **High** | Confirmed by inspection of 3+ real .NET CLI tool repos |
| Community health files content | **High** | Copied from actual Spectre.Console repo |
| README badge syntax | **High** | Confirmed from shields.io docs |
| GitHub community health requirements | **High** | Confirmed from official GitHub docs |

---

## Footnotes

[^1]: [spectreconsole.net — Configuring CommandApp and Commands](https://spectreconsole.net/cli/how-to/configuring-commandapp-and-commands) — Shows `config.SetApplicationName("myapp")` and `config.SetApplicationVersion("1.0.0")` usage.

[^2]: [spectreconsole.net — Built-in Command Behaviors](https://spectreconsole.net/cli/reference/built-in-command-behaviors) — Documents the `--version`, `-v`, `cli version`, `cli explain`, `cli xmldoc` built-in behaviors.

[^3]: [learn.microsoft.com — Set attributes from the project file](https://learn.microsoft.com/en-us/dotnet/standard/assembly/set-attributes-project-file) — `<Version>` → `AssemblyInformationalVersionAttribute` mapping, including .NET 8 Source Link git SHA behavior.

[^4]: [learn.microsoft.com — How to create a .NET tool](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools-how-to-create) — Official Microsoft tutorial. Shows `PackAsTool`, `ToolCommandName`, and `AssemblyInformationalVersionAttribute` usage.

[^5]: [github.com/dotnet-outdated/dotnet-outdated — DotNetOutdated.csproj](https://github.com/dotnet-outdated/dotnet-outdated/blob/main/src/DotNetOutdated/DotNetOutdated.csproj) — Real-world `.csproj` for a .NET global tool with `PackAsTool`, `ToolCommandName`, `PackageId`, `PackageLicenseExpression`, etc.

[^6]: [learn.microsoft.com — dotnet tool install](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-tool-install) — Official `dotnet tool install` command reference, including `--add-source` option.

[^7]: [github.com/dotnet/docfx — .github/workflows/release.yml](https://github.com/dotnet/docfx/blob/main/.github/workflows/release.yml) — Real GitHub Actions workflow that does `dotnet pack`, `dotnet publish --self-contained` for multiple platforms, and uploads to GitHub Releases.

[^8]: [github.com/softprops/action-gh-release](https://github.com/softprops/action-gh-release) — The `softprops/action-gh-release@v2` action documentation, including `files`, `generate_release_notes`, `draft`, and required permissions.

[^9]: [github.com/spectreconsole/spectre.console — LICENSE.md](https://github.com/spectreconsole/spectre.console/blob/main/LICENSE.md) — Spectre.Console uses MIT license.

[^10]: [github.com/dotnet-outdated/dotnet-outdated — LICENSE](https://github.com/dotnet-outdated/dotnet-outdated/blob/main/LICENSE) — dotnet-outdated uses MIT license.

[^11]: [github.com/dotnet-script/dotnet-script — LICENSE](https://github.com/dotnet-script/dotnet-script/blob/master/LICENSE) — dotnet-script uses MIT license.

[^12]: [docs.github.com — About community profiles for public repositories](https://docs.github.com/en/communities/setting-up-your-project-for-healthy-contributions/about-community-profiles-for-public-repositories) — GitHub community health checklist requirements.

[^13]: [github.com/spectreconsole/spectre.console — CONTRIBUTING.md](https://github.com/spectreconsole/spectre.console/blob/main/CONTRIBUTING.md) — Full Spectre.Console contribution guidelines. Adapted for this report.

[^14]: [github.com/spectreconsole/spectre.console — CODE_OF_CONDUCT.md](https://github.com/spectreconsole/spectre.console/blob/main/CODE_OF_CONDUCT.md) — Contributor Covenant v2.1. Enforcement email: conduct@dotnetfoundation.org.

[^15]: [github.com/spectreconsole/spectre.console — .github/ISSUE_TEMPLATE/bug_report.md](https://github.com/spectreconsole/spectre.console/blob/main/.github/ISSUE_TEMPLATE/bug_report.md) — Spectre.Console bug report template.

[^16]: [github.com/spectreconsole/spectre.console — .github/ISSUE_TEMPLATE/feature_request.md](https://github.com/spectreconsole/spectre.console/blob/main/.github/ISSUE_TEMPLATE/feature_request.md) — Spectre.Console feature request template.

[^17]: [github.com/spectreconsole/spectre.console — .github/pull_request_template.md](https://github.com/spectreconsole/spectre.console/blob/main/.github/pull_request_template.md) — Spectre.Console PR template.

[^18]: [shields.io/badges](https://shields.io/badges) — Shields.io static badge URL format and query parameters.

[^19]: [github.com/spectreconsole/spectre.console — README.md](https://github.com/spectreconsole/spectre.console/blob/main/README.md) — Spectre.Console's README structure reference.

[^20]: [github.com/dotnet-script/dotnet-script — README.md](https://github.com/dotnet-script/dotnet-script/blob/master/README.md) — dotnet-script README for CLI tool documentation patterns.
