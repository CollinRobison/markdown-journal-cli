# Best Practices for Documenting an Open Source .NET CLI Tool

> **Research Date:** April 2025  
> **Scope:** README structure, GitHub badges, /docs folder contents, popular .NET CLI tool documentation patterns, Spectre.Console.Cli auto-doc tooling  

---

## Executive Summary

Across the most prominent .NET CLI tools on GitHub — Spectre.Console, dotnet-script, NUKE, Cake, and BenchmarkDotNet — a consistent documentation pattern emerges: a **concise, scannable README** anchored by shields.io badges, a quick-install one-liner, a brief feature tour, and pointers to a dedicated documentation website or `/docs` folder. Extended reference documentation (command references, API docs, tutorials) always lives outside the README, either on a dedicated site (spectreconsole.net, cakebuild.net, nuke.build, benchmarkdotnet.org) or in a `docs/` folder within the repo. For Spectre.Console.Cli specifically, there is no official first-party auto-documentation generator, but several community patterns and a T4/Reflection-based approach exist for generating markdown command references from `CommandApp` metadata.

---

## Table of Contents

1. [README Section Structure](#1-readme-section-structure)
2. [How Popular .NET CLI Tools Structure Their READMEs](#2-how-popular-net-cli-tools-structure-their-readmes)
3. [GitHub Badge Catalog](#3-github-badge-catalog)
4. [Exact Badge Markdown Syntax (Shields.io)](#4-exact-badge-markdown-syntax-shieldsio)
5. [/docs Folder Structure Recommendations](#5-docs-folder-structure-recommendations)
6. [How Popular Tools Document Their CLIs](#6-how-popular-tools-document-their-clis)
7. [Auto-Generating CLI Help Docs from Spectre.Console.Cli](#7-auto-generating-cli-help-docs-from-spectreconsolecli)
8. [Complete README Template](#8-complete-readme-template)
9. [Key Repositories Summary](#9-key-repositories-summary)
10. [Confidence Assessment](#10-confidence-assessment)
11. [Footnotes](#11-footnotes)

---

## 1. README Section Structure

Based on analysis of five prominent .NET CLI tools, the consensus section order for a CLI tool README is:

```
[Logo / Project Name]
[Badges Row]
[One-line elevator pitch]
[Table of Contents]       ← strongly recommended for READMEs > ~30 lines
[Features]
[Prerequisites / Requirements]
[Installing]
[Usage / Quick Start]
[Commands Reference]      ← or link out to /docs
[Configuration]
[Examples]
[Contributing]
[Code of Conduct]
[License]
```

### Section-by-Section Guidance

| Section | Priority | Notes |
|---------|----------|-------|
| **Badges** | Essential | Build, NuGet version, downloads, license. See §4 for exact syntax. |
| **Elevator pitch** | Essential | 1–3 sentences. What it is and why it matters. No jargon. |
| **Table of Contents** | Strongly recommended | GitHub auto-anchors headings. Use `#anchor-name` links. |
| **Features** | Recommended | Bullet list of 4–8 capabilities. |
| **Installing** | Essential | Platform-specific if needed. Always include `dotnet tool install -g <id>`. |
| **Usage / Quick Start** | Essential | A runnable copy-paste example should appear within the first scroll. |
| **Commands Reference** | Recommended | For small CLIs, inline in README. For large CLIs, link to `/docs/commands.md`. |
| **Configuration** | Conditional | Include if the tool has config files or env vars. |
| **Examples** | Recommended | Annotated code blocks or link to `/samples` folder. |
| **Contributing** | Required for OSS | Brief guidance; full details in `CONTRIBUTING.md`. |
| **Code of Conduct** | Required for OSS | Reference `CODE_OF_CONDUCT.md` or .NET Foundation CoC. |
| **License** | Required | SPDX identifier + link to `LICENSE` file. |

> **Note on length**: Popular .NET tools keep their READMEs **short and scannable** (under ~150 lines) and redirect deeper content to external sites or `/docs`. Spectre.Console's README is 47 lines[^1]; NUKE's is 120 lines[^2].

---

## 2. How Popular .NET CLI Tools Structure Their READMEs

### Spectre.Console (`spectreconsole/spectre.console`)

**Sections:** NuGet badge → one-paragraph pitch → ToC → Features (bullet list with screenshot) → Important Notices (pinned issue link) → Installing (`dotnet add package`) → Documentation (external site) → Examples (link to examples repo) → Code of Conduct → .NET Foundation → License[^1]

**Badge style:** Single `style=flat` NuGet version badge in italics at top.

**Key pattern:** README is intentionally minimal. All extended documentation lives at **https://spectreconsole.net**. The repo root has `CODE_OF_CONDUCT.md`, `CONTRIBUTING.md`, and `LICENSE.md` as separate files.[^3]

---

### dotnet-script (`filipw/dotnet-script`)

**Sections:** Title → Build Status badge → NuGet Packages table (multi-package with version badges + framework targets) → Installing (Global Tool, Windows PS1, Linux/Mac curl, Docker, GitHub Releases) → Usage → Scaffolding → Running Scripts → NuGet Packages inline → Creating DLLs/Exes → REPL → Piping → Debugging → CI → Configuration[^4]

**Badge style:** Azure DevOps build badge + per-package NuGet badges displayed in a **Markdown table** (package name | version badge | target frameworks). Uses `http://img.shields.io/nuget/v/{package}.svg?maxAge=10800` URL format.[^4]

**Key pattern:** Has no external doc site; all documentation is in the README itself. This works because the command surface is large but well-organized with headed sections. Makes heavy use of code blocks with `shell` and `c#` syntax highlighting.

---

### NUKE (`nuke-build/nuke`)

**Sections:** Logo → Tagline → Badges row (NuGet release, pre-release, downloads, license — all `style=for-the-badge`) → ToC → Elevator Pitch → Example (screenshot) → Build Status (multi-CI table) → Technology Sponsors[^2]

**Badge style:** `style=for-the-badge` with `logo=nuget` inline logo. Uses a download count badge with an embedded base64 PNG logo.[^2]

**Key pattern:** README is a "marketing page" — it sells the tool and then points to https://nuke.build/docs. The repo also contains a `docs/` folder used by Docusaurus.[^5]

---

### Cake (`cake-build/cake`)

**Sections:** Title → Runner table (NuGet versions for Cake.Tool, Cake.Frosting, Cake.SDK) → Continuous Integration table (8 build servers × platforms) → Code Coverage badge → ToC → Documentation (external site link) → Contributing → Get in Touch → License[^6]

**Badge style:** Multiple CI providers shown in a table with `![Name](badge-url)` syntax. Uses `img.shields.io/nuget/v/{package}.svg` format. Also uses Azure DevOps badge URL (`dev.azure.com/.../badge`) format directly.[^6]

**Key pattern:** The CI table is a signature Cake README feature — showing that it builds on 8+ platforms/CIs simultaneously. All actual documentation at **https://cakebuild.net**. Cake also maintains a `ReleaseNotes.md` in root.

---

### BenchmarkDotNet (`dotnet/BenchmarkDotNet`)

**Sections:** Logo (centered HTML `<div>`) → Badge row (NuGet version, MyGet pre-release, downloads, stars, license, Twitter follow) → Navigation links (Getting started, Documentation, Learn more) → Description paragraph → Quick example code block → Result table → Features (expandable subsections: Simplicity, Automation, Reliability, Friendliness)[^7]

**Badge style:** All badges in a centered `<div align="center">` block using `style=default` (flat). Uses `img.shields.io/badge/license-MIT-blue.svg` static badge for license.[^7]

**Key pattern:** Uses HTML `<div>` alignment for polished centering. Documentation lives at **https://benchmarkdotnet.org**. The repo includes a `docs/` folder with logo assets and documentation source.

---

### dotnet/sdk

**Sections:** Title → Build Status (Azure Pipelines, two rows: Public + MS Internal) → Installing (links to dotnet.microsoft.com) → How to Engage and Contribute → Building the SDK → Testing → PR Timeline → Triage Process → License[^8]

**Key pattern:** Microsoft internal/flagship repos have a simpler badge story (build status only) and lean heavily on developer guide links in `documentation/`.

---

## 3. GitHub Badge Catalog

The following badges are used consistently across high-quality .NET OSS projects:

| Badge | Purpose | Service |
|-------|---------|---------|
| **NuGet Version** | Current stable package version | shields.io / NuGet |
| **NuGet Pre-release Version** | Latest including pre-release | shields.io / NuGet |
| **NuGet Downloads** | Total download count (social proof) | shields.io / NuGet |
| **GitHub Actions Build** | CI pass/fail for workflow | shields.io / GitHub |
| **Azure Pipelines Build** | CI pass/fail (Azure DevOps) | shields.io / Azure |
| **License** | SPDX license identifier | shields.io (static) |
| **.NET Version** | Supported .NET target | shields.io (static) |
| **GitHub Stars** | Star count (social proof) | shields.io / GitHub |
| **Code Coverage** | Test coverage % | Coveralls / Codecov |
| **GitHub Issues** | Open issue count | shields.io / GitHub |
| **Twitter/Follow** | Social media link | shields.io (social) |

---

## 4. Exact Badge Markdown Syntax (Shields.io)

All shields.io badges follow this URL pattern:

```
https://img.shields.io/{category}/{parameters}.svg?{query-options}
```

**Style options:** `flat` (default) · `flat-square` · `plastic` · `for-the-badge` · `social`

---

### 4.1 NuGet Version Badge

**URL format:**[^9]
```
https://img.shields.io/nuget/v/{packageName}.svg
```

**Latest stable version:**
```markdown
[![NuGet Version](https://img.shields.io/nuget/v/YourPackageId.svg?style=flat&label=NuGet)](https://www.nuget.org/packages/YourPackageId)
```

**Latest stable — `for-the-badge` style (NUKE pattern):**
```markdown
[![NuGet](https://img.shields.io/nuget/v/nuke.common?logo=nuget&label=release&style=for-the-badge)](https://www.nuget.org/packages/nuke.common)
```

**Latest stable — simple flat (BenchmarkDotNet pattern):**
```markdown
[![NuGet](https://img.shields.io/nuget/v/BenchmarkDotNet.svg)](https://www.nuget.org/packages/BenchmarkDotNet/)
```

**Pre-release version:**
```markdown
[![NuGet Pre-release](https://img.shields.io/nuget/vpre/YourPackageId.svg?style=flat&label=NuGet+Pre-release)](https://www.nuget.org/packages/YourPackageId/absoluteLatest)
```

**Download count:**
```markdown
[![NuGet Downloads](https://img.shields.io/nuget/dt/YourPackageId.svg)](https://www.nuget.org/packages/YourPackageId/)
```

> Replace `YourPackageId` with the exact NuGet package ID (case-insensitive, but match the casing on NuGet.org for clarity).

---

### 4.2 GitHub Actions Workflow Status Badge

**URL format:**[^10]
```
https://img.shields.io/github/actions/workflow/status/{owner}/{repo}/{workflow-file}?branch={branch}
```

**Standard flat:**
```markdown
[![Build](https://img.shields.io/github/actions/workflow/status/your-org/your-repo/build.yml?branch=main&label=build)](https://github.com/your-org/your-repo/actions/workflows/build.yml)
```

**With GitHub logo (NUKE pattern):**
```markdown
[![GitHub Actions](https://img.shields.io/github/actions/workflow/status/nuke-build/nuke/ubuntu-latest.yml?branch=develop&label=build&style=flat-square&logo=github&logoColor=white)](https://github.com/nuke-build/nuke/actions)
```

**Cake's GitHub Actions pattern (using the workflow badge URL directly):**
```markdown
[![Build Status](https://github.com/cake-build/cake/actions/workflows/build.yml/badge.svg?branch=develop)](https://github.com/cake-build/cake/actions/workflows/build.yml)
```

> **Tip:** Cake and others use GitHub's *native* badge URL (`github.com/{owner}/{repo}/actions/workflows/{file}/badge.svg`) rather than shields.io for GitHub Actions — this is equally valid and has no external dependency.[^6]

---

### 4.3 License Badge

**Static badge (most common pattern):**
```markdown
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
```

**GitHub license (dynamic, reads from repo `LICENSE` file):**
```markdown
[![License](https://img.shields.io/github/license/your-org/your-repo)](https://github.com/your-org/your-repo/blob/main/LICENSE)
```

**MIT with `for-the-badge` style (NUKE pattern):**
```markdown
[![License](https://img.shields.io/badge/license-MIT-blue.svg?style=for-the-badge)](LICENSE.md)
```

**Common license identifiers for the static badge:**

| License | Badge Markdown |
|---------|---------------|
| MIT | `https://img.shields.io/badge/license-MIT-blue.svg` |
| Apache 2.0 | `https://img.shields.io/badge/license-Apache%202.0-blue.svg` |
| GPL v3 | `https://img.shields.io/badge/license-GPL%20v3-blue.svg` |
| BSD 3-Clause | `https://img.shields.io/badge/license-BSD%203--Clause-blue.svg` |

---

### 4.4 .NET Version Badge

There is no dedicated shields.io endpoint for "supported .NET version" — these are always **static badges**:[^11]

```markdown
[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/download/dotnet/9.0)
```

```markdown
[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%209.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/download)
```

> Color `512BD4` is the official .NET purple from Microsoft's brand palette.

**Multi-version example (dotnet-script pattern in table):**
```markdown
| Package | Version | Frameworks |
|---------|---------|------------|
| `your-tool` | [![NuGet](https://img.shields.io/nuget/v/your-tool.svg)](https://nuget.org/packages/your-tool) | `net8.0`, `net9.0` |
```

---

### 4.5 Static Badge (Arbitrary Text)

For custom labels not covered by a service:[^11]

```
https://img.shields.io/badge/{label}-{message}-{color}
```

Encoding rules:
- Spaces → `_` or `%20`
- Underscores → `__`  
- Dashes → `--`

```markdown
[![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20macOS%20%7C%20Linux-lightgrey)](https://github.com/your-org/your-repo)
```

---

### 4.6 Full Recommended Badge Row (Copy-Paste Template)

```markdown
[![NuGet Version](https://img.shields.io/nuget/v/YOUR-PACKAGE-ID.svg?style=flat&label=NuGet)](https://www.nuget.org/packages/YOUR-PACKAGE-ID)
[![NuGet Downloads](https://img.shields.io/nuget/dt/YOUR-PACKAGE-ID.svg?style=flat)](https://www.nuget.org/packages/YOUR-PACKAGE-ID)
[![Build](https://img.shields.io/github/actions/workflow/status/YOUR-ORG/YOUR-REPO/build.yml?branch=main&style=flat&label=build)](https://github.com/YOUR-ORG/YOUR-REPO/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg?style=flat)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet&style=flat)](https://dotnet.microsoft.com/download/dotnet/9.0)
```

---

## 5. /docs Folder Structure Recommendations

### Philosophy

The `/docs` folder strategy depends on your project's scale:

| Scale | Approach |
|-------|---------|
| Small tool (1–5 commands) | Inline in README; no `/docs` folder needed |
| Medium tool (5–20 commands) | `/docs` with Markdown files; link from README |
| Large tool / framework | Dedicated documentation site (MkDocs, Docusaurus, Statiq) |

---

### Recommended `/docs` Folder Structure

```
docs/
├── README.md                   # Index / navigation hub for the docs folder
├── getting-started.md          # Installation → first run walkthrough
├── commands/
│   ├── README.md               # Commands overview / index
│   ├── command-name.md         # One file per top-level command
│   └── subcommand-name.md
├── configuration.md            # Config files, env vars, settings
├── advanced/
│   ├── scripting.md
│   └── integrations.md
├── contributing/
│   ├── development-setup.md    # How to build & test locally
│   └── architecture.md        # Internal design decisions
├── CHANGELOG.md                # Or link to root CHANGELOG.md
└── assets/
    ├── logo.png
    └── screenshots/
```

### Files at Repo Root (Separate from /docs)

These community-standard files should live at the **root**, not in `/docs`:

```
CONTRIBUTING.md        # Contribution guidelines
CODE_OF_CONDUCT.md     # Community standards
SECURITY.md            # Vulnerability disclosure policy
CHANGELOG.md           # Release history (optional at root)
LICENSE                # License file
```

All five reference tools (Spectre.Console[^3], NUKE[^5], Cake[^12], dotnet-script, BenchmarkDotNet) follow this pattern.

---

### Documentation Site Tools for .NET Projects

| Tool | Best For | Notes |
|------|----------|-------|
| **[Docusaurus](https://docusaurus.io/)** | Full doc sites | NUKE uses this at nuke.build[^5] |
| **[MkDocs + Material](https://squidfunk.github.io/mkdocs-material/)** | Python-based, beautiful | Popular for OSS projects |
| **[Statiq](https://www.statiq.dev/)** | .NET-native static sites | Spectre.Console uses Statiq for spectreconsole.net |
| **[VitePress](https://vitepress.dev/)** | Vue-based, fast | Increasingly popular |
| **GitHub Wiki** | Simple, no build | Low friction but limited formatting |
| **GitHub Pages** | Hosting layer | Works with any of the above |

> **Recommendation for a markdown-journal-cli:** Start with `/docs` Markdown files. If the command surface grows beyond ~10 commands, adopt **MkDocs Material** (zero .NET toolchain dependency, excellent search, clean navigation).

---

### Man Pages

Man pages (Unix manual pages) are optional but add polish for tools targeting Linux/macOS power users. They are rarely used by .NET CLI tools (none of the five reference projects include them). If desired:

- Store as `docs/man/tool-name.1` (groff format) or generate with `help2man`
- For .NET global tools, the install path does not auto-register man pages; users must manually copy them to `/usr/local/share/man/man1/`

---

## 6. How Popular Tools Document Their CLIs

### Cake — External Website + CHANGELOG

Cake maintains **https://cakebuild.net/docs** with a full documentation portal covering Getting Started, Fundamentals, Running Builds, Writing Builds, Integrations, and Extending Cake.[^13] The repo root contains a `ReleaseNotes.md` (95 KB — very detailed)[^12] and a `CONTRIBUTING.md` with 9 KB of guidelines.[^12] No `/docs` folder in the repo itself — all doc content is in a separate website repo.

**CI Documentation pattern:** Cake's README is notable for its exhaustive **Continuous Integration table** documenting builds across 8 CI providers on multiple platforms. This is a documentation strategy in itself — it signals cross-platform reliability to potential users.[^6]

---

### NUKE — Docusaurus Site + `/docs` in repo

NUKE's documentation lives at **https://nuke.build/docs** (Docusaurus-powered). The repo has a `docs/` folder[^5] used as the Docusaurus content source. The README contains a `CHANGELOG.md` at root (69 KB)[^5] and `CONTRIBUTING.md`. The README's Build Status table links to CI configuration files (`.teamcity/settings.kts`, `.github/workflows/continuous.yml`, etc.) which doubles as "living documentation" of how the CI works.[^2]

**Fast Track in docs:** NUKE's documentation homepage has a "Fast Track ⏱" section with 4 numbered steps to get from zero to running — a pattern worth copying for any CLI tool.[^14]

---

### dotnet-script — Comprehensive README-only

dotnet-script's README is the entire documentation (~600 lines). It is organized with clear `##` and `###` headings covering every feature.[^4] This works because:
1. The tool has a single primary command (`dotnet script`)
2. The README uses a NuGet packages table with per-package badges
3. Command-line switches are documented in a Markdown table with `| Switch | Long switch | description |` columns[^4]

**Switch documentation table pattern:**
```markdown
| Switch | Long switch              | Description                           |
| ------ | ------------------------ | ------------------------------------- |
| -o     | --output                 | Output directory for published files  |
| -n     | --name                   | Name for generated DLL                |
| -d     | --debug                  | Enables debug output                  |
```

---

### BenchmarkDotNet — External Site + Rich README

BenchmarkDotNet uses **https://benchmarkdotnet.org** for full documentation. The README itself is a sophisticated "marketing + quickstart" document featuring:
- Centered `<div>` HTML layout with logo and badge row[^7]
- Navigation link row (inline HTML) pointing to documentation sections[^7]
- A complete runnable benchmark class with result table[^7]
- Feature descriptions (Simplicity, Automation, Reliability, Friendliness) with code examples

---

### Spectre.Console — External Site as Sole Documentation

Spectre.Console's README is intentionally minimal[^1] — it acts as a discovery page only. All documentation is at **https://spectreconsole.net**, which documents the `Spectre.Console.Cli` command system including:
- Command anatomy (settings, arguments, options)
- Branching and sub-commands
- Dependency injection
- Command interceptors
- Testing[^15]

---

## 7. Auto-Generating CLI Help Docs from Spectre.Console.Cli

### Current State (as of April 2025)

**There is no official first-party tool** from the Spectre.Console project to auto-generate markdown documentation from `CommandApp` command trees. However, several viable approaches exist:

---

### Approach 1: Reflection + `--help` Output Capture (Simplest)

Redirect the `--help` output of each command and subcommand to markdown files as part of a build/doc generation step.

```csharp
// In a dedicated doc-gen project or test
var app = new CommandApp();
app.Configure(config =>
{
    config.AddCommand<MyCommand>("my-command");
});

// Capture help output
var output = new StringWriter();
Console.SetOut(output);
app.Run(new[] { "my-command", "--help" });
var helpText = output.ToString();

// Write to docs/commands/my-command.md
File.WriteAllText("docs/commands/my-command.md", $"# my-command\n\n```\n{helpText}\n```");
```

This is a **low-ceremony, always-accurate** approach because the output comes directly from Spectre.Console's own help renderer.[^15]

---

### Approach 2: CommandApp TypeResolver Introspection

Spectre.Console.Cli exposes a `ICommandApp` with a `Configure` callback that receives an `IConfigurator`. You can walk the command model using reflection on the registered `CommandSettings`-derived types:

```csharp
// Each command's settings class has [CommandArgument] and [CommandOption] attributes
// that can be reflected to build a markdown table

var settingsType = typeof(MyCommand.Settings);
var arguments = settingsType
    .GetProperties()
    .Select(p => new {
        Prop = p,
        Arg = p.GetCustomAttribute<CommandArgumentAttribute>(),
        Opt = p.GetCustomAttribute<CommandOptionAttribute>(),
        Desc = p.GetCustomAttribute<DescriptionAttribute>()
    })
    .Where(x => x.Arg != null || x.Opt != null);
```

This produces structured data you can template into markdown.[^15]

---

### Approach 3: Community Tool — `Spectre.Console.Cli.Extensions` / DocGen Patterns

Search GitHub shows community projects that wrap `CommandApp` to emit XML/Markdown documentation. While no single dominant library has emerged, the pattern appears in several repos using the following approach:

1. Build the `CommandApp` in a unit test or standalone console app
2. Use `ITypeRegistrar` to intercept command registrations
3. Walk the `CommandModel` (internal type accessible via reflection or via [Spectre.Console issue #868](https://github.com/spectreconsole/spectre.console/issues/868))
4. Output to markdown

**GitHub Actions integration for doc generation:**
```yaml
- name: Generate CLI docs
  run: dotnet run --project tools/DocGen -- --output docs/commands
```

---

### Approach 4: T4 Templates or Source Generators

For teams using source generators, a Roslyn analyzer can inspect `CommandSettings`-derived classes at compile time and emit markdown documentation as a build artifact. This is the most sophisticated but also the most maintenance-intensive approach.

---

### Approach 5: `xmldoc2md` or `DefaultDocumentation`

For projects that annotate their command settings with XML doc comments (`<summary>`, `<param>`), tools like:

- **[DefaultDocumentation](https://github.com/Doraku/DefaultDocumentation)** — generates markdown from XML documentation
- **[xmldoc2md](https://github.com/charlesdevandiere/xmldoc2md)** — converts XML docs to markdown

These work at the *class/property* level, not the *CLI command* level, but can complement a command reference.

---

### Recommendation

For a small-to-medium CLI tool (the scope of `markdown-journal-cli`):

1. **Short-term:** Manually maintain `docs/commands/` markdown files using the `--help` output as a template. Keep them in sync with a CI check that runs `dotnet run -- --help` and diffs against the docs.
2. **Medium-term:** Add a `tools/DocGen` console project that uses reflection on `CommandSettings` types to auto-generate command reference pages on each release.
3. **Long-term:** If command surface grows significantly, adopt a community Spectre.Console documentation generator or contribute one upstream.

---

## 8. Complete README Template

The following is a production-ready README template based on patterns observed across all five reference projects:

```markdown
# your-tool-name

> One-sentence description of what this tool does and for whom.

[![NuGet Version](https://img.shields.io/nuget/v/YOUR-PACKAGE-ID.svg?style=flat&label=NuGet)](https://www.nuget.org/packages/YOUR-PACKAGE-ID)
[![NuGet Downloads](https://img.shields.io/nuget/dt/YOUR-PACKAGE-ID.svg?style=flat)](https://www.nuget.org/packages/YOUR-PACKAGE-ID)
[![Build](https://img.shields.io/github/actions/workflow/status/YOUR-ORG/YOUR-REPO/build.yml?branch=main&style=flat&label=build)](https://github.com/YOUR-ORG/YOUR-REPO/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg?style=flat)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet&style=flat)](https://dotnet.microsoft.com/download/dotnet/9.0)

## Table of Contents

- [Features](#features)
- [Prerequisites](#prerequisites)
- [Installing](#installing)
- [Usage](#usage)
- [Commands](#commands)
- [Contributing](#contributing)
- [License](#license)

## Features

- ✅ Feature one — brief benefit statement
- ✅ Feature two
- ✅ Feature three
- ✅ Feature four

## Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) or later

## Installing

```shell
dotnet tool install -g YOUR-PACKAGE-ID
```

Verify the installation:

```shell
your-tool --version
```

## Usage

```shell
your-tool [command] [options]
```

**Quick example:**

```shell
your-tool do-something --option value
```

## Commands

| Command | Description |
|---------|-------------|
| `command-one` | Brief description |
| `command-two` | Brief description |

For full command reference, see [docs/commands/](docs/commands/).

## Contributing

Contributions are welcome! Please read [CONTRIBUTING.md](CONTRIBUTING.md) before submitting a pull request.

This project adheres to the [Contributor Covenant Code of Conduct](CODE_OF_CONDUCT.md).

## License

Copyright © [Year] [Your Name]

`your-tool-name` is provided as-is under the MIT license. See [LICENSE](LICENSE) for details.
```

---

## 9. Key Repositories Summary

| Repository | Tool | Documentation Approach | Key Files |
|------------|------|----------------------|-----------|
| [spectreconsole/spectre.console](https://github.com/spectreconsole/spectre.console) | Spectre.Console | Minimal README + spectreconsole.net | `README.md`, `CONTRIBUTING.md`, `LICENSE.md` |
| [filipw/dotnet-script](https://github.com/filipw/dotnet-script) | dotnet-script | Comprehensive README-only (~600 lines) | `README.md` |
| [nuke-build/nuke](https://github.com/nuke-build/nuke) | NUKE | Marketing README + nuke.build (Docusaurus) | `README.md`, `CHANGELOG.md`, `docs/` |
| [cake-build/cake](https://github.com/cake-build/cake) | Cake | CI-table README + cakebuild.net | `README.md`, `ReleaseNotes.md`, `CONTRIBUTING.md` |
| [dotnet/BenchmarkDotNet](https://github.com/dotnet/BenchmarkDotNet) | BenchmarkDotNet | Rich README + benchmarkdotnet.org | `README.md`, `docs/` |
| [dotnet/sdk](https://github.com/dotnet/sdk) | .NET SDK | Dev-focused README + documentation/ | `README.md`, `documentation/` |
| [badges/shields](https://github.com/badges/shields) | Shields.io | — | https://shields.io |

---

## 10. Confidence Assessment

| Finding | Confidence | Basis |
|---------|----------|-------|
| README section order | **High** | Directly verified across 5 repos |
| Exact badge markdown for NuGet | **High** | Verified from shields.io docs + live badge URLs in READMEs |
| GitHub Actions badge syntax | **High** | Verified from NUKE[^2] and Cake[^6] READMEs |
| License badge syntax | **High** | Verified from BenchmarkDotNet[^7] and NUKE[^2] READMEs |
| .NET version badge | **High** | shields.io static badge docs confirmed[^11] |
| `/docs` structure recommendations | **Medium-High** | Synthesized from observed patterns; no single authoritative standard |
| Spectre.Console.Cli auto-doc tooling | **Medium** | No official tool found; community approaches inferred from Spectre.Console docs and GitHub patterns |
| MkDocs / Docusaurus recommendations | **High** | Directly verified NUKE uses Docusaurus[^5]; Statiq used by Spectre.Console website |

**Assumptions made:**
- `markdown-journal-cli` is distributed as a .NET global tool (`dotnet tool install -g`)
- Uses `Spectre.Console.Cli` for command handling (based on project name and existing code)
- MIT license (standard for .NET OSS tools)
- GitHub Actions as primary CI

---

## 11. Footnotes

[^1]: `README.md` — [spectreconsole/spectre.console](https://github.com/spectreconsole/spectre.console/blob/main/README.md). Fetched April 2025. Full content verified: 47-line README with single NuGet badge, ToC with 7 items, external site link.

[^2]: `README.md` — [nuke-build/nuke](https://github.com/nuke-build/nuke/blob/master/README.md). Fetched April 2025. Badge row uses `style=for-the-badge` with `logo=nuget`. Build Status section contains multi-CI table with 5 providers.

[^3]: Root directory listing — [spectreconsole/spectre.console](https://github.com/spectreconsole/spectre.console). Confirmed files: `CODE_OF_CONDUCT.md`, `CONTRIBUTING.md`, `LICENSE.md`, `README.md`, `README.fa.md`, `README.jp.md`, `README.pt-BR.md`, `README.zh.md`.

[^4]: `README.md` — [filipw/dotnet-script](https://github.com/filipw/dotnet-script/blob/master/README.md). Fetched April 2025. Multi-package NuGet table pattern; Azure DevOps build badge; switch documentation table.

[^5]: Root directory listing — [nuke-build/nuke](https://github.com/nuke-build/nuke). Confirmed: `docs/` directory present, `CHANGELOG.md` (69KB), `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`.

[^6]: `README.md` — [cake-build/cake](https://github.com/cake-build/cake/blob/develop/README.md). Fetched April 2025. Runner table with NuGet badges; 8-provider CI table; `ReleaseNotes.md`; native GitHub Actions badge URL pattern.

[^7]: `README.md` — [dotnet/BenchmarkDotNet](https://github.com/dotnet/BenchmarkDotNet/blob/master/README.md). Fetched April 2025. `<div align="center">` badge row; `img.shields.io/badge/license-MIT-blue.svg` static license badge; MyGet pre-release badge.

[^8]: `README.md` — [dotnet/sdk](https://github.com/dotnet/sdk/blob/main/README.md). Fetched April 2025. Azure Pipelines badge (public + MS internal); `documentation/` folder reference.

[^9]: Shields.io NuGet Version badge documentation — https://shields.io/badges/nu-get-version. Path parameters: `variant` (v or vpre), `packageName`. Query parameters: `style`, `logo`, `label`, `color`.

[^10]: NUKE README `img.shields.io/github/actions/workflow/status/{owner}/{repo}/{workflow}` badge syntax verified from [nuke-build/nuke README](https://github.com/nuke-build/nuke/blob/master/README.md).

[^11]: Shields.io static badge documentation — https://shields.io/badges/static-badge. URL pattern: `https://img.shields.io/badge/{label}-{message}-{color}`. Encoding: spaces→`_`, underscores→`__`, dashes→`--`.

[^12]: Root directory listing — [cake-build/cake](https://github.com/cake-build/cake). Confirmed: `ReleaseNotes.md` (95KB), `CONTRIBUTING.md` (9KB), `SECURITY.md`, `CODE_OF_CONDUCT.md`, `LICENSE`.

[^13]: Cake documentation site — https://cakebuild.net/docs/. Sections: Getting Started, Fundamentals, Running Builds, Writing Builds, Integrations, Extending Cake, Team.

[^14]: NUKE documentation site introduction — https://nuke.build/docs/introduction/. "Fast Track" pattern: 4 steps from zero to running.

[^15]: Spectre.Console CLI tutorial — https://spectreconsole.net/cli/tutorials/quick-start-your-first-cli-app. `[CommandArgument]`, `[CommandOption]`, `[Description]`, `[DefaultValue]` attributes; auto-generated `--help` behavior; built-in validation.
