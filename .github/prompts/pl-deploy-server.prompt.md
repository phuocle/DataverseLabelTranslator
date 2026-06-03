---
name: pl-deploy-server
description: Deploy Dataverse Label Translator server components through the DevKit Server project batch file.
agent: agent
---

<!-- Generated from ../../.agents/skills/pl-deploy-server/SKILL.md. Do not edit manually; run DataverseLabelTranslator.Scripts/sync-ai-config.ps1. -->

Run the Dataverse Label Translator `/pl-deploy-server` workflow.

Before acting, read and follow [AGENTS.md](../../AGENTS.md) and the canonical workflow in [pl-deploy-server skill](../../.agents/skills/pl-deploy-server/SKILL.md).

Use any text supplied after `/pl-deploy-server` as the command arguments. Do not proceed from memory if the canonical skill file cannot be read; stop and report that the skill file is unavailable.

Hard rules:

- Follow the canonical skill file exactly.
- Do not push.
- Do not create pull requests.
- Do not deploy, export, upload, stage, or commit unless that specific workflow explicitly requires it.
