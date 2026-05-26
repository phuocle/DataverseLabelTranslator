<#
.SYNOPSIS
    Removes non-base-language entries from an unpacked Dataverse solution and stamps a fixed release version.

.DESCRIPTION
    Processes all XML files in an unpacked solution folder and:
    1. Removes elements with languagecode attribute != BaseLanguageCode.
    2. Removes elements with LCID attribute != BaseLanguageCode.
    3. Keeps only BaseLanguageCode in <Languages>.
    4. Replaces the About dialog placeholder with the provided Version.

    The version is intentionally passed in by the export workflow. Do not read it from Solution.xml.
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Path,

    [Parameter(Mandatory = $true)]
    [ValidateRange(1, 999999)]
    [int]$BaseLanguageCode,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+\.\d+$')]
    [string]$Version
)

$ErrorActionPreference = "Stop"
$baseLanguageCodeText = [string]$BaseLanguageCode

if (-not (Test-Path -LiteralPath $Path)) {
    throw "Path not found: $Path"
}

$xmlFiles = Get-ChildItem -LiteralPath $Path -Recurse -Filter "*.xml"
$totalCleaned = 0

foreach ($file in $xmlFiles) {
    $modified = $false
    [xml]$xml = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8

    $langNodes = $xml.SelectNodes("//*[@languagecode and @languagecode!='$baseLanguageCodeText']")
    if ($langNodes -and $langNodes.Count -gt 0) {
        foreach ($node in $langNodes) {
            $node.ParentNode.RemoveChild($node) | Out-Null
        }
        $modified = $true
    }

    $lcidNodes = $xml.SelectNodes("//*[@LCID and @LCID!='$baseLanguageCodeText']")
    if ($lcidNodes -and $lcidNodes.Count -gt 0) {
        foreach ($node in $lcidNodes) {
            $node.ParentNode.RemoveChild($node) | Out-Null
        }
        $modified = $true
    }

    $languageNodes = $xml.SelectNodes("//Languages/Language")
    if ($languageNodes -and $languageNodes.Count -gt 0) {
        foreach ($node in $languageNodes) {
            if ($node.InnerText.Trim() -ne $baseLanguageCodeText) {
                $node.ParentNode.RemoveChild($node) | Out-Null
                $modified = $true
            }
        }
    }

    if ($modified) {
        $settings = New-Object System.Xml.XmlWriterSettings
        $settings.Indent = $true
        $settings.Encoding = New-Object System.Text.UTF8Encoding($false)
        $settings.OmitXmlDeclaration = $false

        $writer = [System.Xml.XmlWriter]::Create($file.FullName, $settings)
        $xml.Save($writer)
        $writer.Close()

        $totalCleaned++
        Write-Host "  Cleaned: $($file.FullName)"
    }
}

Write-Host ""
Write-Host "Done. Cleaned $totalCleaned file(s). Only base language ($baseLanguageCodeText) remains."

$versionPlaceholder = "Version: x.xx.xx.xx"
$webResourcesPath = Join-Path $Path "WebResources"
$versionFiles = Get-ChildItem -LiteralPath $webResourcesPath -Recurse -Filter "*.js" |
    Where-Object {
        $content = Get-Content -LiteralPath $_.FullName -Raw -Encoding UTF8
        $content.Contains($versionPlaceholder)
    }

if (-not $versionFiles -or $versionFiles.Count -eq 0) {
    throw "About dialog version placeholder '$versionPlaceholder' not found under: $webResourcesPath"
}

foreach ($file in $versionFiles) {
    $content = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
    $content = $content.Replace($versionPlaceholder, "Version: $Version")
    [System.IO.File]::WriteAllText($file.FullName, $content, (New-Object System.Text.UTF8Encoding($false)))
    Write-Host "  Stamped version $Version in: $($file.FullName)"
}
