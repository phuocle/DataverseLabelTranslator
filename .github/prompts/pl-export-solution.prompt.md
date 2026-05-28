---
name: pl-export-solution
description: Export the DataverseLabelTranslator solution release ZIPs.
agent: agent
---

Run the Dataverse Label Translator export solution workflow.

Before acting, read and follow [AGENTS.md](../../AGENTS.md) and the canonical workflow in [pl-export-solution skill](../../.agents/skills/pl-export-solution/SKILL.md).

This workflow exports `DataverseLabelTranslator`, removes non-base-language labels, stamps the hard-coded release version from the skill, and packs final managed and unmanaged ZIPs.

Hard rules:

- Use the PAC CLI profile named `DataverseLabelTranslator`.
- Do not read or infer a version from Dataverse.
- Do not delete or rewrite `release/<version>/appsource`.
- Do not stage files.
- Do not commit.
- Do not push.
