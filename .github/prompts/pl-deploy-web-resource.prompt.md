---
name: pl-deploy-web-resource
description: Deploy a local Dataverse Label Translator file as a Dataverse web resource through MCP manage_webresource.
argument-hint: '<local-path>'
agent: agent
---

<!-- Generated from ../../.agents/skills/pl-deploy-web-resource/SKILL.md. Do not edit manually; run scripts/sync-ai-config.ps1. -->

Run the Dataverse Label Translator `/pl-deploy-web-resource` workflow.

Before acting, read and follow [AGENTS.md](../../AGENTS.md) and the canonical workflow in [pl-deploy-web-resource skill](../../.agents/skills/pl-deploy-web-resource/SKILL.md).

Use any text supplied after `/pl-deploy-web-resource` as the command arguments. Do not proceed from memory if the canonical skill file cannot be read; stop and report that the skill file is unavailable.

Hard rules:

- Follow the canonical skill file exactly.
- Do not push.
- Do not create pull requests.
- Do not deploy, export, upload, stage, or commit unless that specific workflow explicitly requires it.
