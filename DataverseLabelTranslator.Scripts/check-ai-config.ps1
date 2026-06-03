$ErrorActionPreference = "Stop"

function Get-RepoRoot {
    $root = git rev-parse --show-toplevel
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($root)) {
        throw "Current directory is not inside a git repository."
    }

    return $root.Trim()
}

function Assert-FileExists {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Missing required file: $Path"
    }
}

function Assert-FileMissing {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (Test-Path -LiteralPath $Path) {
        throw "Unexpected file exists: $Path"
    }
}

function Assert-JsonFile {
    param([Parameter(Mandatory = $true)][string]$Path)

    Assert-FileExists -Path $Path
    Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json | Out-Null
}

function Assert-GitIgnored {
    param([Parameter(Mandatory = $true)][string]$Path)

    git check-ignore -q -- $Path
    if ($LASTEXITCODE -ne 0) {
        throw "Sensitive/local file is not ignored by git: $Path"
    }
}

function Assert-NotTracked {
    param([Parameter(Mandatory = $true)][string]$Path)

    $tracked = git ls-files -- $Path
    if (-not [string]::IsNullOrWhiteSpace($tracked)) {
        throw "Sensitive/local file is tracked by git: $Path"
    }
}

function Assert-NoTrackedSecretLikeValues {
    $binaryExtensions = @(
        ".zip",
        ".png",
        ".jpg",
        ".jpeg",
        ".gif",
        ".ico",
        ".dll",
        ".exe",
        ".pdb",
        ".pdf",
        ".docx",
        ".pptx",
        ".xlsx",
        ".mp4",
        ".webp",
        ".nupkg"
    )

    $sasPattern = ("si" + "g=") + "|" + ("Shared" + "Access" + "Signature")
    $trackedFiles = git ls-files
    foreach ($file in $trackedFiles) {
        if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
            continue
        }

        $extension = [System.IO.Path]::GetExtension($file).ToLowerInvariant()
        if ($binaryExtensions -contains $extension) {
            continue
        }

        $item = Get-Item -LiteralPath $file
        if ($item.Length -gt 5MB) {
            continue
        }

        try {
            $content = [System.IO.File]::ReadAllText($file)
        }
        catch {
            continue
        }

        $normalized = $content -replace "`r`n", "`n"
        $lines = $normalized -split "`n"
        for ($index = 0; $index -lt $lines.Count; $index++) {
            $line = $lines[$index]
            $location = "${file}:$($index + 1)"

            if ($line -match "DEVKIT_CLIENT_SECRET" -and $line -notmatch "<client-secret>" -and $line -notmatch "\$\{input:") {
                throw "Tracked DevKit client secret-like value found at $location"
            }

            if ($line -match "DEVKIT_CLIENT_ID" -and $line -notmatch "<client-id>" -and $line -notmatch "\$\{input:") {
                throw "Tracked DevKit client id-like value found at $location"
            }

            if ($line -match "DEVKIT_URL" -and $line -notmatch "<environment>" -and $line -notmatch "\$\{input:") {
                throw "Tracked DevKit URL-like value found at $location"
            }

            if ($line -match $sasPattern) {
                throw "Tracked SAS-like value found at $location"
            }
        }
    }
}

$repoRoot = Get-RepoRoot
Set-Location -LiteralPath $repoRoot

Assert-FileExists -Path "AGENTS.md"
Assert-FileExists -Path "CLAUDE.md"
Assert-FileExists -Path ".env.example"
Assert-FileExists -Path "DataverseLabelTranslator.WebResource\package.json"
Assert-FileExists -Path "DataverseLabelTranslator.Scripts\sync-ai-config.ps1"
Assert-FileExists -Path "DataverseLabelTranslator.Scripts\check-ai-config.ps1"
Assert-FileExists -Path "DataverseLabelTranslator.WebResource\deploy.debug.bat"
Assert-FileExists -Path "DataverseLabelTranslator.Server\deploy.debug.bat"
Assert-FileExists -Path "DataverseLabelTranslator.Server\deploy.debug.only.bat"
Assert-FileExists -Path "DataverseLabelTranslator.Shared\Entities\generator.bat"
Assert-FileMissing -Path "DataverseLabelTranslator.Client"
Assert-FileMissing -Path "DataverseLabelTranslator.Scripts\node_modules"
Assert-FileMissing -Path "DataverseLabelTranslator.Scripts\coverage"
Assert-FileMissing -Path ".github/copilot-instructions.md"
Assert-FileMissing -Path ".codex/commands"
Assert-FileMissing -Path ".claude/commands"
Assert-FileMissing -Path ".agents/workflows"
Assert-FileMissing -Path "scripts"
Assert-FileMissing -Path "js"
Assert-FileMissing -Path "css"
Assert-FileMissing -Path "html"
Assert-FileMissing -Path "img"
Assert-FileMissing -Path "tests"
Assert-FileMissing -Path "docs"
Assert-FileMissing -Path "node_modules"
Assert-FileMissing -Path "coverage"
Assert-FileMissing -Path "site"
Assert-FileMissing -Path "release"
Assert-FileMissing -Path "package.json"
Assert-FileMissing -Path "package-lock.json"
Assert-FileMissing -Path "eslint.config.mjs"
Assert-FileMissing -Path "vitest.config.mjs"
Assert-FileMissing -Path "prettier.config.mjs"
Assert-FileMissing -Path "jsconfig.json"
Assert-FileMissing -Path ".prettierignore"

$claudeMd = Get-Content -Raw -LiteralPath "CLAUDE.md"
$canonicalSkillNames = Get-ChildItem -LiteralPath ".agents\skills" -Directory |
    Where-Object { $_.Name -like "pl-*" } |
    Sort-Object Name |
    Select-Object -ExpandProperty Name

foreach ($skillName in $canonicalSkillNames) {
    if ($claudeMd -notmatch [regex]::Escape("/$skillName")) {
        throw "CLAUDE.md is missing /$skillName"
    }
}

Assert-JsonFile -Path ".vscode/settings.json"
$settings = Get-Content -Raw -LiteralPath ".vscode/settings.json" | ConvertFrom-Json
if ($settings."chat.useAgentsMdFile" -ne $true) {
    throw ".vscode/settings.json must set chat.useAgentsMdFile=true"
}
if ($settings."github.copilot.chat.codeGeneration.useInstructionFiles" -ne $true) {
    throw ".vscode/settings.json must set github.copilot.chat.codeGeneration.useInstructionFiles=true"
}

Assert-JsonFile -Path ".vscode/mcp.json.example"
Assert-JsonFile -Path ".agents/mcp_config.json.example"

foreach ($localPath in @(
    ".vscode/mcp.json",
    ".agents/mcp_config.json",
    ".agents/mcp_oauth_tokens.json",
    ".codex/config.toml",
    ".codex/config.local.toml",
    ".env",
    ".mcp.json",
    "DataverseLabelTranslator.Release/1.0.0.0/appsource/zip/release.md"
)) {
    Assert-GitIgnored -Path $localPath
    Assert-NotTracked -Path $localPath
}

& (Join-Path $repoRoot "DataverseLabelTranslator.Scripts\sync-ai-config.ps1") -Check

Assert-NoTrackedSecretLikeValues

Write-Host "AI config check passed."
