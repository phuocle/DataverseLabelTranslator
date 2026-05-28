---
name: "PL Deploy Azure"
description: Upload the final Dataverse Label Translator AppSource ZIP to Azure Blob Storage and write the Partner Center SAS details to release.md.
argument-hint: "[solution-version]"
disable-model-invocation: true
---

# Deploy Azure

Upload the final Dataverse Label Translator AppSource ZIP to Azure Blob Storage and write the Partner Center SAS details to `release.md`.

This command uploads only the final all-in-one ZIP. It must not export the Dataverse solution and must not run Release AppSource unless the user separately asks for that first.

## Fixed Azure Target

Expected Azure login:

```text
sales@d365iconsandtooltips.com
```

Storage target:

```text
Resource group: SHARED
Storage account: ple
Container: dataverselabeltranslator
```

The script creates the container if it does not exist. The container remains private.

## Input

`$ARGUMENTS` may optionally contain a solution version such as:

```text
1.1.0.0
```

If no version is provided, let the script infer the latest final ZIP.

## Instructions

**Step 1: Confirm repository**

Run from:

```powershell
D:\github\DataverseLabelTranslator
```

Verify:

```powershell
git rev-parse --show-toplevel
```

Expected:

```text
D:\github\DataverseLabelTranslator
```

**Step 2: Optional dry run**

For setup validation only:

```powershell
powershell -ExecutionPolicy Bypass -File D:\github\DataverseLabelTranslator\scripts\deploy-azure.ps1 -DryRun
```

**Step 3: Live deploy**

If `$ARGUMENTS` contains a version:

```powershell
powershell -ExecutionPolicy Bypass -File D:\github\DataverseLabelTranslator\scripts\deploy-azure.ps1 -SolutionVersion $ARGUMENTS
```

If `$ARGUMENTS` is empty:

```powershell
powershell -ExecutionPolicy Bypass -File D:\github\DataverseLabelTranslator\scripts\deploy-azure.ps1
```

**Step 4: Expected behavior**

The script must:

- verify `az account show --query user.name -o tsv` equals `sales@d365iconsandtooltips.com`,
- verify storage account `ple` exists in resource group `SHARED`,
- create private container `dataverselabeltranslator` if missing,
- upload `release\<version>\appsource\zip\DataverseLabelTranslator.v.<major.minor.patch>.zip`,
- generate a read-only HTTPS SAS URL expiring one month from creation,
- write Partner Center details to `release\<version>\appsource\zip\release.md`.

**Step 5: Final response**

Do not paste the SAS URL into chat. Report:

- final ZIP path,
- storage account/container/blob,
- SAS expiry UTC,
- `release.md` path.

Tell anh Phuoc to open `release.md` and paste the SAS URL into:

```text
Partner Center -> Technical configuration -> CRM package -> URL of your package location
```

## Hard Rules

- Do not export the Dataverse solution.
- Do not run Release AppSource unless the user explicitly asks for that separate step.
- Do not upload anything except the final all-in-one ZIP.
- Do not print or paste the SAS URL into chat.
- Do not stage files.
- Do not commit.
- Do not push.
