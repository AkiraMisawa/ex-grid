<#
.SYNOPSIS
    The third run's script (../2026-09-28-windows-excel-3/main-keys.ps1), copied unchanged for Part C
    of docs/specs/exsheet/verify-on-windows-4.md, which runs its case 5 (Ctrl+Enter) only. Part C of
    verify-on-windows-3.md, "What main brought, beside Excel": Excel's side of Delete and Ctrl+Z,
    Backspace, Ctrl+D, Ctrl+R, Ctrl+Enter and Ctrl+F, asked with real keys and the real mouse, one
    case at a time:

        powershell -File main-keys.ps1 -Case 1,2,3,4,5,6

    Each case starts from the cells /sheet opens with (SheetPage.razor's Opening), so that Excel and
    ExSheet start alike: the Linked Table's three Formulas (B11:B13) are left out, since Excel has no
    such table, and B7's date goes in as its serial with the format m/d/yyyy. Each step records the
    Selection, the ActiveCell, the scroll position and the cells it names; steps go to
    main-keys.jsonl beside this script and screenshots to shots\ (C-M<case>-...). Driven through
    ..\2026-09-27-windows-excel\excel-driver.ps1 with the second run's case guard, as active-cell.ps1.
#>
#Requires -Version 5.1
param([string[]]$Case)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($Case) { $Case = @($Case | ForEach-Object { $_ -split ',' }) }

. (Join-Path $PSScriptRoot '..\2026-09-27-windows-excel\excel-driver.ps1')
$script:Shots = Join-Path $PSScriptRoot 'shots'
$Log = Join-Path $PSScriptRoot 'main-keys.jsonl'
$Utf8 = New-Object Text.UTF8Encoding $false
$xl = Connect-Excel
. (Join-Path $PSScriptRoot '..\2026-09-27-windows-excel-2\case-guard.ps1')
Start-CaseGuard

function Write-Row($Row) {
    [IO.File]::AppendAllText($Log, (($Row | ConvertTo-Json -Compress -Depth 5) + "`n"), $Utf8)
}

function Shot-Name([string]$CaseId, [string]$Step) { return ('C-M{0}-{1}' -f $CaseId, ($Step -replace '[^A-Za-z0-9]+', '-')).Trim('-') }

function Snap([string]$CaseId, [string]$Step, [string]$Method, [string[]]$Cells = @(), [switch]$Shot) {
    $state = [ordered]@{ case = $CaseId; step = $Step; method = $Method }
    $read = Get-State $xl $Cells
    foreach ($p in $read.PSObject.Properties) { $state[$p.Name] = $p.Value }
    $state['visible'] = [string]$xl.ActiveWindow.VisibleRange.Address($false, $false)
    $state['shot'] = $null
    if ($Shot) {
        $name = Shot-Name $CaseId $Step
        Save-View $xl $name ($xl.ActiveWindow.VisibleRange.Cells.Item(16, 8).Address($false, $false))
        $state.shot = "shots/$name.png"
    }
    Write-Row $state
    $shown = ($Cells | ForEach-Object { '{0}={1}' -f $_, $state[$_].text }) -join ' '
    Write-Host ('M{0,-2} {1,-44} sel {2,-12} active {3,-6} scroll {4} {5}' -f $CaseId, $Step, $state.selection, $state.activeCell, $state.scrollRow, $shown)
}

function Keys([string]$K, [int]$Settle = 450) { Send-Keys $xl $K $Settle }
function ClickCell([string]$A1, [switch]$Shift, [switch]$Ctrl) { $r = Get-CellRect $xl $A1; Click-At $xl $r.X $r.Y -Shift:$Shift -Ctrl:$Ctrl; Start-Sleep -Milliseconds 250 }

# What /sheet opens with, less the Linked Table's Formulas; B7 is set after, as a serial.
$Opening = @{
    A1 = 'Item'; B1 = 'Qty'; C1 = 'Price'; D1 = 'Amount'
    A2 = 'Apples'; B2 = '12'; C2 = '0.5'; D2 = '=B2*C2'
    A3 = 'Pears'; B3 = '7'; C3 = '0.75'; D3 = '=B3*C3'
    A4 = 'Plums'; B4 = '20'; C4 = '0.2'; D4 = '=B4*C4'
    A5 = 'Total'; B5 = '=SUM(B2:B4)'; D5 = '=SUM(D2:D4)'
    A7 = 'As of'; A8 = 'Due'; B8 = '=B7+30'
    A10 = 'Positions'; A11 = 'Total PV'; A12 = 'PV of R-4471'; A13 = 'Count'
}
function Fresh([hashtable]$More = @{}) {
    $cells = $Opening.Clone()
    foreach ($k in $More.Keys) { $cells[$k] = $More[$k] }
    $ws = Reset-Book $xl $cells
    Set-ComProperty $ws.Range('B7') 'Value2' ([double]46291)
    Set-ComProperty $ws.Range('B7') 'NumberFormat' 'm/d/yyyy'
    Show-Excel $xl; Keys '{ESC}' 200
    return $ws
}

$Block = @('B2', 'C2', 'D2', 'B3', 'C3', 'D3', 'B4', 'C4', 'D4', 'B5', 'D5')
$cases = [ordered]@{}

# 1. Delete over B2:D4, then Ctrl+Z.
$cases['1'] = {
    [void](Fresh)
    ClickCell 'B2'; ClickCell 'D4' -Shift
    Snap '1' 'B2:D4 selected' 'mouse' $Block -Shot
    Keys '{DEL}'
    Snap '1' 'Delete' 'keys' $Block -Shot
    Keys '^z'
    Snap '1' 'then Ctrl+Z' 'keys' $Block -Shot
}

# 2. Backspace on a filled cell: Excel is in Edit mode after it, where COM is refused, so the
#    moment after the key is a screenshot only; Enter then commits.
$cases['2'] = {
    [void](Fresh)
    ClickCell 'B3'
    Snap '2' 'B3 selected' 'mouse' @('B3', 'B5') -Shot
    $rects = Get-ViewRects $xl ($xl.ActiveWindow.VisibleRange.Cells.Item(16, 8).Address($false, $false))
    Keys '{BS}' 600
    $name = Shot-Name '2' 'Backspace, before Enter'
    Save-Crops $rects $name
    Write-Row ([ordered]@{ case = '2'; step = 'Backspace, before Enter'; method = 'keys'; shot = "shots/$name.png"; note = 'Excel in Edit mode: COM is refused, the screenshot is the record' })
    Write-Host "M2  Backspace: screenshot $name"
    Keys '~'
    Snap '2' 'then Enter' 'keys' @('B3', 'B5') -Shot
}

# 3. Ctrl+D over B2:B5 where B2 holds =A2*2 (A2:A5 typed as 1..4 over the labels).
$cases['3'] = {
    [void](Fresh)
    ClickCell 'A2'; Keys '1~2~3~4~'
    ClickCell 'B2'; Keys '=A2*2~'
    ClickCell 'B2'; ClickCell 'B5' -Shift
    Snap '3' 'B2:B5 selected, B2 =A2*2' 'mouse' @('A2', 'A3', 'A4', 'A5', 'B2', 'B3', 'B4', 'B5') -Shot
    Keys '^d'
    Snap '3' 'Ctrl+D' 'keys' @('A2', 'A3', 'A4', 'A5', 'B2', 'B3', 'B4', 'B5') -Shot
}

# 4. Ctrl+R over B2:E2 where B2 holds =B1+1 (B1:E1 typed as 10..40 over the headings).
$cases['4'] = {
    [void](Fresh)
    ClickCell 'B1'; Keys '10{TAB}20{TAB}30{TAB}40~'
    ClickCell 'B2'; Keys '=B1{+}1~'   # '+' alone is Shift in SendKeys
    ClickCell 'B2'; ClickCell 'E2' -Shift
    Snap '4' 'B2:E2 selected, B2 =B1+1' 'mouse' @('B1', 'C1', 'D1', 'E1', 'B2', 'C2', 'D2', 'E2') -Shot
    Keys '^r'
    Snap '4' 'Ctrl+R' 'keys' @('B1', 'C1', 'D1', 'E1', 'B2', 'C2', 'D2', 'E2') -Shot
}

# 5. Ctrl+Enter with =A1 over B2:C3.
$cases['5'] = {
    [void](Fresh)
    ClickCell 'B2'; ClickCell 'C3' -Shift
    Snap '5' 'B2:C3 selected' 'mouse' @('B2', 'C2', 'B3', 'C3') -Shot
    Keys '=A1' 300
    Keys '^~'
    Snap '5' 'typed =A1, Ctrl+Enter' 'keys' @('A1', 'B1', 'A2', 'B2', 'C2', 'B3', 'C3') -Shot
}

# 6. Ctrl+F for a value only in a row far out of view (A5000), from A1. The Find and Replace
#    dialog is Excel's own: while it is up, COM is not asked anything; the screenshot is the record.
$cases['6'] = {
    [void](Fresh @{ A5000 = 'needle' })
    Keys '^{HOME}'
    Snap '6' 'A5000 holds needle; Ctrl+Home' 'keys' @('A5000') -Shot
    Keys '^f' 900
    Keys 'needle' 300
    Keys '~' 900
    $name = Shot-Name '6' 'Ctrl+F, needle, Enter (dialog up)'
    [void](Save-Shot $xl $name)
    Write-Row ([ordered]@{ case = '6'; step = 'Ctrl+F, needle, Enter (dialog up)'; method = 'keys'; shot = "shots/$name.png" })
    Write-Host "M6  Find: screenshot $name"
    Keys '{ESC}' 600
    Snap '6' 'then Escape' 'keys' @('A5000') -Shot
}

foreach ($id in $Case) {
    if (-not $cases.Contains($id)) { throw "no case $id" }
    Set-CaseDue 150
    try { & $cases[$id] }
    catch {
        $row = [ordered]@{ case = $id; step = 'failed'; error = $_.Exception.Message; line = $_.InvocationInfo.ScriptLineNumber; stack = $_.ScriptStackTrace }
        Write-Row $row
        Write-Host "case $id failed: $($_.Exception.Message) (line $($_.InvocationInfo.ScriptLineNumber))`n$($_.ScriptStackTrace)"
        if (-not $script:Guard.Ended) { for ($i = 0; $i -lt 3; $i++) { try { [void]$xl.Workbooks.Count; [void]$xl.ActiveSheet.Name; break } catch { try { Send-KeysRaw '{ESC}' 400 } catch { } } } }
    }
    Clear-CaseDue
    if ($script:Guard.Ended) {
        Write-Row ([ordered]@{ case = $id; step = 'Excel ended'; error = $script:Guard.Ended })
        Write-Host "case ${id}: $($script:Guard.Ended)"
        $xl = Reconnect-Excel
    }
}
