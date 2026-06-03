---
name: "pl-release-2-prepare-appsource"
display-name: "PL Release 2 Prepare AppSource"
description: "Build the final AppSource all-in-one Marketplace ZIP for Dataverse Label Translator. Does not export Dataverse solution or upload to Azure."
argument-hint: "[solution-version]"
---

# Release AppSource

Use this skill when the user asks to run `Release AppSource`, `/pl-release-2-prepare-appsource`, or build the final AppSource package.

## Version Resolution

If the user mentions a version such as `1.1.0.0`, pass it explicitly:

```powershell
powershell -ExecutionPolicy Bypass -File D:\github\DataverseLabelTranslator\DataverseLabelTranslator.Scripts\release-appsource.ps1 -SolutionVersion 1.1.0.0
```

If the user does not mention a version, run the script without `-SolutionVersion`. The script must infer the latest version by scanning:

```text
D:\github\DataverseLabelTranslator\DataverseLabelTranslator.Release\<version>\dataverse\solutions\DataverseLabelTranslator_managed.zip
```

The highest four-part numeric version wins. If no managed release folders exist, the script falls back to `1.0.0.0` and then fails clearly if the managed solution source is missing.

Marketplace package version is derived from the first three solution version parts unless explicitly provided:

```text
1.0.0.0 -> 1.0.0
1.1.0.0 -> 1.1.0
```

Each version is self-contained under:

```text
D:\github\DataverseLabelTranslator\DataverseLabelTranslator.Release\<solution-version>
```

For a new version, copy the whole previous version folder, for example `DataverseLabelTranslator.Release\1.0.0.0` to `DataverseLabelTranslator.Release\1.0.1.0`, replace the managed solution under `dataverse\solutions`, then run this skill with `-SolutionVersion 1.0.1.0`. The script only rebuilds `appsource\src` and `appsource\zip` for the selected version and must not touch older version folders.

## Output Contract

Every successful run must leave the final all-in-one upload ZIP in the selected version folder:

```text
D:\github\DataverseLabelTranslator\DataverseLabelTranslator.Release\<solution-version>\appsource\zip\DataverseLabelTranslator.v.<package-version>.zip
```

This is the file to upload to Azure Blob Storage for Partner Center.

The nested Package Deployer ZIP must also be rebuilt under the selected version:

```text
D:\github\DataverseLabelTranslator\DataverseLabelTranslator.Release\<solution-version>\appsource\src\DataverseLabelTranslator.v.<package-version>\DataverseLabelTranslatorPackage.zip
```

Do not upload the nested Package Deployer ZIP directly. It belongs inside the final all-in-one Marketplace ZIP.

## Source Of Truth

Always trust the selected existing managed solution as latest/newest:

```text
D:\github\DataverseLabelTranslator\DataverseLabelTranslator.Release\<solution-version>\dataverse\solutions\DataverseLabelTranslator_managed.zip
```

Do not run `/pl-release-1-export-solutions`.
Do not run the `pl-release-1-export-solutions` skill.
Do not check Dataverse freshness.
Do not read the version from Dataverse.
Do not suggest exporting in this workflow.

If the managed solution file is missing, stop and report the missing file.

## Workflow

1. Confirm repository root:

```powershell
git rev-parse --show-toplevel
```

Expected:

```text
D:\github\DataverseLabelTranslator
```

2. Run the release script. If user mentioned a version, pass it:

```powershell
powershell -ExecutionPolicy Bypass -File D:\github\DataverseLabelTranslator\DataverseLabelTranslator.Scripts\release-appsource.ps1 -SolutionVersion 1.1.0.0
```

If user did not mention a version, let the script infer latest:

```powershell
powershell -ExecutionPolicy Bypass -File D:\github\DataverseLabelTranslator\DataverseLabelTranslator.Scripts\release-appsource.ps1
```

3. Verify final ZIP root entries are exactly:

```text
DataverseLabelTranslatorPackage.zip
[Content_Types].xml
input.xml
TermsOfUse.html
logo32x32.png
```

4. Report:

- final ZIP path,
- final ZIP size,
- nested Package Deployer ZIP path,
- confirmation that the existing managed solution was used as source,
- confirmation that nothing was uploaded.

## Generated Assets

The script generates non-screenshot assets such as logos, homepage hero, AppSource package flow, AI privacy flow, wizard visual, and video thumbnail under:

```text
D:\github\DataverseLabelTranslator\DataverseLabelTranslator.Release\1.0.0.0\appsource\assets
D:\github\DataverseLabelTranslator\DataverseLabelTranslator.Release\1.0.0.0\appsource\Videos
```

For newer versions, replace `1.0.0.0` with the selected solution version.

Real product screenshots are not generated. They are left for anh Phuoc to capture under:

```text
D:\github\DataverseLabelTranslator\DataverseLabelTranslator.Release\1.0.0.0\appsource\Images
D:\github\DataverseLabelTranslator\DataverseLabelTranslator.Release\1.0.0.0\appsource\Test\screenshots
```

For newer versions, replace `1.0.0.0` with the selected solution version.

## Hard Rules

- Do not stage files.
- Do not commit.
- Do not push.
- Do not upload to Azure.
- Do not write a real SAS URL into git.
- Do not deploy to Dataverse.
- Do not modify older version folders. Treat existing version folders such as `DataverseLabelTranslator.Release\1.0.0.0` as read-only unless the user explicitly selected that version.
