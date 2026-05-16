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

## 9. Native Package Managers — Homebrew, Winget, and Chocolatey

These routes publish **self-contained native binaries** — the user needs no .NET runtime installed. That's the main advantage. The tradeoff is significantly more setup and ongoing maintenance compared to the NuGet global tool route.

### How self-contained publishing works

Instead of `dotnet pack`, you use `dotnet publish --self-contained`:

```bash
# macOS (arm64)
dotnet publish markdown-journal-cli/markdown-journal-cli.csproj \
  --configuration Release \
  --runtime osx-arm64 \
  --self-contained true \
  --output ./publish/osx-arm64

# macOS (x64)
dotnet publish ... --runtime osx-x64 --self-contained true --output ./publish/osx-x64

# Windows (x64)
dotnet publish ... --runtime win-x64 --self-contained true --output ./publish/win-x64

# Linux (x64)
dotnet publish ... --runtime linux-x64 --self-contained true --output ./publish/linux-x64
```

You typically also set `<PublishSingleFile>true</PublishSingleFile>` in the `.csproj` (or pass it as a flag) to produce a single executable file rather than a folder of DLLs. Each platform needs its own build — you can't cross-publish one binary for all OSes from a single run.

```xml
<!-- Add to .csproj if you always want single-file output -->
<PublishSingleFile>true</PublishSingleFile>
```

---

### Option A — Homebrew (macOS / Linux)

Homebrew is the standard package manager for macOS. Users install via `brew install <formula>`.

**What you need to do:**

1. **Build and upload platform binaries to GitHub Releases.** Homebrew formulas download from a URL (typically a GitHub Release tarball or zip). Your CI needs to produce `osx-arm64` and `osx-x64` builds and attach them to each release.

2. **Create a Homebrew tap.** You can't submit arbitrary tools to the main `homebrew-core` repo (it has strict quality requirements). The practical path is a **personal tap** — a GitHub repo named `homebrew-<tapname>` (e.g., `CollinRobison/homebrew-mdjournal`).

3. **Write a formula file** (`mdjournal.rb`):

```ruby
class Mdjournal < Formula
  desc "CLI tool for managing a markdown journal"
  homepage "https://github.com/CollinRobison/markdown-journal-cli"
  version "0.1.0"

  on_macos do
    on_arm do
      url "https://github.com/CollinRobison/markdown-journal-cli/releases/download/v0.1.0/mdjournal-osx-arm64.tar.gz"
      sha256 "REPLACE_WITH_ACTUAL_SHA256"
    end
    on_intel do
      url "https://github.com/CollinRobison/markdown-journal-cli/releases/download/v0.1.0/mdjournal-osx-x64.tar.gz"
      sha256 "REPLACE_WITH_ACTUAL_SHA256"
    end
  end

  def install
    bin.install "mdjournal"
  end

  test do
    system "#{bin}/mdjournal", "--help"
  end
end
```

4. **Users install via:**

```bash
brew tap CollinRobison/mdjournal
brew install mdjournal
```

**Maintenance burden:** Every release, you must rebuild all binaries, upload them to GitHub Releases, compute the new `sha256` hashes, and update the formula. The sha256 must be exact — Homebrew verifies it.

---

### Option B — Winget (Windows)

Winget is Microsoft's built-in package manager on Windows 10/11. Users install via `winget install <id>`.

**What you need to do:**

1. **Build a self-contained Windows binary** and upload it to GitHub Releases (or any stable URL).

2. **Create a manifest** (a set of `.yaml` files). Winget manifests have three files: version, installer, and locale.

   `manifests/c/CollinRobison/mdjournal/0.1.0/CollinRobison.mdjournal.yaml` (version):
   ```yaml
   PackageIdentifier: CollinRobison.mdjournal
   PackageVersion: 0.1.0
   DefaultLocale: en-US
   ManifestType: version
   ManifestVersion: 1.6.0
   ```

   `...CollinRobison.mdjournal.installer.yaml`:
   ```yaml
   PackageIdentifier: CollinRobison.mdjournal
   PackageVersion: 0.1.0
   InstallerType: zip
   Commands:
     - mdjournal
   Installers:
     - Architecture: x64
       InstallerUrl: https://github.com/CollinRobison/markdown-journal-cli/releases/download/v0.1.0/mdjournal-win-x64.zip
       InstallerSha256: REPLACE_WITH_ACTUAL_SHA256
   ManifestType: installer
   ManifestVersion: 1.6.0
   ```

3. **Submit a PR to [microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs)**. This is a public repo where all community packages live. The PR is reviewed automatically by a bot and then manually by maintainers. Turnaround is typically 1–3 days.

