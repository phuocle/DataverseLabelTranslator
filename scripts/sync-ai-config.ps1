param(
    [switch]$Check
)

$ErrorActionPreference = "Stop"

function Get-RepoRoot {
    $root = git rev-parse --show-toplevel
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($root)) {
        throw "Current directory is not inside a git repository."
    }

    return $root.Trim()
}

function Get-FileText {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Missing file: $Path"
    }

    $utf8 = New-Object System.Text.UTF8Encoding($false, $true)
    return [System.IO.File]::ReadAllText($Path, $utf8)
}

function Split-FrontMatter {
    param([Parameter(Mandatory = $true)][string]$Text)

    $normalized = $Text -replace "`r`n", "`n"
    if (-not $normalized.StartsWith("---`n")) {
        return @{
            FrontMatter = @{}
            Body = $Text.TrimStart()
        }
    }

    $end = $normalized.IndexOf("`n---`n", 4)
    if ($end -lt 0) {
        throw "Invalid frontmatter block."
    }

    $frontMatterText = $normalized.Substring(4, $end - 4)
    $body = $normalized.Substring($end + 6).TrimStart()
    $frontMatter = @{}

    foreach ($line in ($frontMatterText -split "`n")) {
        if ($line -match "^\s*([A-Za-z0-9_-]+):\s*(.*)\s*$") {
            $key = $Matches[1]
            $value = $Matches[2].Trim()
            if (($value.StartsWith('"') -and $value.EndsWith('"')) -or ($value.StartsWith("'") -and $value.EndsWith("'"))) {
                $value = $value.Substring(1, $value.Length - 2)
            }
            $frontMatter[$key] = $value
        }
    }

    return @{
        FrontMatter = $frontMatter
        Body = $body
    }
}

function New-ClaudeSkillText {
    param(
        [Parameter(Mandatory = $true)][string]$SkillName,
        [Parameter(Mandatory = $true)][string]$DisplayName,
        [Parameter(Mandatory = $true)][string]$Description,
        [string]$ArgumentHint,
        [Parameter(Mandatory = $true)][string]$CanonicalBody
    )

    $lines = @(
        "---",
        "name: `"$DisplayName`"",
        "description: `"$Description`""
    )

    if ($ArgumentHint) {
        $lines += "argument-hint: `"$ArgumentHint`""
    }

    $lines += @(
        "disable-model-invocation: true",
        "---",
        "",
        "<!-- Generated from ../../.agents/skills/$SkillName/SKILL.md. Do not edit manually; run scripts/sync-ai-config.ps1. -->",
        "",
        $CanonicalBody.TrimEnd(),
        ""
    )

    return ($lines -join "`n")
}

function New-CopilotPromptText {
    param(
        [Parameter(Mandatory = $true)][string]$SkillName,
        [Parameter(Mandatory = $true)][string]$Description,
        [string]$ArgumentHint
    )

    $lines = @(
        "---",
        "name: $SkillName",
        "description: $Description"
    )

    if ($ArgumentHint) {
        $lines += "argument-hint: '$ArgumentHint'"
    }

    $lines += @(
        "agent: agent",
        "---",
        "",
        "<!-- Generated from ../../.agents/skills/$SkillName/SKILL.md. Do not edit manually; run scripts/sync-ai-config.ps1. -->",
        "",
        "Run the Dataverse Label Translator ``/$SkillName`` workflow.",
        "",
        "Before acting, read and follow [AGENTS.md](../../AGENTS.md) and the canonical workflow in [$SkillName skill](../../.agents/skills/$SkillName/SKILL.md).",
        "",
        "Use any text supplied after ``/$SkillName`` as the command arguments. Do not proceed from memory if the canonical skill file cannot be read; stop and report that the skill file is unavailable.",
        "",
        "Hard rules:",
        "",
        "- Follow the canonical skill file exactly.",
        "- Do not push.",
        "- Do not create pull requests.",
        "- Do not deploy, export, upload, stage, or commit unless that specific workflow explicitly requires it."
    )

    return ($lines -join "`n") + "`n"
}

function Set-Or-CompareFile {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$ExpectedText,
        [Parameter(Mandatory = $true)][switch]$Check
    )

    $expectedNormalized = ($ExpectedText -replace "`r`n", "`n").TrimEnd() + "`n"

    if ($Check) {
        if (-not (Test-Path -LiteralPath $Path)) {
            return "Missing generated file: $Path"
        }

        $actualNormalized = ((Get-FileText -Path $Path) -replace "`r`n", "`n").TrimEnd() + "`n"
        if ($actualNormalized -ne $expectedNormalized) {
            return "Generated file is out of sync: $Path"
        }

        return $null
    }

    $directory = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $expectedNormalized, $utf8NoBom)
    return $null
}

$repoRoot = Get-RepoRoot
$canonicalSkillRoot = Join-Path $repoRoot ".agents\skills"
$claudeSkillRoot = Join-Path $repoRoot ".claude\skills"
$copilotPromptRoot = Join-Path $repoRoot ".github\prompts"

