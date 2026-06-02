---
name: pl-unit-tests
description: Run Dataverse Label Translator Vitest unit tests and coverage checks with mocked Dataverse APIs.
argument-hint: '[coverage|<test-filter>]'
agent: agent
---

<!-- Generated from ../../.agents/skills/pl-unit-tests/SKILL.md. Do not edit manually; run DataverseLabelTranslator.Scripts/sync-ai-config.ps1. -->

Run the Dataverse Label Translator `/pl-unit-tests` workflow.

Before acting, read and follow [AGENTS.md](../../AGENTS.md) and the canonical workflow in [pl-unit-tests skill](../../.agents/skills/pl-unit-tests/SKILL.md).

Use any text supplied after `/pl-unit-tests` as the command arguments. Do not proceed from memory if the canonical skill file cannot be read; stop and report that the skill file is unavailable.

Hard rules:

- Follow the canonical skill file exactly.
- Do not push.
- Do not create pull requests.
- Do not deploy, export, upload, stage, or commit unless that specific workflow explicitly requires it.
