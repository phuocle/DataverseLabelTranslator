---
name: pl-deploy-azure
description: Upload the final AppSource ZIP to Azure Blob Storage and write Partner Center SAS details locally.
argument-hint: "[solution-version]"
agent: agent
---

Run the Dataverse Label Translator Deploy Azure workflow.

Before acting, read and follow [AGENTS.md](../../AGENTS.md) and the canonical workflow in [pl-deploy-azure skill](../../.agents/skills/pl-deploy-azure/SKILL.md).

Use a solution version supplied after `/pl-deploy-azure` when present. If no version is supplied, run the script without `-SolutionVersion` and let it infer the latest final AppSource ZIP.

Hard rules:

- Do not export the Dataverse solution.
- Do not run Release AppSource unless the user separately asked for that workflow.
- Upload only the final all-in-one ZIP from `release/<version>/appsource/zip/`.
- Verify Azure CLI is logged in as `sales@d365iconsandtooltips.com`.
- Use resource group `SHARED`, storage account `ple`, container `dataverselabeltranslator`.
- Do not paste the SAS URL into chat.
- Do not stage, commit, or push.
