---
name: "PL Test Package Deployer"
description: "Prepare the local Package Deployer cache with the Dataverse Label Translator package for manual pac tool pd testing."
argument-hint: "[solution-version]"
disable-model-invocation: true
---

<!-- Generated from ../../.agents/skills/pl-test-package-deployer/SKILL.md. Do not edit manually; run scripts/sync-ai-config.ps1. -->

# Test Package Deployer

Use this skill when the user asks to run `Test Package Deployer`, `/pl-test-package-deployer`, test with Package Deployer, or prepare `pac tool pd`.

This workflow does not launch Package Deployer. It prepares the local PD tools folder, then tells anh Phuoc to run:

```powershell
pac tool pd
```

## Version Resolution

If the user mentions a version such as `1.1.0.0`, pass it explicitly:

```powershell
powershell -ExecutionPolicy Bypass -File D:\github\DataverseLabelTranslator\scripts\test-package-deployer.ps1 -SolutionVersion 1.1.0.0
```

If the user does not mention a version, run the script without `-SolutionVersion`. The script must infer the latest release version by scanning:

```text
D:\github\DataverseLabelTranslator\release\<version>\appsource\src\DataverseLabelTranslatorPackage
```

If package source does not exist yet, the script uses the versioned managed solution under:

```text
D:\github\DataverseLabelTranslator\release\<version>\dataverse\solutions\DataverseLabelTranslator_managed.zip
```

and runs `Release AppSource` for that selected version.

## What Gets Copied

The script copies the inner Package Deployer package folder:

```text
D:\github\DataverseLabelTranslator\release\<version>\appsource\src\DataverseLabelTranslatorPackage
```

into the active local Package Deployer tools folder, normally:

```text
%LOCALAPPDATA%\Microsoft\PowerPlatform\PD\<pd-version>\tools
```

The script must discover the correct PD folder itself. It should prefer `pac tool list` and fall back to the highest local folder under:

```text
%LOCALAPPDATA%\Microsoft\PowerPlatform\PD
```

Required copied items:

```text
PkgFolder
PL.DataverseLabelTranslator.PackageDeployment.dll
[Content_Types].xml
```

The script removes stale custom `PL.*.PackageDeployment.dll` files from the PD tools folder before copying, so Package Deployer does not accidentally load an old custom package definition.

## Workflow

1. Confirm repository root:

```powershell
git rev-parse --show-toplevel
```

Expected:

```text
D:\github\DataverseLabelTranslator
```

2. Run the script. If user mentioned a version, pass it:

```powershell
powershell -ExecutionPolicy Bypass -File D:\github\DataverseLabelTranslator\scripts\test-package-deployer.ps1 -SolutionVersion 1.1.0.0
```

If user did not mention a version:

```powershell
powershell -ExecutionPolicy Bypass -File D:\github\DataverseLabelTranslator\scripts\test-package-deployer.ps1
```

If the user explicitly asks to rebuild first, pass `-BuildRelease`:

```powershell
powershell -ExecutionPolicy Bypass -File D:\github\DataverseLabelTranslator\scripts\test-package-deployer.ps1 -SolutionVersion 1.1.0.0 -BuildRelease
```

3. Verify the script output includes:

- selected solution version,
- package source folder,
- PD tools folder,
- copied package items,
- `Ready. Ask aP to run: pac tool pd`.

4. Final response must only tell anh Phuoc the important paths and:

```text
Anh hãy run: pac tool pd
```

Do not launch `pac tool pd` yourself.

## Hard Rules

- Do not export the Dataverse solution.
- Do not upload to Azure.
- Do not deploy to Dataverse.
- Do not launch Package Deployer.
- Do not stage files.
- Do not commit.
- Only copy into a verified `%LOCALAPPDATA%\Microsoft\PowerPlatform\PD\<version>\tools` folder that contains `PackageDeployer.exe`.
