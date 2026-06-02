---
name: "pl-ai-sync"
display-name: "PL AI Sync"
description: "Synchronize and validate AI tool configuration generated from canonical Dataverse Label Translator skills."
---

# PL AI Sync

Use this skill when the user asks to sync AI config, validate AI tooling, regenerate Claude/Copilot adapters, or check that Codex, Claude, Copilot, and Antigravity command definitions are in sync.

## What This Skill Does

1. Regenerates downstream AI adapters from canonical `.agents/skills/pl-*/SKILL.md` files.
2. Checks that generated files are in sync.
3. Checks that deprecated or duplicate AI config files do not reappear.
4. Checks that local secret-bearing files are ignored by git.
5. Checks tracked text files for DevKit/SAS secret-like values.

## Workflow

Run from the repository root:

```powershell
D:\github\DataverseLabelTranslator
```

First regenerate adapters:

```powershell
powershell -ExecutionPolicy Bypass -File DataverseLabelTranslator.Scripts\sync-ai-config.ps1
```

Then validate the full AI config:

```powershell
powershell -ExecutionPolicy Bypass -File DataverseLabelTranslator.Scripts\check-ai-config.ps1
```

## Canonical Sources

Only edit these by hand:

```text
AGENTS.md
.agents/skills/pl-*/SKILL.md
.agents/rules/
.vscode/*.example
.agents/*.example
DataverseLabelTranslator.Scripts/sync-ai-config.ps1
DataverseLabelTranslator.Scripts/check-ai-config.ps1
```

Generated adapters must not be edited directly:

```text
.claude/skills/pl-*/SKILL.md
.github/prompts/pl-*.prompt.md
```

If a generated adapter needs to change, update `.agents/skills/pl-*/SKILL.md` or `DataverseLabelTranslator.Scripts/sync-ai-config.ps1`, then rerun this skill.

## Hard Rules

- Do not commit automatically.
- Do not push.
- Do not deploy, export, or upload anything.
- Do not write credentials into tracked files.
- Do not remove local ignored MCP config files.
- If `DataverseLabelTranslator.Scripts\check-ai-config.ps1` fails, report the exact failure and stop.
