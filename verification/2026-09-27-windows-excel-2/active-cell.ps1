<#
.SYNOPSIS
    Part B of docs/specs/exsheet/verify-on-windows-2.md: Excel's active cell (ADR-0052), asked
    with real keys and the real mouse, one case at a time:

        powershell -File active-cell.ps1 -Case 1,2,3

    Each step records the Selection, the ActiveCell, the scroll position and the visible range
    (which cell Excel keeps in view), and a crop of the Name Box, the Formula Bar and the cells,
    taken during the gesture where the gesture has a "during" (a drag with the button still down,
    an extension with Shift still held). Steps are appended to active-cell.jsonl beside this
    script; screenshots go to shots\. Driven through ..\2026-09-27-windows-excel\excel-driver.ps1.
#>
#Requires -Version 5.1
param([string[]]$Case)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($Case) { $Case = @($Case | ForEach-Object { $_ -split ',' }) }

. (Join-Path $PSScriptRoot '..\2026-09-27-windows-excel\excel-driver.ps1')
$script:Shots = Join-Path $PSScriptRoot 'shots'
$Log = Join-Path $PSScriptRoot 'active-cell.jsonl'
$Utf8 = New-Object Text.UTF8Encoding $false
$xl = Connect-Excel
. (Join-Path $PSScriptRoot 'case-guard.ps1')
Start-CaseGuard

function Snap([string]$CaseId, [string]$Step, [string]$Method, [switch]$Shot, [string]$Through = 'L16') {
    $state = [ordered]@{
        case = $CaseId; step = $Step; method = $Method
        selection = [string]$xl.Selection.Address($false, $false)
        activeCell = [string]$xl.ActiveCell.Address($false, $false)
        scrollRow = [int]$xl.ActiveWindow.ScrollRow
        scrollColumn = [int]$xl.ActiveWindow.ScrollColumn
        visible = [string]$xl.ActiveWindow.VisibleRange.Address($false, $false)
        shot = $null
    }
    if ($Shot) {
        $name = ('B{0}-{1}' -f $CaseId, ($Step -replace '[^A-Za-z0-9]+', '-')).Trim('-')
        # The crop runs to the cell 16 rows and 12 columns into the view, wherever it is scrolled.
        if ($Through -eq 'L16') { $Through = $xl.ActiveWindow.VisibleRange.Cells.Item(16, 12).Address($false, $false) }
        Save-View $xl $name $Through
        $state.shot = "shots/$name.png"
    }
    [IO.File]::AppendAllText($Log, (($state | ConvertTo-Json -Compress) + "`n"), $Utf8)
    Write-Host ('{0,-3} {1,-44} sel {2,-22} active {3,-6} scroll {4},{5}' -f $CaseId, $Step, $state.selection, $state.activeCell, $state.scrollRow, $state.scrollColumn)
}

# A crop taken while nothing may be read through COM (a drag in progress, Shift held): the
# rectangles are read beforehand.
function Shot-Now($Rects, [string]$CaseId, [string]$Step) {
    $name = ('B{0}-{1}' -f $CaseId, ($Step -replace '[^A-Za-z0-9]+', '-')).Trim('-')
    Save-Crops $Rects $name
    $row = [ordered]@{ case = $CaseId; step = $Step; method = 'screenshot during the gesture'; shot = "shots/$name.png" }
    [IO.File]::AppendAllText($Log, (($row | ConvertTo-Json -Compress) + "`n"), $Utf8)
    Write-Host ('{0,-3} {1,-44} (screenshot during the gesture)' -f $CaseId, $Step)
}

function Keys([string]$K, [int]$Settle = 450) { Send-Keys $xl $K $Settle }
function ClickCell([string]$A1, [switch]$Shift, [switch]$Ctrl) { $r = Get-CellRect $xl $A1; Click-At $xl $r.X $r.Y -Shift:$Shift -Ctrl:$Ctrl; Start-Sleep -Milliseconds 250 }
function Fresh([hashtable]$Cells = @{}) { $ws = Reset-Book $xl $Cells; Show-Excel $xl; Keys '{ESC}' 200; return $ws }
# An arrow held down with Shift, as a hand does it: the extended-key flag keeps it off the keypad.
function ShiftArrow([byte]$Vk, [int]$Times) {
    for ($i = 0; $i -lt $Times; $i++) {
        [ExcelDriver.Native]::keybd_event($Vk, 0, 1, [UIntPtr]::Zero); Start-Sleep -Milliseconds 40
        [ExcelDriver.Native]::keybd_event($Vk, 0, 3, [UIntPtr]::Zero); Start-Sleep -Milliseconds 250
    }
}

