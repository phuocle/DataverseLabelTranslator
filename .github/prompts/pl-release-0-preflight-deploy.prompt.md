---
name: pl-release-0-preflight-deploy
description: Run 100% coverage release preflight checks, then deploy server and web resources to the dev Dataverse environment.
agent: agent
---

<!-- Generated from ../../.agents/skills/pl-release-0-preflight-deploy/SKILL.md. Do not edit manually; run DataverseLabelTranslator.Scripts/sync-ai-config.ps1. -->

Run the Dataverse Label Translator `/pl-release-0-preflight-deploy` workflow.

Before acting, read and follow [AGENTS.md](../../AGENTS.md) and the canonical workflow in [pl-release-0-preflight-deploy skill](../../.agents/skills/pl-release-0-preflight-deploy/SKILL.md).

Use any text supplied after `/pl-release-0-preflight-deploy` as the command arguments. Do not proceed from memory if the canonical skill file cannot be read; stop and report that the skill file is unavailable.

Hard rules:

- Follow the canonical skill file exactly.
- Do not push.
- Do not create pull requests.
- Do not deploy, export, upload, stage, or commit unless that specific workflow explicitly requires it.
