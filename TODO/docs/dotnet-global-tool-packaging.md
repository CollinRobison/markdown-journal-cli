# .NET Global Tool Packaging & Publishing

> **Technical Deep-dive** — How to package, publish, and distribute a .NET console app as a global tool (`dotnet tool install -g`)

---

## Executive Summary

A .NET global tool is a NuGet package that contains a console application, installable via `dotnet tool install -g <PackageId>`. The entire mechanism hinges on three `.csproj` properties — `<PackAsTool>true</PackAsTool>`, `<ToolCommandName>`, and `<PackageId>` — combined with `dotnet pack` to produce a `.nupkg` artifact. That artifact can be pushed to NuGet.org, GitHub Packages, or installed directly from a local path or URL-accessible feed. Real-world examples like `nuke-build/nuke` and `dotnet-script/dotnet-script` illustrate the minimal surface area required and show how shared `Directory.Build.props` files carry common metadata (license, authors, repo URL) while the per-project `.csproj` stays tightly focused.

---

## Architecture / System Overview

```
┌─────────────────────────────────────────────────────────────┐
│                      Developer Workstation                  │
│                                                             │
│  MyTool.csproj                                              │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  <PackAsTool>true</PackAsTool>                        │  │
│  │  <ToolCommandName>my-tool</ToolCommandName>           │  │
│  │  <PackageId>MyTool</PackageId>                        │  │
│  └──────────────────────────────────────────────────────┘  │
│            │                                                │
│     dotnet pack                                             │
│            │                                                │
│            ▼                                                │
│   ./nupkg/MyTool.1.0.0.nupkg                                │
└────────────────────┬────────────────────────────────────────┘
                     │
          ┌──────────┴──────────┐
          │                     │
          ▼                     ▼
  ┌───────────────┐    ┌────────────────────┐
  │  NuGet.org    │    │  GitHub Packages   │
  │  (public)     │    │  (org-scoped)      │
  │               │    │                    │
  │ dotnet nuget  │    │ dotnet nuget push  │
  │ push --source │    │ --source github    │
  │ nuget.org     │    │                    │
  └───────┬───────┘    └─────────┬──────────┘
          │                      │
          └──────────┬───────────┘
                     │
          ┌──────────┴──────────┐
          │  Consumer           │
          │  dotnet tool        │
          │  install -g MyTool  │
          └─────────────────────┘

Alternative: Local / GitHub Releases feed
  ┌──────────────────────────────────────┐
  │ dotnet tool install -g MyTool        │
  │   --add-source ./nupkg               │
  │   --add-source https://example.com/  │
  └──────────────────────────────────────┘
```

---

## 1. Required `.csproj` Properties

### Minimum Required Properties

| Property | Required? | Purpose |
|---|---|---|
| `<PackAsTool>` | **Mandatory** | Signals the SDK to produce a tool package |
| `<ToolCommandName>` | Recommended | Sets the CLI command name; defaults to assembly name |
| `<PackageId>` | Recommended | NuGet package ID (used in `dotnet tool install`); defaults to project name |
| `<OutputType>Exe</OutputType>` | **Mandatory** | Must be a console application |
| `<TargetFramework>` | **Mandatory** | The target TFM(s) the tool runs on |

### Full Metadata Properties

| Property | Required for NuGet.org? | Description |
|---|---|---|
| `<Version>` | Yes | NuGet package version (SemVer 2.0) |
| `<Authors>` | Yes | Comma-separated list of authors |
| `<Description>` | Yes | Package description, shown on NuGet.org |
| `<PackageLicenseExpression>` | Strongly recommended | SPDX license identifier, e.g. `MIT` |
| `<RepositoryUrl>` | Strongly recommended | GitHub repo URL; auto-links package to the repo on GitHub Packages |
| `<PackageProjectUrl>` | Recommended | Homepage / project URL |
| `<PackageTags>` | Recommended | Space-separated search tags |
| `<PackageOutputPath>` | Optional | Output directory for `.nupkg`, e.g. `./nupkg` |
| `<PackageIconUrl>` | Deprecated | Use `<PackageIcon>` with embedded image instead |
| `<RepositoryType>` | Optional | Usually `git` |

