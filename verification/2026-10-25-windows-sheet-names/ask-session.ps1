<#
.SYNOPSIS
    Whether a copy, a move or a deletion uses up a number of Excel's count of new Sheets within one session
    (follow-up of ask-names.ps1). Each case: a fresh workbook of one Sheet, the steps, then two Sheets added
    after the last; the order printed. One JSON object per case on a line of <out>.
#>
param([string]$Out)
$ErrorActionPreference = 'Stop'
$xl = New-Object -ComObject Excel.Application; $xl.Visible = $false; $xl.DisplayAlerts = $false
function Order($b) { (1..$b.Worksheets.Count | ForEach-Object { $b.Worksheets.Item($_).Name }) -join ',' }
function Add($b) { [void]$b.Worksheets.Add([Type]::Missing, $b.Worksheets.Item($b.Worksheets.Count)) }
$cases = [ordered]@{
    'nothing'       = { param($b) }
    'copy'          = { param($b) $b.Worksheets.Item(1).Copy([Type]::Missing, $b.Worksheets.Item(1)) }
    'copy-twice'    = { param($b) $b.Worksheets.Item(1).Copy([Type]::Missing, $b.Worksheets.Item(1)); $b.Worksheets.Item(1).Copy([Type]::Missing, $b.Worksheets.Item(1)) }
    'add-delete'    = { param($b) Add $b; $b.Worksheets.Item('Sheet2').Delete() }
    'add-rename'    = { param($b) Add $b; $b.Worksheets.Item('Sheet2').Name = 'Feb' }
    'move'          = { param($b) Add $b; $b.Worksheets.Item('Sheet2').Move($b.Worksheets.Item(1)) }
}
Set-Content -Encoding UTF8 $Out -Value (([ordered]@{ group = 'meta'; version = $xl.Version; build = $xl.Build; started = (Get-Date).ToString('o') }) | ConvertTo-Json -Compress)
foreach ($k in $cases.Keys) {
    $rec = [ordered]@{ id = $k }
    try {
        $b = $xl.Workbooks.Add(); while ($b.Worksheets.Count -gt 1) { $b.Worksheets.Item($b.Worksheets.Count).Delete() }
        & $cases[$k] $b; $rec.before = Order $b; Add $b; Add $b; $rec.after = Order $b; $b.Close($false)
    } catch { $rec.error = $_.Exception.Message }
    ($rec | ConvertTo-Json -Compress) | Add-Content -Encoding UTF8 $Out
    Write-Host "$k $($rec.before) -> $($rec.after) $($rec.error)"
}
$xl.Quit()
