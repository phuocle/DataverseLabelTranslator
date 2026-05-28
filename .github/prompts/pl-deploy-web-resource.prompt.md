---
name: pl-deploy-web-resource
description: Deploy a local Dataverse Label Translator file as a Dataverse web resource.
argument-hint: "<local-path>"
agent: agent
---

Run the Dataverse Label Translator deploy web resource workflow.

Before acting, read and follow [AGENTS.md](../../AGENTS.md) and the canonical workflow in [pl-deploy-web-resource skill](../../.agents/skills/pl-deploy-web-resource/SKILL.md).

Use the path supplied after `/pl-deploy-web-resource` as the local file path. Validate that it exists, resolve the Dataverse unique name from `.codex/mapping.xml` or the documented naming convention, then deploy with MCP `manage_webresource`.

Hard rules:

- Do not deploy PropertyEditor resources into this solution.
- Do not use the `devkit` CLI.
- Use MCP `manage_webresource` directly. If that MCP tool is unavailable in VS Code, stop and report that the Dataverse MCP connection is required.
- Create only under solution `DataverseLabelTranslator`.
