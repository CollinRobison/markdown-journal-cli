# Markdown Journal CLI

A CLI for creating and maintaining markdown journals — with file-change tracking,
automatic table-of-contents generation, and safe transactional updates.

[![NuGet](https://img.shields.io/nuget/v/markdown-journal-cli?label=NuGet)](https://www.nuget.org/packages/markdown-journal-cli) [![CI](https://github.com/CollinRobison/markdown-journal-cli/actions/workflows/ci.yml/badge.svg)](https://github.com/CollinRobison/markdown-journal-cli/actions/workflows/ci.yml) [![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

> Badge notes:
> - NuGet badge URL is ready for package ID `markdown-journal-cli` after publish.
> - CI badge will go live once the workflow runs for the first time.

## Quick Start

```bash
# Create a new journal in the current directory
mdjournal new MyJournal

# Adopt an existing markdown directory as a managed journal
mdjournal init --path ~/Documents/Notes

# Add an entry
mdjournal add entry "Daily Standup" --path ~/Documents/MyJournal

# Sync tracking/config/TOC after a pull or merge (no Last Edited writes)
mdjournal update --path ~/Documents/MyJournal journal --sync
```

## Installation

### Download and install (recommended)

Go to the [Releases page](https://github.com/CollinRobison/markdown-journal-cli/releases) and download the asset that matches your OS and chip.

<details>
<summary><strong>macOS</strong></summary>

Pick the right asset:
- **Apple Silicon:** `mdjournal-osx-arm64.tar.gz`
- **Intel:** `mdjournal-osx-x64.tar.gz`

```bash
# Replace the filename with the one you downloaded
tar -xzf mdjournal-osx-arm64.tar.gz
chmod +x mdjournal
sudo mv mdjournal /usr/local/bin/
mdjournal --version
```

If macOS blocks the binary ("cannot be opened because the developer cannot be verified"), run:

```bash
xattr -d com.apple.quarantine /usr/local/bin/mdjournal
```

**Update:** download the new release asset and repeat the install steps above — the `mv` will replace the existing binary.

**Remove:**

```bash
sudo rm /usr/local/bin/mdjournal
```

</details>

<details>
<summary><strong>Linux</strong></summary>

Pick the right asset:
- **x64 (most desktops/servers):** `mdjournal-linux-x64.tar.gz`
- **ARM64 (Raspberry Pi, etc.):** `mdjournal-linux-arm64.tar.gz`

```bash
# Replace the filename with the one you downloaded
tar -xzf mdjournal-linux-x64.tar.gz
chmod +x mdjournal
sudo mv mdjournal /usr/local/bin/
mdjournal --version
```

**Update:** download the new release asset and repeat the install steps above — the `mv` will replace the existing binary.

**Remove:**

```bash
sudo rm /usr/local/bin/mdjournal
```

</details>

<details>
<summary><strong>Windows</strong></summary>

Download `mdjournal-win-x64.zip`, then extract it.

To add `mdjournal.exe` to your PATH:
1. Move `mdjournal.exe` to a permanent folder, e.g. `C:\Tools\`.
2. Open **Settings → System → About → Advanced system settings → Environment Variables**.
3. Under **System variables**, select **Path** and click **Edit**.
4. Click **New** and add your folder path (e.g. `C:\Tools`).
5. Click OK, then open a **new** terminal and verify:

```powershell
mdjournal --version
```

**Update:** download the new release asset, extract it, and replace `mdjournal.exe` in the folder you chose above.

**Remove:** delete `mdjournal.exe` from its folder. If you added the folder solely for this tool, also remove it from your PATH using the same Environment Variables dialog.

</details>

If a release does not yet include your platform asset, see the install options below.

### Install via dotnet tool

```bash
dotnet tool install -g markdown-journal-cli
mdjournal --version
```

**Update:**

```bash
dotnet tool update -g markdown-journal-cli
```

**Remove:**

```bash
dotnet tool uninstall -g markdown-journal-cli
```

### Build from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
git clone https://github.com/CollinRobison/markdown-journal-cli.git
cd markdown-journal-cli
dotnet pack markdown-journal-cli --configuration Release
dotnet tool install -g --add-source ./markdown-journal-cli/nupkg CollinRobison.mdjournal
mdjournal --version
```

**Update:** pull the latest changes and re-pack, then update the tool:

```bash
git pull
dotnet pack markdown-journal-cli --configuration Release
dotnet tool update -g --add-source ./markdown-journal-cli/nupkg CollinRobison.mdjournal
```

**Remove:**

```bash
dotnet tool uninstall -g CollinRobison.mdjournal
```

## How It Works

Each journal has normal markdown files plus three managed metadata artifacts:

- `.journalrc` for user-facing journal settings (name, TOC filename, ignore list)
- `.mdjournal/.journalindex` for file-change tracking (hash-based)
- `.mdjournal/.journaltoc` for TOC structure (topics + root entries)

`mdjournal update ... journal` keeps them in sync by detecting file changes,
updating tracking/config state, and regenerating the markdown TOC.

## Documentation

- Full command reference: [docs/COMMANDS.md](docs/COMMANDS.md)
- Development workflow: [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md)
- Testing infrastructure: [docs/TESTING.md](docs/TESTING.md)
- Architecture details: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)
- Contribution guide: [CONTRIBUTING.md](CONTRIBUTING.md)
- Security policy: [SECURITY.md](SECURITY.md)
- Code of conduct: [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md)
- Changelog: [CHANGELOG.md](CHANGELOG.md)
- Roadmap: [ROADMAP.md](ROADMAP.md)

## License

MIT. See `LICENSE`.

## Community

- Issues: https://github.com/CollinRobison/markdown-journal-cli/issues
