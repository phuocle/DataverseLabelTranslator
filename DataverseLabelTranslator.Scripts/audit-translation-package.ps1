param(
    [Parameter(Mandatory = $true)]
    [string]$EnvironmentUrl,

    [Parameter(Mandatory = $true)]
    [string[]]$SolutionName,

    [string]$AccessToken = $env:DATAVERSE_ACCESS_TOKEN,

    [string]$OutputRoot = (Join-Path $env:TEMP "dataverse-label-translator-translation-audit"),

    [int]$TimeoutSec = 600
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($AccessToken)) {
    throw "AccessToken is required. Pass -AccessToken or set DATAVERSE_ACCESS_TOKEN."
}

Add-Type -AssemblyName System.IO.Compression.FileSystem

function New-CleanDirectory {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path $Path | Out-Null
}

function Get-CellText {
    param(
        [Parameter(Mandatory = $true)]$Cell,
        [Parameter(Mandatory = $true)]$NamespaceManager
    )

    $data = $Cell.SelectSingleNode("./ss:Data", $NamespaceManager)
    if ($null -eq $data) {
        return ""
    }

    return [string]$data.InnerText
}

function Get-WorksheetName {
    param([Parameter(Mandatory = $true)]$Worksheet)

    $name = $Worksheet.GetAttribute("Name", "urn:schemas-microsoft-com:office:spreadsheet")
    if ([string]::IsNullOrWhiteSpace($name)) {
        $name = $Worksheet.GetAttribute("ss:Name")
    }
    if ([string]::IsNullOrWhiteSpace($name)) {
        $name = $Worksheet.GetAttribute("Name")
    }

    return $name
}

function Get-RowCells {
    param(
        [Parameter(Mandatory = $true)]$Row,
        [Parameter(Mandatory = $true)]$NamespaceManager
    )

    $cells = @{}
    $columnIndex = 1

    foreach ($cell in $Row.SelectNodes("./ss:Cell", $NamespaceManager)) {
        $explicitIndex = $cell.GetAttribute("Index", "urn:schemas-microsoft-com:office:spreadsheet")
        if (-not [string]::IsNullOrWhiteSpace($explicitIndex)) {
            $columnIndex = [int]$explicitIndex
        }

        $name = $cell.GetAttribute("name")
        $loc = $null
        $data = $cell.SelectSingleNode("./ss:Data", $NamespaceManager)
        if ($null -ne $data) {
            $loc = $data.GetAttribute("Loc", "urn:schemas-microsoft-com:office:spreadsheet")
            if ([string]::IsNullOrWhiteSpace($loc)) {
                $loc = $data.GetAttribute("ss:Loc")
            }
            if ([string]::IsNullOrWhiteSpace($loc)) {
                $loc = $data.GetAttribute("Loc")
            }
        }

        $cells[$columnIndex] = [pscustomobject]@{
            Index = $columnIndex
            Name  = $name
            Loc   = $loc
            Text  = Get-CellText -Cell $cell -NamespaceManager $NamespaceManager
        }

        $columnIndex++
    }

    return $cells
}

function Get-FirstNonEmpty {
    param([object[]]$Values)

    foreach ($value in $Values) {
        if (-not [string]::IsNullOrWhiteSpace([string]$value)) {
            return [string]$value
        }
    }

    return ""
}

