<#
.SYNOPSIS
    Runs all Dataverse Label Translator test suites in sequence.

.DESCRIPTION
    1. JavaScript unit tests: npm run test:coverage.
    2. C# server unit tests: dotnet test with coverlet JSON coverage.
    3. UI automation tests: dotnet test with UITEST_HEADLESS=true for this run only.

    The script is intentionally non-deploying and non-mutating except for normal
    test/build/coverage outputs produced by the underlying tools.

.NOTES
    Run from the repository root:
        .\DataverseLabelTranslator.Scripts\test-all-in-one.ps1
#>

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent $PSScriptRoot
$results = [ordered]@{}

function Write-Banner([string]$Title) {
    $line = '-' * 70
    Write-Host ''
    Write-Host $line -ForegroundColor Cyan
    Write-Host "  $Title" -ForegroundColor Cyan
    Write-Host $line -ForegroundColor Cyan
    Write-Host ''
}

function Write-Result([string]$Suite, [bool]$Passed) {
    if ($Passed) {
        Write-Host "  PASS  $Suite" -ForegroundColor Green
    } else {
        Write-Host "  FAIL  $Suite" -ForegroundColor Red
    }
}

function Format-Elapsed([TimeSpan]$Elapsed) {
    return $Elapsed.ToString('hh\:mm\:ss')
}

function Stop-UiTestProcesses {
    $patterns = @(
        'DataverseLabelTranslator.UiTest',
        'chromedriver',
        'UITEST_HEADLESS'
    )

    $processes = Get-CimInstance Win32_Process |
        Where-Object {
            $commandLine = $_.CommandLine
            ($_.Name -match '^(testhost|vstest\.console|chromedriver|chrome)\.exe$') -and
            @($patterns | Where-Object { $commandLine -match [regex]::Escape($_) }).Count -gt 0
        }

    foreach ($process in $processes) {
        try {
            Stop-Process -Id $process.ProcessId -Force -ErrorAction Stop
            Write-Host ("  [UI cleanup] Stopped leftover process {0} ({1})." -f $process.Name, $process.ProcessId) -ForegroundColor DarkYellow
        } catch {
            Write-Host ("  [UI cleanup] Could not stop process {0} ({1}): {2}" -f $process.Name, $process.ProcessId, $_.Exception.Message) -ForegroundColor DarkYellow
        }
    }
}

function Invoke-LoggedCommand {
    param(
        [Parameter(Mandatory = $true)]
        [scriptblock]$Command
    )

    & $Command 2>&1 | ForEach-Object { Write-Host $_ }
    return $LASTEXITCODE
}

function Invoke-UiTestsWithHeartbeat {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectPath,

        [Parameter(Mandatory = $true)]
        [string]$RunSettingsPath
    )

    Write-Host '  UI tests can take several minutes because they open Dataverse, load grids, edit cells, save, reload, and verify persisted values.' -ForegroundColor Yellow
    Write-Host '  Progress heartbeat prints every 60 seconds while MSTest is quiet.' -ForegroundColor Yellow
    Write-Host '  Expected UI coverage: Login, GlobalOptionSet, WebResource, View.' -ForegroundColor Yellow
    Write-Host ''

    $startedAt = Get-Date
    $lastHeartbeatAt = $startedAt
    $job = Start-Job -ScriptBlock {
        param($ProjectPath, $RunSettingsPath)

        $env:UITEST_HEADLESS = 'true'

        & dotnet test $ProjectPath `
            --configuration Debug `
            --no-restore `
            --settings $RunSettingsPath `
            --logger "console;verbosity=normal" 2>&1 | ForEach-Object { $_ }

        [pscustomobject]@{
            __TestAllInOneExitCode = $LASTEXITCODE
        }
    } -ArgumentList $ProjectPath, $RunSettingsPath

    try {
        while ($job.State -eq 'Running') {
            Receive-Job -Job $job | ForEach-Object {
                if ($_.PSObject.Properties.Name -contains '__TestAllInOneExitCode') {
                    return
                }

                Write-Host $_
            }

            $now = Get-Date
            if (($now - $lastHeartbeatAt).TotalSeconds -ge 60) {
                $elapsed = $now - $startedAt
                Write-Host ("  [UI heartbeat] Still running... elapsed {0}" -f (Format-Elapsed $elapsed)) -ForegroundColor DarkYellow
                $lastHeartbeatAt = $now
            }

            Start-Sleep -Seconds 5
        }

        $exitCode = 1
        Receive-Job -Job $job | ForEach-Object {
            if ($_.PSObject.Properties.Name -contains '__TestAllInOneExitCode') {
                $exitCode = [int]$_.__TestAllInOneExitCode
            } else {
                Write-Host $_
            }
        }

        $elapsedTotal = (Get-Date) - $startedAt
        Write-Host ("  [UI heartbeat] Finished after {0}" -f (Format-Elapsed $elapsedTotal)) -ForegroundColor DarkYellow
        return $exitCode
    } finally {
        Remove-Job -Job $job -Force -ErrorAction SilentlyContinue
    }
}

