# Markdown Journal CLI

A CLI tool to create and manage markdown journals with a clean, user-friendly interface.

## 🚀 Quick Start

```bash
# Create a new journal in the current directory
mdjournal new MyJournal

# Create a journal at a specific path
mdjournal new WorkJournal --path ~/Documents/Journals
```

## 📋 Table of Contents

- [Installation](#installation)
- [Usage](#usage)
- [Commands](#commands)
- [Contributing](#contributing)
- [Development Status](#development-status)

## 📚 Documentation

- **[Architecture Guide](docs/ARCHITECTURE.md)** - Technical architecture, design decisions, and system patterns
- **[Development Guide](docs/DEVELOPMENT.md)** - Setup, contribution workflow, coding standards, and testing

## Installation

### Prerequisites
- .NET 9.0 or later

### Build from Source
```bash
git clone https://github.com/CollinRobison/markdown-journal-cli.git
cd markdown-journal-cli
dotnet build
dotnet run --project markdown-journal-cli -- --help
```

### Global Installation
```bash
# TODO: Add instructions for global tool installation
# dotnet pack && dotnet tool install -g markdown-journal-cli
```

## Usage

### Basic Usage
```bash
mdjournal <command> [options]
```

### Examples
```bash
# Create a new journal with default settings
mdjournal new

# Create a journal with custom name
mdjournal new "My Daily Journal"

# Create a journal at specific location
mdjournal new ProjectNotes --path ~/Work/Projects

# Add a new journal entry
mdjournal add --path ~/Documents/MyJournal entry "My First Entry"

# Add an entry with heading and subheading
mdjournal add --path ~/Documents/MyJournal entry "Meeting Notes" --heading "Work" --subheading "Team-Meetings"

# Create a .journalrc config for an existing journal
mdjournal add --path ~/Documents/MyJournal config

# Create a Table of Contents file for an existing journal
mdjournal add --path ~/Documents/MyJournal toc

# Create or refresh the tracking index for a journal
mdjournal add --path ~/Documents/MyJournal tracking

# Update journal (config, dates, tracking, and TOC) based on file changes
mdjournal update --path ~/Documents/MyJournal journal

# Update only specific aspects
mdjournal update --path ~/Documents/MyJournal journal --config    # Only update configuration
mdjournal update --path ~/Documents/MyJournal journal --dates     # Only update last edited dates and tracking
mdjournal update --path ~/Documents/MyJournal journal --tracking  # Only update tracking index without metadata
mdjournal update --path ~/Documents/MyJournal journal --toc       # Only update table of contents

# Rename an entry and update all references
mdjournal update --path ~/Documents/MyJournal entry my_entry --name new_name

# Move an entry to a different heading and update its title
mdjournal update --path ~/Documents/MyJournal entry my_entry --headings "Work-Projects" --title "Q1 Goals"

# Add an entry to the ignore list
mdjournal update --path ~/Documents/MyJournal entry draft_notes --ignore
```

## Commands

### `new` - Create New Journal
Creates a new markdown journal directory structure.

**Syntax:**
```bash
mdjournal new [name] [options]
```

**Arguments:**
- `name` - Name of the journal (default: "MyJournal")

**Options:**
- `-p|--path <path>` - Directory where journal will be created (default: current directory)

**Examples:**
```bash
mdjournal new
mdjournal new "Travel Journal" --path ~/Documents
```

### `add entry` - Add New Journal Entry
Adds a new markdown entry to an existing journal.

**Syntax:**
```bash
mdjournal add entry <name> [options]
```

**Arguments:**
- `name` - Name of the journal entry (required)

**Options:**
- `-p|--path <path>` - Path to the journal directory (default: current directory)
- `-t|--title <title>` - Custom title for the entry (default: uses entry name)
- `--he|--heading <heading>` - Top-level topic heading
- `--sh|--subheading <subheading>` - Nested subheadings (use `-` as separator for multiple levels)
- `--ignore` - Add entry file but exclude it from table of contents

**Examples:**
```bash
# Add a simple entry
mdjournal add entry "Daily Standup"

# Add entry with custom title and heading
mdjournal add entry "standup_notes" --title "Daily Standup" --heading "Work"

# Add entry with nested topics
mdjournal add --path ~/Documents/MyJournal entry "api_design" --heading "Tech" --subheading "Backend-API"

# Add entry but don't include in TOC
mdjournal add entry "draft_thoughts" --ignore
```

### `add config` - Create Journal Configuration
Creates a new `.journalrc` configuration file for an existing journal when one does not already exist.

**Syntax:**
```bash
mdjournal add config [options]
```

**Options:**
- `-p|--path <path>` - Path to the journal directory (default: current directory)
- `--toc|--tableofcontents <name>` - TOC file name to parse (without `.md`)
- `-n|--name|--journalname <name>` - Journal name (default: directory name)

**Examples:**
```bash
mdjournal add config --path ~/Documents/MyJournal
mdjournal add config --path ~/Documents/MyJournal --toc TableOfContents --name "My Journal"
```

### `add toc` - Create Table of Contents
Creates a Table of Contents file for an existing journal if it does not already exist.

**Syntax:**
```bash
mdjournal add toc [options]
```

**Options:**
- `-p|--path <path>` - Path to the journal directory (default: current directory)
- `-n|--name|--toc|--tableofcontents <name>` - TOC file name (without `.md`)

**Examples:**
```bash
mdjournal add toc --path ~/Documents/MyJournal
mdjournal add toc --path ~/Documents/MyJournal --name TableOfContents
```

### `add tracking` - Create File Tracking Index
Creates the tracking index for an existing journal if one does not already exist.
If a tracking index file is already present, this command leaves it unchanged.
**Syntax:**
```bash
mdjournal add tracking [options]
```

**Options:**
- `-p|--path <path>` - Path to the journal directory (default: current directory)
- `--ignoreconfig|--ic` - Skip the `.journalrc` existence check

**Examples:**
```bash
mdjournal add tracking --path ~/Documents/MyJournal
mdjournal add tracking --path ~/Documents/MyJournal --ignoreconfig
```

### `update journal` - Update Journal
Detects and synchronizes file changes in your journal. Updates configuration, last edited dates, and table of contents based on added, modified, or deleted files. All updates are performed by default unless specific flags are provided.

**Syntax:**
```bash
mdjournal update journal [options]
```

**Options:**
- `-p|--path <path>` - Path to the journal directory (default: current directory)
- `-c|--config` - Only update the `.journalrc` configuration (add/remove entries)
- `-d|--dates` - Only update "Last Edited:" dates for modified files and update tracking
- `-t|--tracking` - Only update tracking index without modifying "Last Edited:" metadata (overrides `--dates`)
- `--toc|--tableofcontents` - Only regenerate the table of contents

**Behavior:**
- **Without flags**: Updates configuration, dates, and TOC (equivalent to `--config --dates --toc`)
- **With flags**: Only performs the specified updates
- **Tracking override**: When `--tracking` is specified with `--dates`, tracking takes precedence and metadata is not modified
- **Change Detection**: Uses SHA256 hashing to identify added, modified, and deleted files. 
- **TOC File Exclusion**: Automatically prevents the TOC file from appearing as an entry in its own contents

**Examples:**
```bash
# Update everything (config, dates, and TOC)
mdjournal update journal --path ~/Documents/MyJournal

# Only update configuration with new/deleted entries
mdjournal update journal --path ~/Documents/MyJournal --config

# Only update last edited dates for modified files and refresh tracking
mdjournal update journal --path ~/Documents/MyJournal --dates

# Only update tracking index without modifying file metadata
mdjournal update journal --path ~/Documents/MyJournal --tracking

# Only regenerate the table of contents
mdjournal update journal --path ~/Documents/MyJournal --toc

# Update both config and TOC, but skip dates
mdjournal update journal --path ~/Documents/MyJournal --config --toc
```

**What Gets Updated:**
- **Configuration (`--config`)**: Adds new markdown files to `.journalrc` and removes deleted files
- **Dates (`--dates`)**: Updates "Last Edited:" metadata in modified files and refreshes the tracking index
- **Tracking (`--tracking`)**: Updates tracking index without modifying "Last Edited:" metadata (useful for resynchronizing without changing file contents)
- **Table of Contents (`--toc`)**: Regenerates the TOC markdown file from current configuration

### `update entry` - Update a Journal Entry
Renames an entry file, changes its TOC title, moves it to a different heading, or updates its ignore status. All referenced locations (config, tracking index, TOC) are updated atomically.

**Syntax:**
```bash
mdjournal update entry <fileName> [options]
```

**Arguments:**
- `fileName` - Name of the file to update (with or without `.md`)

**Options:**
- `-p|--path <path>` - Path to the journal directory (default: current directory)
- `-n|--name <name>` - New entry name; updates the filename and TOC title when they currently match (letters, digits, underscores, and spaces only)
- `-t|--title <title>` - New display title shown in the table of contents (does not rename the file)
- `-h|--headings <headings>` - New location in the TOC hierarchy; use `-` to separate heading levels and `_` for spaces within a heading (e.g. `Projects-2024_Goals`)
- `--ignore` - Add the entry to the ignore list so it won't appear in the TOC
- `--unignore` - Remove the entry from the ignore list

**Examples:**
```bash
# Rename an entry (updates filename and TOC title)
mdjournal update entry my_notes --name meeting_notes

# Update only the TOC display title without renaming the file
mdjournal update entry meeting_notes --title "Q1 Meeting Notes"

# Move an entry to a different heading location
mdjournal update entry meeting_notes --headings "Work-Meetings"

# Rename the file and move its heading in one command
mdjournal update entry draft --name api_design --headings "Tech-Backend" --title "API Design Doc"

# Exclude an entry from the TOC
mdjournal update entry draft_thoughts --ignore

# Re-include a previously ignored entry
mdjournal update entry draft_thoughts --unignore
```

## Contributing

Interested in contributing? Check out the **[Development Guide](docs/DEVELOPMENT.md)** for:
- Setup instructions
- How to add new commands
- Testing guidelines
- Code standards

For technical details about the project architecture, see the **[Architecture Guide](docs/ARCHITECTURE.md)**.

## Development Status

**Current Status:**
- ✅ Basic project structure
- ✅ `new` command implementation  
- ✅ `add entry` command for creating journal entries
- ✅ `add config`, `add toc`, and `add tracking` commands for existing journals
- ✅ **`update journal` command** for synchronizing file changes (config, dates, TOC, tracking)
- ✅ **`update entry` command** for renaming entries, updating TOC titles, moving headings, and managing ignore status
- ✅ Exception handling with custom exception hierarchy
- ✅ **798+ passing unit tests** covering core functionality
- ✅ Service-oriented architecture with dependency injection
- ✅ Configuration system with `.journalrc` files
- ✅ **Automatic table of contents generation** with smart parent-child detection
- ✅ **File tracking and change detection** using SHA256 hashing
- ✅ **Automatic "Last Edited" date updates** for modified files
- ✅ **Tracking-only update mode** to resynchronize without modifying file metadata
- ✅ **TOC file exclusion** - prevents TOC from appearing in its own contents
- ✅ **Natural alphanumeric sorting** for entries (file_5 before file_10)
- ✅ **Ignore files functionality** to exclude entries from TOC
- ✅ Entry formatting with customizable separators
- ✅ Nested topic hierarchy support

**Planned Features:**
- ⏳ `init` command — adopt an existing markdown directory as a journal
- ⏳ `update entry --rename-toc` — rename the TOC file and update all references
- ⏳ `open` command — open journal in default editor
- ⏳ `search` command — full-text search across entries
- ⏳ `--version` flag
- ⏳ Global tool installation
- ⏳ Pre-update change preview (`--check` flag)

**Known Limitations:**
- Global tool installation not yet configured

## 📄 License

TODO: Add license information

## 🤝 Support

TODO: Add support information
- Issue tracker
- Discussions  
- Contact information