function Get-Coverage {
    param(
        [string]$Worksheet,
        [string]$RowType,
        [string]$Parent,
        [string]$ItemName,
        [string]$Loc
    )

    $combined = (($Worksheet, $RowType, $Parent, $ItemName, $Loc) -join " ").ToLowerInvariant()

    if ($Worksheet -eq "Information") {
        return [pscustomobject]@{
            Status  = "info"
            Handler = ""
            Notes   = "Translation package metadata; not a translatable app surface."
        }
    }

    if ($Worksheet -eq "Display Strings") {
        if ($combined -match "entity name.*display string key") {
            return [pscustomobject]@{
                Status  = "info"
                Handler = ""
                Notes   = "Worksheet header."
            }
        }

        return [pscustomobject]@{
            Status  = "partial"
            Handler = "13. Entity Messages"
            Notes   = "Only selected-entity display strings are currently loaded; package-wide display strings need audit/list mode."
        }
    }

    if ($Worksheet -eq "Localized Labels" -and $combined -match "entity name.*object column name") {
        return [pscustomobject]@{
            Status  = "info"
            Handler = ""
            Notes   = "Worksheet header."
        }
    }

    if ($Worksheet -eq "Localized Labels" -and $combined -match "^(localized labels\s+)?(solution|publisher)\.") {
        return [pscustomobject]@{ Status = "missing"; Handler = ""; Notes = "Solution/publisher friendly names are exported but no current app handler edits them." }
    }

    if ($combined -match "appaction|command") {
        return [pscustomobject]@{ Status = "covered"; Handler = "12. Commands"; Notes = "Covered through appaction RetrieveLocLabels/SetLocLabels, not translation package import." }
    }
    if ($combined -match "ribbon") {
        return [pscustomobject]@{ Status = "covered"; Handler = "11. Ribbons"; Notes = "Covered through classic RibbonDiffXml LocLabels." }
    }
    if ($combined -match "workflow|business rule|process") {
        if ($combined -match "workflow categories\.name") {
            return [pscustomobject]@{ Status = "missing"; Handler = ""; Notes = "Business rule/workflow record name is exported, but 10. Business Rules only edits step labels from workflow XAML." }
        }
        if ($combined -match "workflow categories\.description") {
            return [pscustomobject]@{ Status = "partial"; Handler = "10. Business Rules"; Notes = "Business rule step-label descriptions are covered; workflow record description itself is not currently edited." }
        }
        return [pscustomobject]@{ Status = "covered"; Handler = "10. Business Rules / 9. BPF"; Notes = "Business rule/BPF labels are covered through workflow XML handlers where entity-scoped." }
    }
    if ($combined -match "sitemap|site map") {
        return [pscustomobject]@{ Status = "covered"; Handler = "15. Sitemap"; Notes = "Covered through sitemap XML localization." }
    }
    if ($combined -match "dashboard") {
        return [pscustomobject]@{ Status = "covered"; Handler = "16. Dashboards"; Notes = "Covered through dashboard system forms." }
    }
    if ($combined -match "web resource|resx|json") {
        return [pscustomobject]@{ Status = "partial"; Handler = "17. Web Resources"; Notes = "RESX/JSON localized web resources are covered; arbitrary web resource metadata labels are not a first-class translation type." }
    }
    if ($combined -match "global option|option set|optionset|picklist|state|status|option") {
        return [pscustomobject]@{ Status = "covered"; Handler = "2. Option Sets / 18. Global Option Sets"; Notes = "Choice labels are covered by local/global option set handlers." }
    }
    if ($combined -match "attribute|column|field") {
        return [pscustomobject]@{ Status = "covered"; Handler = "1. Attributes"; Notes = "Column display names/descriptions are covered." }
    }
    if ($Worksheet -eq "Localized Labels" -and $combined -match "\.localized(collection)?name(\s|$)") {
        return [pscustomobject]@{ Status = "covered"; Handler = "6. Entity Metadata"; Notes = "Table singular/plural names are covered." }
    }
    if ($Worksheet -eq "Localized Labels" -and $combined -match "\.displayname(\s|$)") {
        return [pscustomobject]@{ Status = "covered"; Handler = "1. Attributes / 3. Forms"; Notes = "Display labels for columns and form controls are covered by existing entity-scoped handlers." }
    }
    if ($Worksheet -eq "Localized Labels" -and $combined -match "\.description(\s|$)") {
        return [pscustomobject]@{ Status = "partial"; Handler = "1. Attributes"; Notes = "Attribute descriptions are covered; form, view, and workflow record descriptions are exported but not all are first-class editable rows." }
    }
    if ($Worksheet -eq "Localized Labels" -and $combined -match "\.name(\s|$)") {
        return [pscustomobject]@{ Status = "covered"; Handler = "4. Views / 5. Form Metadata"; Notes = "Form and view names are covered." }
    }
    if ($combined -match "entity relationship|relationship") {
        return [pscustomobject]@{ Status = "covered"; Handler = "7. Relationships"; Notes = "Associated menu labels are covered; relationship technical metadata may remain readonly/out of scope." }
    }
    if ($combined -match "entity|table") {
        return [pscustomobject]@{ Status = "covered"; Handler = "6. Entity Metadata"; Notes = "Table singular/plural names are covered; entity messages are handled separately." }
    }
    if ($combined -match "form") {
        return [pscustomobject]@{ Status = "covered"; Handler = "3. Forms / 5. Form Metadata"; Notes = "Form labels and form names are covered." }
    }
    if ($combined -match "view|savedquery|saved query") {
        return [pscustomobject]@{ Status = "covered"; Handler = "4. Views"; Notes = "View labels are covered." }
    }
    if ($combined -match "chart|visualization") {
        return [pscustomobject]@{ Status = "covered"; Handler = "8. Charts"; Notes = "Chart labels are covered." }
    }
    if ($combined -match "content snippet") {
        return [pscustomobject]@{ Status = "covered"; Handler = "14. Content Snippets"; Notes = "Portal content snippets are covered." }
    }
    if ($combined -match "custom api|customapi|request parameter|response property") {
        return [pscustomobject]@{ Status = "missing"; Handler = ""; Notes = "No handler for Custom API/request/response localized labels." }
    }
    if ($combined -match "entity key|alternate key|key") {
        return [pscustomobject]@{ Status = "missing"; Handler = ""; Notes = "No handler for entity/alternate key display labels." }
    }
    if ($combined -match "app module|model-driven app|appmodule|app module sitemap|app setting") {
        return [pscustomobject]@{ Status = "missing"; Handler = ""; Notes = "No app-module metadata/app setting label handler." }
    }
    if ($combined -match "environment variable") {
        return [pscustomobject]@{ Status = "missing"; Handler = ""; Notes = "No environment variable definition label/description handler." }
    }
    return [pscustomobject]@{
        Status  = "unknown"
        Handler = ""
        Notes   = "Needs manual inspection; no current handler mapping matched this XML row."
    }
}

