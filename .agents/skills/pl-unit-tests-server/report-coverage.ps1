<#
.SYNOPSIS
    Converts the latest .coverage binary to XML and reports line/block coverage
    for the three Dataverse Label Translator handler classes.
.DESCRIPTION
    Finds the most recent .coverage file in DataverseLabelTranslator.Test\TestResults,
    converts it to XML via dotnet-coverage merge, then reports coverage percentages
    for Dashboard, WebResource, and GlobalOptionSet classes from Server.dll.
.PARAMETER CoverageDir
    Path to the TestResults directory. Defaults to "DataverseLabelTranslator.Test\TestResults".
.PARAMETER OutputXml
    Path for the merged XML output. Defaults to "$CoverageDir\coverage.xml".
#>
param(
    [string]$CoverageDir = "DataverseLabelTranslator.Test\TestResults",
    [string]$OutputXml = ""
)

$ErrorActionPreference = "Stop"

# Resolve repo root: walk up from script dir until we find AGENTS.md
$repoRoot = $PSScriptRoot
while ($repoRoot -and -not (Test-Path (Join-Path $repoRoot "AGENTS.md"))) {
    $repoRoot = Split-Path -Parent $repoRoot
}
if (-not $repoRoot) {
    Write-Error "Cannot find repo root (no AGENTS.md found in ancestors of $PSScriptRoot)"
    exit 1
}
Push-Location $repoRoot
if (-not $OutputXml) {
    $OutputXml = Join-Path $CoverageDir "coverage.xml"
} else {
    $OutputXml = Join-Path $repoRoot $OutputXml
}

# Find latest .coverage file
$latest = Get-ChildItem $CoverageDir -Recurse -Filter *.coverage -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $latest) {
    Write-Error "No .coverage file found in $CoverageDir. Run tests with --collect:'Code Coverage' first."
    exit 1
}

Write-Host "Converting: $($latest.FullName)"
$mergeOutput = dotnet-coverage merge $latest.FullName --output $OutputXml --output-format xml 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet-coverage merge failed: $mergeOutput"
    exit $LASTEXITCODE
}
Write-Host "Merged to: $OutputXml"

# Parse and report
[xml]$cov = Get-Content $OutputXml

$targetClasses = @('Dashboard', 'WebResource', 'GlobalOptionSet')

$results = $cov.results.modules.module |
    Where-Object { $_.name -like "*Server.dll" } |
    ForEach-Object {
        $_.functions.function | Group-Object type_name | ForEach-Object {
            $cls = $_.Name
            $totalLines = 0; $coveredLines = 0
            $totalBlocks = 0; $coveredBlocks = 0
            $_.Group | ForEach-Object {
                $totalLines += [int]$_.lines_covered + [int]$_.lines_partially_covered + [int]$_.lines_not_covered
                $coveredLines += [int]$_.lines_covered
                $totalBlocks += [int]$_.blocks_covered + [int]$_.blocks_not_covered
                $coveredBlocks += [int]$_.blocks_covered
            }
            $linePct = if ($totalLines -gt 0) { [math]::Round(($coveredLines / $totalLines) * 100, 1) } else { -1 }
            $blockPct = if ($totalBlocks -gt 0) { [math]::Round(($coveredBlocks / $totalBlocks) * 100, 1) } else { -1 }
            [PSCustomObject]@{
                Class   = $cls
                LinePct = $linePct
                BlockPct = $blockPct
                Lines   = "$coveredLines/$totalLines"
                Blocks  = "$coveredBlocks/$totalBlocks"
            }
        }
    }

$filtered = $results | Where-Object { $_.Class -in $targetClasses } |
    Sort-Object { $targetClasses.IndexOf($_.Class) }

if (-not $filtered) {
    Write-Warning "No matching handler classes found in coverage data."
    exit 0
}

$filtered | Format-Table Class,
    @{N='Line%'; E={$_.LinePct}; Width=8},
    @{N='Block%'; E={$_.BlockPct}; Width=8},
    @{N='Lines'; E={$_.Lines}; Width=12},
    @{N='Blocks'; E={$_.Blocks}; Width=12} -AutoSize | Out-String -Width 200 | Write-Host

# Check thresholds
$below = $filtered | Where-Object { $_.LinePct -lt 100 }
if ($below) {
    Write-Host "WARNING: Classes below 100% line coverage:" -ForegroundColor Yellow
    $below | ForEach-Object { Write-Host "  $($_.Class): $($_.LinePct)%" -ForegroundColor Yellow }
    Pop-Location
    exit 1
} else {
    Write-Host "All handler classes at 100% coverage." -ForegroundColor Green
    Pop-Location
    exit 0
}