$cases = [ordered]@{}

$cases['0'] = {
    # The setting Excel's Enter depends on (File > Options > Advanced).
    $row = [ordered]@{ case = '0'; step = 'settings'; method = 'COM'
        moveAfterReturn = [bool]$xl.MoveAfterReturn; moveAfterReturnDirection = [int]$xl.MoveAfterReturnDirection
        zoom = [int]$xl.ActiveWindow.Zoom; excel = ('{0} (build {1})' -f $xl.Version, $xl.Build) }
    [IO.File]::AppendAllText($Log, (($row | ConvertTo-Json -Compress) + "`n"), $Utf8)
    Write-Host ($row | ConvertTo-Json -Compress)
}

$cases['1'] = {
    [void](Fresh)
    ClickCell 'B2'; Snap '1' 'click B2' 'mouse' -Shot
    Keys '+{DOWN}'; Snap '1' 'Shift+Down' 'keys' -Shot
    Keys '+{DOWN}'; Snap '1' 'Shift+Down again' 'keys' -Shot
    Keys '+{RIGHT}'; Snap '1' 'Shift+Right' 'keys' -Shot
    # The same with Shift held throughout, and the Name Box read while it is still down.
    [void](Fresh); ClickCell 'B2'
    $rects = Get-ViewRects $xl 'L16'
    Hold-Key 0x10; ShiftArrow 0x28 2; ShiftArrow 0x27 1
    Shot-Now $rects '1' 'Shift held after Down Down Right'
    Let-Key 0x10; Start-Sleep -Milliseconds 300
    Snap '1' 'Shift released' 'keys'
    # Leaving the screen: from the cell at the bottom-right corner of the view, down, then right.
    [void](Fresh)
    $vis = $xl.ActiveWindow.VisibleRange
    $lastRow = $vis.Row + $vis.Rows.Count - 3; $lastCol = $vis.Column + $vis.Columns.Count - 3
    $start = $xl.ActiveSheet.Cells.Item($lastRow, $lastCol).Address($false, $false)
    ClickCell $start; Snap '1' "click $start (near the view's bottom-right)" 'mouse'
    for ($i = 1; $i -le 4; $i++) { Keys '+{DOWN}'; Snap '1' "Shift+Down x$i from $start" 'keys' }
    for ($i = 1; $i -le 4; $i++) { Keys '+{RIGHT}'; Snap '1' "Shift+Right x$i" 'keys' }
    for ($i = 1; $i -le 2; $i++) { Keys '+{UP}'; Snap '1' "Shift+Up x$i" 'keys' }
}

$cases['2'] = {
    [void](Fresh)
    $d5 = Get-CellRect $xl 'D5'; $b2 = Get-CellRect $xl 'B2'
    $rects = Get-ViewRects $xl 'L16'
    Show-Excel $xl
    Move-Mouse $d5.X $d5.Y; Press-Mouse
    for ($i = 1; $i -le 16; $i++) { Move-Mouse ([int]($d5.X + ($b2.X - $d5.X) * $i / 16)) ([int]($d5.Y + ($b2.Y - $d5.Y) * $i / 16)) }
    Start-Sleep -Milliseconds 300
    Shot-Now $rects '2' 'dragging D5 to B2, button down'
    Release-Mouse; Start-Sleep -Milliseconds 300
    Snap '2' 'drag D5 to B2 released' 'mouse' -Shot
    [void](Fresh)
    Show-Excel $xl
    Move-Mouse $b2.X $b2.Y; Press-Mouse
    for ($i = 1; $i -le 16; $i++) { Move-Mouse ([int]($b2.X + ($d5.X - $b2.X) * $i / 16)) ([int]($b2.Y + ($d5.Y - $b2.Y) * $i / 16)) }
    Start-Sleep -Milliseconds 300
    Shot-Now $rects '2' 'dragging B2 to D5, button down'
    Release-Mouse; Start-Sleep -Milliseconds 300
    Snap '2' 'drag B2 to D5 released' 'mouse' -Shot
}