### Complete Minimal `.csproj` Example

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>

    <!-- Tool packaging -->
    <PackAsTool>true</PackAsTool>
    <ToolCommandName>my-tool</ToolCommandName>
    <PackageOutputPath>./nupkg</PackageOutputPath>

    <!-- NuGet identity -->
    <PackageId>MyCompany.MyTool</PackageId>
    <Version>1.0.0</Version>
    <Authors>Your Name</Authors>
    <Description>A brief description of what the tool does.</Description>

    <!-- Metadata -->
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <RepositoryUrl>https://github.com/yourorg/your-repo</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
    <PackageProjectUrl>https://github.com/yourorg/your-repo</PackageProjectUrl>
    <PackageTags>cli tool utility</PackageTags>
  </PropertyGroup>

</Project>
```

### Multi-Framework Targeting

To support multiple .NET versions simultaneously, use `<TargetFrameworks>` (plural)[^1]:

```xml
<TargetFrameworks>net9.0;net8.0;net6.0</TargetFrameworks>
```

The .NET SDK will install the most appropriate TFM for the end user's runtime.

---

## 2. The `dotnet pack` + `dotnet tool install` Workflow

### Step 1 — Build and Pack

```bash
# Produces ./nupkg/MyCompany.MyTool.1.0.0.nupkg
dotnet pack --configuration Release
```

`dotnet pack` compiles the project and wraps it in a `.nupkg` file. The `PackageOutputPath` property controls where the file lands[^1].

### Step 2 — Test Locally Before Publishing

```bash
# Install directly from the local nupkg folder (no NuGet server needed)
dotnet tool install -g MyCompany.MyTool --add-source ./nupkg

# Run it
my-tool --help

# Uninstall when done testing
dotnet tool uninstall -g MyCompany.MyTool
```

### Step 3 — Publish to NuGet.org

```bash
# Get your API key from https://www.nuget.org/account/apikeys
dotnet nuget push ./nupkg/MyCompany.MyTool.1.0.0.nupkg \
  --api-key YOUR_NUGET_API_KEY \
  --source https://api.nuget.org/v3/index.json
```

### Step 4 — Consumer Installs It

```bash
# Install latest stable from NuGet.org
dotnet tool install -g MyCompany.MyTool

# Install specific version
dotnet tool install -g MyCompany.MyTool --version 1.2.3

# Install a prerelease
dotnet tool install -g MyCompany.MyTool --prerelease
```

### Installation Locations

| OS | Default global tool path |
|---|---|
| Linux / macOS | `$HOME/.dotnet/tools` |
| Windows | `%USERPROFILE%\.dotnet\tools` |

The SDK automatically adds these paths to `PATH` on first run[^2].

### Lifecycle Commands

```bash
dotnet tool list -g                          # List all installed global tools
dotnet tool update -g MyCompany.MyTool       # Update to latest
dotnet tool uninstall -g MyCompany.MyTool    # Remove
dotnet tool search MyTool                    # Search NuGet.org
```

---

## 3. Installing from a GitHub Releases `.nupkg` Artifact

GitHub does **not** provide a native NuGet v3 feed from Release assets. However, there are two supported approaches:

### Option A — Download + Local Folder Install

```bash
# 1. Download the .nupkg from GitHub Releases
curl -L -o MyTool.1.0.0.nupkg \
  https://github.com/yourorg/your-repo/releases/download/v1.0.0/MyTool.1.0.0.nupkg

# 2. Place it in a local folder (or use the current directory)
mkdir ./tool-feed
mv MyTool.1.0.0.nupkg ./tool-feed/

# 3. Install using --add-source pointing at that folder
dotnet tool install -g MyCompany.MyTool \
  --add-source ./tool-feed \
  --version 1.0.0
```

The `--add-source` option adds a **supplementary** NuGet source for this operation only — it does not permanently modify `nuget.config`[^2].

### Option B — HTTP Feed (Self-Hosted or GitHub Pages)

If you serve the `.nupkg` file from any HTTP(S) URL that exposes a flat folder or NuGet v2/v3 feed:

```bash
dotnet tool install -g MyCompany.MyTool \
  --add-source https://yourserver.example.com/packages/
```

**Note:** A raw GitHub Release asset URL (e.g. `https://github.com/.../releases/download/...`) is **not** a NuGet feed endpoint. You must either download first (Option A) or host a proper NuGet feed (Option B/C).

### Option C — GitHub Packages NuGet Feed

If you publish to GitHub Packages, users authenticate and install via its NuGet feed endpoint:

