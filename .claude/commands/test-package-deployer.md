# Test Package Deployer

Prepare the local Package Deployer cache with the Dataverse Label Translator package for manual `pac tool pd` testing.

This command does not launch Package Deployer. It copies the built package into the correct local PD tools folder and then tells anh Phuoc to run:

```powershell
pac tool pd
```

## Input

`$ARGUMENTS` may optionally contain a solution version such as:

```text
1.1.0.0
```

If no version is provided, let the script infer the latest release version.

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

**Step 2: Run the preparation script**

If `$ARGUMENTS` contains a version:

```powershell
powershell -ExecutionPolicy Bypass -File D:\github\DataverseLabelTranslator\scripts\test-package-deployer.ps1 -SolutionVersion $ARGUMENTS
```

If `$ARGUMENTS` is empty:

```powershell
powershell -ExecutionPolicy Bypass -File D:\github\DataverseLabelTranslator\scripts\test-package-deployer.ps1
```

If the user explicitly asks to rebuild the AppSource package before copying:

```powershell
powershell -ExecutionPolicy Bypass -File D:\github\DataverseLabelTranslator\scripts\test-package-deployer.ps1 -SolutionVersion $ARGUMENTS -BuildRelease
```

**Step 3: What the script must do**

The script must copy this package source:

```text
D:\github\DataverseLabelTranslator\release\<version>\appsource\src\DataverseLabelTranslatorPackage
```

into the active local Package Deployer tools folder:

```text
%LOCALAPPDATA%\Microsoft\PowerPlatform\PD\<pd-version>\tools
```

The script must find the PD folder automatically by using `pac tool list` first, then falling back to the highest local PD cache folder.

Required copied items:

```text
PkgFolder
PL.DataverseLabelTranslator.PackageDeployment.dll
[Content_Types].xml
```

Before copying, the script removes stale custom `PL.*.PackageDeployment.dll` files from the PD tools folder so Package Deployer does not load an old custom package definition.

If the package source is missing, the script may run `Release AppSource` for the selected version. It must not export the Dataverse solution.

**Step 4: Final response**

Report:

- selected solution version,
- package source folder,
- PD tools folder,
- copied items.

Then tell anh Phuoc:

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
