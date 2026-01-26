# Markdown Journal CLI

A CLI tool to create and manage markdown journals with a clean, user-friendly interface.

## 🚀 Quick Start

```bash
# Create a new journal in the current directory
md-journal new MyJournal

# Create a journal at a specific path
md-journal new WorkJournal --path ~/Documents/Journals
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
md-journal <command> [options]
```

### Examples
```bash
# Create a new journal with default settings
md-journal new

# Create a journal with custom name
md-journal new "My Daily Journal"

# Create a journal at specific location
md-journal new ProjectNotes --path ~/Work/Projects

# Add a new journal entry
md-journal add --path ~/Documents/MyJournal entry "My First Entry"

# Add an entry with heading and subheading
md-journal add --path ~/Documents/MyJournal entry "Meeting Notes" --heading "Work" --subheading "Team-Meetings"
```

## Commands

### `new` - Create New Journal
Creates a new markdown journal directory structure.

**Syntax:**
```bash
md-journal new [name] [options]
```

**Arguments:**
- `name` - Name of the journal (default: "MyJournal")

**Options:**
- `-p|--path <path>` - Directory where journal will be created (default: current directory)

**Examples:**
```bash
md-journal new
md-journal new "Travel Journal" --path ~/Documents
```

### `add entry` - Add New Journal Entry
Adds a new markdown entry to an existing journal.

**Syntax:**
```bash
md-journal add entry <name> [options]
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
md-journal add entry "Daily Standup"

# Add entry with custom title and heading
md-journal add entry "standup_notes" --title "Daily Standup" --heading "Work"

# Add entry with nested topics
md-journal add --path ~/Documents/MyJournal entry "api_design" --heading "Tech" --subheading "Backend-API"

# Add entry but don't include in TOC
md-journal add entry "draft_thoughts" --ignore
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
- ✅ Exception handling with custom exception hierarchy
- ✅ **509 passing unit tests** covering core functionality
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
mdjournal add entry [title] [header] [template] [table of contents] [journalrc]   # Add a new journal entry or file

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