```bash
# Authenticate once
dotnet nuget add source \
  --username GITHUB_USER \
  --password YOUR_PAT \
  --store-password-in-clear-text \
  --name github \
  "https://nuget.pkg.github.com/OWNER/index.json"

# Install
dotnet tool install -g MyCompany.MyTool --add-source github
```

---

## 4. Installing from a Local `.nupkg` or URL

### From a Local File / Directory

```bash
# All three forms work:
dotnet tool install -g MyTool --add-source /absolute/path/to/nupkg-folder
dotnet tool install -g MyTool --add-source ./relative/nupkg-folder
dotnet tool install -g MyTool --add-source .  # current directory if .nupkg is here
```

The tool name must match the `<PackageId>` in the `.csproj`, not the `.nupkg` filename[^2].

### From a Remote NuGet Feed URL

```bash
dotnet tool install -g MyTool --add-source https://myserver.com/nuget/v3/index.json
```

### Install a Specific Version from a Local Source

```bash
dotnet tool install -g MyTool \
  --add-source ./nupkg \
  --version 1.2.3-beta.1
```

### Combined: Custom Path + Custom Source

```bash
dotnet tool install MyTool \
  --tool-path ~/custom-tools \
  --add-source ./nupkg
```

---

## 5. NuGet.org vs. GitHub Packages vs. Self-Hosted

| Aspect | NuGet.org | GitHub Packages | Self-Hosted (Nexus, Artifactory, local) |
|---|---|---|---|
| **Discovery** | Public index, searchable via `dotnet tool search` | Organization-scoped; not on NuGet.org search | No public discovery |
| **Authentication** | API key required to **push**; no auth to **install** | PAT or `GITHUB_TOKEN` required for both push **and** install (even public packages) | Depends on server; often credentials required |
| **Visibility** | Public by default | Private by default; can be made public | Configurable |
| **Pricing** | Free for open-source | Free for public packages; storage limits for private | Infrastructure cost |
| **Package size limit** | 250 MB | 2.147 GB | Configurable |
| **CI push (GitHub Actions)** | Secret `NUGET_API_KEY` + `https://api.nuget.org/v3/index.json` | `secrets.GITHUB_TOKEN` + org NuGet feed URL | Custom credentials |
| **Consumer setup** | Zero setup — `dotnet tool install -g <id>` works out of the box | Must add source via `nuget.config` or `--add-source` | Must add source and credentials |
| **Package linking** | Unlinked from source repos | Auto-links to GitHub repo when `<RepositoryUrl>` is set | N/A |
| **Best for** | Public OSS tools | Internal org tools; keeping packages near source | Enterprise air-gapped or compliance scenarios |

### GitHub Packages `nuget.config` Example

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="github" value="https://nuget.pkg.github.com/OWNER/index.json" />
  </packageSources>
  <packageSourceCredentials>
    <github>
      <add key="Username" value="GITHUB_USERNAME" />
      <add key="ClearTextPassword" value="YOUR_PAT_OR_GITHUB_TOKEN" />
    </github>
  </packageSourceCredentials>
</configuration>
```

### GitHub Actions: Push to NuGet.org

```yaml
- name: Pack
  run: dotnet pack --configuration Release --output ./nupkg

- name: Push to NuGet.org
  run: |
    dotnet nuget push "./nupkg/*.nupkg" \
      --api-key ${{ secrets.NUGET_API_KEY }} \
      --source https://api.nuget.org/v3/index.json \
      --skip-duplicate
```

### GitHub Actions: Push to GitHub Packages

```yaml
- name: Pack
  run: dotnet pack --configuration Release --output ./nupkg

- name: Push to GitHub Packages
  run: |
    dotnet nuget add source \
      --username ${{ github.actor }} \
      --password ${{ secrets.GITHUB_TOKEN }} \
      --store-password-in-clear-text \
      --name github \
      "https://nuget.pkg.github.com/${{ github.repository_owner }}/index.json"
    dotnet nuget push "./nupkg/*.nupkg" \
      --source "github" \
      --skip-duplicate
