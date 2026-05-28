---
name: pl-test-package-deployer
description: Prepare the local Package Deployer cache for manual pac tool pd testing.
argument-hint: "[solution-version]"
agent: agent
---

Run the Dataverse Label Translator Test Package Deployer workflow.

Before acting, read and follow [AGENTS.md](../../AGENTS.md) and the canonical workflow in [pl-test-package-deployer skill](../../.agents/skills/pl-test-package-deployer/SKILL.md).

Use a solution version supplied after `/pl-test-package-deployer` when present. If no version is supplied, run the script without `-SolutionVersion` and let it infer the latest package source.

Hard rules:

- Do not export the Dataverse solution.
- Do not upload to Azure.
- Do not deploy to Dataverse.
- Do not launch Package Deployer.
- Do not stage, commit, or push.
- Only copy into a verified `%LOCALAPPDATA%\Microsoft\PowerPlatform\PD\<version>\tools` folder that contains `PackageDeployer.exe`.
- After success, tell the user to run `pac tool pd`.
