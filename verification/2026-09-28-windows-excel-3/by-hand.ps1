<#
.SYNOPSIS
    The corpus cases the oracle cannot ask through COM, asked through Excel's own UI with real
    keys (Part A of verify-on-windows-2.md; copied unchanged for verify-on-windows-3.md): the undo
    cases (Excel's undo does not reach a change made through COM, so the change is made through
    the ribbon and undone with Ctrl+Z) and the qualifier naming no sheet (which Excel may answer
    with a file dialog). One case at a time:

        powershell -File by-hand.ps1 -Case NAME-036,CW-013

    Each step is appended to by-hand.jsonl beside this script, with a screenshot in shots\.
#>
#Requires -Version 5.1
param([string[]]$Case)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($Case) { $Case = @($Case | ForEach-Object { $_ -split ',' }) }

. (Join-Path $PSScriptRoot '..\2026-09-27-windows-excel\excel-driver.ps1')
$script:Shots = Join-Path $PSScriptRoot 'shots'
$Log = Join-Path $PSScriptRoot 'by-hand.jsonl'
$Utf8 = New-Object Text.UTF8Encoding $false
$xl = Connect-Excel
. (Join-Path $PSScriptRoot '..\2026-09-27-windows-excel-2\case-guard.ps1')
Start-CaseGuard

function Record([string]$Id, [string]$Step, [string[]]$Cells = @(), [switch]$Shot, $Extra = $null) {
    $row = [ordered]@{ case = $Id; step = $Step }
    try {
        $ws = $xl.ActiveSheet
        $row.sheetName = [string]$ws.Name
        foreach ($a in $Cells) {
            $c = $ws.Range($a); $v = $c.Value2
            $row[$a] = [ordered]@{
                value2 = if ($v -is [double]) { $v.ToString('R', [Globalization.CultureInfo]::InvariantCulture) } elseif ($v -is [int]) { "error $v" } else { $v }
                text = [string]$c.Text; formula2 = [string]$c.Formula2
                numberFormat = [string](Get-ComProperty $c 'NumberFormat'); columnWidth = [double]$c.ColumnWidth
            }
        }
    }
    catch { $row.comRefused = $_.Exception.Message }
    if ($null -ne $Extra) { $row.note = $Extra }
    if ($Shot) { $name = ('A-{0}-{1}' -f $Id, ($Step -replace '[^A-Za-z0-9]+', '-')).Trim('-'); Save-Shot $null $name | Out-Null; $row.shot = "shots/$name.png" }
    [IO.File]::AppendAllText($Log, (($row | ConvertTo-Json -Compress -Depth 4) + "`n"), $Utf8)
    Write-Host (($row | ConvertTo-Json -Compress -Depth 4))
}
function Keys([string]$K, [int]$Settle = 500) { Send-Keys $xl $K $Settle }
function Ribbon([string[]]$Letters) { Keys '%' 600; foreach ($l in $Letters) { Keys $l 700 } }
function Fresh([hashtable]$Cells = @{}) { $ws = Reset-Book $xl $Cells; Show-Excel $xl; Keys '{ESC}' 200; return $ws }
function Go([string]$A1) { [void]$xl.Goto($xl.ActiveSheet.Range($A1), $false); Start-Sleep -Milliseconds 200 }
function Type-Entry([string]$A1, [string]$Text) { Go $A1; Keys ($Text + '~') 1200 }
# Whatever Excel has in front after an entry: read, screenshot, then Escape until Ready.
function Settle([string]$Id, [string]$Step) {
    for ($i = 0; $i -lt 6; $i++) {
        $front = [ExcelDriver.Native]::GetForegroundWindow()
        if ($front -ne $script:Hwnd) {
            Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
            $text = ''
            try {
                $el = [System.Windows.Automation.AutomationElement]::FromHandle($front)
                $text = (@($el.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition) | ForEach-Object { $_.Current.Name } | Where-Object { $_ }) | Select-Object -Unique -First 25) -join ' | '
                $text = '[' + $el.Current.Name + '] ' + $text
            } catch { $text = 'unreadable' }
            Record $Id "$Step, dialog in front" -Shot -Extra $text
            Keys '{ESC}' 900
            continue
        }
        try { [void]$xl.ActiveSheet.Name; return } catch { Keys '{ESC}' 600 }
    }
}

$cases = [ordered]@{}