```

---

## 6. How Popular Open-Source .NET Global Tools Handle This

### `nuke-build/nuke` — `Nuke.GlobalTool`

The NUKE build system ships its CLI entry-point as a dedicated project (`source/Nuke.GlobalTool/`) that references the core libraries as project references. The `.csproj` is minimal — only tool-specific properties live there; all shared metadata (authors, license, description, repo URL) is centralized in `source/Directory.Build.props`[^3][^4].

**`source/Nuke.GlobalTool/Nuke.GlobalTool.csproj`** (abbreviated)[^3]:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <RollForward>LatestMajor</RollForward>
    <PackAsTool>true</PackAsTool>
    <ToolCommandName>nuke</ToolCommandName>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\Nuke.Build\Nuke.Build.csproj" />
    <ProjectReference Include="..\Nuke.Common\Nuke.Common.csproj" />
  </ItemGroup>

</Project>
```

**`source/Directory.Build.props`** (shared metadata)[^4]:

```xml
<PropertyGroup>
  <Description>The AKEless Build System for C#/.NET</Description>
  <Authors>Matthias Koch and contributors</Authors>
  <Copyright>Copyright $([System.DateTime]::Now.Year) Maintainers of NUKE</Copyright>
  <PackageLicenseExpression>MIT</PackageLicenseExpression>
  <PackageProjectUrl>https://nuke.build</PackageProjectUrl>
  <PackageIcon>icon.png</PackageIcon>
</PropertyGroup>
```

**Key patterns from NUKE:**
- `<RollForward>LatestMajor</RollForward>` — allows the tool to run on newer .NET runtimes without needing a separate version[^3]
- Separation of concerns: tool project is minimal; common metadata in `Directory.Build.props`
- Template files embedded as resources in the tool assembly

### `dotnet-script/dotnet-script`

The dotnet-script project (`src/Dotnet.Script/Dotnet.Script.csproj`) sets `PackAsTool` to **`false`** in its current iteration, because the team distributes the tool via a custom build script (`build/Build.csx`, itself a dotnet-script file) that handles pack/publish with more control[^5][^6]. However, all the standard NuGet metadata properties are present, and the tool is still published to NuGet.org as `dotnet-script`, installable via `dotnet tool install -g dotnet-script`.

**`src/Dotnet.Script/Dotnet.Script.csproj`** (abbreviated)[^5]:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <Description>Dotnet CLI tool allowing you to run C# (CSX) scripts.</Description>
    <VersionPrefix>2.0.0</VersionPrefix>
    <Authors>filipw</Authors>
    <PackageId>Dotnet.Script</PackageId>
    <TargetFrameworks>net10.0;net9.0;net8.0</TargetFrameworks>
    <AssemblyName>dotnet-script</AssemblyName>
    <OutputType>Exe</OutputType>
    <PackageTags>dotnet;cli;script;csx;csharp;roslyn</PackageTags>
    <PackageProjectUrl>https://github.com/dotnet-script/dotnet-script</PackageProjectUrl>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <RepositoryType>git</RepositoryType>
    <RepositoryUrl>https://github.com/dotnet-script/dotnet-script.git</RepositoryUrl>
    <PackAsTool>false</PackAsTool>  <!-- managed by Build.csx -->
  </PropertyGroup>
