# Open-Source Readiness Research: Making markdown-journal-cli Collaborator-Ready

> **Researched:** Based on live fetches from spectreconsole/spectre.console, cake-build/cake, nuke-build/nuke, dotnet/sdk, dotnet/runtime, and contributor-covenant.org
> **Applies to:** `markdown-journal-cli` — a .NET CLI tool

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [License Choice: MIT vs Apache 2.0 vs GPL](#license-choice)
3. [GitHub Community Health Score](#github-community-health-score)
4. [Required Community Files — Complete Templates](#required-community-files)
   - [LICENSE](#licensetxt)
   - [CONTRIBUTING.md](#contributingmd)
   - [CODE_OF_CONDUCT.md](#code_of_conductmd)
   - [SECURITY.md](#securitymd)
   - [.github/PULL_REQUEST_TEMPLATE.md](#githubpull_request_templatemd)
   - [.github/ISSUE_TEMPLATE/bug_report.yml](#githubissue_templatebug_reportyml)
   - [.github/ISSUE_TEMPLATE/feature_request.yml](#githubissue_templatefeature_requestyml)
   - [.github/ISSUE_TEMPLATE/config.yml](#githubissue_templateconfigyml)
   - [.github/CODEOWNERS](#githubcodeowners)
5. [Branch Protection Configuration](#branch-protection-configuration)
6. [Recommended GitHub Labels](#recommended-github-labels)
7. [Key Repositories Summary](#key-repositories-summary)
8. [Confidence Assessment](#confidence-assessment)
9. [Footnotes](#footnotes)

---

## Executive Summary

The de-facto standard for .NET open source CLI tools is the **MIT License**. Every major reference project — spectre.console, cake-build/cake, nuke-build/nuke, and dotnet/sdk — uses MIT without exception. The GitHub Community Health Score checks for six mandatory files: `README.md`, `LICENSE`, `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, issue templates, and a PR template. For `CODE_OF_CONDUCT.md`, the **Contributor Covenant v2.1** is the modern standard (used by spectre.console and the .NET Foundation). Community files for this project should follow the spectre.console/cake-build pattern, which itself descends from the Chocolatey project template — a well-tested, battle-hardened style already familiar to most .NET contributors. This document provides exact, ready-to-use file contents for each required file.

---

## License Choice

### MIT vs Apache 2.0 vs GPL

| License | Patent Grant | Copyleft | Commercial Use | .NET Ecosystem Adoption |
|---------|-------------|----------|----------------|------------------------|
| **MIT** | ❌ None | ❌ None | ✅ Unrestricted | ⭐⭐⭐⭐⭐ Dominant |
| **Apache 2.0** | ✅ Explicit patent grant | ❌ None | ✅ Unrestricted | ⭐⭐ Some large projects |
| **GPL v3** | ✅ via GPL | ✅ Strong (viral) | ⚠️ Restricted (must OSS derivatives) | ⭐ Rare for libraries/tools |

### What .NET Open Source CLI Tools Actually Use

Every major .NET open source CLI tool uses **MIT**:

- **spectre.console** — MIT[^1] (copyright held by `.NET Foundation and Contributors`)
- **cake-build/cake** — MIT[^2] (copyright held by `.NET Foundation and Contributors`)
- **nuke-build/nuke** — MIT[^3] (copyright held by `Maintainers of NUKE`)
- **dotnet/sdk** — MIT[^4] (copyright held by `.NET Foundation and Contributors`)
- **dotnet/runtime** — MIT (same pattern)

### Recommendation for markdown-journal-cli

**Use MIT.** Rationale:
1. It is the universal expectation in the .NET ecosystem — contributors know it, employers allow it, corporate users accept it.
2. No friction for adoption — users can embed, redistribute, or build commercial products without asking.
3. Apache 2.0's main addition (explicit patent grant) is only relevant if you hold patents, which is not typical for an individual/small-team CLI tool.
4. GPL would be hostile to adoption as a CLI tool — it would require anyone who bundles your tool to open-source their project.

---

## GitHub Community Health Score

GitHub measures community health at `https://github.com/<owner>/<repo>/community`. It displays a checklist graded as ✅ Added or ⚠️ Not added yet.[^5]

### Files Checked by the Community Profile

| File / Section | Where GitHub Looks | Notes |
|---|---|---|
| **Description** | Repository metadata | Set in Settings → General |
| **README** | Root, `docs/`, `.github/` | Already present |
| **Code of conduct** | `CODE_OF_CONDUCT.md` in root, `docs/`, or `.github/` | Must match known patterns |
| **Contributing** | `CONTRIBUTING.md` in root, `docs/`, or `.github/` | Any content qualifies |
| **License** | `LICENSE`, `LICENSE.md`, `LICENSE.txt` in root | Must be recognized SPDX |
| **Security policy** | `SECURITY.md` in root or `.github/` | Must have content |
| **Issue templates** | `.github/ISSUE_TEMPLATE/` | Must have `name:` + `about:` (md) or `name:` + `description:` (yml) |
| **Pull request template** | `.github/PULL_REQUEST_TEMPLATE.md` | Any content qualifies |

> **Key rule for issue templates:** For `.yml` issue forms, the frontmatter must include `name:` and `description:`. For `.md` templates, it must include `name:` and `about:`.[^5]

---

## Required Community Files

### `LICENSE`

Use the standard MIT license text. Match the copyright line to your project.

```
MIT License

Copyright (c) 2024 Collin Robison

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

**Source pattern:** spectre.console[^1], cake-build/cake[^2], nuke-build/nuke[^3], dotnet/sdk[^4] all use this exact MIT text with only the copyright name differing.

---

### `CONTRIBUTING.md`

This template is adapted from the battle-tested spectre.console / cake-build style (which itself descends from the Chocolatey project, used with permission)[^6][^8]. It is the most widely recognized pattern in the .NET ecosystem.

```markdown
# Contribution Guidelines

* [Prerequisites](#prerequisites)
* [Definition of trivial contributions](#definition-of-trivial-contributions)
* [Code](#code)
  * [Code style](#code-style)
  * [Unit tests](#unit-tests)
* [Contributing process](#contributing-process)
  * [Get buyoff or find open community issues or features](#get-buyoff-or-find-open-community-issues-or-features)
  * [Set up your environment](#set-up-your-environment)
  * [Prepare commits](#prepare-commits)
  * [Submit pull request](#submit-pull-request)
  * [Respond to feedback on pull request](#respond-to-feedback-on-pull-request)
* [Other general information](#other-general-information)

## Prerequisites

By contributing to markdown-journal-cli, you assert that:

* The contribution is your own original work.
* You have the right to assign the copyright for the work (it is not owned by
  your employer, or you have been given copyright assignment in writing).
* You [license](./LICENSE) the contribution under the MIT license applied to
  the rest of the markdown-journal-cli project.
* You agree to follow the [code of conduct](./CODE_OF_CONDUCT.md).

## Definition of trivial contributions

It's hard to define what is a trivial contribution. Sometimes even a 1
character change can be considered significant. The decision on what is trivial
comes from the maintainers of the project.

What is generally considered trivial:

* Fixing a typo.
* Documentation changes.

What is generally **not** considered trivial:

* Changes to any code that would be delivered as part of the final product.
  Yes, even 1 character changes to logic can be considered non-trivial.

## Code

### Code style

Normal .NET coding guidelines apply. See the
[Framework Design Guidelines](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/)
for more information.

### Unit tests

Make sure to run all unit tests before creating a pull request.
Any new code should also have reasonable unit test coverage.

```bash
dotnet test
```

## Contributing process

### Get buyoff or find open community issues or features

* Through [GitHub Issues](https://github.com/YOUR_USERNAME/markdown-journal-cli/issues)
  or [GitHub Discussions](https://github.com/YOUR_USERNAME/markdown-journal-cli/discussions)
  (preferred), discuss a feature you would like to see, or a bug you have found, and why it
  should be addressed.
  * If approved through Discussions, ensure an accompanying GitHub issue is
    created with information and a link back to the discussion.
* Once you get a nod from a maintainer, you can start on the feature.
* Alternatively, if a feature is on the issues list with the
  [good first issue](https://github.com/YOUR_USERNAME/markdown-journal-cli/labels/good%20first%20issue)
  label, it is open for a community member to patch. Comment that you are
  signing up for it so someone else doesn't also sign up for the work.

### Set up your environment

* Fork `YOUR_USERNAME/markdown-journal-cli` under your GitHub account.
* Create a branch named specifically for the feature or bug.
* In the branch, do work specific to the feature.
* Please observe the following:
  * No reformatting of existing code.
  * No changing files that are not specific to the feature.
* Test your changes and help us out by updating and implementing automated tests.

**Prerequisites:**
* [.NET 8 SDK](https://dotnet.microsoft.com/download) or later
* Any IDE: Visual Studio, VS Code (with C# Dev Kit), or Rider

```bash
# Clone your fork
git clone https://github.com/YOUR_USERNAME/markdown-journal-cli
cd markdown-journal-cli

# Build
dotnet build

# Run tests
dotnet test

# Run the CLI locally
dotnet run --project markdown-journal-cli -- [command]
```

### Prepare commits

A commit should observe the following:

* A commit is a small logical unit that represents a change.
* Should include new or changed tests relevant to the changes you are making.
* No unnecessary whitespace. Check with `git diff --check` and
  `git diff --cached --check` before committing.

### Submit pull request

Prerequisites:

* You are making commits in a feature branch.
* All code should compile without errors or warnings.
* All tests should be passing.

Submitting a PR:

* Once you feel it is ready, submit the pull request to the
  `YOUR_USERNAME/markdown-journal-cli` repository against the `main` branch
  unless specifically requested otherwise.
* In the pull request, outline what you did and point to specific conversations
  (URLs) and issues you are resolving.
* Once the pull request is in, please do not delete the branch or close the
  pull request (unless something is wrong with it).
* A maintainer will evaluate it within a reasonable time period (usually 1–3
  weeks). We do not have a Service Level Agreement (SLA) for pull requests.

### Respond to feedback on pull request

We may have feedback for you to fix or change things. We generally like to see
that pushed against the same topic branch (it will automatically update the
Pull Request). You can also fix/squash/rebase commits and push with `--force`
on topic branches.

If we have comments or questions when we evaluate and receive no response, it
will lessen the chance of getting accepted. Eventually this means it will be
closed if not accepted. Please know this doesn't mean we don't value your
contribution — things go stale. Feel free to address our feedback and reopen
the issue / open a new PR at any time.

## Other general information

If you reformat code or hit core functionality without approval from a
maintainer, it will probably not get accepted — reformatting makes it harder
to evaluate exactly what was changed.

If you stray outside of the guidelines above, it doesn't mean we will ignore
your pull request. It will just make things harder to evaluate and may lengthen
the time to review.
```

**Key sections explained:**

| Section | Purpose | Evidence from research |
|---|---|---|
| Prerequisites / assertions | Sets legal expectations; contributor asserts IP ownership | Used by spectre.console[^6] and cake-build[^8] verbatim |
| Trivial vs. non-trivial | Clarifies CLA-adjacent concerns | Standard in both Chocolatey-lineage projects |
| Code style + unit tests | Sets technical bar | spectre.console, cake-build both reference Framework Design Guidelines[^6][^8] |
| Get buyoff first | Prevents wasted effort | Explicit in nuke-build's "discuss non-trivial changes in an issue" guidance[^9] |
| Set up your environment | Onboarding | Unique to this project; should include real build commands |
| Prepare commits | Commit hygiene | Identical guidance across all reference projects |
| Submit PR | Process clarity | All reference projects specify target branch and response time expectations |
| Respond to feedback | Manage staleness | "Things go stale" language appears in spectre.console[^6] and cake-build[^8] |

---

### `CODE_OF_CONDUCT.md`

This is **Contributor Covenant v2.1**, the current standard.[^10] spectre.console uses this exact version[^7]. Replace `[INSERT CONTACT METHOD]` with your email or GitHub Discussions link.

```markdown
# Contributor Covenant Code of Conduct

## Our Pledge

We as members, contributors, and leaders pledge to make participation in our
community a harassment-free experience for everyone, regardless of age, body
size, visible or invisible disability, ethnicity, sex characteristics, gender
identity and expression, level of experience, education, socio-economic status,
nationality, personal appearance, race, caste, color, religion, or sexual
identity and orientation.

We pledge to act and interact in ways that contribute to an open, welcoming,
diverse, inclusive, and healthy community.

## Our Standards

Examples of behavior that contributes to a positive environment for our
community include:

* Demonstrating empathy and kindness toward other people
* Being respectful of differing opinions, viewpoints, and experiences
* Giving and gracefully accepting constructive feedback
* Accepting responsibility and apologizing to those affected by our mistakes,
  and learning from the experience
* Focusing on what is best not just for us as individuals, but for the overall
  community

Examples of unacceptable behavior include:

* The use of sexualized language or imagery, and sexual attention or advances of
  any kind
* Trolling, insulting or derogatory comments, and personal or political attacks
* Public or private harassment
* Publishing others' private information, such as a physical or email address,
  without their explicit permission
* Other conduct which could reasonably be considered inappropriate in a
  professional setting

## Enforcement Responsibilities

Community leaders are responsible for clarifying and enforcing our standards of
acceptable behavior and will take appropriate and fair corrective action in
response to any behavior that they deem inappropriate, threatening, offensive,
or harmful.

Community leaders have the right and responsibility to remove, edit, or reject
comments, commits, code, wiki edits, issues, and other contributions that are
not aligned to this Code of Conduct, and will communicate reasons for moderation
decisions when appropriate.

## Scope

This Code of Conduct applies within all community spaces, and also applies when
an individual is officially representing the community in public spaces.
Examples of representing our community include using an official e-mail address,
posting via an official social media account, or acting as an appointed
representative at an online or offline event.

## Enforcement

Instances of abusive, harassing, or otherwise unacceptable behavior may be
reported to the community leaders responsible for enforcement at
[INSERT CONTACT METHOD — e.g. your email or GitHub Discussions link].
All complaints will be reviewed and investigated promptly and fairly.

All community leaders are obligated to respect the privacy and security of the
reporter of any incident.

## Enforcement Guidelines

Community leaders will follow these Community Impact Guidelines in determining
the consequences for any action they deem in violation of this Code of Conduct:

### 1. Correction

**Community Impact**: Use of inappropriate language or other behavior deemed
unprofessional or unwelcome in the community.

**Consequence**: A private, written warning from community leaders, providing
clarity around the nature of the violation and an explanation of why the
behavior was inappropriate. A public apology may be requested.

### 2. Warning

**Community Impact**: A violation through a single incident or series of
actions.

**Consequence**: A warning with consequences for continued behavior. No
interaction with the people involved, including unsolicited interaction with
those enforcing the Code of Conduct, for a specified period of time. This
includes avoiding interactions in community spaces as well as external channels
like social media. Violating these terms may lead to a temporary or permanent
ban.

### 3. Temporary Ban

**Community Impact**: A serious violation of community standards, including
sustained inappropriate behavior.

**Consequence**: A temporary ban from any sort of interaction or public
communication with the community for a specified period of time. No public or
private interaction with the people involved, including unsolicited interaction
with those enforcing the Code of Conduct, is allowed during this period.
Violating these terms may lead to a permanent ban.

### 4. Permanent Ban

**Community Impact**: Demonstrating a pattern of violation of community
standards, including sustained inappropriate behavior, harassment of an
individual, or aggression toward or disparagement of classes of individuals.

**Consequence**: A permanent ban from any sort of public interaction within
the community.

## Attribution

This Code of Conduct is adapted from the [Contributor Covenant][homepage],
version 2.1, available at
[https://www.contributor-covenant.org/version/2/1/code_of_conduct.html][v2.1].

Community Impact Guidelines were inspired by
[Mozilla's code of conduct enforcement ladder][Mozilla CoC].

For answers to common questions about this code of conduct, see the FAQ at
[https://www.contributor-covenant.org/faq][FAQ]. Translations are available at
[https://www.contributor-covenant.org/translations][translations].

[homepage]: https://www.contributor-covenant.org
[v2.1]: https://www.contributor-covenant.org/version/2/1/code_of_conduct.html
[Mozilla CoC]: https://github.com/mozilla/diversity
[FAQ]: https://www.contributor-covenant.org/faq
[translations]: https://www.contributor-covenant.org/translations
```

> **Note on versions:** cake-build uses Contributor Covenant v1.3.0[^11] and nuke-build uses v1.4[^12]. spectre.console uses v2.1[^7], which is the current version and recommended for new projects.

> **Enforcement contact:** spectre.console routes to `conduct@dotnetfoundation.org` (they are a .NET Foundation project)[^7]. For an independent project, use your personal email or a GitHub Discussions link like `https://github.com/YOUR_USERNAME/markdown-journal-cli/discussions`.

---

### `SECURITY.md`

Placed at root or in `.github/`. Pattern adapted from dotnet/runtime[^13] and nuke-build[^14]:

```markdown
# Security Policy

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| latest  | ✅ Yes             |
| < 1.0   | ❌ No              |

## Reporting a Vulnerability

**Please do not open public GitHub issues for security vulnerabilities.**

To report a security issue, please email **[your-email@example.com]** with the
subject line "SECURITY: markdown-journal-cli".

You should receive a response within 72 hours. If for some reason you do not,
please follow up via email to ensure we received your original message.

Please include:
- A description of the vulnerability
- Steps to reproduce the issue
- Potential impact
- Any suggested fixes (optional)

We will acknowledge receipt, investigate the report, and work to release a fix
before publicly disclosing the issue.
```

> **Why this matters for the health score:** GitHub surfaces the security policy in the Community Profile and shows a "Report a vulnerability" button in the Security tab only when `SECURITY.md` is present.

---

### `.github/PULL_REQUEST_TEMPLATE.md`

Adapted from spectre.console[^15], which has the most complete template of the reference projects:

```markdown
<!--
Do NOT open a PR without first discussing the changes in an open issue.

Add the issue number here. e.g. Fixes #123
-->
Fixes #

<!-- Formalities. These are not optional. -->

- [ ] I have read the [Contribution Guidelines](./CONTRIBUTING.md)
- [ ] I have checked that there isn't already another pull request that solves this issue
- [ ] All newly added code is adequately covered by tests
- [ ] All existing tests pass (`dotnet test`)

## What changed and why

<!-- Describe the changes you made and the motivation behind them. -->

## Type of change

- [ ] Bug fix (non-breaking change that fixes an issue)
- [ ] New feature (non-breaking change that adds functionality)
- [ ] Breaking change (fix or feature that would cause existing functionality to change)
- [ ] Documentation update

---
Please upvote 👍 this pull request if you are interested in it.
```

---

### `.github/ISSUE_TEMPLATE/bug_report.yml`

Using YAML issue forms (the modern format used by nuke-build[^16] and cake-build[^17]):

```yaml
name: 🐞 Bug Report
description: Report something that doesn't look right.
labels: ["bug", "needs triage"]
body:
  - type: markdown
    attributes:
      value: |
        Thanks for taking the time to report a bug! Please search existing issues
        before submitting to avoid duplicates.

  - type: input
    id: version
    attributes:
      label: Version
      description: What version of markdown-journal-cli are you running? (run `journal --version`)
      placeholder: "e.g. 1.2.0"
    validations:
      required: true

  - type: dropdown
    id: os
    attributes:
      label: Operating System
      options:
        - Windows
        - macOS
        - Linux
    validations:
      required: true

  - type: textarea
    id: description
    attributes:
      label: Describe the Bug
      description: A clear and concise description of what the bug is.
      placeholder: When I run `journal new`, it...
    validations:
      required: true

  - type: textarea
    id: reproduction
    attributes:
      label: Steps to Reproduce
      description: |
        Provide the minimal steps to reproduce the issue. Include the exact
        command(s) you ran.
      placeholder: |
        1. Run `journal new "My Entry"`
        2. See error...
    validations:
      required: true

  - type: textarea
    id: expected
    attributes:
      label: Expected Behavior
      description: What did you expect to happen?
    validations:
      required: true

  - type: textarea
    id: actual
    attributes:
      label: Actual Behavior
      description: |
        What actually happened? Include any error messages or stack traces as
        text (not screenshots).
    validations:
      required: true

  - type: textarea
    id: workaround
    attributes:
      label: Known Workarounds
      description: If you found a workaround, describe it here.
    validations:
      required: false

  - type: dropdown
    id: pr
    attributes:
      label: Would you like to submit a pull request to fix this?
      options:
        - "No"
        - "Yes"
    validations:
      required: true
```

---

### `.github/ISSUE_TEMPLATE/feature_request.yml`

Adapted from nuke-build's feature idea form[^18]:

```yaml
name: 💡 Feature Request
description: Suggest a new feature or improvement.
labels: ["enhancement", "needs triage"]
body:
  - type: markdown
    attributes:
      value: |
        Thanks for suggesting a feature! Please check existing issues and
        discussions before submitting.

  - type: textarea
    attributes:
      label: Problem / Motivation
      description: |
        Is your feature request related to a problem? Describe it. E.g. "I'm
        always frustrated when..."
    validations:
      required: true

  - type: textarea
    attributes:
      label: Proposed Solution
      description: A clear description of what you want to happen.
    validations:
      required: true

  - type: textarea
    attributes:
      label: Usage Example
      description: |
        Show how the feature would be used. E.g. the CLI command and expected
        output.
    validations:
      required: false

  - type: textarea
    attributes:
      label: Alternatives Considered
      description: Any alternative solutions or features you've considered.
    validations:
      required: false

  - type: dropdown
    id: pr
    attributes:
      label: Would you like to submit a pull request for this?
      options:
        - "No"
        - "Yes"
    validations:
      required: true
```

---

### `.github/ISSUE_TEMPLATE/config.yml`

Controls whether blank issues are allowed and adds community links. spectre.console sets `blank_issues_enabled: false`[^19] to force use of templates:

```yaml
blank_issues_enabled: false
contact_links:
  - name: 💬 Ask a Question
    url: https://github.com/YOUR_USERNAME/markdown-journal-cli/discussions
    about: Use GitHub Discussions for questions, ideas, and general conversation.
```

---

### `.github/CODEOWNERS`

CODEOWNERS files define who is automatically requested as a reviewer for PRs touching specific paths.[^20] The file lives at `.github/CODEOWNERS`, root `CODEOWNERS`, or `docs/CODEOWNERS` — GitHub searches in that order.

**Syntax:**

```
# This is a comment.
# Pattern format follows gitignore rules.
# Each line: <pattern> <@owner or @org/team>

# Default owner for everything in the repo
*       @YOUR_GITHUB_USERNAME

# Specific paths can override the default
/docs/  @YOUR_GITHUB_USERNAME
/src/   @YOUR_GITHUB_USERNAME

# Multiple owners
*.cs    @YOUR_GITHUB_USERNAME @COLLABORATOR_USERNAME
```

**Important rules:**[^20]
- Owners must have **write access** to the repository.
- The **last matching rule** in the file wins for a given file path.
- CODEOWNERS is processed per-branch; put it on `main` for it to apply to PRs targeting `main`.
- Draft PRs do **not** automatically request code owners — only when marked ready for review.
- To **require** code owner approval before merging, enable it in branch protection settings.

**For a solo maintainer with one collaborator:**

```
# All files — require maintainer review on all PRs
*       @YOUR_GITHUB_USERNAME

# If you add collaborators with focused areas:
# /docs/ @DOCS_COLLABORATOR
```

---

## Branch Protection Configuration

Branch protection rules make the repo "collaborator-ready" by ensuring code quality gates are enforced even for collaborators with write access.

### Step-by-Step: Protecting `main`

Navigate to: **Settings → Branches → Add branch ruleset** (or the legacy "Add rule" under Branch protection rules)

#### Recommended Settings for a Solo Maintainer Transitioning to Open Source

| Setting | Value | Reason |
|---|---|---|
| **Require a pull request before merging** | ✅ Enabled | All changes go through PR review |
| — Required approvals | `1` | At minimum, someone reviews every PR |
| — Dismiss stale reviews on new commits | ✅ Enabled | Re-review required after updates |
| — Require review from Code Owners | ✅ Enabled | Triggers CODEOWNERS enforcement |
| **Require status checks to pass** | ✅ Enabled | CI must pass before merge |
| — Require branches to be up-to-date | ✅ Enabled | Prevents integration bugs |
| — Add required status checks | Add your CI workflow job names (e.g., `build`, `test`) | Specific CI jobs |
| **Require conversation resolution before merging** | ✅ Enabled | All review comments must be addressed |
| **Do not allow bypassing the above settings** | ✅ Enabled | Enforces rules even for admins |
| **Restrict who can push to matching branches** | Optional | Only needed if you want to lock down force pushes |
| **Allow force pushes** | ❌ Disabled | Protect history |
| **Allow deletions** | ❌ Disabled | Prevent accidental branch deletion |

### GitHub Ruleset API (Modern Approach)

GitHub now recommends **Rulesets** over the legacy Branch Protection Rules UI. You can configure these via:
- **Settings → Rules → Rulesets → New branch ruleset**
- Or via the GitHub REST API / GitHub CLI: `gh api /repos/OWNER/REPO/rulesets`

Rulesets allow you to target branches by pattern (e.g., `main`, `release/*`) and enforce them across multiple branches.

---

## Recommended GitHub Labels

Based on patterns observed in spectre.console[^6], nuke-build[^9], and cake-build[^8]:

### Labels to Create

| Label Name | Color | Description |
|---|---|---|
| `bug` | `#d73a4a` (red) | Something isn't working |
| `enhancement` | `#a2eeef` (teal) | New feature or request |
| `documentation` | `#0075ca` (blue) | Improvements or additions to documentation |
| `good first issue` | `#7057ff` (purple) | Good for newcomers |
| `help wanted` | `#008672` (green) | Extra attention is needed |
| `needs triage` | `#e4e669` (yellow) | Needs maintainer review and categorization |
| `question` | `#d876e3` (pink) | Further information is requested |
| `wontfix` | `#ffffff` (white) | This will not be worked on |
| `duplicate` | `#cfd3d7` (gray) | This issue or PR already exists |
| `invalid` | `#e4e669` (yellow) | This doesn't seem right |
| `breaking change` | `#b60205` (dark red) | Introduces breaking API/behavior changes |
| `dependencies` | `#0366d6` (blue) | Pull requests that update a dependency file |

> **Tip:** You can create labels via the GitHub CLI: `gh label create "good first issue" --color 7057ff --description "Good for newcomers"`
>
> Or bulk-create by using a labels YAML file + the `gh label import` command.

---

## Key Repositories Summary

| Repository | License | CoC Version | Issue Form Format | PR Template | SECURITY.md |
|---|---|---|---|---|---|
| [spectreconsole/spectre.console](https://github.com/spectreconsole/spectre.console) | MIT | Contributor Covenant v2.1 | `.md` templates | ✅ `.github/pull_request_template.md` | ❌ Not present |
| [cake-build/cake](https://github.com/cake-build/cake) | MIT | Contributor Covenant v1.3.0 | `.yml` forms | ✅ `.github/PULL_REQUEST_TEMPLATE.md` | ❌ Not present |
| [nuke-build/nuke](https://github.com/nuke-build/nuke) | MIT | Contributor Covenant v1.4 | `.yml` forms | ✅ `.github/PULL_REQUEST_TEMPLATE.md` | ✅ `.github/SECURITY.md` |
| [dotnet/sdk](https://github.com/dotnet/sdk) | MIT | (via dotnet org policy) | `.md` templates | ❌ Not found | ✅ `SECURITY.md` (root) |
| [dotnet/runtime](https://github.com/dotnet/runtime) | MIT | (via dotnet org policy) | — | — | ✅ `SECURITY.md` (root) |

---

## Complete File Checklist for markdown-journal-cli

```
markdown-journal-cli/
├── LICENSE                                     # MIT License
├── CONTRIBUTING.md                             # Contribution guidelines
├── CODE_OF_CONDUCT.md                          # Contributor Covenant v2.1
├── SECURITY.md                                 # Security vulnerability reporting
├── README.md                                   # Already present — expand with badges
└── .github/
    ├── CODEOWNERS                              # Code ownership assignments
    ├── PULL_REQUEST_TEMPLATE.md               # PR checklist
    └── ISSUE_TEMPLATE/
        ├── config.yml                          # blank_issues_enabled: false
        ├── bug_report.yml                      # Bug report form
        └── feature_request.yml                 # Feature request form
```

### README Enhancements

Add the following badges to the top of `README.md` once the files are in place:

```markdown
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Contributor Covenant](https://img.shields.io/badge/Contributor%20Covenant-2.1-4baaaa.svg)](CODE_OF_CONDUCT.md)
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg)](CONTRIBUTING.md)
```

---

## Confidence Assessment

| Finding | Confidence | Basis |
|---|---|---|
| MIT is dominant for .NET CLI tools | ✅ High | Verified in 5 repositories: spectre.console[^1], cake[^2], nuke[^3], dotnet/sdk[^4], dotnet/runtime |
| Contributor Covenant v2.1 is current standard | ✅ High | Fetched directly from contributor-covenant.org[^10]; spectre.console uses it[^7] |
| Cake/spectre.console CONTRIBUTING.md descended from Chocolatey template | ✅ High | Both files contain explicit attribution to Chocolatey[^6][^8] |
| GitHub Community Health checks these 6+ files | ✅ High | Fetched directly from GitHub Docs[^5] |
| YAML issue forms are preferred modern format | ✅ High | Nuke[^16] and cake[^17] use `.yml`; dotnet/sdk still uses older `.md` format |
| Branch protection "require code owner review" needs CODEOWNERS | ✅ High | GitHub Docs CODEOWNERS article[^20] |
| Apache 2.0 would be acceptable but unusual for this type of project | ✅ Medium | Pattern observation; no explicit community statement found |
| GPL would harm adoption | ✅ High | Standard open source community consensus; all reference projects chose permissive licenses |

---

## Footnotes

[^1]: `LICENSE` — [spectreconsole/spectre.console](https://github.com/spectreconsole/spectre.console/blob/main/LICENSE) — MIT License, copyright `.NET Foundation and Contributors` (SHA: 403462acc328d811d4854dc97d17baf119ca655b)

[^2]: `LICENSE` — [cake-build/cake](https://github.com/cake-build/cake/blob/develop/LICENSE) — MIT License, copyright `.NET Foundation and Contributors` (SHA: 403462acc328d811d4854dc97d17baf119ca655b)

[^3]: `LICENSE` — [nuke-build/nuke](https://github.com/nuke-build/nuke/blob/develop/LICENSE) — MIT License, copyright `Maintainers of NUKE` (SHA: ff0663911c23ce41c5d67207c06909b326f7b107)

[^4]: `LICENSE.TXT` — [dotnet/sdk](https://github.com/dotnet/sdk/blob/main/LICENSE.TXT) — MIT License, copyright `.NET Foundation and Contributors` (SHA: 984713a49622a96da110443c15477613bc12656b)

[^5]: GitHub Docs — [About community profiles for public repositories](https://docs.github.com/en/communities/setting-up-your-project-for-healthy-contributions/about-community-profiles-for-public-repositories) — Describes all files checked by the Community Health checklist

[^6]: `CONTRIBUTING.md` — [spectreconsole/spectre.console](https://github.com/spectreconsole/spectre.console/blob/main/CONTRIBUTING.md) — (SHA: 4c95b29743ff017b51bba9a7c62e468597afb7b2) — Adapted from Chocolatey project with permission

[^7]: `CODE_OF_CONDUCT.md` — [spectreconsole/spectre.console](https://github.com/spectreconsole/spectre.console/blob/main/CODE_OF_CONDUCT.md) — Contributor Covenant v2.1, enforcement contact: `conduct@dotnetfoundation.org` (SHA: 7b01ef21ba36332a8396df147acfa36812f67da2)

[^8]: `CONTRIBUTING.md` — [cake-build/cake](https://github.com/cake-build/cake/blob/develop/CONTRIBUTING.md) — (SHA: fd179be4326febdebabafbb4bb6b82a11a78cc98) — Adapted from Chocolatey project with permission

[^9]: `CONTRIBUTING.md` — [nuke-build/nuke](https://github.com/nuke-build/nuke/blob/develop/CONTRIBUTING.md) — (SHA: dfa69c59d12d295a5074282c1a212c74fc5f2518) — Unique sustainability-focused style, single-maintainer context

[^10]: Contributor Covenant v2.1 — [https://www.contributor-covenant.org/version/2/1/code_of_conduct/](https://www.contributor-covenant.org/version/2/1/code_of_conduct/) — Official canonical text

[^11]: `CODE_OF_CONDUCT.md` — [cake-build/cake](https://github.com/cake-build/cake/blob/develop/CODE_OF_CONDUCT.md) — Contributor Covenant v1.3.0, enforcement contact: `caketeam@cakebuild.net` (SHA: 021c03b84d43cd232e290673bc64e3b4f4bd2469)

[^12]: `CODE_OF_CONDUCT.md` — [nuke-build/nuke](https://github.com/nuke-build/nuke/blob/develop/CODE_OF_CONDUCT.md) — Contributor Covenant v1.4, enforcement contact: `ithrowexceptions@gmail.com` (SHA: 68b02de9afa14a95a5ea8dd1adccf7b554745263)

[^13]: `SECURITY.md` — [dotnet/runtime](https://github.com/dotnet/runtime/blob/main/SECURITY.md) — Routes to MSRC (Microsoft Security Response Center) (SHA: 125b77a85822ce339556eb1698685c80656577d1)

[^14]: `.github/SECURITY.md` — [nuke-build/nuke](https://github.com/nuke-build/nuke/blob/develop/.github/SECURITY.md) — Simple email-based reporting to `ithrowexceptions@gmail.com` (SHA: ff98d86fd8e2853f006730be9a94d1fa637b5ee6)

[^15]: `.github/pull_request_template.md` — [spectreconsole/spectre.console](https://github.com/spectreconsole/spectre.console/blob/main/.github/pull_request_template.md) — Includes AI disclosure requirement (SHA: c5c44bc169834452f16370c692400463d9ab8ac6)

[^16]: `.github/ISSUE_TEMPLATE/bug_report.yml` — [nuke-build/nuke](https://github.com/nuke-build/nuke/blob/develop/.github/ISSUE_TEMPLATE/bug_report.yml) — YAML issue form with required fields (SHA: cf0fb0e1491720370a131bd277c1f6567929fee1)

[^17]: `.github/ISSUE_TEMPLATE/bug-report.yml` — [cake-build/cake](https://github.com/cake-build/cake/blob/develop/.github/ISSUE_TEMPLATE/bug-report.yml) — YAML issue form with prerequisite checkboxes (SHA: f6dfd77147408c0e3fb8a86abc62110745a4cb50)

[^18]: `.github/ISSUE_TEMPLATE/feature_idea.yml` — [nuke-build/nuke](https://github.com/nuke-build/nuke/blob/develop/.github/ISSUE_TEMPLATE/feature_idea.yml) — YAML feature request form (SHA: 8526933a9f20a3358011a2f2cedd0e0b4e59faa6)

[^19]: `.github/ISSUE_TEMPLATE/config.yml` — [spectreconsole/spectre.console](https://github.com/spectreconsole/spectre.console/blob/main/.github/ISSUE_TEMPLATE/config.yml) — Sets `blank_issues_enabled: false` (SHA: 3ba13e0cec6cbbfd462e9ebf529dd2093148cd69)

[^20]: GitHub Docs — [About code owners](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/about-code-owners) — Describes CODEOWNERS file location, syntax, and branch protection integration
