# Roadmap

This document outlines the planned work for Markdown Journal CLI. Items are grouped by theme and roughly ordered by priority. Status reflects the current state of each item.

> **Legend:** ✅ Done · 🚧 In Progress · ⏳ Planned · 💡 Under Consideration

---

## Release Readiness

These items must be completed before the repository goes public.

| Status | Item |
|--------|------|
| ⏳ | **Build pipeline & versioned releases** — CI/CD pipeline that builds, packages, and publishes versioned artifacts to GitHub Releases so users can download pre-built binaries. |
| ⏳ | **Documentation cleanup** — Review and polish all docs (`README`, `CONTRIBUTING`, `docs/`) to ensure they are accurate, complete, and contributor-friendly. |
| ⏳ | **Public repo & collaborator readiness** — Configure branch protection rules on `main` (required reviews, no direct pushes), set up issue/PR templates, and make the repository public. |

---

## Commands

New CLI commands planned for upcoming releases.

| Status | Command | Description |
|--------|---------|-------------|
| ⏳ | `open` | Open the current journal in the system's default editor. |
| ⏳ | `search` | Full-text search across all journal entries with filtering options. |

---

## Architecture

| Status | Item |
|--------|------|
| 💡 | **SDK / library layer** — Extract core business logic into a separate NuGet package so developers can build their own tools on top of the journal engine without depending on the CLI. |

---

## Developer Experience & Tooling

| Status | Item |
|--------|------|
| 💡 | **AI agent integrations** — Ship first-class custom agents, prompts, and skills for Copilot, Claude, OpenCode, and similar tools. Include a CLI command (`mdjournal agents add`) to scaffold these files into any project. |

---

## Contributing

Have an idea or want to pick something up? Open an issue and let's talk.
