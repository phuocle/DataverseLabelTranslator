param(
    [string]$RepoRoot = "D:\github\DataverseLabelTranslator",
    [string]$SolutionVersion = "",
    [string]$PackageVersion = "",
    [switch]$SkipPackageDeployerBuild
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.Drawing

$PackageDeployerNuGetVersion = "9.1.0.182"

function Resolve-LatestSolutionVersion {
    param([Parameter(Mandatory = $true)][string]$Root)

    $releaseRoot = Join-Path $Root "release"
    if (-not (Test-Path -LiteralPath $releaseRoot -PathType Container)) {
        return "1.0.0.0"
    }

    $candidates = @(
        Get-ChildItem -LiteralPath $releaseRoot -Directory |
            Where-Object { $_.Name -match '^\d+\.\d+\.\d+\.\d+$' } |
            Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName "dataverse\solutions\DataverseLabelTranslator_managed.zip") } |
            ForEach-Object {
                [pscustomobject]@{
                    Name = $_.Name
                    Version = [version]$_.Name
                }
            }
    )

    if ($candidates.Count -eq 0) {
        return "1.0.0.0"
    }

    return ($candidates | Sort-Object Version -Descending | Select-Object -First 1).Name
}

function Convert-ToPackageVersion {
    param([Parameter(Mandatory = $true)][string]$Version)

    $parts = $Version.Split('.')
    if ($parts.Count -lt 3) {
        throw "Solution version must have at least three numeric parts. Got: $Version"
    }

    return "$($parts[0]).$($parts[1]).$($parts[2])"
}

if ([string]::IsNullOrWhiteSpace($SolutionVersion)) {
    $SolutionVersion = Resolve-LatestSolutionVersion -Root $RepoRoot
    Write-Host "No SolutionVersion provided. Using latest managed release version: $SolutionVersion"
}

if ($SolutionVersion -notmatch '^\d+\.\d+\.\d+\.\d+$') {
    throw "SolutionVersion must use four numeric parts, e.g. 1.1.0.0. Got: $SolutionVersion"
}

if ([string]::IsNullOrWhiteSpace($PackageVersion)) {
    $PackageVersion = Convert-ToPackageVersion -Version $SolutionVersion
    Write-Host "No PackageVersion provided. Using marketplace package version: $PackageVersion"
}

$ManagedSolutionZip = Join-Path $RepoRoot "release\$SolutionVersion\dataverse\solutions\DataverseLabelTranslator_managed.zip"
$AppSourceRoot = Join-Path $RepoRoot "release\$SolutionVersion\appsource"
$SrcRoot = Join-Path $AppSourceRoot "src"
$PackageProjectDir = Join-Path $SrcRoot "DataverseLabelTranslatorPackage"
$PkgFolder = Join-Path $PackageProjectDir "PkgFolder"
$MarketplaceRoot = Join-Path $SrcRoot "DataverseLabelTranslator.v.$PackageVersion"
$ZipDir = Join-Path $AppSourceRoot "zip"
$AssetsDir = Join-Path $AppSourceRoot "assets"
$ImagesDir = Join-Path $AppSourceRoot "Images"
$VideosDir = Join-Path $AppSourceRoot "Videos"
$DocumentsDir = Join-Path $AppSourceRoot "Documents"
$TestDir = Join-Path $AppSourceRoot "Test"
$TestScreenshotsDir = Join-Path $TestDir "screenshots"
$NestedPackageZip = Join-Path $MarketplaceRoot "DataverseLabelTranslatorPackage.zip"
$FinalZip = Join-Path $ZipDir "DataverseLabelTranslator.v.$PackageVersion.zip"
$PackageDllName = "PL.DataverseLabelTranslator.PackageDeployment.dll"
$PackageDll = Join-Path $PackageProjectDir $PackageDllName

function Write-FileUtf8 {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Content
    )
    $dir = Split-Path -Parent $Path
    if ($dir -and -not (Test-Path -LiteralPath $dir)) {
        New-Item -ItemType Directory -Path $dir | Out-Null
    }
    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Content, $encoding)
}

function Write-FileUtf16 {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Content
    )
    $dir = Split-Path -Parent $Path
    if ($dir -and -not (Test-Path -LiteralPath $dir)) {
        New-Item -ItemType Directory -Path $dir | Out-Null
    }
    [System.IO.File]::WriteAllText($Path, $Content, [System.Text.Encoding]::Unicode)
}

function New-CleanDirectory {
    param([Parameter(Mandatory = $true)][string]$Path)
    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
    New-Item -ItemType Directory -Path $Path | Out-Null
}

function Assert-FileExists {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Message
    )
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw $Message
    }
}

