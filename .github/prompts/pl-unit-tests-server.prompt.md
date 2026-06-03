---
name: pl-unit-tests-server
description: Regenerate Dataverse proxy types and run Dataverse Label Translator server-side MSTest/FakeXrmEasy unit tests.
argument-hint: '[generate-proxy|no-proxy|<test-filter>]'
agent: agent
---

<!-- Generated from ../../.agents/skills/pl-unit-tests-server/SKILL.md. Do not edit manually; run DataverseLabelTranslator.Scripts/sync-ai-config.ps1. -->

Run the Dataverse Label Translator `/pl-unit-tests-server` workflow.

Before acting, read and follow [AGENTS.md](../../AGENTS.md) and the canonical workflow in [pl-unit-tests-server skill](../../.agents/skills/pl-unit-tests-server/SKILL.md).

Use any text supplied after `/pl-unit-tests-server` as the command arguments. Do not proceed from memory if the canonical skill file cannot be read; stop and report that the skill file is unavailable.

Hard rules:

- Follow the canonical skill file exactly.
- Do not push.
- Do not create pull requests.
- Do not deploy, export, upload, stage, or commit unless that specific workflow explicitly requires it.
