---
name: "pl-release-1-export-solutions"
display-name: "PL Release 1 Export Solutions"
description: "Export, clean, pack, and publish release ZIPs through the DevKit SolutionPackager project."
---

# pl-release-1-export-solutions

Use this workflow when the user asks to run `pl-release-1-export-solutions`.

## Command Template

# Export Solutions With SolutionPackager

Export the Dataverse Label Translator (`DataverseLabelTranslator`) solution through the checked-in DevKit SolutionPackager project, remove non-base-language labels directly from the SolutionPackager unpacked tree, stamp a hard-coded release version, pack through SolutionPackager, and copy the final managed and unmanaged release ZIPs into the release folder.

The release workflow must use these checked-in batch files:

```text
D:\github\DataverseLabelTranslator\DataverseLabelTranslator.SolutionPackager\Extract-Both.bat
D:\github\DataverseLabelTranslator\DataverseLabelTranslator.SolutionPackager\Pack-Both.bat
```

Do not invoke Power Platform CLI solution export, unpack, or pack commands directly in this workflow. The DevKit SolutionPackager project is the source of truth for export, unpack, and pack.

The label cleanup and version stamp must update this source-controlled SolutionPackager unpack folder directly:

```text
D:\github\DataverseLabelTranslator\DataverseLabelTranslator.SolutionPackager\DataverseLabelTranslator\Both
```

## Current Release Version

```powershell
$Version = "1.0.0.0"
```

Do not read the version from Dataverse or from `Solution.xml`. When a new version is needed, update this file manually.

## Output Contract

Each run must leave exactly these two ZIP files in the versioned release folder:

```text
D:\github\DataverseLabelTranslator\DataverseLabelTranslator.Release\1.0.0.0\dataverse\solutions\DataverseLabelTranslator.zip
D:\github\DataverseLabelTranslator\DataverseLabelTranslator.Release\1.0.0.0\dataverse\solutions\DataverseLabelTranslator_managed.zip
```

`Extract-Both.bat` updates the normal SolutionPackager output under:

```text
D:\github\DataverseLabelTranslator\DataverseLabelTranslator.SolutionPackager\DataverseLabelTranslator\Both
D:\github\DataverseLabelTranslator\DataverseLabelTranslator.SolutionPackager\DataverseLabelTranslator\Solutions-Extract
```

`Pack-Both.bat` creates the packed solution ZIPs under the SolutionPackager project. Copy those packed ZIPs into the versioned release folder using the fixed output names above.

The cleaned unpacked solution must also remain available for review at:

```text
D:\github\DataverseLabelTranslator\DataverseLabelTranslator.Release\1.0.0.0\dataverse\unpack
```

After packing, do not stage anything. Leave generated files as normal git changes. The `/pl-commit` command is responsible for staging and committing when needed. Do not commit and do not push.

## Instructions

### Step 1: Confirm Repository

Run from:

```powershell
D:\github\DataverseLabelTranslator
```

Verify the current folder:

```powershell
git rev-parse --show-toplevel
```

If the result is not `D:\github\DataverseLabelTranslator`, stop.

### Step 2: Confirm SolutionPackager Project

Prepare and validate the SolutionPackager paths:

```powershell
$RepoRoot = "D:\github\DataverseLabelTranslator"
$SolutionPackagerDir = Join-Path $RepoRoot "DataverseLabelTranslator.SolutionPackager"
$ExtractBat = Join-Path $SolutionPackagerDir "Extract-Both.bat"
$PackBat = Join-Path $SolutionPackagerDir "Pack-Both.bat"
$PackagerSolutionDir = Join-Path $SolutionPackagerDir "DataverseLabelTranslator"
$PackagerUnpackDir = Join-Path $PackagerSolutionDir "Both"
$PackagerExtractDir = Join-Path $PackagerSolutionDir "Solutions-Extract"
$CleanLanguageScript = Join-Path $RepoRoot "DataverseLabelTranslator.Scripts\clean-language.ps1"

if (-not (Test-Path -LiteralPath $ExtractBat)) {
    throw "Missing SolutionPackager extract script: $ExtractBat"
}

if (-not (Test-Path -LiteralPath $PackBat)) {
    throw "Missing SolutionPackager pack script: $PackBat"
}

if (-not (Test-Path -LiteralPath $CleanLanguageScript)) {
    throw "Missing clean-language script: $CleanLanguageScript"
}
```