function New-ZipFromDirectoryRoot {
    param(
        [Parameter(Mandatory = $true)][string]$SourceDir,
        [Parameter(Mandatory = $true)][string]$ZipPath
    )
    if (Test-Path -LiteralPath $ZipPath) {
        Remove-Item -LiteralPath $ZipPath -Force
    }
    $zipParent = Split-Path -Parent $ZipPath
    if (-not (Test-Path -LiteralPath $zipParent)) {
        New-Item -ItemType Directory -Path $zipParent | Out-Null
    }
    [System.IO.Compression.ZipFile]::CreateFromDirectory($SourceDir, $ZipPath, [System.IO.Compression.CompressionLevel]::Optimal, $false)
}

function Get-ZipEntries {
    param([Parameter(Mandatory = $true)][string]$ZipPath)
    $zip = [System.IO.Compression.ZipFile]::OpenRead($ZipPath)
    try {
        return @($zip.Entries | ForEach-Object { $_.FullName -replace '\\', '/' })
    }
    finally {
        $zip.Dispose()
    }
}

function Assert-ZipRootEntriesExactly {
    param(
        [Parameter(Mandatory = $true)][string]$ZipPath,
        [Parameter(Mandatory = $true)][string[]]$ExpectedEntries
    )
    $entries = Get-ZipEntries $ZipPath
    $rootEntries = @($entries | Where-Object { $_ -notmatch "[\\/]" } | Sort-Object)
    $expected = @($ExpectedEntries | Sort-Object)
    $actualText = $rootEntries -join "`n"
    $expectedText = $expected -join "`n"
    if ($actualText -ne $expectedText) {
        throw "ZIP root entries mismatch for $ZipPath.`nExpected:`n$expectedText`nActual:`n$actualText"
    }
}

function Assert-ZipContainsEntries {
    param(
        [Parameter(Mandatory = $true)][string]$ZipPath,
        [Parameter(Mandatory = $true)][string[]]$RequiredEntries
    )
    $entries = Get-ZipEntries $ZipPath
    foreach ($required in $RequiredEntries) {
        if ($entries -notcontains $required) {
            throw "Missing ZIP entry '$required' in $ZipPath"
        }
    }
}

function New-Png {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][int]$Width,
        [Parameter(Mandatory = $true)][int]$Height,
        [Parameter(Mandatory = $true)][scriptblock]$Draw
    )
    $dir = Split-Path -Parent $Path
    if ($dir -and -not (Test-Path -LiteralPath $dir)) {
        New-Item -ItemType Directory -Path $dir | Out-Null
    }
    $bmp = New-Object System.Drawing.Bitmap($Width, $Height)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    try {
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit
        $Draw.Invoke($g, $Width, $Height) | Out-Null
        $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $g.Dispose()
        $bmp.Dispose()
    }
}

function New-SolidGif {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$HexColor
    )
    $dir = Split-Path -Parent $Path
    if ($dir -and -not (Test-Path -LiteralPath $dir)) {
        New-Item -ItemType Directory -Path $dir | Out-Null
    }
    $bmp = New-Object System.Drawing.Bitmap(24, 24)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    try {
        $g.Clear([System.Drawing.ColorTranslator]::FromHtml($HexColor))
        $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Gif)
    }
    finally {
        $g.Dispose()
        $bmp.Dispose()
    }
}

function Draw-Logo {
    param($g, [int]$Width, [int]$Height)
    if ($g -is [object[]] -and $g.Count -ge 3) {
        $Height = [int]$g[2]
        $Width = [int]$g[1]
        $g = $g[0]
    }
    if ($Width -is [object[]] -and $Width.Count -ge 2) {
        $Height = [int]$Width[1]
        $Width = [int]$Width[0]
    }
    $rect = New-Object System.Drawing.Rectangle(0, 0, $Width, $Height)
    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, [System.Drawing.ColorTranslator]::FromHtml("#1C75BC"), [System.Drawing.ColorTranslator]::FromHtml("#173B73"), 45)
    $white = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(245, 255, 255, 255))
    $gold = New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml("#FFD24A"))
    $line = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(130, 255, 255, 255), [Math]::Max(1, [int]($Width / 42)))
    try {
        $g.FillRectangle($bg, $rect)
        $pad = [Math]::Max(3, [int]($Width * 0.13))
        $grid = New-Object System.Drawing.Rectangle($pad, [int]($Height * 0.18), [int]($Width - (2 * $pad)), [int]($Height * 0.52))
        $g.DrawRectangle($line, $grid)
        $midX = [int]($grid.X + ($grid.Width / 2))
        $g.DrawLine($line, $midX, $grid.Y, $midX, $grid.Bottom)
        $g.DrawLine($line, $grid.X, [int]($grid.Y + ($grid.Height * 0.32)), $grid.Right, [int]($grid.Y + ($grid.Height * 0.32)))
        $g.DrawLine($line, $grid.X, [int]($grid.Y + ($grid.Height * 0.62)), $grid.Right, [int]($grid.Y + ($grid.Height * 0.62)))
        $fontSize = [Math]::Max(7, [int]($Width * 0.13))
        $font = New-Object System.Drawing.Font("Segoe UI", $fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
        try {
            $fmt = New-Object System.Drawing.StringFormat
            $fmt.Alignment = [System.Drawing.StringAlignment]::Center
            $fmt.LineAlignment = [System.Drawing.StringAlignment]::Center
            $g.DrawString("EN", $font, $white, (New-Object System.Drawing.RectangleF($grid.X, $grid.Y, ($grid.Width / 2), ($grid.Height * 0.32))), $fmt)
            $g.DrawString("VI", $font, $gold, (New-Object System.Drawing.RectangleF($midX, $grid.Y, ($grid.Width / 2), ($grid.Height * 0.32))), $fmt)
        }
        finally {
            $font.Dispose()
        }
        $badgeSize = [int]($Width * 0.27)
        $badgeRect = New-Object System.Drawing.Rectangle([int]($Width - $badgeSize - [int]($Width * 0.08)), [int]($Height - $badgeSize - [int]($Height * 0.08)), $badgeSize, $badgeSize)
        $g.FillEllipse($gold, $badgeRect)
    }
    finally {
        $bg.Dispose()
        $white.Dispose()
        $gold.Dispose()
        $line.Dispose()
    }
}