4. **Users install via:**
   ```bash
   winget install CollinRobison.mdjournal
   ```

**Maintenance burden:** Every release requires a new PR to `winget-pkgs` with updated manifests. There are tools (`wingetcreate`) that automate much of this.

---

### Option C — Chocolatey (Windows)

Chocolatey is the older Windows package manager with a large existing user base. Users install via `choco install <package>`.

**What you need to do:**

1. **Build a self-contained Windows binary** and upload it to GitHub Releases.

2. **Create a Chocolatey package** — a folder with a `.nuspec` and a `tools/` directory:

   ```
   mdjournal/
     mdjournal.nuspec
     tools/
       chocolateyInstall.ps1
       chocolateyUninstall.ps1  (optional)
   ```

   `mdjournal.nuspec`:
   ```xml
   <?xml version="1.0" encoding="utf-8"?>
   <package xmlns="http://schemas.microsoft.com/packaging/2015/06/nuspec.xsd">
     <metadata>
       <id>mdjournal</id>
       <version>0.1.0</version>
       <title>mdjournal</title>
       <authors>CollinRobison</authors>
       <projectUrl>https://github.com/CollinRobison/markdown-journal-cli</projectUrl>
       <licenseUrl>https://github.com/CollinRobison/markdown-journal-cli/blob/main/LICENSE</licenseUrl>
       <requireLicenseAcceptance>false</requireLicenseAcceptance>
       <description>CLI tool for managing a markdown journal.</description>
       <tags>cli markdown journal notes</tags>
     </metadata>
   </package>
   ```

   `tools/chocolateyInstall.ps1`:
   ```powershell
   $ErrorActionPreference = 'Stop'

   $packageArgs = @{
     packageName   = $env:ChocolateyPackageName
     unzipLocation = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
     url64bit      = 'https://github.com/CollinRobison/markdown-journal-cli/releases/download/v0.1.0/mdjournal-win-x64.zip'
     checksum64    = 'REPLACE_WITH_ACTUAL_SHA256'
     checksumType64= 'sha256'
   }

   Install-ChocolateyZipPackage @packageArgs
   ```

3. **Pack and push:**
   ```bash
   # Install Chocolatey CLI first: https://chocolatey.org/install
   choco pack mdjournal/mdjournal.nuspec

   # Submit to the community repository (requires a Chocolatey account)
   choco push mdjournal.0.1.0.nupkg --source https://push.chocolatey.org --api-key YOUR_CHOCO_API_KEY
   ```

4. **The package goes through moderation** on https://community.chocolatey.org — automated and human review. First submission can take several days.

5. **Users install via:**
   ```bash
   choco install mdjournal
   ```

**Maintenance burden:** Every release requires updating the `.nuspec` version, the installer URL, and the sha256 hash, then repacking and pushing. Like Winget, there are community tools that automate parts of this.

---

### Comparison

| | Homebrew | Winget | Chocolatey |
|---|---|---|---|
| **Platform** | macOS + Linux | Windows | Windows |
| **Users need .NET?** | No | No | No |
| **Submission review** | Instant (personal tap) / months (homebrew-core) | 1–3 days PR | Several days moderation |
| **Per-release work** | Rebuild binaries + update sha256 | New manifest PR | Repack + push |
| **Automation possible?** | Yes (GitHub Actions + `brew bump-formula-pr`) | Yes (`wingetcreate`) | Yes (GitHub Actions) |
| **Audience** | macOS/Linux developers | General Windows users | Windows power users |

### GitHub Actions — Build All Platform Binaries

This workflow produces the binaries needed for all three package managers:

```yaml
name: Build Release Binaries

on:
  release:
    types: [published]

jobs:
  build:
    strategy:
      matrix:
        include:
          - os: macos-latest
            rid: osx-arm64
          - os: macos-latest
            rid: osx-x64
          - os: windows-latest
            rid: win-x64
          - os: ubuntu-latest
            rid: linux-x64

    runs-on: ${{ matrix.os }}

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.x'

      - name: Publish self-contained binary
        run: |
          dotnet publish markdown-journal-cli/markdown-journal-cli.csproj \
            --configuration Release \
            --runtime ${{ matrix.rid }} \
            --self-contained true \
            -p:PublishSingleFile=true \
            --output ./publish/${{ matrix.rid }}

      - name: Upload to GitHub Release
        uses: softprops/action-gh-release@v2
        with:
          files: ./publish/${{ matrix.rid }}/*
```

---

## 10. GitHub Releases Direct Download (No Package Manager, No .NET Required)

The simplest possible distribution route. You build self-contained single-file binaries and attach them to a GitHub Release. Users download the right file for their platform and put it somewhere on their `PATH`. No package manager, no .NET runtime, no installer.