$cases['3'] = {
    [void](Fresh)
    ClickCell 'A1'; ClickCell 'C3' -Shift; Snap '3' 'A1:C3 by click, Shift+click' 'mouse' -Shot
    Keys '~'; Snap '3' 'Enter' 'keys' -Shot
    Keys '~'; Snap '3' 'Enter again' 'keys' -Shot
    Keys '+{RIGHT}'; Snap '3' 'then Shift+Right' 'keys' -Shot
    Keys '+{DOWN}'; Snap '3' 'then Shift+Down' 'keys' -Shot
    # From the range's last row: Enter twice more, then Shift+Up.
    [void](Fresh)
    ClickCell 'A1'; ClickCell 'C3' -Shift; Keys '~'; Keys '~'; Keys '~'
    Snap '3' 'A1:C3, Enter x3' 'keys'
    Keys '+{UP}'; Snap '3' 'then Shift+Up' 'keys' -Shot
    [void](Fresh)
    ClickCell 'A1'; ClickCell 'C3' -Shift; Keys '{TAB}'; Keys '~'
    Snap '3' 'A1:C3, Tab then Enter (active B2)' 'keys'
    Keys '+{LEFT}'; Snap '3' 'then Shift+Left' 'keys' -Shot
}

$cases['4'] = {
    foreach ($k in @(@('{TAB}', 'Tab'), @('+{TAB}', 'Shift+Tab'), @('~', 'Enter'), @('+~', 'Shift+Enter'))) {
        [void](Fresh)
        ClickCell 'A1'; ClickCell 'C3' -Shift
        for ($i = 1; $i -le 10; $i++) { Keys $k[0] 350; Snap '4' ('{0} x{1}' -f $k[1], $i) 'keys' }
    }
    # Starting from the range's other corner (Shift+click from C3 to A1): the cycle's start.
    [void](Fresh)
    ClickCell 'C3'; ClickCell 'A1' -Shift; Snap '4' 'C3:A1 by click C3, Shift+click A1' 'mouse'
    for ($i = 1; $i -le 4; $i++) { Keys '~' 350; Snap '4' ('from C3: Enter x{0}' -f $i) 'keys' }
}

$cases['5'] = {
    [void](Fresh)
    ClickCell 'A1'; ClickCell 'B2' -Shift; ClickCell 'D5' -Ctrl
    Snap '5' 'A1:B2, Ctrl+click D5' 'mouse' -Shot
    Keys '+{DOWN}'; Snap '5' 'then Shift+Down' 'keys' -Shot
    # The other way: Ctrl+click D5 first, then Ctrl+drag A1:B2.
    [void](Fresh)
    ClickCell 'D5'
    $a1 = Get-CellRect $xl 'A1'; $b2 = Get-CellRect $xl 'B2'
    Show-Excel $xl; Hold-Key 0x11; Move-Mouse $a1.X $a1.Y; Press-Mouse
    for ($i = 1; $i -le 8; $i++) { Move-Mouse ([int]($a1.X + ($b2.X - $a1.X) * $i / 8)) ([int]($a1.Y + ($b2.Y - $a1.Y) * $i / 8)) }
    Release-Mouse; Let-Key 0x11; Start-Sleep -Milliseconds 300
    Snap '5' 'D5, then Ctrl+drag A1:B2' 'mouse' -Shot
    Keys '+{DOWN}'; Snap '5' 'then Shift+Down' 'keys' -Shot
}

$cases['6'] = {
    [void](Fresh)
    ClickCell 'A1'; ClickCell 'C3' -Shift; ClickCell 'B2' -Ctrl
    Snap '6' 'A1:C3, Ctrl+click B2 (selected)' 'mouse' -Shot
    Keys '+{DOWN}'; Snap '6' 'then Shift+Down' 'keys' -Shot
    [void](Fresh)
    ClickCell 'A1'; ClickCell 'C3' -Shift; ClickCell 'A1' -Ctrl
    Snap '6' 'A1:C3, Ctrl+click A1 (the active cell)' 'mouse' -Shot
    Keys '+{DOWN}'; Snap '6' 'then Shift+Down' 'keys' -Shot
    [void](Fresh)
    ClickCell 'B2'; ClickCell 'B2' -Ctrl
    Snap '6' 'B2 alone, Ctrl+click B2' 'mouse' -Shot
}

