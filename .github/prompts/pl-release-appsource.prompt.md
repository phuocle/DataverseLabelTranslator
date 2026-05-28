---
name: pl-release-appsource
description: Build the final AppSource all-in-one Marketplace ZIP.
argument-hint: "[solution-version]"
agent: agent
---

Run the Dataverse Label Translator Release AppSource workflow.

Before acting, read and follow [AGENTS.md](../../AGENTS.md) and the canonical workflow in [pl-release-appsource skill](../../.agents/skills/pl-release-appsource/SKILL.md).

Use a solution version supplied after `/pl-release-appsource` when present. If no version is supplied, run the script without `-SolutionVersion` and let it infer the latest managed release ZIP.

Hard rules:

- Trust the selected existing managed solution ZIP under `release/<version>/dataverse/solutions/DataverseLabelTranslator_managed.zip` as the source of truth.
- Do not export a Dataverse solution.
- Do not upload to Azure.
- Do not deploy to Dataverse.
- Do not stage, commit, or push.
- Do not modify older version folders unless the user explicitly selected that version.
