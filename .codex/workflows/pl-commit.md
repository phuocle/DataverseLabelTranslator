---
name: "pl-commit"
display-name: "PL Commit"
description: "Stage local changes and create one high-quality local git commit. Never push."
argument-hint: '[-m "commit message"]'
---

# pl-commit

Use this workflow only when the user explicitly asks to run `pl-commit` or create a commit.

## Command Template

# Commit Workflow

Create a local git commit for the current repository. This command is intentionally local-only: commit the work and stop. Do not push, create PRs, deploy, or trigger remote workflows.

## What This Workflow Does

1. Inspect repository status and changed files.
2. Stage all intended local changes.
3. Generate a clear commit message from the staged diff.
4. Create exactly one local commit.
5. Verify the local repository state after the commit.

## Usage

```bash
/pl-commit
/pl-commit -m "your commit message"
```

## Workflow Steps

### Step 1: Verify Git Repository

Check that the current directory is inside a git repository:

```bash
git rev-parse --is-inside-work-tree
git rev-parse --show-toplevel
```

If this fails, stop and report that the current directory is not a git repository. Do not initialize a new repository unless the user explicitly asked for that.

### Step 2: Inspect Changes

Review the worktree before staging:

```bash
git status --short
git diff --stat
git diff --name-status
```

If there are no changes, report `Nothing to commit - working tree clean` and stop.

If the diff includes unrelated or risky changes, do not guess silently. Report the suspicious paths and ask the user before committing.

### Step 3: Stage All Changes

Stage tracked changes, untracked files, and deletions that belong to the requested work:

```bash
git add -A
```

Then inspect what is staged:

```bash
git diff --cached --stat
git diff --cached --name-status
```

If nothing is staged, stop and report `Nothing staged to commit`.

### Step 4: Generate A Better Commit Message

If a message was provided with `-m`, use it as the commit subject unless it is empty or misleading.

If no message was provided, generate the message from the staged diff. Inspect both file names and the actual staged content when needed:

```bash
git diff --cached --stat
git diff --cached --name-status
git diff --cached
```

Commit message rules:

- Use an imperative subject line: `Rename main web resource`, `Document dictionary workflow`, `Deploy App web resource`.
- Keep the subject under 72 characters when practical.
- Describe the user-facing or repository-level change, not implementation trivia.
- Avoid vague subjects such as `update`, `fix`, `changes`, `misc`, or `wip`.
- Do not mention tools, agents, prompts, or internal assistant workflow.
- Do not mention pushing, PRs, deployments, or remote actions.
- Use a body only when it adds useful context.

Message format:

```text
<imperative subject>

- <important feature or setup change>
- <documentation or workflow change>
- <release artifact or packaging note, if relevant>
```

Use a single-line commit when the change is small:

```text
Document Dataverse Label Translator dictionary workflow
```

Use a body when the staged diff spans multiple areas, renames files, changes deployment metadata, or includes generated release artifacts.

### Step 5: Commit

Create one normal verified local commit:

```bash
git commit -m "Your commit message here"
```

For a multi-line message, use multiple `-m` arguments:

```bash
git commit -m "Your commit subject" -m "- Detail one" -m "- Detail two"
```

Never use `--no-verify`. Let hooks run.

### Step 6: Verify Local State

After committing, inspect the repository again and capture the new commit:

```bash
git log -1 --oneline
git status --short
```

If files remain, report the remaining paths. Do not create additional commits unless the user explicitly asks.

Stop after the local commit. Do not continue into push, PR creation, deployment, or release export.

## Hard Rules

- Do not run `git push`.
- Do not run `git push -u`.
- Do not run any command whose purpose is pushing to a remote.
- Do not create or update pull requests.
- Do not deploy.
- Do not run `/improve`.
- Do not run `/compact`.
- Do not initialize a repository unless explicitly requested.
- Do not use `--no-verify`.
- Do not amend a previous commit unless explicitly requested.
- Do not create more than one commit unless explicitly requested.

## Output Format

```markdown
## Commit Summary

### Repository
[Repo path]

### Changes Committed
[Brief summary of what was committed]

### Commit
[Commit hash and message]

### Status
[Clean or remaining local changes]

### Remote
Not pushed.
```