function Export-TranslationPackage {
    param(
        [Parameter(Mandatory = $true)][string]$EnvironmentUrl,
        [Parameter(Mandatory = $true)][string]$SolutionName,
        [Parameter(Mandatory = $true)][string]$AccessToken,
        [Parameter(Mandatory = $true)][string]$OutputDirectory,
        [Parameter(Mandatory = $true)][int]$TimeoutSec
    )

    $uri = ($EnvironmentUrl.TrimEnd("/") + "/api/data/v9.2/solutions/Microsoft.Dynamics.CRM.ExportTranslation")
    $body = @{ SolutionName = $SolutionName } | ConvertTo-Json
    $headers = @{
        Authorization     = "Bearer $AccessToken"
        Accept            = "application/json"
        "OData-MaxVersion" = "4.0"
        "OData-Version"    = "4.0"
    }

    $response = Invoke-RestMethod -Method Post -Uri $uri -Headers $headers -ContentType "application/json" -Body $body -TimeoutSec $TimeoutSec
    if ([string]::IsNullOrWhiteSpace($response.ExportTranslationFile)) {
        throw "ExportTranslation did not return ExportTranslationFile for $SolutionName."
    }

    $zipPath = Join-Path $OutputDirectory "$SolutionName.translation.zip"
    [IO.File]::WriteAllBytes($zipPath, [Convert]::FromBase64String($response.ExportTranslationFile))

    $extractPath = Join-Path $OutputDirectory $SolutionName
    New-CleanDirectory -Path $extractPath
    [System.IO.Compression.ZipFile]::ExtractToDirectory($zipPath, $extractPath)

    $xmlPath = Join-Path $extractPath "CrmTranslations.xml"
    if (!(Test-Path -LiteralPath $xmlPath)) {
        throw "CrmTranslations.xml was not found in $zipPath."
    }

    return [pscustomobject]@{
        SolutionName = $SolutionName
        ZipPath      = $zipPath
        ExtractPath  = $extractPath
        XmlPath      = $xmlPath
    }
}

