param(
    [string]$SolutionVersion = "",
    [string]$RepositoryRoot = "",
    [string]$ExpectedAzureUser = "sales@d365iconsandtooltips.com",
    [string]$StorageAccountName = "ple",
    [string]$StorageResourceGroup = "SHARED",
    [string]$ContainerName = "dataverselabeltranslator",
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

function Resolve-RepositoryRoot {
    param([string]$RequestedRoot)

    if (-not [string]::IsNullOrWhiteSpace($RequestedRoot)) {
        return (Resolve-Path -LiteralPath $RequestedRoot).Path
    }

    try {
        $gitRoot = (& git rev-parse --show-toplevel 2>$null).Trim()
        if (-not [string]::IsNullOrWhiteSpace($gitRoot)) {
            return $gitRoot
        }
    }
    catch {
    }

    return (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
}

function Get-VersionValue {
    param([string]$VersionText)

    try {
        return [version]$VersionText
    }
    catch {
        return [version]"0.0.0.0"
    }
}

function Get-PackageVersion {
    param([string]$VersionText)

    $parts = $VersionText.Split(".")
    if ($parts.Count -lt 3) {
        throw "Solution version must have at least three numeric parts. Actual: $VersionText"
    }

    return "$($parts[0]).$($parts[1]).$($parts[2])"
}

function Resolve-FinalZip {
    param(
        [string]$Root,
        [string]$RequestedVersion
    )

    $releaseRoot = Join-Path $Root "DataverseLabelTranslator.Release"
    if (-not (Test-Path -LiteralPath $releaseRoot)) {
        throw "Release folder was not found: $releaseRoot"
    }

    if (-not [string]::IsNullOrWhiteSpace($RequestedVersion)) {
        $packageVersion = Get-PackageVersion -VersionText $RequestedVersion
        $zipPath = Join-Path $Root "DataverseLabelTranslator.Release\$RequestedVersion\appsource\zip\DataverseLabelTranslator.v.$packageVersion.zip"
        if (-not (Test-Path -LiteralPath $zipPath)) {
            throw "Final AppSource ZIP was not found for version $RequestedVersion`: $zipPath. Run Release AppSource first."
        }

        return [pscustomobject]@{
            SolutionVersion = $RequestedVersion
            PackageVersion = $packageVersion
            ZipPath = (Resolve-Path -LiteralPath $zipPath).Path
        }
    }

    $candidates = @()
    $versionFolders = Get-ChildItem -LiteralPath $releaseRoot -Directory |
        Where-Object { $_.Name -match '^\d+\.\d+\.\d+\.\d+$' }

    foreach ($folder in $versionFolders) {
        $packageVersion = Get-PackageVersion -VersionText $folder.Name
        $zipPath = Join-Path $folder.FullName "appsource\zip\DataverseLabelTranslator.v.$packageVersion.zip"
        if (Test-Path -LiteralPath $zipPath) {
            $candidates += [pscustomobject]@{
                SolutionVersion = $folder.Name
                PackageVersion = $packageVersion
                ZipPath = (Resolve-Path -LiteralPath $zipPath).Path
                VersionValue = Get-VersionValue -VersionText $folder.Name
            }
        }
    }

    if ($candidates.Count -eq 0) {
        throw "No final AppSource ZIP was found under DataverseLabelTranslator.Release\<version>\appsource\zip. Run Release AppSource first."
    }

    $selected = $candidates | Sort-Object -Property VersionValue -Descending | Select-Object -First 1
    return [pscustomobject]@{
        SolutionVersion = $selected.SolutionVersion
        PackageVersion = $selected.PackageVersion
        ZipPath = $selected.ZipPath
    }
}

function Assert-AzureCli {
    if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
        throw "Azure CLI 'az' was not found on PATH."
    }
}

function Assert-AzureLogin {
    param([string]$ExpectedUser)

    $actualUser = (& az account show --query "user.name" -o tsv 2>$null).Trim()
    if ([string]::IsNullOrWhiteSpace($actualUser)) {
        throw "Azure CLI is not logged in. Run 'az login' with $ExpectedUser."
    }

    if ($actualUser -ine $ExpectedUser) {
        throw "Wrong Azure account. Expected '$ExpectedUser', actual '$actualUser'. Stop without upload."
    }

    return $actualUser
}

function Get-StorageAccount {
    param(
        [string]$AccountName,
        [string]$ResourceGroup
    )

    try {
        $json = & az storage account show --name $AccountName --resource-group $ResourceGroup -o json
        return ($json | ConvertFrom-Json)
    }
    catch {
        throw "Azure Storage account '$AccountName' in resource group '$ResourceGroup' was not found or is not accessible. Stop without upload."
    }
}

function Get-StorageAccountKey {
    param(
        [string]$AccountName,
        [string]$ResourceGroup
    )

    $key = (& az storage account keys list --account-name $AccountName --resource-group $ResourceGroup --query "[0].value" -o tsv).Trim()
    if ([string]::IsNullOrWhiteSpace($key)) {
        throw "Could not read a storage account key for '$AccountName'. Stop without upload."
    }

    return $key
}

function New-ReleaseMarkdown {
    param(
        [string]$OutputPath,
        [string]$SolutionVersion,
        [string]$PackageVersion,
        [string]$FinalZipPath,
        [long]$FinalZipSize,
        [string]$Sha256,
        [string]$StorageAccount,
        [string]$ResourceGroup,
        [string]$Container,
        [string]$BlobName,
        [string]$BlobUrl,
        [string]$SasUrl,
        [string]$SasExpiryUtc,
        [string]$AzureUser,
        [string]$GeneratedAtUtc
    )

    $content = @"
# Deploy Azure Release

Generated at UTC: $GeneratedAtUtc

## AppSource Package

- Solution version: $SolutionVersion
- Marketplace package version: $PackageVersion
- Final ZIP: $FinalZipPath
- Size bytes: $FinalZipSize
- SHA256: $Sha256

## Azure Blob

- Azure user: $AzureUser
- Resource group: $ResourceGroup
- Storage account: $StorageAccount
- Container: $Container
- Blob name: $BlobName
- Blob URL: $BlobUrl
- SAS expiry UTC: $SasExpiryUtc

## Partner Center

Paste this value into Partner Center:

```text
Technical configuration -> CRM package -> URL of your package location
```

SAS URL:

```text
$SasUrl
```

## Notes

- This URL is read-only and HTTPS-only.
- This file contains a real SAS URL and must stay local/private.
- Do not commit this file.
"@

    Set-Content -LiteralPath $OutputPath -Value $content -Encoding UTF8
}

$root = Resolve-RepositoryRoot -RequestedRoot $RepositoryRoot
$zipInfo = Resolve-FinalZip -Root $root -RequestedVersion $SolutionVersion
$zipItem = Get-Item -LiteralPath $zipInfo.ZipPath
$zipHash = (Get-FileHash -LiteralPath $zipInfo.ZipPath -Algorithm SHA256).Hash
$zipDir = Split-Path -Parent $zipInfo.ZipPath
$releaseMarkdownPath = Join-Path $zipDir "release.md"
$blobName = Split-Path -Leaf $zipInfo.ZipPath

Assert-AzureCli
$azureUser = Assert-AzureLogin -ExpectedUser $ExpectedAzureUser
$storage = Get-StorageAccount -AccountName $StorageAccountName -ResourceGroup $StorageResourceGroup
$blobEndpoint = [string]$storage.primaryEndpoints.blob
if ([string]::IsNullOrWhiteSpace($blobEndpoint)) {
    throw "Storage account '$StorageAccountName' does not expose a blob endpoint. Stop without upload."
}

$blobUrl = "$($blobEndpoint.TrimEnd('/'))/$ContainerName/$blobName"

if ($DryRun) {
    Write-Host ""
    Write-Host "Deploy Azure dry run completed."
    Write-Host "Azure user: $azureUser"
    Write-Host "Storage account: $StorageAccountName"
    Write-Host "Resource group: $StorageResourceGroup"
    Write-Host "Container: $ContainerName (will be created if missing on live run)"
    Write-Host "Solution version: $($zipInfo.SolutionVersion)"
    Write-Host "Package version: $($zipInfo.PackageVersion)"
    Write-Host "Final ZIP: $($zipInfo.ZipPath)"
    Write-Host "Size bytes: $($zipItem.Length)"
    Write-Host "SHA256: $zipHash"
    Write-Host "Target blob: $blobUrl"
    Write-Host "Release markdown path: $releaseMarkdownPath"
    Write-Host ""
    Write-Host "Dry run only. No upload, no SAS, no release.md."
    exit 0
}

$accountKey = Get-StorageAccountKey -AccountName $StorageAccountName -ResourceGroup $StorageResourceGroup

& az storage container create `
    --account-name $StorageAccountName `
    --account-key $accountKey `
    --name $ContainerName `
    --public-access off `
    --only-show-errors `
    -o none

& az storage blob upload `
    --account-name $StorageAccountName `
    --account-key $accountKey `
    --container-name $ContainerName `
    --name $blobName `
    --file $zipInfo.ZipPath `
    --overwrite true `
    --only-show-errors `
    -o none

$blobSize = (& az storage blob show `
    --account-name $StorageAccountName `
    --account-key $accountKey `
    --container-name $ContainerName `
    --name $blobName `
    --query "properties.contentLength" `
    -o tsv).Trim()

if ([int64]$blobSize -ne [int64]$zipItem.Length) {
    throw "Uploaded blob size mismatch. Local=$($zipItem.Length), Blob=$blobSize"
}

$sasExpiryUtc = [DateTime]::UtcNow.AddMonths(1).ToString("yyyy-MM-ddTHH:mmZ")
$sasUrl = (& az storage blob generate-sas `
    --account-name $StorageAccountName `
    --account-key $accountKey `
    --container-name $ContainerName `
    --name $blobName `
    --permissions r `
    --expiry $sasExpiryUtc `
    --https-only `
    --full-uri `
    -o tsv).Trim()

if ([string]::IsNullOrWhiteSpace($sasUrl)) {
    throw "Azure CLI returned an empty SAS URL."
}

$headResponse = Invoke-WebRequest -Uri $sasUrl -Method Head -UseBasicParsing
if ([int]$headResponse.StatusCode -lt 200 -or [int]$headResponse.StatusCode -ge 400) {
    throw "SAS URL HEAD verification failed. Status code: $($headResponse.StatusCode)"
}

$generatedAtUtc = [DateTime]::UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
New-ReleaseMarkdown `
    -OutputPath $releaseMarkdownPath `
    -SolutionVersion $zipInfo.SolutionVersion `
    -PackageVersion $zipInfo.PackageVersion `
    -FinalZipPath $zipInfo.ZipPath `
    -FinalZipSize $zipItem.Length `
    -Sha256 $zipHash `
    -StorageAccount $StorageAccountName `
    -ResourceGroup $StorageResourceGroup `
    -Container $ContainerName `
    -BlobName $blobName `
    -BlobUrl $blobUrl `
    -SasUrl $sasUrl `
    -SasExpiryUtc $sasExpiryUtc `
    -AzureUser $azureUser `
    -GeneratedAtUtc $generatedAtUtc

Write-Host ""
Write-Host "Deploy Azure completed."
Write-Host "Azure user: $azureUser"
Write-Host "Storage account: $StorageAccountName"
Write-Host "Container: $ContainerName"
Write-Host "Blob: $blobName"
Write-Host "Solution version: $($zipInfo.SolutionVersion)"
Write-Host "Final ZIP uploaded: $($zipInfo.ZipPath)"
Write-Host "Size bytes: $($zipItem.Length)"
Write-Host "SHA256: $zipHash"
Write-Host "SAS expiry UTC: $sasExpiryUtc"
Write-Host "Release markdown: $releaseMarkdownPath"
Write-Host ""
Write-Host "Open release.md and paste the SAS URL into Partner Center."