foreach ($q in @(@('NAME-006', '=Sheet1!A1', 'My Sheet'), @('NAME-007', '=Sheet2!A1', $null), @('NAME-008', "='Sheet1 '!A1", $null))) {
    $id = $q[0]; $formula = $q[1]; $rename = $q[2]
    $cases[$id] = [scriptblock]::Create(@"
        `$ws = Fresh @{ A1 = '3' }
        $(if ($rename) { "`$ws.Name = '$rename'" })
        Go 'ZZ1000'
        # Excel's alerts are on while a user types; the driver keeps them off otherwise.
        `$xl.DisplayAlerts = `$true
        Keys ('$($formula -replace "'", "''")' -replace '([+^%~(){}\[\]])', '{`$1}') 400
        Keys '~' 1500
        Settle '$id' 'typed $($formula -replace "'", "''") and Enter'
        `$xl.DisplayAlerts = `$false
        Record '$id' 'after' @('ZZ1000') -Shot
"@)
}

$cases['NAME-036'] = {
    [void](Fresh @{ A1 = '5'; B1 = '=Sheet1!A1' })
    Go 'A1'; Ribbon @('h', 'o', 'r'); Keys 'Data~' 800
    Record 'NAME-036' 'renamed to Data through Home > Format > Rename Sheet' @('B1')
    Keys '^z' 900
    Record 'NAME-036' 'Ctrl+Z' @('B1') -Shot
}

$cases['CW-013'] = {
    [void](Fresh)
    Go 'B1'; Ribbon @('h', 'o', 'w'); Keys '20~' 900
    Record 'CW-013' 'column B set to 20 through Home > Format > Column Width' @('B1')
    Keys '^z' 900
    Record 'CW-013' 'Ctrl+Z' @('B1') -Shot -Extra ('StandardWidth ' + $xl.ActiveSheet.StandardWidth)
}

$cases['CW-014'] = {
    [void](Fresh)
    Go 'B1'; Ribbon @('h', 'o', 'w'); Keys '20~' 900
    Record 'CW-014' 'column B set to 20' @('B1')
    Go 'B1'; Ribbon @('h', 'd', 'c')
    Record 'CW-014' 'column B deleted through Home > Delete > Delete Sheet Columns' @('B1')
    Keys '^z' 900
    Record 'CW-014' 'Ctrl+Z' @('B1') -Shot
}

# A whole column or row is selected by a click on its header. Ctrl+Space would do it too, but the
# Japanese IME this machine types through takes Ctrl+Space for itself (2026-09-27, second run).
function Get-HeaderPoints {
    $b1 = Get-CellRect $xl 'B1'; $a2 = Get-CellRect $xl 'A2'
    $f = ($b1.Bottom - $b1.Top) / [double]$xl.ActiveSheet.Range('B1').Height
    return @{ ColumnB = @($b1.X, [int]($b1.Top - 8 * $f)); Row2 = @([int]($a2.Left - 10 * $f), $a2.Y) }
}
function ClickColumnB([switch]$Ctrl) { $h = Get-HeaderPoints; Click-At $xl $h.ColumnB[0] $h.ColumnB[1] -Ctrl:$Ctrl; Start-Sleep -Milliseconds 300 }
function ClickRow2([switch]$Ctrl) { $h = Get-HeaderPoints; Click-At $xl $h.Row2[0] $h.Row2[1] -Ctrl:$Ctrl; Start-Sleep -Milliseconds 300 }

# "0%" through Ctrl+Shift+5; "0.00" through the ribbon's number-format box set to Number.
function Percent { Keys '^+5' 700 }
function NumberFormatNumber { Ribbon @('h', 'n'); Keys 'Number~' 900 }

$cases['LVL-023'] = {
    [void](Fresh @{ B2 = '0.5' })
    Go 'B2'; Percent
    Record 'LVL-023' 'B2 formatted with Ctrl+Shift+5' @('B2')
    ClickColumnB; NumberFormatNumber
    Record 'LVL-023' 'column B (its header clicked) set to Number' @('B2', 'B5') -Extra ([string]$xl.Selection.Address($false, $false))
    Keys '^z' 900
    Record 'LVL-023' 'Ctrl+Z' @('B2', 'B5') -Shot
}

$cases['LVL-024'] = {
    [void](Fresh @{ B2 = '0.5' })
    ClickRow2; Percent
    Record 'LVL-024' 'row 2 (its header clicked) formatted with Ctrl+Shift+5' @('B2', 'E2') -Extra ([string]$xl.Selection.Address($false, $false))
    Go 'B2'; Ribbon @('h', 'd', 'r')
    Record 'LVL-024' 'row 2 deleted through Home > Delete > Delete Sheet Rows' @('B2')
    Keys '^z' 900
    Record 'LVL-024' 'Ctrl+Z' @('B2', 'E2') -Shot
}

$cases['LVL-030'] = {
    [void](Fresh @{ B2 = '0.5' })
    Go 'B2'; Percent
    Record 'LVL-030' 'B2 formatted with Ctrl+Shift+5' @('B2')
    # Column B and row 2 together: a click on B's letter, a Ctrl+click on row 2's number.
    ClickColumnB; ClickRow2 -Ctrl
    Record 'LVL-030' 'column B and row 2 selected' @() -Extra ([string]$xl.Selection.Address($false, $false))
    NumberFormatNumber
    Record 'LVL-030' 'both set to Number at once' @('B2', 'B5', 'E2')
    Keys '^z' 900
    Record 'LVL-030' 'Ctrl+Z' @('B2', 'B5', 'E2') -Shot
}

foreach ($id in $Case) {
    if (-not $cases.Contains($id)) { throw "no case $id" }
    Set-CaseDue 60
    try { & $cases[$id] }
    catch {
        $row = [ordered]@{ case = $id; step = 'failed'; error = $_.Exception.Message; line = $_.InvocationInfo.ScriptLineNumber }
        [IO.File]::AppendAllText($Log, (($row | ConvertTo-Json -Compress) + "`n"), $Utf8)
        Write-Host "case $id failed: $($_.Exception.Message) (line $($_.InvocationInfo.ScriptLineNumber))"
        if (-not $script:Guard.Ended) { for ($i = 0; $i -lt 4; $i++) { try { [void]$xl.ActiveSheet.Name; break } catch { try { Send-KeysRaw '{ESC}' 400 } catch { } } } }
    }
    Clear-CaseDue
    if ($script:Guard.Ended) {
        $row = [ordered]@{ case = $id; step = 'Excel ended'; error = $script:Guard.Ended }
        [IO.File]::AppendAllText($Log, (($row | ConvertTo-Json -Compress) + "`n"), $Utf8)
        Write-Host "case ${id}: $($script:Guard.Ended)"
        $xl = Reconnect-Excel
    }
}