function Analyze-CrmTranslations {
    param(
        [Parameter(Mandatory = $true)][string]$SolutionName,
        [Parameter(Mandatory = $true)][string]$XmlPath,
        [Parameter(Mandatory = $true)][string]$OutputDirectory
    )

    [xml]$xml = Get-Content -LiteralPath $XmlPath -Raw -Encoding UTF8
    $ns = New-Object System.Xml.XmlNamespaceManager($xml.NameTable)
    $ns.AddNamespace("ss", "urn:schemas-microsoft-com:office:spreadsheet")

    $rows = New-Object System.Collections.Generic.List[object]
    $worksheetSummaries = New-Object System.Collections.Generic.List[object]

    foreach ($worksheet in $xml.SelectNodes("//ss:Worksheet", $ns)) {
        $worksheetName = Get-WorksheetName -Worksheet $worksheet
        $rowNodes = $worksheet.SelectNodes(".//ss:Table/ss:Row", $ns)
        $worksheetSummaries.Add([pscustomobject]@{
            Solution  = $SolutionName
            Worksheet = $worksheetName
            Rows      = $rowNodes.Count
        })

        $rowNumber = 0
        foreach ($row in $rowNodes) {
            $rowNumber++
            $cells = Get-RowCells -Row $row -NamespaceManager $ns
            $cellValues = @($cells.Values | Sort-Object Index)
            $locValues = @($cellValues | Where-Object { -not [string]::IsNullOrWhiteSpace($_.Loc) } | Select-Object -ExpandProperty Loc)
            $namedCells = @{}

            foreach ($cell in $cellValues) {
                if (-not [string]::IsNullOrWhiteSpace($cell.Name) -and -not $namedCells.ContainsKey($cell.Name)) {
                    $namedCells[$cell.Name] = $cell.Text
                }
            }

            $rowType = $row.GetAttribute("type")
            $parent = $row.GetAttribute("parent")
            if ($worksheetName -eq "Localized Labels" -and $cellValues.Count -ge 3) {
                $itemName = ($cellValues[0].Text + "." + $cellValues[2].Text)
            }
            elseif ($worksheetName -eq "Display Strings" -and $cellValues.Count -ge 2) {
                $itemName = ($cellValues[0].Text + "." + $cellValues[1].Text)
            }
            else {
                $itemName = Get-FirstNonEmpty @(
                    $namedCells["Name"],
                    $namedCells["DisplayName"],
                    $namedCells["LocalizedName"],
                    $namedCells["OriginalName"],
                    $namedCells["Default"],
                    $namedCells["DefaultDisplayString"],
                    $namedCells["ResourceKey"],
                    $namedCells["Id"],
                    ($cellValues | Select-Object -First 1 -ExpandProperty Text)
                )
            }
            $loc = Get-FirstNonEmpty $locValues
            $coverage = Get-Coverage -Worksheet $worksheetName -RowType $rowType -Parent $parent -ItemName $itemName -Loc $loc

            $rows.Add([pscustomobject]@{
                Solution  = $SolutionName
                Worksheet = $worksheetName
                RowNumber = $rowNumber
                RowType   = $rowType
                Parent    = $parent
                ItemName  = $itemName
                Loc       = $loc
                Status    = $coverage.Status
                Handler   = $coverage.Handler
                Notes     = $coverage.Notes
            })
        }
    }

    $rowCsv = Join-Path $OutputDirectory "$SolutionName.rows.csv"
    $summaryCsv = Join-Path $OutputDirectory "$SolutionName.summary.csv"
    $rows | Export-Csv -LiteralPath $rowCsv -NoTypeInformation -Encoding UTF8

    $summary = $rows |
        Group-Object Worksheet, RowType, Status, Handler |
        Sort-Object Count -Descending |
        ForEach-Object {
            $parts = $_.Name -split ", "
            [pscustomobject]@{
                Solution  = $SolutionName
                Worksheet = $parts[0]
                RowType   = $parts[1]
                Status    = $parts[2]
                Handler   = $parts[3]
                Count     = $_.Count
            }
        }
    $summary | Export-Csv -LiteralPath $summaryCsv -NoTypeInformation -Encoding UTF8

    return [pscustomobject]@{
        WorksheetSummaries = $worksheetSummaries
        Rows               = $rows
        Summary            = $summary
        RowCsv             = $rowCsv
        SummaryCsv         = $summaryCsv
    }
}

