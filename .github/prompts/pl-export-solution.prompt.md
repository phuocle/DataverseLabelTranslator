---
name: pl-export-solution
description: Export the DataverseLabelTranslator solution with cleaned labels and final managed/unmanaged release ZIPs.
agent: agent
---

<!-- Generated from ../../.agents/skills/pl-export-solution/SKILL.md. Do not edit manually; run DataverseLabelTranslator.Scripts/sync-ai-config.ps1. -->

Run the Dataverse Label Translator `/pl-export-solution` workflow.

Before acting, read and follow [AGENTS.md](../../AGENTS.md) and the canonical workflow in [pl-export-solution skill](../../.agents/skills/pl-export-solution/SKILL.md).

Use any text supplied after `/pl-export-solution` as the command arguments. Do not proceed from memory if the canonical skill file cannot be read; stop and report that the skill file is unavailable.

Hard rules:

- Follow the canonical skill file exactly.
- Do not push.
- Do not create pull requests.
- Do not deploy, export, upload, stage, or commit unless that specific workflow explicitly requires it.
