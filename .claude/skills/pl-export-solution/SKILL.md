---
name: "PL Export Solution"
description: Export the DataverseLabelTranslator solution with cleaned labels and final managed/unmanaged release ZIPs.
disable-model-invocation: true
---

# Export Solution

Export the Dataverse Label Translator (`DataverseLabelTranslator`) solution, remove non-base-language labels, stamp a hard-coded release version, and pack the final managed/unmanaged release ZIPs.

## Current Release Version

```powershell
$Version = "1.0.0.0"
```

Do not read the version from Dataverse or from `Solution.xml`. When a new version is needed, update this file manually.

## Output Contract

Each run must leave exactly these two ZIP files in the versioned release folder:

```text
D:\github\DataverseLabelTranslator\release\1.0.0.0\dataverse\solutions\DataverseLabelTranslator.zip
D:\github\DataverseLabelTranslator\release\1.0.0.0\dataverse\solutions\DataverseLabelTranslator_managed.zip
```

Temporary raw export files must be created outside the release folder and removed after the final ZIPs are packed.

The cleaned unpacked solution must remain available for review at:

```text
D:\github\DataverseLabelTranslator\release\1.0.0.0\dataverse\unpack
```

After packing, do not stage anything. Leave the generated ZIP files as normal git changes. The `/pl-commit` command is responsible for staging and committing when needed. Do not commit and do not push.

## Instructions

**Step 1: Confirm repository**

Run from:

```powershell
D:\github\DataverseLabelTranslator
```

Verify the current folder:

```powershell
git rev-parse --show-toplevel
```

If the result is not `D:\github\DataverseLabelTranslator`, stop.

**Step 2: Check PAC profile `DataverseLabelTranslator`**

Run:

```powershell
pac auth list
```

Look for a profile named exactly `DataverseLabelTranslator`.

If the profile does not exist, stop and ask the user to create it:

```powershell
pac auth create --name DataverseLabelTranslator --url <your-environment-url>
```

**Step 3: Select PAC profile `DataverseLabelTranslator`**

Run:

```powershell
pac auth select --name DataverseLabelTranslator
```

If this fails, show the error and stop.

**Step 4: Prepare paths**

Use the hard-coded version and reset only that version's `dataverse` folder. Do not delete or rewrite `release\$Version\appsource`.

```powershell
$RepoRoot = "D:\github\DataverseLabelTranslator"
$Version = "1.0.0.0"
$BaseLanguageCode = 1033
$ReleaseDir = Join-Path $RepoRoot "release\$Version"
$DataverseDir = Join-Path $ReleaseDir "dataverse"
$SolutionsDir = Join-Path $DataverseDir "solutions"
$ReleaseUnpackDir = Join-Path $DataverseDir "unpack"
$TempRoot = Join-Path ([System.IO.Path]::GetTempPath()) "DataverseLabelTranslator-export-$Version"
$TempRawDir = Join-Path $TempRoot "raw"
$TempUnpackDir = Join-Path $TempRoot "unpack"
$CleanLanguageScript = Join-Path $RepoRoot "scripts\clean-language.ps1"

if (Test-Path -LiteralPath $DataverseDir) {
    Remove-Item -LiteralPath $DataverseDir -Recurse -Force
}

if (Test-Path -LiteralPath $TempRoot) {
    Remove-Item -LiteralPath $TempRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $SolutionsDir -Force | Out-Null
New-Item -ItemType Directory -Path $TempRawDir | Out-Null
```

**Step 5: Export raw solution ZIPs to temp**

Run exactly these two exports:

```powershell
$RawUnmanagedZip = Join-Path $TempRawDir "DataverseLabelTranslator.zip"
$RawManagedZip = Join-Path $TempRawDir "DataverseLabelTranslator_managed.zip"

pac solution export --name DataverseLabelTranslator --path $RawUnmanagedZip --overwrite
pac solution export --name DataverseLabelTranslator --path $RawManagedZip --managed --overwrite
```

If either export fails, show the full error output and stop.

**Step 6: Unpack**

Unpack to temp only:

```powershell
pac solution unpack --zipfile $RawUnmanagedZip --folder $TempUnpackDir --packagetype Both --allowWrite true --clobber true
```

If unpack fails, show the full error output and stop.

**Step 7: Remove non-base-language entries and stamp hard-coded version**

Run:

```powershell
powershell -ExecutionPolicy Bypass -File $CleanLanguageScript -Path $TempUnpackDir -BaseLanguageCode $BaseLanguageCode -Version $Version
```

This removes non-base-language entries and replaces `Version: x.xx.xx.xx` with `Version: 1.0.0.0`.

If cleanup fails, show the full error output and stop.

**Step 8: Pack final release ZIPs**

Pack only these two final ZIPs into the versioned `dataverse\solutions` folder:

```powershell
$UnmanagedZip = Join-Path $SolutionsDir "DataverseLabelTranslator.zip"
$ManagedZip = Join-Path $SolutionsDir "DataverseLabelTranslator_managed.zip"

pac solution pack --zipfile $UnmanagedZip --folder $TempUnpackDir --packagetype Unmanaged
pac solution pack --zipfile $ManagedZip --folder $TempUnpackDir --packagetype Managed
```

If either pack fails, show the full error output and stop.

**Step 9: Keep cleaned unpacked files for review and remove raw temp files**

Copy the cleaned unpacked solution into `dataverse\unpack`:

```powershell
Copy-Item -LiteralPath $TempUnpackDir -Destination $ReleaseUnpackDir -Recurse -Force
```

Then remove only the temp root. The review copy under the `dataverse` folder must remain:

```powershell
Remove-Item -LiteralPath $TempRoot -Recurse -Force
```

**Step 10: Verify output contract**

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

**Step 11: Do not stage files**

Do not run `git add`.
Do not run `git commit`.
Do not run `git push`.

Leave these files as normal uncommitted git changes:

```text
release\$Version\dataverse\solutions\DataverseLabelTranslator.zip
release\$Version\dataverse\solutions\DataverseLabelTranslator_managed.zip
```

The `release\$Version\dataverse\unpack` folder is for manual review only and should be ignored by git.

**Step 12: Report result**

Report:

- Release version: `1.0.0.0`
- Release folder: `D:\github\DataverseLabelTranslator\release\1.0.0.0`
- Unpacked review folder: `D:\github\DataverseLabelTranslator\release\1.0.0.0\dataverse\unpack`
- Exported files:
  - `DataverseLabelTranslator.zip`
  - `DataverseLabelTranslator_managed.zip`
- Confirm the ZIP files were generated but not staged.
- Confirm `release\1.0.0.0\dataverse\unpack` is present for review and ignored by git.