function Draw-TextBox {
    param($g, [string]$Text, [int]$X, [int]$Y, [int]$W, [int]$H, [string]$Fill, [string]$Stroke, [int]$FontSize = 20, [bool]$Bold = $false)
    $brush = New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml($Fill))
    $pen = New-Object System.Drawing.Pen([System.Drawing.ColorTranslator]::FromHtml($Stroke), 2)
    $textBrush = New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml("#182033"))
    $style = if ($Bold) { [System.Drawing.FontStyle]::Bold } else { [System.Drawing.FontStyle]::Regular }
    $font = New-Object System.Drawing.Font("Segoe UI", $FontSize, $style, [System.Drawing.GraphicsUnit]::Pixel)
    $fmt = New-Object System.Drawing.StringFormat
    $fmt.Alignment = [System.Drawing.StringAlignment]::Center
    $fmt.LineAlignment = [System.Drawing.StringAlignment]::Center
    $fmt.FormatFlags = [System.Drawing.StringFormatFlags]::NoClip
    try {
        $rect = New-Object System.Drawing.Rectangle($X, $Y, $W, $H)
        $g.FillRectangle($brush, $rect)
        $g.DrawRectangle($pen, $rect)
        $g.DrawString($Text, $font, $textBrush, (New-Object System.Drawing.RectangleF([float]($X + 10), [float]($Y + 8), [float]($W - 20), [float]($H - 16))), $fmt)
    }
    finally {
        $brush.Dispose()
        $pen.Dispose()
        $textBrush.Dispose()
        $font.Dispose()
    }
}

