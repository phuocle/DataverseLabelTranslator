param(
    [string]$SolutionVersion = "",
    [string]$RepositoryRoot = "",
    [switch]$BuildRelease
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

function Resolve-SolutionVersion {
    param(
        [string]$Root,
        [string]$RequestedVersion
    )

    if (-not [string]::IsNullOrWhiteSpace($RequestedVersion)) {
        return $RequestedVersion
    }

    $releaseRoot = Join-Path $Root "release"
    if (-not (Test-Path -LiteralPath $releaseRoot)) {
        return "1.0.0.0"
    }

    $versions = Get-ChildItem -LiteralPath $releaseRoot -Directory |
        Where-Object {
            $_.Name -match '^\d+\.\d+\.\d+\.\d+$' -and
            (Test-Path -LiteralPath (Join-Path $_.FullName "appsource\src\DataverseLabelTranslatorPackage"))
        } |
        Sort-Object @{ Expression = { Get-VersionValue $_.Name }; Descending = $true }

    if ($versions.Count -gt 0) {
        return $versions[0].Name
    }

    $versions = Get-ChildItem -LiteralPath $releaseRoot -Directory |
        Where-Object {
            $_.Name -match '^\d+\.\d+\.\d+\.\d+$' -and
            (Test-Path -LiteralPath (Join-Path $_.FullName "dataverse\solutions\DataverseLabelTranslator_managed.zip"))
        } |
        Sort-Object @{ Expression = { Get-VersionValue $_.Name }; Descending = $true }

    if ($versions.Count -gt 0) {
        return $versions[0].Name
    }

    return "1.0.0.0"
}

function Get-PacToolListInfo {
    try {
        $lines = & pac tool list 2>$null
        $pdLine = $lines | Where-Object { $_ -match '^\s*PD\s+' } | Select-Object -First 1
        if ($pdLine -and $pdLine -match '^\s*PD\s+(?<installed>\S+)\s+(?<installedVersion>\S+)\s+(?<nuget>\S+)\s+(?<status>.+)$') {
            return [pscustomobject]@{
                Installed = $matches.installed
                InstalledVersion = $matches.installedVersion
                NuGetVersion = $matches.nuget
                Status = $matches.status.Trim()
            }
        }
    }
    catch {
    }

    return $null
}

function Resolve-PackageDeployerToolsPath {
    $basePath = Join-Path $env:LOCALAPPDATA "Microsoft\PowerPlatform\PD"
    $toolInfo = Get-PacToolListInfo

    if ($toolInfo -and $toolInfo.Installed -eq "Yes" -and $toolInfo.InstalledVersion -ne "N/A") {
        $candidate = Join-Path $basePath (Join-Path $toolInfo.InstalledVersion "tools")
        if (Test-Path -LiteralPath (Join-Path $candidate "PackageDeployer.exe")) {
            return [pscustomobject]@{
                Path = (Resolve-Path -LiteralPath $candidate).Path
                Version = $toolInfo.InstalledVersion
                Source = "pac tool list"
            }
        }
    }

    if (Test-Path -LiteralPath $basePath) {
        $candidates = Get-ChildItem -LiteralPath $basePath -Directory |
            Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName "tools\PackageDeployer.exe") } |
            Sort-Object @{ Expression = { Get-VersionValue $_.Name }; Descending = $true }

        if ($candidates.Count -gt 0) {
            return [pscustomobject]@{
                Path = (Join-Path $candidates[0].FullName "tools")
                Version = $candidates[0].Name
                Source = "latest local cache"
            }
        }
    }

    throw "Package Deployer is not installed in '$basePath'. Run 'pac tool pd' once to install it, close Package Deployer, then run this workflow again."
}

