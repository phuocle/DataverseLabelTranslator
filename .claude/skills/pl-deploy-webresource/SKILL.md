---
name: "PL Deploy WebResource"
description: "Deploy Dataverse Label Translator web resources through the DevKit WebResource project batch file."
disable-model-invocation: true
---

<!-- Generated from ../../.agents/skills/pl-deploy-webresource/SKILL.md. Do not edit manually; run DataverseLabelTranslator.Scripts/sync-ai-config.ps1. -->

# pl-deploy-webresource

Use this skill when the user asks to run the Dataverse Label Translator command `pl-deploy-webresource`.

## Command Template

# Deploy Web Resource

When files are edited or created under `DataverseLabelTranslator.WebResource/html`, `css`, `js`, or `img`, run `DataverseLabelTranslator.WebResource\deploy.debug.bat` from the repo root and report only the DevKit summary or error.