Write-Banner '1/3 JavaScript Unit Tests (Vitest + coverage)'

$jsExitCode = Invoke-LoggedCommand {
    Push-Location (Join-Path $RepoRoot 'DataverseLabelTranslator.WebResource')
    try {
        & npm run test:coverage
    } finally {
        Pop-Location
    }
}
$results['JavaScript'] = ($jsExitCode -eq 0)

Write-Banner '2/3 C# Server Unit Tests (MSTest + Coverlet)'

$serverTestProj = Join-Path $RepoRoot 'DataverseLabelTranslator.Server.Test\DataverseLabelTranslator.Server.Test.csproj'
$serverExitCode = Invoke-LoggedCommand {
    & dotnet test $serverTestProj `
        --configuration Debug `
        --no-restore `
        /p:CollectCoverage=true `
        /p:CoverletOutputFormat=json `
        /p:Include="[DataverseLabelTranslator.Server]*"
}
$results['C# Server'] = ($serverExitCode -eq 0)

Write-Banner '3/3 UI Automation Tests (MSTest + Selenium, headless)'

Write-Host '  UITEST_HEADLESS=true is set for this run only.' -ForegroundColor Yellow
Write-Host '  TestSettings.cs uses this environment variable to enable headless mode.' -ForegroundColor Yellow
Write-Host ''

$uiTestProj = Join-Path $RepoRoot 'DataverseLabelTranslator.UiTest\DataverseLabelTranslator.UiTest.csproj'
$uiRunSettings = Join-Path $RepoRoot 'DataverseLabelTranslator.UiTest\DataverseLabelTranslator.UiTest.runsettings'
$previousHeadless = [Environment]::GetEnvironmentVariable('UITEST_HEADLESS', 'Process')
[Environment]::SetEnvironmentVariable('UITEST_HEADLESS', 'true', 'Process')

try {
    $uiExitCode = Invoke-UiTestsWithHeartbeat -ProjectPath $uiTestProj -RunSettingsPath $uiRunSettings
    $results['UI Tests'] = ($uiExitCode -eq 0)
} finally {
    [Environment]::SetEnvironmentVariable('UITEST_HEADLESS', $previousHeadless, 'Process')
    Stop-UiTestProcesses
}

Write-Banner 'Test Summary'

foreach ($suite in $results.Keys) {
    Write-Result $suite ([bool]$results[$suite])
}

$failed = @($results.Values | Where-Object { -not $_ }).Count
if ($failed -gt 0) {
    Write-Host ''
    Write-Host "  $failed suite(s) failed." -ForegroundColor Red
    exit 1
}

Write-Host ''
Write-Host '  All suites passed.' -ForegroundColor Green
exit 0