Do not require a separate auth profile in this workflow. The DevKit batch files read connection settings from root `.env` / `DEVKIT_*` environment variables through the checked-in batch format.

### Step 3: Prepare Release Paths

Use the hard-coded version and reset only that version's `dataverse` folder. Do not delete or rewrite `DataverseLabelTranslator.Release\$Version\appsource`. Do not delete the SolutionPackager project folder.

```powershell
$Version = "1.0.0.0"
$BaseLanguageCode = 1033
$ReleaseDir = Join-Path $RepoRoot "DataverseLabelTranslator.Release\$Version"
$DataverseDir = Join-Path $ReleaseDir "dataverse"
$SolutionsDir = Join-Path $DataverseDir "solutions"
$ReleaseUnpackDir = Join-Path $DataverseDir "unpack"

if (Test-Path -LiteralPath $DataverseDir) {
    Remove-Item -LiteralPath $DataverseDir -Recurse -Force
}

New-Item -ItemType Directory -Path $SolutionsDir -Force | Out-Null
New-Item -ItemType Directory -Path $ReleaseUnpackDir -Force | Out-Null
```

### Step 4: Extract Through SolutionPackager

Run the checked-in DevKit SolutionPackager extract batch file from the repository root:

```powershell
& $ExtractBat
if ($LASTEXITCODE -ne 0) {
    throw "Extract-Both.bat failed with exit code $LASTEXITCODE."
}
```

If the batch file fails, show the full output and stop.

Verify the expected SolutionPackager output exists:

```powershell
if (-not (Test-Path -LiteralPath $PackagerUnpackDir)) {
    throw "Missing SolutionPackager unpack folder: $PackagerUnpackDir"
}

if (-not (Test-Path -LiteralPath $PackagerExtractDir)) {
    throw "Missing SolutionPackager extract folder: $PackagerExtractDir"
}
```

### Step 5: Clean SolutionPackager Unpack And Stamp Version

Run cleanup directly against the SolutionPackager unpack folder:

```powershell
powershell -ExecutionPolicy Bypass -File $CleanLanguageScript -Path $PackagerUnpackDir -BaseLanguageCode $BaseLanguageCode -Version $Version
```

This removes non-base-language entries from the source-controlled SolutionPackager unpacked solution and replaces `Version: x.xx.xx.xx` with `Version: 1.0.0.0`.

If cleanup fails, show the full output and stop.

### Step 6: Pack Through SolutionPackager

Run the checked-in DevKit SolutionPackager pack batch file from the repository root:

```powershell
$PackStartedAt = Get-Date

& $PackBat
if ($LASTEXITCODE -ne 0) {
    throw "Pack-Both.bat failed with exit code $LASTEXITCODE."
}
```

If the batch file fails, show the full output and stop.

### Step 7: Copy Packed ZIPs To Release Folder

Find the ZIPs created by `Pack-Both.bat` under the SolutionPackager project. Do not use ZIPs from `Solutions-Extract` as the final release output.

