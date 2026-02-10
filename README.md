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
- ✅ Exception handling with custom exception hierarchy
- ✅ **500+ passing unit tests** covering core functionality
- ✅ Service-oriented architecture with dependency injection
- ✅ Configuration system with `.journalrc` files
- ✅ **Automatic table of contents generation** with smart parent-child detection
- ✅ **File tracking and change detection** using SHA256 hashing
- ✅ **Natural alphanumeric sorting** for entries (file_5 before file_10)
- ✅ **Ignore files functionality** to exclude entries from TOC
- ✅ Entry formatting with customizable separators
- ✅ Nested topic hierarchy support

**Planned Features:**
- ⏳ Additional commands (update, rename, open, search)
- ⏳ Global tool installation
- ⏳ Advanced configuration options
- ⏳ Automatic change detection and synchronization

### Planned Commands
```bash
# TODO: Document these commands when implemented

mdjournal <path> init [name] # adds a the needed items (journalrc, file tracking, and toc) to an existing md file directory and updates all to include directories md files.

mdjournal update --config --dates --toc no flag = all # look at what has changed and update .jounralrc, table of contents, and last edited dates. have an option to check all files in directory and if any aren't in journalrc list them out so people can confirm whether they want to update everything --check to list changes before applying to journalrc --all to appky change without listing.

md journal update file --ignore # updates a file with specific settings like moving to ignore may this and rename should be the same command. 

mdjournal rename <file> --file-added # A command that renames a file and updates that change in the journalrc and table of contents. (maybe have it search and update in other places as well such a places where referenced) --file-added flag is saying someone renamed the file manually and it just needs to be reflected everywhere else. 

mdjournal open [name]                   # Open journal in default editor (start with vscode and vim support)

mdjournal search <term>                 # Search across journal entries


```

**Known Limitations:**
- Global tool installation not yet configured

## 📄 License

TODO: Add license information

## 🤝 Support

TODO: Add support information
- Issue tracker
- Discussions  
- Contact information