$cases['7'] = {
    [void](Fresh)
    ClickCell 'B2'; ClickCell 'D4' -Shift; Snap '7' 'B2:D4' 'mouse'
    Keys '+{BACKSPACE}'; Snap '7' 'Shift+Backspace' 'keys' -Shot
    [void](Fresh)
    ClickCell 'D4'; ClickCell 'B2' -Shift; Snap '7' 'D4:B2 (active D4)' 'mouse'
    Keys '+{BACKSPACE}'; Snap '7' 'Shift+Backspace' 'keys'
    [void](Fresh)
    ClickCell 'B2'; ClickCell 'D4' -Shift
    $xl.ActiveWindow.ScrollRow = 300; $xl.ActiveWindow.ScrollColumn = 20; Start-Sleep -Milliseconds 300
    Snap '7' 'B2:D4, scrolled to row 300, column T' 'COM (scroll)'
    Keys '^{BACKSPACE}'; Snap '7' 'Ctrl+Backspace' 'keys' -Shot
    # After Enter has moved the active cell inside the range.
    [void](Fresh)
    ClickCell 'B2'; ClickCell 'D4' -Shift; Keys '~'; Keys '~'
    $xl.ActiveWindow.ScrollRow = 300; Start-Sleep -Milliseconds 300
    Snap '7' 'B2:D4, Enter x2, scrolled to row 300' 'keys'
    Keys '^{BACKSPACE}'; Snap '7' 'Ctrl+Backspace' 'keys'
}

$cases['8'] = {
    [void](Fresh)
    ClickCell 'A1'; ClickCell 'C3' -Shift
    for ($i = 1; $i -le 5; $i++) { Keys '^.' 350; Snap '8' "Ctrl+. x$i" 'keys' }
    [void](Fresh)
    ClickCell 'A1'; ClickCell 'C3' -Shift; ClickCell 'E5' -Ctrl; ClickCell 'F6' -Shift
    Snap '8' 'A1:C3 and E5:F6' 'mouse'
    for ($i = 1; $i -le 9; $i++) { Keys '^.' 350; Snap '8' "two ranges: Ctrl+. x$i" 'keys' }
}