### What users do

```bash
# macOS (Apple Silicon) — download, make executable, move to PATH
curl -L -o mdjournal \
  https://github.com/CollinRobison/markdown-journal-cli/releases/latest/download/mdjournal-osx-arm64
chmod +x mdjournal
mv mdjournal /usr/local/bin/

# macOS (Intel)
curl -L -o mdjournal \
  https://github.com/CollinRobison/markdown-journal-cli/releases/latest/download/mdjournal-osx-x64
chmod +x mdjournal && mv mdjournal /usr/local/bin/

# Windows — download mdjournal-win-x64.exe, put it anywhere on your PATH
# (or just run it from the download folder by double-clicking / calling directly)

# Linux
curl -L -o mdjournal \
  https://github.com/CollinRobison/markdown-journal-cli/releases/latest/download/mdjournal-linux-x64
chmod +x mdjournal && sudo mv mdjournal /usr/local/bin/
```

You can document this in the README with a one-liner per platform. That's all users need.

### What you need to do

**1. Add these properties to the `.csproj`** to enable single-file output:

```xml
<PropertyGroup>
  <!-- Trim unused code to reduce binary size (~30–60% smaller) -->
  <PublishTrimmed>true</PublishTrimmed>
  <!-- Bundle everything into one file -->
  <PublishSingleFile>true</PublishSingleFile>
</PropertyGroup>
```

> **Note on trimming:** `PublishTrimmed` can break apps that use reflection heavily. Test the trimmed binary before releasing — if things break, remove `PublishTrimmed` and just use `PublishSingleFile`.

**2. Add a GitHub Actions workflow** (`.github/workflows/release.yml`) that builds per-platform binaries whenever you create a GitHub Release:

```yaml
name: Release

on:
  release:
    types: [published]

jobs:
  build:
    strategy:
      matrix:
        include:
          - os: macos-latest
            rid: osx-arm64
            asset_name: mdjournal-osx-arm64
          - os: macos-latest
            rid: osx-x64
            asset_name: mdjournal-osx-x64
          - os: windows-latest
            rid: win-x64
            asset_name: mdjournal-win-x64.exe
          - os: ubuntu-latest
            rid: linux-x64
            asset_name: mdjournal-linux-x64

    runs-on: ${{ matrix.os }}

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.x'

      - name: Publish
        shell: bash
        run: |
          dotnet publish markdown-journal-cli/markdown-journal-cli.csproj \
            --configuration Release \
            --runtime ${{ matrix.rid }} \
            --self-contained true \
            -p:PublishSingleFile=true \
            -p:PublishTrimmed=true \
            --output ./publish

          # Rename the output to the asset name
          find ./publish -maxdepth 1 -type f ! -name "*.pdb" \
            -exec mv {} ./publish/${{ matrix.asset_name }} \;

      - name: Upload to Release
        uses: softprops/action-gh-release@v2
        with:
          files: ./publish/${{ matrix.asset_name }}
```

**3. Create a GitHub Release** (tag `v0.1.0`, mark as latest). The workflow fires automatically, builds all four binaries, and attaches them to the release. That's it.

### How to update

1. Bump `<Version>` in the `.csproj`, commit, push.
2. On GitHub: **Releases → Draft a new release → Tag: v0.2.0 → Publish**.
3. The workflow runs and uploads new binaries to that release.
4. Users re-download the file, or you document an update one-liner.

### Tradeoffs vs. package managers

| | Direct Download | Homebrew / Winget / Choco |
|---|---|---|
| **Setup effort** | Low — one workflow file | High — formula/manifest + submission process |
| **User experience** | Manual PATH setup | `brew install mdjournal` just works |
| **Auto-updates** | Users must re-download manually | `brew upgrade`, `winget upgrade`, `choco upgrade` |
| **Discovery** | Only via your repo/README | Searchable in the package manager |
| **Good for** | Early releases, developer tools, power users | Broad consumer distribution |

This is the right starting point before investing in package manager submissions. Many popular developer tools (like `gh` CLI before Homebrew submission, or various Rust tools) start with this model.

### Uninstalling

Because installation is just "copy a file to a directory", uninstalling is just deleting that file:

```bash
# macOS / Linux
rm /usr/local/bin/mdjournal

# Windows (PowerShell) — replace the path with wherever you put it
Remove-Item "$env:USERPROFILE\bin\mdjournal.exe"

# Windows (Command Prompt)
del "%USERPROFILE%\bin\mdjournal.exe"
```

There is no registry entry, no installer database, and no package manager state to clean up. The binary is the entire installation.

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