function New-GeneratedImages {
    $logo32 = Join-Path $AssetsDir "logo32x32.png"
    $logoLarge = Join-Path $AssetsDir "logo-large.png"
    $hero = Join-Path $AssetsDir "homepage-hero.png"
    $packageFlow = Join-Path $AssetsDir "appsource-package-flow.png"
    $privacyFlow = Join-Path $AssetsDir "ai-privacy-flow.png"
    $wizardVisual = Join-Path $AssetsDir "install-wizard-visual.png"
    $videoThumb = Join-Path $VideosDir "video-01-thumbnail.png"

    New-Png -Path $logo32 -Width 32 -Height 32 -Draw { param($g, $w, $h) Draw-Logo $g $w $h }
    New-Png -Path $logoLarge -Width 216 -Height 216 -Draw { param($g, $w, $h) Draw-Logo $g $w $h }
    Copy-Item -LiteralPath $logo32 -Destination (Join-Path $MarketplaceRoot "logo32x32.png") -Force

    New-Png -Path $hero -Width 1600 -Height 900 -Draw {
        param($g, $w, $h)
        $rect = New-Object System.Drawing.Rectangle(0, 0, $w, $h)
        $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, [System.Drawing.ColorTranslator]::FromHtml("#F8FBFF"), [System.Drawing.ColorTranslator]::FromHtml("#E8F1FA"), 20)
        $g.FillRectangle($bg, $rect)
        $bg.Dispose()
        Draw-TextBox $g "Dataverse Label Translator" 80 76 560 96 "#FFFFFF" "#B9CBE0" 34 $true
        Draw-TextBox $g "AI-assisted labels" 112 260 300 96 "#EAF6FF" "#6AA9D8" 24 $true
        Draw-TextBox $g "Manual review before save" 470 260 360 96 "#FFF8E2" "#E8C14A" 23 $true
        Draw-TextBox $g "No publisher server" 890 260 330 96 "#ECF8EF" "#71B982" 23 $true
        Draw-TextBox $g "Dictionary in Dataverse" 1280 260 260 96 "#F4EDFF" "#9B7BCE" 22 $true
        Draw-TextBox $g "Translate metadata labels across attributes, forms, views, option sets, sitemaps, dashboards, web resources, and global option sets." 130 510 1340 128 "#FFFFFF" "#D6E2EF" 26 $false
    }
    $siteAssets = Join-Path $RepoRoot "site\assets"
    if (Test-Path -LiteralPath $siteAssets) {
        Copy-Item -LiteralPath $hero -Destination (Join-Path $siteAssets "homepage-hero.png") -Force
    }

    New-Png -Path $packageFlow -Width 1600 -Height 900 -Draw {
        param($g, $w, $h)
        $g.Clear([System.Drawing.ColorTranslator]::FromHtml("#FFFFFF"))
        Draw-TextBox $g "Existing managed solution" 80 140 300 100 "#EAF6FF" "#6AA9D8" 21 $true
        Draw-TextBox $g "Package Deployer package" 460 140 340 100 "#FFF8E2" "#E8C14A" 21 $true
        Draw-TextBox $g "Marketplace root" 880 140 290 100 "#ECF8EF" "#71B982" 21 $true
        Draw-TextBox $g "Final all-in-one ZIP" 1250 140 290 100 "#F4EDFF" "#9B7BCE" 21 $true
        Draw-TextBox $g "Managed solution ZIP" 80 430 300 92 "#F7FAFD" "#B9CBE0" 18 $false
        Draw-TextBox $g "DataverseLabelTranslatorPackage.zip" 460 430 340 92 "#F7FAFD" "#B9CBE0" 18 $false
        Draw-TextBox $g "input.xml + TermsOfUse.html + logo32x32.png" 880 430 290 110 "#F7FAFD" "#B9CBE0" 17 $false
        Draw-TextBox $g "Upload this ZIP to Azure Blob" 1250 430 290 92 "#F7FAFD" "#B9CBE0" 18 $true
    }

    New-Png -Path $privacyFlow -Width 1600 -Height 900 -Draw {
        param($g, $w, $h)
        $g.Clear([System.Drawing.ColorTranslator]::FromHtml("#FFFFFF"))
        Draw-TextBox $g "User browser" 130 140 300 100 "#EAF6FF" "#6AA9D8" 23 $true
        Draw-TextBox $g "Dataverse Web API" 650 140 300 100 "#ECF8EF" "#71B982" 23 $true
        Draw-TextBox $g "Configured AI provider" 1170 140 300 100 "#FFF8E2" "#E8C14A" 23 $true
        Draw-TextBox $g "AI settings may be stored in browser localStorage" 95 420 370 120 "#F7FAFD" "#B9CBE0" 19 $false
        Draw-TextBox $g "Dictionary data stays in customer's Dataverse environment" 615 420 370 120 "#F7FAFD" "#B9CBE0" 19 $false
        Draw-TextBox $g "Selected labels are sent directly to the user-configured endpoint" 1135 420 390 120 "#F7FAFD" "#B9CBE0" 19 $false
        Draw-TextBox $g "No customer labels, API keys, telemetry, or usage data are sent to a PhuocLe publisher server." 260 690 1080 110 "#FFF4F1" "#D88A72" 25 $true
    }

    New-Png -Path $wizardVisual -Width 1280 -Height 720 -Draw {
        param($g, $w, $h)
        $g.Clear([System.Drawing.ColorTranslator]::FromHtml("#F4F7FB"))
        Draw-TextBox $g "Package Deployer Welcome" 90 80 520 86 "#FFFFFF" "#B9CBE0" 28 $true
        Draw-TextBox $g "Dataverse Label Translator package [$SolutionVersion]" 90 210 800 86 "#EAF6FF" "#6AA9D8" 24 $true
        Draw-TextBox $g "Imports the managed solution and shows clean install guidance for Microsoft review." 90 335 800 130 "#FFFFFF" "#D6E2EF" 22 $false
        Draw-TextBox $g "System Administrator or System Customizer required" 90 520 800 80 "#FFF8E2" "#E8C14A" 20 $true
    }

    New-Png -Path $videoThumb -Width 1280 -Height 720 -Draw {
        param($g, $w, $h)
        $g.Clear([System.Drawing.ColorTranslator]::FromHtml("#173B73"))
        Draw-TextBox $g "Dataverse Label Translator" 160 110 960 110 "#FFFFFF" "#B9CBE0" 34 $true
        Draw-TextBox $g "AI-assisted metadata translation demo" 240 300 800 100 "#FFF8E2" "#E8C14A" 28 $true
        Draw-TextBox $g "Generated thumbnail - not a product screenshot" 320 500 640 80 "#EAF6FF" "#6AA9D8" 21 $false
    }
}

function New-WizardImageAssets {
    $gifColors = @{
        "body_back.gif" = "#F4F7FB"
        "content_back.gif" = "#FFFFFF"
        "content_back_orig.gif" = "#FFFFFF"
        "contentarea_back.gif" = "#F8FBFF"
        "contentArea_back_home.gif" = "#EAF6FF"
        "footer_back.gif" = "#173B73"
        "header_back.gif" = "#1C75BC"
        "nav_back.gif" = "#E8F1FA"
        "nav_list_back.gif" = "#D6E2EF"
        "top_item_selected_bg.gif" = "#FFD24A"
    }
    foreach ($root in @(
        (Join-Path $PkgFolder "Content\en-us\WelcomeHtml\Images"),
        (Join-Path $PkgFolder "Content\en-us\EndHtml\Images")
    )) {
        foreach ($name in $gifColors.Keys) {
            New-SolidGif -Path (Join-Path $root $name) -HexColor $gifColors[$name]
        }
    }
}