function Write-MarkdownReport {
    param(
        [Parameter(Mandatory = $true)][object[]]$Exports,
        [Parameter(Mandatory = $true)][object[]]$Analyses,
        [Parameter(Mandatory = $true)][string]$OutputDirectory
    )

    $reportPath = Join-Path $OutputDirectory "translation-coverage-audit.md"
    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add("# Translation Coverage Audit")
    $lines.Add("")
    $lines.Add("Generated: $(Get-Date -Format s)")
    $lines.Add("")
    $lines.Add("## Exported Packages")
    $lines.Add("")
    foreach ($export in $Exports) {
        $lines.Add("- " + $export.SolutionName + ": " + $export.XmlPath)
    }
    $lines.Add("")
    $lines.Add("## Worksheets")
    $lines.Add("")
    $lines.Add("| Solution | Worksheet | Rows |")
    $lines.Add("|---|---:|---:|")
    foreach ($analysis in $Analyses) {
        foreach ($worksheet in $analysis.WorksheetSummaries) {
            $lines.Add("| $($worksheet.Solution) | $($worksheet.Worksheet) | $($worksheet.Rows) |")
        }
    }

    $allRows = @($Analyses | ForEach-Object { $_.Rows })
    $allSummary = @($Analyses | ForEach-Object { $_.Summary })
    $gapRows = @($allRows | Where-Object { $_.Status -in @("missing", "partial", "unknown") })
    $gapSummary = @($gapRows | Group-Object Worksheet, RowType, Status, Handler, Notes | Sort-Object Count -Descending)

    $lines.Add("")
    $lines.Add("## Potential Gaps")
    $lines.Add("")
    $lines.Add("| Status | Count | Worksheet | Row type | Current handler | Notes |")
    $lines.Add("|---|---:|---|---|---|---|")
    foreach ($group in $gapSummary) {
        $sample = $group.Group | Select-Object -First 1
        $lines.Add("| $($sample.Status) | $($group.Count) | $($sample.Worksheet) | $($sample.RowType) | $($sample.Handler) | $($sample.Notes) |")
    }

    $lines.Add("")
    $lines.Add("## Top Covered Rows")
    $lines.Add("")
    $lines.Add("| Count | Worksheet | Row type | Handler |")
    $lines.Add("|---:|---|---|---|")
    foreach ($item in ($allSummary | Where-Object { $_.Status -eq "covered" } | Select-Object -First 30)) {
        $lines.Add("| $($item.Count) | $($item.Worksheet) | $($item.RowType) | $($item.Handler) |")
    }

    $lines.Add("")
    $lines.Add("## Output Files")
    $lines.Add("")
    foreach ($analysis in $Analyses) {
        $lines.Add("- Rows CSV: " + $analysis.RowCsv)
        $lines.Add("- Summary CSV: " + $analysis.SummaryCsv)
    }

    Set-Content -LiteralPath $reportPath -Value $lines -Encoding UTF8
    return $reportPath
}

New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

$exports = New-Object System.Collections.Generic.List[object]
$analyses = New-Object System.Collections.Generic.List[object]

foreach ($solution in $SolutionName) {
    $solutionOutput = Join-Path $OutputRoot $solution
    New-CleanDirectory -Path $solutionOutput
    Write-Host "Exporting translation package for $solution..."
    $export = Export-TranslationPackage -EnvironmentUrl $EnvironmentUrl -SolutionName $solution -AccessToken $AccessToken -OutputDirectory $solutionOutput -TimeoutSec $TimeoutSec
    $exports.Add($export)

    Write-Host "Analyzing $($export.XmlPath)..."
    $analysis = Analyze-CrmTranslations -SolutionName $solution -XmlPath $export.XmlPath -OutputDirectory $solutionOutput
    $analyses.Add($analysis)
}

$report = Write-MarkdownReport -Exports $exports -Analyses $analyses -OutputDirectory $OutputRoot

[pscustomobject]@{
    OutputRoot = $OutputRoot
    Report     = $report
    Packages   = @($exports | Select-Object SolutionName, ZipPath, XmlPath)
} | ConvertTo-Json -Depth 4