# The Japanese IME this machine types through takes Ctrl+Space for itself, and Excel never sees
# it. For case 9 Excel's window is switched to the English (UK) keyboard, as Win+Space would, and
# back to what it was afterwards, with the IME off.
Add-Type -Namespace ActiveCell -Name Keyboard -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr w, IntPtr l);
[DllImport("user32.dll")] public static extern IntPtr GetKeyboardLayout(uint thread);
[DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
[DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
'@
function Get-ExcelKeyboard { [uint32]$p = 0; $t = [ActiveCell.Keyboard]::GetWindowThreadProcessId($script:Hwnd, [ref]$p); return [ActiveCell.Keyboard]::GetKeyboardLayout($t) }
function Set-ExcelKeyboard([IntPtr]$Layout) {
    [void][ActiveCell.Keyboard]::PostMessage($script:Hwnd, 0x0050, [IntPtr]::Zero, $Layout)   # WM_INPUTLANGCHANGEREQUEST
    Start-Sleep -Milliseconds 600
    $row = [ordered]@{ case = '9'; step = 'keyboard'; method = 'WM_INPUTLANGCHANGEREQUEST'; layout = ('0x{0:x8}' -f [long](Get-ExcelKeyboard)) }
    [IO.File]::AppendAllText($Log, (($row | ConvertTo-Json -Compress) + "`n"), $Utf8)
    Write-Host ($row | ConvertTo-Json -Compress)
}

$cases['9'] = {
    $was = Get-ExcelKeyboard
    Show-Excel $xl
    Set-ExcelKeyboard ([IntPtr]0x08090809)
    try { & $case9 }
    finally {
        Set-ExcelKeyboard $was
        [ActiveCell.Keyboard]::keybd_event(0x1A, 0, 0, [UIntPtr]::Zero); [ActiveCell.Keyboard]::keybd_event(0x1A, 0, 2, [UIntPtr]::Zero)   # VK_IME_OFF
    }
}
$case9 = {
    [void](Fresh)
    ClickCell 'C3'; Keys '^ '; Snap '9' 'C3, Ctrl+Space' 'keys' -Shot
    [void](Fresh)
    ClickCell 'C3'; Keys '+ '; Snap '9' 'C3, Shift+Space' 'keys' -Shot
    [void](Fresh)
    ClickCell 'B2'; ClickCell 'D4' -Shift; Keys '~'
    Snap '9' 'B2:D4, Enter (active B3)' 'keys'
    Keys '^ '; Snap '9' 'then Ctrl+Space' 'keys' -Shot
    [void](Fresh)
    ClickCell 'B2'; ClickCell 'D4' -Shift; Keys '~'
    Keys '+ '; Snap '9' 'B2:D4, Enter, then Shift+Space' 'keys' -Shot
}

$cases['10'] = {
    $cells = @{}
    foreach ($c in 'A', 'B', 'C', 'D') { foreach ($r in 1..5) { $cells["$c$r"] = "$c$r" } }
    [void](Fresh $cells)
    ClickCell 'C3'; Keys '^a'; Snap '10' 'C3 in A1:D5 data, Ctrl+A' 'keys' -Shot
    Keys '^a'; Snap '10' 'Ctrl+A again' 'keys' -Shot
    [void](Fresh $cells)
    ClickCell 'G8'; Keys '^a'; Snap '10' 'G8 outside the data, Ctrl+A' 'keys'
}

$cases['11'] = {
    $cells = @{}
    foreach ($c in 'A', 'B', 'C', 'D', 'E') { foreach ($r in 1..10) { $cells["$c$r"] = '1' } }
    [void](Fresh $cells)
    ClickCell 'B2'; Keys '^+{END}'; Snap '11' 'B2, Ctrl+Shift+End (data A1:E10)' 'keys' -Shot
    [void](Fresh $cells)
    ClickCell 'B2'; Keys '^+{HOME}'; Snap '11' 'B2, Ctrl+Shift+Home' 'keys' -Shot
    [void](Fresh $cells)
    ClickCell 'B2'; Keys '+{PGDN}'; Snap '11' 'B2, Shift+PageDown' 'keys' -Shot
    Keys '+{PGDN}'; Snap '11' 'Shift+PageDown again' 'keys'
    Keys '+{PGUP}'; Snap '11' 'then Shift+PageUp' 'keys'
}

$cases['12'] = {
    $ws = Fresh
    ClickCell 'B2'; ClickCell 'D4' -Shift
    Keys 'x' 400; Keys '~'
    Snap '12' 'B2:D4, typed x, Enter' 'keys' -Shot
    $filled = @($ws.Range('A1:E5').Cells | Where-Object { $null -ne $_.Value2 } | ForEach-Object { $_.Address($false, $false) + '=' + $_.Value2 })
    [IO.File]::AppendAllText($Log, ((([ordered]@{ case = '12'; step = 'cells holding a value'; cells = ($filled -join ' ') }) | ConvertTo-Json -Compress) + "`n"), $Utf8)
    Write-Host ('12  holding a value: ' + ($filled -join ' '))
    Keys 'y' 400; Keys '~'; Keys 'z' 400; Keys '~'
    Snap '12' 'then y Enter, z Enter' 'keys'
    $filled = @($ws.Range('A1:E5').Cells | Where-Object { $null -ne $_.Value2 } | ForEach-Object { $_.Address($false, $false) + '=' + $_.Value2 })
    [IO.File]::AppendAllText($Log, ((([ordered]@{ case = '12'; step = 'cells holding a value after y and z'; cells = ($filled -join ' ') }) | ConvertTo-Json -Compress) + "`n"), $Utf8)
    Write-Host ('12  holding a value: ' + ($filled -join ' '))
}

$cases['13'] = {
    $ws = Fresh
    ClickCell 'B2'; ClickCell 'D4' -Shift
    Keys 'x' 400; Keys '^~' 600
    Snap '13' 'B2:D4, typed x, Ctrl+Enter' 'keys' -Shot
    $filled = @($ws.Range('A1:E5').Cells | Where-Object { $null -ne $_.Value2 } | ForEach-Object { $_.Address($false, $false) })
    [IO.File]::AppendAllText($Log, ((([ordered]@{ case = '13'; step = 'cells holding x'; cells = ($filled -join ' ') }) | ConvertTo-Json -Compress) + "`n"), $Utf8)
    Write-Host ('13  holding x: ' + ($filled -join ' '))
}

$cases['14'] = {
    $cells = @{}
    foreach ($c in 'A', 'B', 'C', 'D', 'E') { foreach ($r in 1..5) { $cells["$c$r"] = '1' } }
    $ws = Fresh $cells
    ClickCell 'B2'; ClickCell 'D4' -Shift
    Keys '{DEL}' 600
    Snap '14' 'B2:D4 of a filled A1:E5, Delete' 'keys' -Shot
    $empty = @($ws.Range('A1:E5').Cells | Where-Object { $null -eq $_.Value2 } | ForEach-Object { $_.Address($false, $false) })
    [IO.File]::AppendAllText($Log, ((([ordered]@{ case = '14'; step = 'cells cleared'; cells = ($empty -join ' ') }) | ConvertTo-Json -Compress) + "`n"), $Utf8)
    Write-Host ('14  cleared: ' + ($empty -join ' '))
}

$cases['15'] = {
    $cells = @{}
    foreach ($c in 'A', 'B', 'C') { foreach ($r in 1..3) { $cells["$c$r"] = "$c$r" } }
    [void](Fresh $cells)
    ClickCell 'A1'; ClickCell 'C3' -Shift; Keys '^c'
    ClickCell 'F6'; Keys '^v' 800
    Snap '15' 'A1:C3 copied, pasted onto F6' 'keys' -Shot
    Keys '{ESC}' 200
    # Pasted onto a cell near the view's bottom, so the block leaves the screen.
    [void](Fresh $cells)
    $vis = $xl.ActiveWindow.VisibleRange
    $low = $xl.ActiveSheet.Cells.Item($vis.Row + $vis.Rows.Count - 3, 6).Address($false, $false)
    ClickCell 'A1'; ClickCell 'C3' -Shift; Keys '^c'
    ClickCell $low; Keys '^v' 800
    Snap '15' "A1:C3 pasted onto $low (near the bottom)" 'keys'
    Keys '{ESC}' 200
    # Rows inserted over a selected range (Home > Insert > Insert Sheet Rows).
    [void](Fresh @{ B3 = '3'; B4 = '4' })
    ClickCell 'B3'; ClickCell 'C4' -Shift
    Keys '%' 600; Keys 'h' 600; Keys 'i' 700; Keys 'r' 900
    Snap '15' 'B3:C4, Insert Sheet Rows' 'keys' -Shot
    [void](Fresh @{ B3 = '3'; B4 = '4' })
    ClickCell 'C4'; ClickCell 'B3' -Shift
    Keys '%' 600; Keys 'h' 600; Keys 'i' 700; Keys 'r' 900
    Snap '15' 'C4:B3 (active C4), Insert Sheet Rows' 'keys'
}

foreach ($id in $Case) {
    if (-not $cases.Contains($id)) { throw "no case $id" }
    Set-CaseDue 120
    try { & $cases[$id] }
    catch {
        $row = [ordered]@{ case = $id; step = 'failed'; error = $_.Exception.Message; line = $_.InvocationInfo.ScriptLineNumber }
        [IO.File]::AppendAllText($Log, (($row | ConvertTo-Json -Compress) + "`n"), $Utf8)
        Write-Host "case $id failed: $($_.Exception.Message) (line $($_.InvocationInfo.ScriptLineNumber))"
        # Leave Excel Ready for the next case.
        if (-not $script:Guard.Ended) { for ($i = 0; $i -lt 3; $i++) { try { [void]$xl.ActiveSheet.Name; break } catch { try { Send-KeysRaw '{ESC}' 400 } catch { } } } }
    }
    Clear-CaseDue
    if ($script:Guard.Ended) {
        $row = [ordered]@{ case = $id; step = 'Excel ended'; error = $script:Guard.Ended }
        [IO.File]::AppendAllText($Log, (($row | ConvertTo-Json -Compress) + "`n"), $Utf8)
        Write-Host "case ${id}: $($script:Guard.Ended)"
        $xl = Reconnect-Excel
    }
}
