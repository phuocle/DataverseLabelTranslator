---
name: pl-commit
description: Stage local changes and create one high-quality local git commit. Never push.
argument-hint: '[-m "commit message"]'
agent: agent
---

<!-- Generated from ../../.agents/skills/pl-commit/SKILL.md. Do not edit manually; run scripts/sync-ai-config.ps1. -->

Run the Dataverse Label Translator `/pl-commit` workflow.

Before acting, read and follow [AGENTS.md](../../AGENTS.md) and the canonical workflow in [pl-commit skill](../../.agents/skills/pl-commit/SKILL.md).

Use any text supplied after `/pl-commit` as the command arguments. Do not proceed from memory if the canonical skill file cannot be read; stop and report that the skill file is unavailable.

Hard rules:

- Follow the canonical skill file exactly.
- Do not push.
- Do not create pull requests.
- Do not deploy, export, upload, stage, or commit unless that specific workflow explicitly requires it.