$skillDirectories = Get-ChildItem -LiteralPath $canonicalSkillRoot -Directory |
    Where-Object { $_.Name -like "pl-*" } |
    Sort-Object Name

if ($skillDirectories.Count -eq 0) {
    throw "No canonical skills found under $canonicalSkillRoot"
}

$drift = New-Object System.Collections.Generic.List[string]
$canonicalNames = @()

foreach ($skillDirectory in $skillDirectories) {
    $skillName = $skillDirectory.Name
    $canonicalNames += $skillName
    $canonicalPath = Join-Path $skillDirectory.FullName "SKILL.md"
    $canonicalText = Get-FileText -Path $canonicalPath
    $parts = Split-FrontMatter -Text $canonicalText
    $frontMatter = $parts.FrontMatter

    if (-not $frontMatter.ContainsKey("name") -or $frontMatter["name"] -ne $skillName) {
        $drift.Add("Canonical skill name mismatch in $canonicalPath. Expected name: `"$skillName`".")
    }

    if (-not $frontMatter.ContainsKey("display-name") -or [string]::IsNullOrWhiteSpace($frontMatter["display-name"])) {
        $drift.Add("Canonical skill is missing display-name: $canonicalPath")
        $displayName = $skillName
    }
    else {
        $displayName = $frontMatter["display-name"]
    }

    if (-not $frontMatter.ContainsKey("description") -or [string]::IsNullOrWhiteSpace($frontMatter["description"])) {
        $drift.Add("Canonical skill is missing description: $canonicalPath")
        $description = "Run the $skillName workflow."
    }
    else {
        $description = $frontMatter["description"]
    }

    $argumentHint = $null
    if ($frontMatter.ContainsKey("argument-hint") -and -not [string]::IsNullOrWhiteSpace($frontMatter["argument-hint"])) {
        $argumentHint = $frontMatter["argument-hint"]
    }

    $claudePath = Join-Path (Join-Path $claudeSkillRoot $skillName) "SKILL.md"
    $copilotPath = Join-Path $copilotPromptRoot "$skillName.prompt.md"

    $claudeText = New-ClaudeSkillText -SkillName $skillName -DisplayName $displayName -Description $description -ArgumentHint $argumentHint -CanonicalBody $parts.Body
    $copilotText = New-CopilotPromptText -SkillName $skillName -Description $description -ArgumentHint $argumentHint

    $claudeIssue = Set-Or-CompareFile -Path $claudePath -ExpectedText $claudeText -Check:$Check
    if ($claudeIssue) {
        $drift.Add($claudeIssue)
    }

    $copilotIssue = Set-Or-CompareFile -Path $copilotPath -ExpectedText $copilotText -Check:$Check
    if ($copilotIssue) {
        $drift.Add($copilotIssue)
    }
}

$expectedClaudeNames = $canonicalNames
$actualClaudeNames = @()
if (Test-Path -LiteralPath $claudeSkillRoot) {
    $actualClaudeNames = Get-ChildItem -LiteralPath $claudeSkillRoot -Directory | Sort-Object Name | Select-Object -ExpandProperty Name
}

$expectedCopilotNames = $canonicalNames | ForEach-Object { "$_.prompt.md" }
$actualCopilotNames = @()
if (Test-Path -LiteralPath $copilotPromptRoot) {
    $actualCopilotNames = Get-ChildItem -LiteralPath $copilotPromptRoot -File -Filter "*.prompt.md" | Sort-Object Name | Select-Object -ExpandProperty Name
}

$extraClaude = Compare-Object -ReferenceObject $expectedClaudeNames -DifferenceObject $actualClaudeNames | Where-Object { $_.SideIndicator -eq "=>" }
$missingClaude = Compare-Object -ReferenceObject $expectedClaudeNames -DifferenceObject $actualClaudeNames | Where-Object { $_.SideIndicator -eq "<=" }
$extraCopilot = Compare-Object -ReferenceObject $expectedCopilotNames -DifferenceObject $actualCopilotNames | Where-Object { $_.SideIndicator -eq "=>" }
$missingCopilot = Compare-Object -ReferenceObject $expectedCopilotNames -DifferenceObject $actualCopilotNames | Where-Object { $_.SideIndicator -eq "<=" }

foreach ($item in $extraClaude) { $drift.Add("Unexpected Claude skill: $($item.InputObject)") }
foreach ($item in $missingClaude) { $drift.Add("Missing Claude skill: $($item.InputObject)") }
foreach ($item in $extraCopilot) { $drift.Add("Unexpected Copilot prompt: $($item.InputObject)") }
foreach ($item in $missingCopilot) { $drift.Add("Missing Copilot prompt: $($item.InputObject)") }

if ($Check -and $drift.Count -gt 0) {
    $drift | ForEach-Object { Write-Error $_ }
    throw "AI generated adapters are out of sync. Run scripts/sync-ai-config.ps1."
}

if (-not $Check) {
    Write-Host "Synced AI adapters for $($canonicalNames.Count) canonical skills:"
    $canonicalNames | ForEach-Object { Write-Host " - $_" }
}
else {
    Write-Host "AI generated adapters are in sync."
}
