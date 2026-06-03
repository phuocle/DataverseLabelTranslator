---
name: pl-release-2-prepare-appsource
description: Build the final AppSource all-in-one Marketplace ZIP for Dataverse Label Translator. Does not export Dataverse solution or upload to Azure.
argument-hint: '[solution-version]'
agent: agent
---

<!-- Generated from ../../.agents/skills/pl-release-2-prepare-appsource/SKILL.md. Do not edit manually; run DataverseLabelTranslator.Scripts/sync-ai-config.ps1. -->

Run the Dataverse Label Translator `/pl-release-2-prepare-appsource` workflow.

Before acting, read and follow [AGENTS.md](../../AGENTS.md) and the canonical workflow in [pl-release-2-prepare-appsource skill](../../.agents/skills/pl-release-2-prepare-appsource/SKILL.md).

Use any text supplied after `/pl-release-2-prepare-appsource` as the command arguments. Do not proceed from memory if the canonical skill file cannot be read; stop and report that the skill file is unavailable.

Hard rules:

- Follow the canonical skill file exactly.
- Do not push.
- Do not create pull requests.
- Do not deploy, export, upload, stage, or commit unless that specific workflow explicitly requires it.
