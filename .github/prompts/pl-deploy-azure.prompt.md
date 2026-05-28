---
name: pl-deploy-azure
description: Upload the final Dataverse Label Translator AppSource ZIP to Azure Blob Storage and write the Partner Center SAS details to release.md.
argument-hint: '[solution-version]'
agent: agent
---

<!-- Generated from ../../.agents/skills/pl-deploy-azure/SKILL.md. Do not edit manually; run scripts/sync-ai-config.ps1. -->

Run the Dataverse Label Translator `/pl-deploy-azure` workflow.

Before acting, read and follow [AGENTS.md](../../AGENTS.md) and the canonical workflow in [pl-deploy-azure skill](../../.agents/skills/pl-deploy-azure/SKILL.md).

Use any text supplied after `/pl-deploy-azure` as the command arguments. Do not proceed from memory if the canonical skill file cannot be read; stop and report that the skill file is unavailable.

Hard rules:

- Follow the canonical skill file exactly.
- Do not push.
- Do not create pull requests.
- Do not deploy, export, upload, stage, or commit unless that specific workflow explicitly requires it.
