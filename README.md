# Markdown Journal CLI

A CLI for creating and maintaining markdown journals — with file-change tracking,
automatic table-of-contents generation, and safe transactional updates.

[![NuGet](https://img.shields.io/nuget/v/mdjournal?label=NuGet)](https://www.nuget.org/packages/mdjournal) [![CI](https://github.com/CollinRobison/markdown-journal-cli/actions/workflows/ci.yml/badge.svg)](https://github.com/CollinRobison/markdown-journal-cli/actions/workflows/ci.yml) [![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

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
- **Apple Silicon:** `mdjournal-<version>-osx-arm64.zip`
- **Intel:** `mdjournal-<version>-osx-x64.zip`

```bash
# Replace the filename with the one you downloaded
unzip mdjournal-<version>-osx-arm64.zip
chmod +x markdown-journal-cli
sudo mv markdown-journal-cli /usr/local/bin/mdjournal
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
- **x64 (most desktops/servers):** `mdjournal-<version>-linux-x64.zip`
- **ARM64 (Raspberry Pi, etc.):** `mdjournal-<version>-linux-arm64.zip`

```bash
# Replace the filename with the one you downloaded
unzip mdjournal-<version>-linux-x64.zip
chmod +x markdown-journal-cli
sudo mv markdown-journal-cli /usr/local/bin/mdjournal
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

Pick the right asset:
- **x64 (most PCs):** `mdjournal-<version>-win-x64.zip`
- **ARM64:** `mdjournal-<version>-win-arm64.zip`

Download the asset you need, then extract it.

To add `mdjournal` to your PATH:
1. Rename `markdown-journal-cli.exe` to `mdjournal.exe`, then move it to a permanent folder, e.g. `C:\Tools\`.
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
dotnet tool install -g mdjournal
mdjournal --version
```

**Update:**

```bash
dotnet tool update -g mdjournal
```

**Remove:**

```bash
dotnet tool uninstall -g mdjournal
```

### Build from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
git clone https://github.com/CollinRobison/markdown-journal-cli.git
cd markdown-journal-cli
dotnet pack markdown-journal-cli --configuration Release
dotnet tool install -g --add-source ./markdown-journal-cli/nupkg mdjournal
mdjournal --version
```

**Update:** pull the latest changes and re-pack, then update the tool:

```bash
git pull
dotnet pack markdown-journal-cli --configuration Release
dotnet tool update -g --add-source ./markdown-journal-cli/nupkg mdjournal
```

**Remove:**

```bash
dotnet tool uninstall -g mdjournal
```

## How It Works

Each journal has normal markdown files plus three managed metadata artifacts:

- `.journalrc` for user-facing journal settings (name, TOC filename, TOC ignore list, tracking no-track list)
- `.mdjournal/.journalindex` for file-change tracking (hash-based)
- `.mdjournal/.journaltoc` for TOC structure (topics + root entries)

`mdjournal update ... journal` keeps them in sync by detecting file changes,
updating tracking/config state, and regenerating the markdown TOC only when the
generated TOC differs from the current file beyond the `Last Edited` metadata
date.

### `.journalrc` ignore vs no-track

`.journalrc` has two separate exclusion concepts:

- `tableOfContents.ignoreFiles`: entries stay tracked, but are omitted from the generated TOC.
- `trackingIndex.noTrack`: files are excluded from the tracking index entirely, so update/sync does not report, hash, or add them to `.mdjournal/.journalindex`.

`trackingIndex.noTrack` accepts file names, relative file paths, or directories. Matching is case-insensitive and normalizes slashes; glob patterns are not currently supported.

```json
{
  "journalName": "MyJournal",
  "tableOfContents": {
    "file": "1a-TableOfContents.md",
    "extensions": [".md"],
    "ignoreFiles": ["draft.md"]
  },
  "trackingIndex": {
    "noTrack": ["scratch.md", "private/secret.md", "archive"]
  }
}
```

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