</Project>
```

The CI (`.github/workflows/main.yml`) bootstraps itself by installing `dotnet-script` globally from NuGet, then invokes `dotnet-script build/Build.csx` to orchestrate the rest[^6].

### Key Observations from Both Projects

| Pattern | NUKE | dotnet-script |
|---|---|---|
| `PackAsTool` in individual project | ✅ `true` directly | ✅ (via build script) |
| Shared metadata via `Directory.Build.props` | ✅ All common metadata | ❌ Inline per project |
| Multi-TFM targeting | ❌ Single (`net10.0`) | ✅ `net10.0;net9.0;net8.0` |
| `RollForward` | ✅ `LatestMajor` | ❌ |
| `RepositoryUrl` set | ✅ (in Directory.Build.props) | ✅ |
| `AssemblyName` ≠ `PackageId` | ❌ | ✅ (`dotnet-script` vs `Dotnet.Script`) |

---

## 7. `<ToolCommandName>` Naming Best Practices

The `<ToolCommandName>` value becomes the shell command users type after installation. The official guidance and community patterns converge on the following rules[^1][^2]:

### Rules

1. **Lowercase, hyphen-separated** — `my-tool`, not `MyTool` or `my_tool`
2. **Prefix with `dotnet-` for `dotnet`-subcommand invocation** — a tool named `dotnet-my-tool` can be invoked as both `dotnet-my-tool` and `dotnet my-tool`[^2]
3. **No file extensions** — do not use `.exe` or `.cmd`; the SDK generates platform-appropriate host executables
4. **Keep it unique and descriptive** — avoid names that shadow common system commands (`test`, `run`, `build`, etc.)
5. **`ToolCommandName` ≠ `PackageId`** — it's fine (and often recommended) for the two to differ; `PackageId` is for NuGet identity, `ToolCommandName` is the shell command
6. **`AssemblyName` should match `ToolCommandName`** when you want the entry-point executable to match the command name (e.g., `<AssemblyName>dotnet-script</AssemblyName>`)

### Examples

| ToolCommandName | Invocation | Notes |
|---|---|---|
| `nuke` | `nuke setup` | Short, memorable brand |
| `dotnet-script` | `dotnet-script run.csx` **or** `dotnet script run.csx` | `dotnet-` prefix enables `dotnet` dispatch |
| `botsay` | `botsay "Hello"` | Microsoft's tutorial example |
| `dotnet-format` | `dotnet-format` **or** `dotnet format` | `dotnet-` prefix convention |

### The `dotnet-` Prefix Convention

If the command name starts with `dotnet-`, two invocation forms work automatically[^2]:

```bash
dotnet-mything --help     # Direct invocation (always works)
dotnet mything --help     # Shorthand via dotnet dispatcher (works if no conflict with local tool)
```

---

## 8. Complete Production-Ready `.csproj` Template

This template incorporates all best practices observed from the Microsoft docs and real-world examples:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <!-- Application -->
    <OutputType>Exe</OutputType>
    <TargetFrameworks>net9.0;net8.0</TargetFrameworks>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>

    <!-- Tool packaging -->
    <PackAsTool>true</PackAsTool>
    <ToolCommandName>my-tool</ToolCommandName>
    <PackageOutputPath>./nupkg</PackageOutputPath>

    <!-- Runtime rollforward for future .NET versions -->
    <RollForward>LatestMajor</RollForward>

    <!-- NuGet identity -->
    <PackageId>MyCompany.MyTool</PackageId>
    <Version>1.0.0</Version>
    <Authors>Your Name or Org</Authors>
    <Description>A short one-line description of the tool's purpose.</Description>

    <!-- Discoverability and licensing -->
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageProjectUrl>https://github.com/yourorg/your-repo</PackageProjectUrl>
    <RepositoryUrl>https://github.com/yourorg/your-repo.git</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
    <PackageTags>cli tool your-keywords here</PackageTags>

    <!-- Source linking (for debugging) -->
    <PublishRepositoryUrl>true</PublishRepositoryUrl>
    <EmbedUntrackedSources>true</EmbedUntrackedSources>
    <DebugType>embedded</DebugType>
  </PropertyGroup>

  <ItemGroup>
    <!-- Source Link for GitHub -->
    <PackageReference Include="Microsoft.SourceLink.GitHub" Version="8.0.0" PrivateAssets="All" />
  </ItemGroup>

</Project>
```

---

## Key Repositories Summary