```powershell
$PackedZipCandidates = Get-ChildItem -LiteralPath $PackagerSolutionDir -Recurse -File -Filter "*.zip" |
    Where-Object {
        $_.FullName -notlike "$PackagerExtractDir\*" -and
        $_.Name -like "DataverseLabelTranslator*.zip" -and
        $_.LastWriteTime -ge $PackStartedAt.AddMinutes(-1)
    }

$PackagerUnmanagedZip = $PackedZipCandidates |
    Where-Object { $_.Name -notlike "*_managed.zip" } |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

$PackagerManagedZip = $PackedZipCandidates |
    Where-Object { $_.Name -like "*_managed.zip" } |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $PackagerUnmanagedZip) {
    throw "Could not find unmanaged ZIP created by Pack-Both.bat under: $PackagerSolutionDir"
}

if (-not $PackagerManagedZip) {
    throw "Could not find managed ZIP created by Pack-Both.bat under: $PackagerSolutionDir"
}

$UnmanagedZip = Join-Path $SolutionsDir "DataverseLabelTranslator.zip"
$ManagedZip = Join-Path $SolutionsDir "DataverseLabelTranslator_managed.zip"

Copy-Item -LiteralPath $PackagerUnmanagedZip.FullName -Destination $UnmanagedZip -Force
Copy-Item -LiteralPath $PackagerManagedZip.FullName -Destination $ManagedZip -Force
```

### Step 8: Keep Review Copy

Copy the cleaned SolutionPackager unpacked solution into `dataverse\unpack`:

```powershell
Get-ChildItem -LiteralPath $PackagerUnpackDir -Force |
    Copy-Item -Destination $ReleaseUnpackDir -Recurse -Force
```

### Step 9: Verify Output Contract

Ensure the `dataverse\solutions` folder contains exactly two ZIP files:

```powershell
$zipFiles = Get-ChildItem -LiteralPath $SolutionsDir -File -Filter *.zip
if ($zipFiles.Count -ne 2) {
    throw "Expected exactly 2 ZIP files in $SolutionsDir, found $($zipFiles.Count)."
}
```

Ensure both expected paths exist:

```powershell
if (-not (Test-Path -LiteralPath $UnmanagedZip)) {
    throw "Missing unmanaged export: $UnmanagedZip"
}

if (-not (Test-Path -LiteralPath $ManagedZip)) {
    throw "Missing managed export: $ManagedZip"
}
```

Ensure the unpack review folder exists:

```powershell
if (-not (Test-Path -LiteralPath $ReleaseUnpackDir)) {
    throw "Missing unpack review folder: $ReleaseUnpackDir"
}
```

### Step 10: Do Not Stage Files

Do not run `git add`.
Do not run `git commit`.
Do not run `git push`.

Leave generated files as normal uncommitted git changes. At minimum, the final release ZIPs must exist:

```text
DataverseLabelTranslator.Release\$Version\dataverse\solutions\DataverseLabelTranslator.zip
DataverseLabelTranslator.Release\$Version\dataverse\solutions\DataverseLabelTranslator_managed.zip
```

The `DataverseLabelTranslator.Release\$Version\dataverse\unpack` folder is for manual review only and should be ignored by git.

### Step 11: Report Result

Report:

- Release version: `1.0.0.0`
- SolutionPackager extract script: `D:\github\DataverseLabelTranslator\DataverseLabelTranslator.SolutionPackager\Extract-Both.bat`
- SolutionPackager pack script: `D:\github\DataverseLabelTranslator\DataverseLabelTranslator.SolutionPackager\Pack-Both.bat`
- Cleaned SolutionPackager unpack folder: `D:\github\DataverseLabelTranslator\DataverseLabelTranslator.SolutionPackager\DataverseLabelTranslator\Both`
- Release folder: `D:\github\DataverseLabelTranslator\DataverseLabelTranslator.Release\1.0.0.0`
- Unpacked review folder: `D:\github\DataverseLabelTranslator\DataverseLabelTranslator.Release\1.0.0.0\dataverse\unpack`
- Exported files: `DataverseLabelTranslator.zip`, `DataverseLabelTranslator_managed.zip`
- Confirm the ZIP files were generated but not staged.
- Confirm `DataverseLabelTranslator.Release\1.0.0.0\dataverse\unpack` is present for review and ignored by git.
