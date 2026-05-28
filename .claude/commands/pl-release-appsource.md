# Release AppSource

Build the final AppSource all-in-one Marketplace ZIP for Dataverse Label Translator.

This command does not export the Dataverse solution and does not upload to Azure.

## Input

`$ARGUMENTS` may optionally contain a solution version such as:

```text
1.1.0.0
```

If no version is provided, let the script infer the latest release version.

## Version Resolution

If `$ARGUMENTS` contains a version, pass it explicitly:

```powershell
powershell -ExecutionPolicy Bypass -File D:\github\DataverseLabelTranslator\scripts\release-appsource.ps1 -SolutionVersion $ARGUMENTS
```

If `$ARGUMENTS` is empty, run:

```powershell
powershell -ExecutionPolicy Bypass -File D:\github\DataverseLabelTranslator\scripts\release-appsource.ps1
```

The script infers the latest version by scanning:

```text
D:\github\DataverseLabelTranslator\release\<version>\dataverse\solutions\DataverseLabelTranslator_managed.zip
```

Marketplace package version is derived from the first three solution version parts:

```text
1.0.0.0 -> 1.0.0
1.1.0.0 -> 1.1.0
```

## Source Of Truth

Always trust the selected existing managed solution as latest/newest:

```text
D:\github\DataverseLabelTranslator\release\<solution-version>\dataverse\solutions\DataverseLabelTranslator_managed.zip
```

Do not run `/pl-export-solution`.
Do not run the `pl-export-solution` skill.
Do not check Dataverse freshness.
Do not read the version from Dataverse.
Do not suggest exporting in this workflow.

If the managed solution file is missing, stop and report the missing file.

## Workflow

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

**Step 2: Build release package**

If `$ARGUMENTS` contains a version:

```powershell
powershell -ExecutionPolicy Bypass -File D:\github\DataverseLabelTranslator\scripts\release-appsource.ps1 -SolutionVersion $ARGUMENTS
```

If `$ARGUMENTS` is empty:

```powershell
powershell -ExecutionPolicy Bypass -File D:\github\DataverseLabelTranslator\scripts\release-appsource.ps1
```

**Step 3: Verify final ZIP**

The final all-in-one upload ZIP must be:

```text
D:\github\DataverseLabelTranslator\release\<solution-version>\appsource\zip\DataverseLabelTranslator.v.<package-version>.zip
```

The final ZIP root entries must be exactly:

```text
DataverseLabelTranslatorPackage.zip
[Content_Types].xml
input.xml
TermsOfUse.html
logo32x32.png
```

The nested Package Deployer ZIP must also be rebuilt:

```text
D:\github\DataverseLabelTranslator\release\<solution-version>\appsource\src\DataverseLabelTranslator.v.<package-version>\DataverseLabelTranslatorPackage.zip
```

Do not upload the nested ZIP directly.

**Step 4: Final response**

Report:

- final ZIP path,
- final ZIP size,
- nested Package Deployer ZIP path,
- confirmation that the existing managed solution was used as source,
- confirmation that nothing was uploaded.

## Hard Rules

- Do not stage files.
- Do not commit.
- Do not push.
- Do not upload to Azure.
- Do not write a real SAS URL into git.
- Do not deploy to Dataverse.
- Do not modify older version folders. Treat existing version folders such as `release\1.0.0.0` as read-only unless the user explicitly selected that version.
