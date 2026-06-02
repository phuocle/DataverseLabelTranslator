---
name: pl-test-package-deployer
description: Prepare the local Package Deployer cache with the Dataverse Label Translator package for manual pac tool pd testing.
argument-hint: '[solution-version]'
agent: agent
---

<!-- Generated from ../../.agents/skills/pl-test-package-deployer/SKILL.md. Do not edit manually; run DataverseLabelTranslator.Scripts/sync-ai-config.ps1. -->

Run the Dataverse Label Translator `/pl-test-package-deployer` workflow.

Before acting, read and follow [AGENTS.md](../../AGENTS.md) and the canonical workflow in [pl-test-package-deployer skill](../../.agents/skills/pl-test-package-deployer/SKILL.md).

Use any text supplied after `/pl-test-package-deployer` as the command arguments. Do not proceed from memory if the canonical skill file cannot be read; stop and report that the skill file is unavailable.

Hard rules:

- Follow the canonical skill file exactly.
- Do not push.
- Do not create pull requests.
- Do not deploy, export, upload, stage, or commit unless that specific workflow explicitly requires it.
