param(
    [Parameter(Mandatory = $true)]
    [string]$EnvFile
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $EnvFile)) {
    exit 0
}

foreach ($rawLine in Get-Content -LiteralPath $EnvFile) {
    $line = $rawLine.Trim()
    if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith("#")) {
        continue
    }

    $separatorIndex = $line.IndexOf("=")
    if ($separatorIndex -lt 1) {
        continue
    }

    $key = $line.Substring(0, $separatorIndex).Trim()
    if ($key -notmatch "^[A-Za-z_][A-Za-z0-9_]*$") {
        continue
    }

    $value = $line.Substring($separatorIndex + 1).Trim()
    if (
        ($value.StartsWith('"') -and $value.EndsWith('"')) -or
        ($value.StartsWith("'") -and $value.EndsWith("'"))
    ) {
        $value = $value.Substring(1, $value.Length - 2)
    }

    $value = $value.Replace('"', "")
    Write-Output "set `"$key=$value`""
}
