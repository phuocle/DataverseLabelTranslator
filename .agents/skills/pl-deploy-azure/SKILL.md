---
name: "pl-deploy-azure"
display-name: "PL Deploy Azure"
description: "Upload the final Dataverse Label Translator AppSource ZIP to Azure Blob Storage and write the Partner Center SAS details to release.md."
argument-hint: "[solution-version]"
---

# Deploy Azure

Use this skill when the user asks to run `Deploy Azure`, `/pl-deploy-azure`, upload the final AppSource ZIP to Azure Blob, or generate the Partner Center SAS URL.

This workflow uploads only the final all-in-one AppSource ZIP. It does not export a Dataverse solution and does not run Release AppSource unless the user separately asks for that first.

## Fixed Azure Target

Expected Azure login:

```text
sales@d365iconsandtooltips.com
```

Storage account:

```text
Resource group: SHARED
Storage account: ple
Container: dataverselabeltranslator
```

The script creates the container if it does not exist. The container stays private.

## Version Resolution

If the user mentions a version such as `1.1.0.0`, pass it explicitly:

```powershell
powershell -ExecutionPolicy Bypass -File D:\github\DataverseLabelTranslator\scripts\deploy-azure.ps1 -SolutionVersion 1.1.0.0
```

If the user does not mention a version, run without `-SolutionVersion`. The script scans the latest final ZIP under:

```text
D:\github\DataverseLabelTranslator\release\<version>\appsource\zip\DataverseLabelTranslator.v.<major.minor.patch>.zip
```

If the final ZIP is missing, stop and tell the user to run Release AppSource first. Do not export a solution.

## Workflow

1. Confirm repository root:

```powershell
git rev-parse --show-toplevel
```

Expected:

```text
D:\github\DataverseLabelTranslator
```

2. Optional dry run when validating setup:

```powershell
powershell -ExecutionPolicy Bypass -File D:\github\DataverseLabelTranslator\scripts\deploy-azure.ps1 -DryRun
```

3. Run the live deploy. With explicit version:

```powershell
powershell -ExecutionPolicy Bypass -File D:\github\DataverseLabelTranslator\scripts\deploy-azure.ps1 -SolutionVersion 1.1.0.0
```

Without explicit version:

```powershell
powershell -ExecutionPolicy Bypass -File D:\github\DataverseLabelTranslator\scripts\deploy-azure.ps1
```

4. Verify script output includes:

- Azure user `sales@d365iconsandtooltips.com`,
- uploaded ZIP path,
- storage account `ple`,
- container `dataverselabeltranslator`,
- SAS expiry UTC,
- generated `release.md` path.

5. Final response must not paste the SAS URL. Tell the user to open:

```text
D:\github\DataverseLabelTranslator\release\<version>\appsource\zip\release.md
```

and paste the SAS URL into:

```text
Partner Center -> Technical configuration -> CRM package -> URL of your package location
```

## Output Contract

The script writes sensitive Partner Center upload details to:

```text
D:\github\DataverseLabelTranslator\release\<version>\appsource\zip\release.md
```

`release.md` contains the real read-only SAS URL and must remain local/private. It is ignored by git.

## Hard Rules

- Do not export the Dataverse solution.
- Do not run Release AppSource unless the user explicitly asks for that separate step.
- Do not upload anything except the final all-in-one ZIP.
- Do not print or paste the SAS URL into chat.
- Do not stage files.
- Do not commit.
- Do not push.
