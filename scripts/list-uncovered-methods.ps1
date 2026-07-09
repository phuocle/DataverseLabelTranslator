$testFile = "d:\github\DataverseLabelTranslator\DataverseLabelTranslator.WebResource\tests\DataverseLabelTranslator.test.js"
$testFile2 = "d:\github\DataverseLabelTranslator\DataverseLabelTranslator.WebResource\tests\DataverseLabelTranslatorExtra.test.js"

$lines1 = Get-Content $testFile
$lines2 = Get-Content $testFile2
$testContent = ($lines1 -join "`n") + "`n" + ($lines2 -join "`n")

$file = "d:\github\DataverseLabelTranslator\DataverseLabelTranslator.WebResource\js\DataverseLabelTranslator.js"
$methods = @()
Select-String -Path $file -Pattern "^\s*DataverseLabelTranslator\.\w+\s*=" | ForEach-Object {
    if ($_ -match 'DataverseLabelTranslator\.(\w+)\s*=') {
        $methods += $Matches[1]
    }
}
$methods = $methods | Sort-Object -Unique

$notInTests = @()
foreach ($m in $methods) {
    $test1 = $testContent -match "DataverseLabelTranslator\.$m\b"
    $test2 = $testContent -match "Translator\.$m\b"
    if (-not $test1 -and -not $test2) {
        $notInTests += $m
    }
}

Write-Host "Methods not in tests:"
$notInTests | ForEach-Object { Write-Host "  $_" }
