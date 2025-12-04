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

### Planned Commands
```bash
# TODO: Document these commands when implemented
md-journal add entry [title]     # Add a new journal entry
md-journal open [name]           # Open journal in default editor
md-journal search <term>         # Search across journal entries
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
- ✅ Exception handling
- ✅ Unit tests for core functionality
- ✅ Service-oriented architecture with dependency injection
- ✅ Configuration system with `.journalrc` files

**Planned Features:**
- ⏳ Additional commands (add, open, search)
- ⏳ Global tool installation
- ⏳ Advanced configuration options

**Known Limitations:**
- Global tool installation not yet configured

## 📄 License

TODO: Add license information

## 🤝 Support

TODO: Add support information
- Issue tracker
- Discussions  
- Contact information