| Repository | Purpose | Key Files | NuGet Package |
|---|---|---|---|
| [nuke-build/nuke](https://github.com/nuke-build/nuke) | NUKE build system global tool | `source/Nuke.GlobalTool/Nuke.GlobalTool.csproj`, `source/Directory.Build.props` | [`Nuke.GlobalTool`](https://www.nuget.org/packages/Nuke.GlobalTool) |
| [dotnet-script/dotnet-script](https://github.com/dotnet-script/dotnet-script) | Run C# scripts from CLI | `src/Dotnet.Script/Dotnet.Script.csproj`, `.github/workflows/main.yml` | [`dotnet-script`](https://www.nuget.org/packages/dotnet-script) |
| [dotnet/sdk](https://github.com/dotnet/sdk) | .NET SDK (hosts the tool infrastructure) | (reference implementation) | N/A |

### Microsoft Documentation

| Resource | URL |
|---|---|
| How to Create a .NET Tool | https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools-how-to-create |
| `dotnet tool install` reference | https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-tool-install |
| .NET Tools Overview | https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools |
| `dotnet nuget push` reference | https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-nuget-push |
| GitHub Packages NuGet Registry | https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-nuget-registry |
| Publish to NuGet.org | https://learn.microsoft.com/en-us/nuget/nuget-org/publish-a-package |

---

## Confidence Assessment

| Topic | Confidence | Basis |
|---|---|---|
| Required `.csproj` properties and semantics | **High** | Directly verified from Microsoft docs and two real OSS repos |
| `dotnet pack` + `dotnet tool install` workflow | **High** | Microsoft official documentation (verified) |
| Installing from local `.nupkg` with `--add-source` | **High** | Microsoft docs explicitly cover this scenario |
| GitHub Releases `.nupkg` distribution (download-first approach) | **High** | Verified from `dotnet tool install` docs — `--add-source` accepts folder paths |
| GitHub Packages authentication details | **High** | Verified from GitHub official documentation |
| NuGet.org vs GitHub Packages comparison | **High** | Cross-referenced from multiple official docs |
| `nuke-build/nuke` `.csproj` contents | **High** | Fetched directly from `source/Nuke.GlobalTool/Nuke.GlobalTool.csproj` at HEAD |
| `dotnet-script` `.csproj` contents | **High** | Fetched directly from `src/Dotnet.Script/Dotnet.Script.csproj` at HEAD |
| `dotnet-` prefix invocation shorthand | **High** | Verified from Microsoft `.NET tools overview` documentation |
| `RollForward` best practice | **Medium** | Observed in NUKE; supported by .NET docs but not universally adopted |
| GitHub Releases as a *direct* NuGet feed | **High** | Confirmed **not** supported; raw asset URLs are not NuGet feeds |

---

## Footnotes

[^1]: `src/Dotnet.Script/Dotnet.Script.csproj` — Microsoft How-to-Create tutorial: https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools-how-to-create — covers `<PackAsTool>`, `<ToolCommandName>`, `<PackageOutputPath>`, multi-TFM usage

[^2]: `dotnet tool install` reference documentation: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-tool-install — covers `--add-source`, `--global`, `--tool-path`, installation locations, and `dotnet-` prefix dispatch

[^3]: `source/Nuke.GlobalTool/Nuke.GlobalTool.csproj` (commit `27e8077`) in [nuke-build/nuke](https://github.com/nuke-build/nuke/blob/27e8077b8ab01222c16c3610a899c25d1bce910c/source/Nuke.GlobalTool/Nuke.GlobalTool.csproj) — shows `<PackAsTool>true</PackAsTool>`, `<ToolCommandName>nuke</ToolCommandName>`, `<RollForward>LatestMajor</RollForward>`

[^4]: `source/Directory.Build.props` (commit `27e8077`) in [nuke-build/nuke](https://github.com/nuke-build/nuke/blob/27e8077b8ab01222c16c3610a899c25d1bce910c/source/Directory.Build.props) — shows centralized `Authors`, `PackageLicenseExpression`, `Description`, `PackageProjectUrl`

[^5]: `src/Dotnet.Script/Dotnet.Script.csproj` (commit `2cf97b9`) in [dotnet-script/dotnet-script](https://github.com/dotnet-script/dotnet-script/blob/2cf97b9efeceebbddd2581dca73bd8e35f447fd8/src/Dotnet.Script/Dotnet.Script.csproj) — shows `<PackAsTool>false</PackAsTool>`, multi-TFM targeting, `AssemblyName` set to `dotnet-script`

[^6]: `.github/workflows/main.yml` (commit `2cf97b9`) in [dotnet-script/dotnet-script](https://github.com/dotnet-script/dotnet-script/blob/2cf97b9efeceebbddd2581dca73bd8e35f447fd8/.github/workflows/main.yml) — shows CI bootstrap: installs `dotnet-script` globally then runs `dotnet-script build/Build.csx` for pack/publish

[^7]: GitHub Packages NuGet Registry documentation: https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-nuget-registry — covers authentication, feed URL format, and push/install commands

[^8]: NuGet.org Publish a Package documentation: https://learn.microsoft.com/en-us/nuget/nuget-org/publish-a-package — covers API key creation, `dotnet nuget push`, 250 MB limit

[^9]: `dotnet nuget push` reference: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-nuget-push — all push options including `--source`, `--api-key`, `--skip-duplicate`

[^10]: .NET Tools Overview: https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools — covers `dotnet-` prefix invocation shorthand, local vs global tools, tool manifest
