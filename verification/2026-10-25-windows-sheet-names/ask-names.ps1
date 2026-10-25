<#
.SYNOPSIS
    Which name Excel gives a new Sheet after its workbook is saved and opened again (exsheet-workbook ticket 12,
    answer 13, carried on). Each case: a fresh workbook of one Sheet, the steps through COM, then saved as .xlsx,
    closed, opened again, and Worksheets.Add after the last Sheet. One JSON object per case on a line of <out>.

        powershell -File ask-names.ps1 <out.jsonl>
#>
param([string]$Out)
$ErrorActionPreference = 'Stop'
$xl = New-Object -ComObject Excel.Application; $xl.Visible = $false; $xl.DisplayAlerts = $false
function Order($b) { (1..$b.Worksheets.Count | ForEach-Object { $b.Worksheets.Item($_).Name }) -join ',' }
function Add($b) { [void]$b.Worksheets.Add([Type]::Missing, $b.Worksheets.Item($b.Worksheets.Count)) }
$cases = [ordered]@{
    'three'          = { param($b) Add $b; Add $b }
    'gap'            = { param($b) Add $b; Add $b; $b.Worksheets.Item('Sheet2').Delete() }
    'last-deleted'   = { param($b) Add $b; Add $b; $b.Worksheets.Item('Sheet3').Delete() }
    'renamed-5'      = { param($b) $b.Worksheets.Item(1).Name = 'Sheet5' }
    'renamed-jan'    = { param($b) $b.Worksheets.Item(1).Name = 'Jan' }
    'many-deleted'   = { param($b) 1..5 | ForEach-Object { Add $b }; 2..6 | ForEach-Object { $b.Worksheets.Item("Sheet$_").Delete() } }
    'jan-feb'        = { param($b) Add $b; $b.Worksheets.Item(1).Name = 'Jan'; $b.Worksheets.Item(2).Name = 'Feb' }
    'sheet9-only'    = { param($b) $b.Worksheets.Item(1).Name = 'Sheet9' }
}
Set-Content -Encoding UTF8 $Out -Value (([ordered]@{ group = 'meta'; version = $xl.Version; build = $xl.Build; culture = (Get-Culture).Name; started = (Get-Date).ToString('o') }) | ConvertTo-Json -Compress)
foreach ($k in $cases.Keys) {
    $rec = [ordered]@{ id = $k }
    $f = Join-Path $env:TEMP "names-$k.xlsx"
    try {
        $b = $xl.Workbooks.Add()
        while ($b.Worksheets.Count -gt 1) { $b.Worksheets.Item($b.Worksheets.Count).Delete() }
        & $cases[$k] $b
        $rec.saved = Order $b
        $probe = $xl.Workbooks.Add(); while ($probe.Worksheets.Count -gt 1) { $probe.Worksheets.Item($probe.Worksheets.Count).Delete() }
        # What it would be named before the save, on a copy of the state: Add then read, on the open book itself after the save.
        $b.SaveAs($f, 51); $b.Close($false); $probe.Close($false)
        $c = $xl.Workbooks.Open($f)
        Add $c; Add $c
        $rec.reopenedThenAddedTwice = Order $c
        $c.Close($false)
    } catch { $rec.error = $_.Exception.Message }
    finally { if (Test-Path $f) { Remove-Item $f } }
    ($rec | ConvertTo-Json -Compress) | Add-Content -Encoding UTF8 $Out
    Write-Host "$k $($rec.saved) -> $($rec.reopenedThenAddedTwice) $($rec.error)"
}
$xl.Quit()