function Assert-SafePackageDeployerPath {
    param([string]$Path)

    $expectedRoot = Join-Path $env:LOCALAPPDATA "Microsoft\PowerPlatform\PD"
    $resolved = (Resolve-Path -LiteralPath $Path).Path
    if (-not $resolved.StartsWith($expectedRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to copy into unexpected PD folder: $resolved"
    }

    if (-not (Test-Path -LiteralPath (Join-Path $resolved "PackageDeployer.exe"))) {
        throw "Refusing to copy because PackageDeployer.exe was not found in: $resolved"
    }
}

function Copy-PackageToPackageDeployer {
    param(
        [string]$PackageSource,
        [string]$PackageDeployerToolsPath
    )

    Assert-SafePackageDeployerPath -Path $PackageDeployerToolsPath

    $requiredItems = @(
        "PkgFolder",
        "PL.DataverseLabelTranslator.PackageDeployment.dll",
        "[Content_Types].xml"
    )

    foreach ($item in $requiredItems) {
        $sourcePath = Join-Path $PackageSource $item
        if (-not (Test-Path -LiteralPath $sourcePath)) {
            throw "Missing package source item: $sourcePath"
        }
    }

    $staleDlls = Get-ChildItem -LiteralPath $PackageDeployerToolsPath -Filter "PL.*.PackageDeployment.dll" -File -ErrorAction SilentlyContinue
    foreach ($dll in $staleDlls) {
        Remove-Item -LiteralPath $dll.FullName -Force
    }

    $targetPkgFolder = Join-Path $PackageDeployerToolsPath "PkgFolder"
    if (Test-Path -LiteralPath $targetPkgFolder) {
        Remove-Item -LiteralPath $targetPkgFolder -Recurse -Force
    }

    Copy-Item -LiteralPath (Join-Path $PackageSource "PkgFolder") -Destination $PackageDeployerToolsPath -Recurse -Force
    Copy-Item -LiteralPath (Join-Path $PackageSource "PL.DataverseLabelTranslator.PackageDeployment.dll") -Destination $PackageDeployerToolsPath -Force
    Copy-Item -LiteralPath (Join-Path $PackageSource "[Content_Types].xml") -Destination $PackageDeployerToolsPath -Force

    return $staleDlls.FullName
}

$root = Resolve-RepositoryRoot -RequestedRoot $RepositoryRoot
$version = Resolve-SolutionVersion -Root $root -RequestedVersion $SolutionVersion
$packageSource = Join-Path $root "release\$version\appsource\src\DataverseLabelTranslatorPackage"

if ($BuildRelease -or -not (Test-Path -LiteralPath $packageSource)) {
    $releaseScript = Join-Path $root "scripts\release-appsource.ps1"
    if (-not (Test-Path -LiteralPath $releaseScript)) {
        throw "Missing release script: $releaseScript"
    }

    & powershell -NoProfile -ExecutionPolicy Bypass -File $releaseScript -SolutionVersion $version
}

if (-not (Test-Path -LiteralPath $packageSource)) {
    throw "Package source folder was not found after release build: $packageSource"
}

$pd = Resolve-PackageDeployerToolsPath
$removedDlls = Copy-PackageToPackageDeployer -PackageSource $packageSource -PackageDeployerToolsPath $pd.Path

$importConfig = Join-Path $pd.Path "PkgFolder\ImportConfig.xml"
$managedSolution = Join-Path $pd.Path "PkgFolder\DataverseLabelTranslator_managed.zip"

Write-Host ""
Write-Host "Test Package Deployer prepared."
Write-Host "Solution version: $version"
Write-Host "Package source: $packageSource"
Write-Host "PD tools folder: $($pd.Path)"
Write-Host "PD version: $($pd.Version) ($($pd.Source))"
Write-Host "Copied: PkgFolder, PL.DataverseLabelTranslator.PackageDeployment.dll, [Content_Types].xml"
if ($removedDlls -and $removedDlls.Count -gt 0) {
    Write-Host "Removed stale custom package DLLs:"
    foreach ($dll in $removedDlls) {
        Write-Host "  $dll"
    }
}
Write-Host "ImportConfig: $importConfig"
Write-Host "Managed solution in package: $managedSolution"
Write-Host ""
Write-Host "Ready. Ask aP to run: pac tool pd"