function Build-PackageDeployerDll {
    if ($SkipPackageDeployerBuild) {
        Assert-FileExists $PackageDll "Missing Package Deployer DLL: $PackageDll"
        return
    }

    $buildRoot = Join-Path $AppSourceRoot "package-project"
    $projectDir = Join-Path $buildRoot "PL.DataverseLabelTranslator.PackageDeployment"
    if (Test-Path -LiteralPath $projectDir) {
        Remove-Item -LiteralPath $projectDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $projectDir | Out-Null

    $csprojPath = Join-Path $projectDir "PL.DataverseLabelTranslator.PackageDeployment.csproj"
    $classPath = Join-Path $projectDir "PackageImportExtension.cs"

    $csproj = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net462</TargetFramework>
    <OutputType>Library</OutputType>
    <AssemblyName>PL.DataverseLabelTranslator.PackageDeployment</AssemblyName>
    <RootNamespace>PL.DataverseLabelTranslator.PackageDeployment</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.CrmSdk.XrmTooling.PackageDeployment.Wpf" Version="$PackageDeployerNuGetVersion" PrivateAssets="all" />
    <Reference Include="Microsoft.Xrm.Tooling.PackageDeployment.CrmPackageExtentionBase">
      <HintPath>`$(NuGetPackageRoot)\microsoft.crmsdk.xrmtooling.packagedeployment.wpf\$PackageDeployerNuGetVersion\tools\Microsoft.Xrm.Tooling.PackageDeployment.CrmPackageExtentionBase.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="Microsoft.Xrm.Tooling.Connector">
      <HintPath>`$(NuGetPackageRoot)\microsoft.crmsdk.xrmtooling.packagedeployment.wpf\$PackageDeployerNuGetVersion\tools\Microsoft.Xrm.Tooling.Connector.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="Microsoft.Xrm.Sdk">
      <HintPath>`$(NuGetPackageRoot)\microsoft.crmsdk.xrmtooling.packagedeployment.wpf\$PackageDeployerNuGetVersion\tools\Microsoft.Xrm.Sdk.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="WindowsBase" />
  </ItemGroup>
</Project>
"@

    $class = @'
using Microsoft.Xrm.Tooling.PackageDeployment.CrmPackageExtentionBase;

namespace PL.DataverseLabelTranslator.PackageDeployment
{
    public class PackageImportExtension : ImportExtension
    {
        public override bool BeforeImportStage()
        {
            return true;
        }

        public override bool AfterPrimaryImport()
        {
            return true;
        }

        public override void InitializeCustomExtension()
        {
        }

        public override string GetNameOfImport(bool plural)
        {
            return plural ? "Dataverse Label Translator packages" : "Dataverse Label Translator package";
        }

        public override string GetLongNameOfImport
        {
            get { return "Dataverse Label Translator"; }
        }

        public override string GetImportPackageDataFolderName
        {
            get { return "PkgFolder"; }
        }

        public override string GetImportPackageDescriptionText
        {
            get { return "Installs the Dataverse Label Translator managed solution."; }
        }
    }
}
'@

    Write-FileUtf8 -Path $csprojPath -Content $csproj
    Write-FileUtf8 -Path $classPath -Content $class

    & dotnet restore $csprojPath | Write-Host
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet restore failed for Package Deployer project."
    }

    & dotnet build $csprojPath -c Release --no-restore | Write-Host
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed for Package Deployer project."
    }

    $builtDll = Join-Path $projectDir "bin\Release\net462\$PackageDllName"
    Assert-FileExists $builtDll "Package Deployer DLL was not produced: $builtDll"
    Copy-Item -LiteralPath $builtDll -Destination $PackageDll -Force
}

function Ensure-VersionedReviewFile {
    param(
        [Parameter(Mandatory = $true)][string]$Directory,
        [Parameter(Mandatory = $true)][string]$Pattern,
        [Parameter(Mandatory = $true)][string]$TargetName
    )

    $target = Join-Path $Directory $TargetName
    if (Test-Path -LiteralPath $target -PathType Leaf) {
        return
    }

    $candidates = @(Get-ChildItem -LiteralPath $Directory -File -Filter $Pattern -ErrorAction SilentlyContinue)
    if ($candidates.Count -eq 1) {
        Move-Item -LiteralPath $candidates[0].FullName -Destination $target -Force
        Write-Host "Renamed review file to selected version: $target"
    }
    elseif ($candidates.Count -gt 1) {
        Write-Host "Multiple review files match '$Pattern' in $Directory. Leaving them unchanged."
    }
}

function Sync-ReviewDocuments {
    Ensure-VersionedReviewFile -Directory $DocumentsDir -Pattern "UserGuide.*.docx" -TargetName "UserGuide.$SolutionVersion.docx"
    Ensure-VersionedReviewFile -Directory $DocumentsDir -Pattern "UserGuide.*.pdf" -TargetName "UserGuide.$SolutionVersion.pdf"
    Ensure-VersionedReviewFile -Directory $TestDir -Pattern "E2E User Scenario.*.docx" -TargetName "E2E User Scenario.$SolutionVersion.docx"
    Ensure-VersionedReviewFile -Directory $TestDir -Pattern "E2E User Scenario.*.pdf" -TargetName "E2E User Scenario.$SolutionVersion.pdf"

    $readme = @"
# Screenshot Tasks

These are real screenshots for anh Phuoc to capture from a clean Dataverse environment. Do not replace them with generated images.

- screenshot-01-main-grid.png
- screenshot-02-ai-settings.png
- screenshot-03-auto-translate-review.png
- screenshot-04-dictionary.png
- screenshot-05-save-result.png

Preferred size: 1280 x 720 PNG.
Hide API keys, SAS URLs, tenant IDs, internal URLs, and user emails.
"@
    Write-FileUtf8 -Path (Join-Path $ImagesDir "README.md") -Content $readme
    Write-FileUtf8 -Path (Join-Path $TestScreenshotsDir "README.md") -Content $readme
}

function Write-AppSourceFiles {
    $contentTypes = @'
<?xml version="1.0" encoding="utf-8"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="xml" ContentType="application/octet-stream" />
  <Default Extension="xaml" ContentType="application/octet-stream" />
  <Default Extension="dll" ContentType="application/octet-stream" />
  <Default Extension="zip" ContentType="application/octet-stream" />
  <Default Extension="jpb" ContentType="application/octet-stream" />
  <Default Extension="gif" ContentType="application/octet-stream" />
  <Default Extension="png" ContentType="application/octet-stream" />
  <Default Extension="htm" ContentType="application/octet-stream" />
  <Default Extension="html" ContentType="application/octet-stream" />
  <Default Extension="txt" ContentType="application/octet-stream" />
  <Default Extension="db" ContentType="application/octet-stream" />
  <Default Extension="css" ContentType="application/octet-stream" />
</Types>
'@

    $importConfig = @'
<?xml version="1.0" encoding="utf-16"?>
<configdatastorage xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema"
                   installsampledata="false"
                   waitforsampledatatoinstall="true"
                   agentdesktopzipfile=""
                   agentdesktopexename=""
                   crmmigdataimportfile="">
  <solutions>
    <configsolutionfile solutionpackagefilename="DataverseLabelTranslator_managed.zip" />
  </solutions>
</configdatastorage>
'@

    $commonCss = @'
body {
  margin: 0;
  color: #182033;
  background: #f4f7fb;
  font-family: "Segoe UI", Arial, sans-serif;
}
.page {
  box-sizing: border-box;
  min-height: 100vh;
  padding: 34px 42px;
}
.panel {
  max-width: 820px;
  padding: 28px 32px;
  background: #fff;
  border: 1px solid #d6e2ef;
}
h1 {
  margin: 0 0 16px;
  color: #173b73;
  font-size: 28px;
  font-weight: 600;
}
p, li {
  font-size: 15px;
  line-height: 1.55;
}
.notice {
  margin-top: 22px;
  padding: 14px 16px;
  border-left: 4px solid #1c75bc;
  background: #eaf6ff;
}
'@

    $welcomeHtml = @"
<!doctype html>
<html>
<head>
  <meta charset="utf-8">
  <title>Dataverse Label Translator package [$SolutionVersion]</title>
  <link rel="stylesheet" href="../CSS/common.css">
</head>
<body>
  <main class="page">
    <section class="panel">
      <h1>Dataverse Label Translator package [$SolutionVersion]</h1>
      <p>This free model-driven Dataverse admin utility helps translate labels and metadata across attributes, option sets, forms, views, sitemaps, dashboards, web resources, and global option sets.</p>
      <ul>
        <li>Requires System Administrator, System Customizer, or equivalent customization privileges.</li>
        <li>AI-assisted translation is included. Provider endpoint, model, and key are supplied by the customer or temporary certification notes.</li>
        <li>Auto Translate sends selected label and metadata text directly to the configured AI provider.</li>
      </ul>
      <div class="notice">Review generated AI translations before saving metadata changes.</div>
    </section>
  </main>
</body>
</html>
"@

    $endHtml = @"
<!doctype html>
<html>
<head>
  <meta charset="utf-8">
  <title>Dataverse Label Translator package [$SolutionVersion]</title>
  <link rel="stylesheet" href="../CSS/common.css">
</head>
<body>
  <main class="page">
    <section class="panel">
      <h1>Dataverse Label Translator is installed</h1>
      <p>Open the Dataverse Label Translator model-driven app in Power Apps or Dynamics 365 to begin translating metadata labels.</p>
      <ul>
        <li>Confirm installed languages are enabled in the target environment.</li>
        <li>Configure AI Settings before running Auto Translate.</li>
        <li>Use Manage Dictionary for approved terminology and consistency.</li>
      </ul>
      <div class="notice">No data is sent to a PhuocLe publisher server by this package.</div>
    </section>
  </main>
</body>
</html>
"@

    $termsHtml = @'
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <title>Dataverse Label Translator Terms of Use</title>
</head>
<body>
  <h1>Dataverse Label Translator Terms of Use</h1>
  <p>Dataverse Label Translator is provided by PhuocLe as a free Dataverse and Dynamics 365 admin utility. There is no paid license, trial expiration, or publisher-managed license requirement for this AppSource release.</p>
  <h2>Free Use</h2>
  <p>You may install, deploy, copy, and use the app in any number of your own Dataverse or Dynamics 365 environments.</p>
  <h2>No Publisher Backend</h2>
  <p>The app does not send customer data, labels, metadata, API keys, telemetry, or usage data to a PhuocLe or Dataverse Label Translator publisher server. The app has no publisher-controlled backend server for data collection.</p>
  <h2>AI-Assisted Translation</h2>
  <p>When you run Auto Translate, selected labels and metadata text may be sent directly from your browser or client to the AI provider endpoint configured by you. AI provider endpoint, model, key, cost, quota, retention, privacy, security, and compliance are your responsibility. Use AI output at your own risk and review translations before saving.</p>
  <h2>Local Storage</h2>
  <p>AI settings may be stored in browser localStorage for convenience. Treat values stored in localStorage as sensitive according to your organization policy.</p>
  <h2>Dictionary Data</h2>
  <p>Translation dictionary data is stored in your Dataverse environment. You are responsible for cleanup and governance of customer-owned data.</p>
  <h2>No Warranty</h2>
  <p>The app is provided as-is, without warranty. Back up your solutions and metadata before making translation changes.</p>
</body>
</html>
'@

    $licenseMd = @'
# Dataverse Label Translator License

Publisher: PhuocLe

Dataverse Label Translator is free. No paid license is required. There is no trial expiration and no ISV app license management requirement for this AppSource release.

You may install, deploy, copy, and use the app in any number of your own Dataverse or Dynamics 365 environments.

The app is provided as-is, without warranty. Use it at your own risk. You are responsible for backing up solutions, metadata, and environments before saving translation changes.

You are responsible for compliance with Microsoft, Dataverse, Dynamics 365, Power Platform, your organization, and any AI provider terms that apply to your use.
'@

    $termMd = @'
# Dataverse Label Translator Terms

Dataverse Label Translator is a free Dataverse and Dynamics 365 admin utility by PhuocLe.

The app does not send customer data, labels, metadata, API keys, telemetry, or usage data to a PhuocLe or Dataverse Label Translator publisher server. There is no publisher-controlled backend server for data collection.

When Auto Translate is used, selected label and metadata text is sent directly from the user's browser or client to the AI provider endpoint configured by the user. AI settings may be stored in browser localStorage for convenience.

Users are responsible for AI provider selection, endpoint security, API keys, model behavior, output quality, cost, quota, retention, privacy, security, and compliance. Use AI output at your own risk and review translations before saving.

Translation dictionary data is stored in the customer's Dataverse environment. Customers are responsible for cleanup and governance of customer-owned data.
'@

    $inputXml = @"
<PvsPackageData>
  <ProviderName>PhuocLe</ProviderName>
  <PackageFile>DataverseLabelTranslatorPackage.zip</PackageFile>
  <SolutionAnchorName>DataverseLabelTranslator_managed.zip</SolutionAnchorName>
  <StartDate>01/01/2026</StartDate>
  <EndDate>12/31/2035</EndDate>
  <SupportedCountries>AE,AL,AM,AO,AR,AT,AU,AZ,BA,BB,BD,BE,BG,BH,BM,BN,BO,BR,BY,CA,CH,CI,CL,CM,CO,CR,CV,CW,CY,CZ,DE,DK,DO,DZ,EC,EE,EG,ES,FI,FR,GB,GE,GH,GR,GT,HK,HN,HR,HU,ID,IE,IL,IN,IQ,IS,IT,JM,JO,JP,KE,KG,KN,KR,KW,KY,KZ,LB,LK,LT,LU,LV,LY,MA,MC,MD,ME,MK,MN,MO,MT,MU,MX,MY,NG,NI,NL,NO,NZ,OM,PA,PE,PH,PK,PL,PR,PS,PT,PY,QA,RO,RS,RU,RW,SA,SE,SG,SI,SK,SN,SV,TH,TM,TN,TR,TT,TW,UA,US,UY,UZ,VE,VI,VN,ZA,ZW</SupportedCountries>
  <LearnMoreLink>https://phuocle.github.io/DataverseLabelTranslator/</LearnMoreLink>
  <Locales>
    <PackageLocale Code="1033" IsDefault="true">
       <Logo>logo32x32.png</Logo>
       <Terms>
         <PackageTerm File="TermsOfUse.html" />
       </Terms>
    </PackageLocale>
  </Locales>
</PvsPackageData>
"@

    Write-FileUtf8 -Path (Join-Path $PackageProjectDir "[Content_Types].xml") -Content $contentTypes
    Write-FileUtf8 -Path (Join-Path $MarketplaceRoot "[Content_Types].xml") -Content $contentTypes
    Write-FileUtf16 -Path (Join-Path $PkgFolder "ImportConfig.xml") -Content $importConfig
    Write-FileUtf8 -Path (Join-Path $PkgFolder "Content\en-us\WelcomeHtml\HTML\Default.htm") -Content $welcomeHtml
    Write-FileUtf8 -Path (Join-Path $PkgFolder "Content\en-us\WelcomeHtml\CSS\common.css") -Content $commonCss
    Write-FileUtf8 -Path (Join-Path $PkgFolder "Content\en-us\EndHtml\HTML\Default.htm") -Content $endHtml
    Write-FileUtf8 -Path (Join-Path $PkgFolder "Content\en-us\EndHtml\CSS\common.css") -Content $commonCss
    Write-FileUtf8 -Path (Join-Path $MarketplaceRoot "TermsOfUse.html") -Content $termsHtml
    Write-FileUtf8 -Path (Join-Path $MarketplaceRoot "input.xml") -Content $inputXml
    Write-FileUtf8 -Path (Join-Path $AssetsDir "license.md") -Content $licenseMd
    Write-FileUtf8 -Path (Join-Path $AssetsDir "term.md") -Content $termMd
    Write-FileUtf8 -Path (Join-Path $AssetsDir "terms.html") -Content $termsHtml
}

$repoTop = (& git -C $RepoRoot rev-parse --show-toplevel).Trim()
if ($LASTEXITCODE -ne 0 -or $repoTop -replace '/', '\' -ne $RepoRoot.TrimEnd('\')) {
    throw "Repo root mismatch. Expected $RepoRoot, got $repoTop"
}

Assert-FileExists $ManagedSolutionZip "Missing managed solution source: $ManagedSolutionZip. Release AppSource cannot continue without this user-controlled file."

New-CleanDirectory $SrcRoot
New-CleanDirectory $ZipDir
foreach ($dir in @($PackageProjectDir, $PkgFolder, $MarketplaceRoot, $AssetsDir, $ImagesDir, $VideosDir, $DocumentsDir, $TestDir, $TestScreenshotsDir)) {
    if (-not (Test-Path -LiteralPath $dir)) {
        New-Item -ItemType Directory -Path $dir | Out-Null
    }
}

Write-AppSourceFiles
Copy-Item -LiteralPath $ManagedSolutionZip -Destination (Join-Path $PkgFolder "DataverseLabelTranslator_managed.zip") -Force
New-WizardImageAssets
New-GeneratedImages
Sync-ReviewDocuments
Build-PackageDeployerDll

Assert-FileExists $PackageDll "Missing Package Deployer DLL: $PackageDll"

New-ZipFromDirectoryRoot -SourceDir $PackageProjectDir -ZipPath $NestedPackageZip

$nestedRequired = @(
    "[Content_Types].xml",
    "PL.DataverseLabelTranslator.PackageDeployment.dll",
    "PkgFolder/ImportConfig.xml",
    "PkgFolder/DataverseLabelTranslator_managed.zip",
    "PkgFolder/Content/en-us/WelcomeHtml/HTML/Default.htm",
    "PkgFolder/Content/en-us/WelcomeHtml/CSS/common.css",
    "PkgFolder/Content/en-us/EndHtml/HTML/Default.htm",
    "PkgFolder/Content/en-us/EndHtml/CSS/common.css"
)
Assert-ZipContainsEntries -ZipPath $NestedPackageZip -RequiredEntries $nestedRequired

New-ZipFromDirectoryRoot -SourceDir $MarketplaceRoot -ZipPath $FinalZip

$finalRoot = @(
    "DataverseLabelTranslatorPackage.zip",
    "[Content_Types].xml",
    "input.xml",
    "TermsOfUse.html",
    "logo32x32.png"
)
Assert-ZipRootEntriesExactly -ZipPath $FinalZip -ExpectedEntries $finalRoot

$finalInfo = Get-Item -LiteralPath $FinalZip
$nestedInfo = Get-Item -LiteralPath $NestedPackageZip

Write-Host ""
Write-Host "Release AppSource completed."
Write-Host "Solution version: $SolutionVersion"
Write-Host "Package version: $PackageVersion"
Write-Host "Managed solution source: $ManagedSolutionZip"
Write-Host "Nested Package Deployer ZIP: $NestedPackageZip ($($nestedInfo.Length) bytes)"
Write-Host "Final upload ZIP: $FinalZip ($($finalInfo.Length) bytes)"
Write-Host ""
Write-Host "Upload this final ZIP to Azure Blob:"
Write-Host $FinalZip
