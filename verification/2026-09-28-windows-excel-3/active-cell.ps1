<#
.SYNOPSIS
    Part B of docs/specs/exsheet/verify-on-windows-3.md: what ADR-0052 still extrapolates, and
    Excel's scroll steps for ADR-0053, asked with real keys and the real mouse, one case at a time:

        powershell -File active-cell.ps1 -Case 0,1,2,3

    The second run's method (../2026-09-27-windows-excel-2/active-cell.ps1): each step records the
    Selection, the ActiveCell, the scroll position and the visible range, and a crop of the Name Box,
    the Formula Bar and the cells. Steps are appended to active-cell.jsonl beside this script;
    screenshots go to shots\. Driven through ..\2026-09-27-windows-excel\excel-driver.ps1, with the
    second run's case guard.
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
. (Join-Path $PSScriptRoot '..\2026-09-27-windows-excel-2\case-guard.ps1')
Start-CaseGuard

function Write-Row($Row) {
    [IO.File]::AppendAllText($Log, (($Row | ConvertTo-Json -Compress) + "`n"), $Utf8)
}

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
    Write-Row $state
    Write-Host ('{0,-3} {1,-52} sel {2,-24} active {3,-6} scroll {4},{5}' -f $CaseId, $Step, $state.selection, $state.activeCell, $state.scrollRow, $state.scrollColumn)
    return $state
}
function Snap-Quiet([string]$CaseId, [string]$Step, [string]$Method, [switch]$Shot) { [void](Snap $CaseId $Step $Method -Shot:$Shot) }

function Keys([string]$K, [int]$Settle = 450) { Send-Keys $xl $K $Settle }
function ClickCell([string]$A1, [switch]$Shift, [switch]$Ctrl) { $r = Get-CellRect $xl $A1; Click-At $xl $r.X $r.Y -Shift:$Shift -Ctrl:$Ctrl; Start-Sleep -Milliseconds 250 }
function Fresh([hashtable]$Cells = @{}) { $ws = Reset-Book $xl $Cells; Show-Excel $xl; Keys '{ESC}' 200; return $ws }
# A drag with Ctrl held from the start, as a hand adds a range to the Selection.
function CtrlDrag([string]$From, [string]$To) {
    $a = Get-CellRect $xl $From; $b = Get-CellRect $xl $To
    Show-Excel $xl; Hold-Key 0x11; Move-Mouse $a.X $a.Y; Press-Mouse
    for ($i = 1; $i -le 8; $i++) { Move-Mouse ([int]($a.X + ($b.X - $a.X) * $i / 8)) ([int]($a.Y + ($b.Y - $a.Y) * $i / 8)) }
    Release-Mouse; Let-Key 0x11; Start-Sleep -Milliseconds 300
}
function Test-In([string]$Cell, [string]$Range) {
    return $null -ne $xl.Intersect($xl.ActiveSheet.Range($Cell), $xl.ActiveSheet.Range($Range))
}

# The Japanese IME this machine types through takes Ctrl+Space for itself, and Excel never sees
# it. Steps that send it switch Excel's window to the English (UK) keyboard, as Win+Space would, and
# back to what it was afterwards, with the IME off (the second run's case 9).
Add-Type -Namespace ActiveCell3 -Name Native -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr w, IntPtr l);
[DllImport("user32.dll")] public static extern IntPtr GetKeyboardLayout(uint thread);
[DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
[DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
[DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
[DllImport("user32.dll")] public static extern bool SystemParametersInfo(uint action, uint param, ref uint value, uint winIni);
[DllImport("user32.dll")] public static extern int GetSystemMetrics(int index);
'@
function Get-ExcelKeyboard { [uint32]$p = 0; $t = [ActiveCell3.Native]::GetWindowThreadProcessId($script:Hwnd, [ref]$p); return [ActiveCell3.Native]::GetKeyboardLayout($t) }
function Set-ExcelKeyboard([string]$CaseId, [IntPtr]$Layout) {
    [void][ActiveCell3.Native]::PostMessage($script:Hwnd, 0x0050, [IntPtr]::Zero, $Layout)   # WM_INPUTLANGCHANGEREQUEST
    Start-Sleep -Milliseconds 600
    Write-Row ([ordered]@{ case = $CaseId; step = 'keyboard'; method = 'WM_INPUTLANGCHANGEREQUEST'; layout = ('0x{0:x8}' -f [long](Get-ExcelKeyboard)) })
}
function With-EnglishKeyboard([string]$CaseId, [scriptblock]$Body) {
    $was = Get-ExcelKeyboard
    Show-Excel $xl
    Set-ExcelKeyboard $CaseId ([IntPtr]0x08090809)
    try { & $Body }
    finally {
        Set-ExcelKeyboard $CaseId $was
        [ActiveCell3.Native]::keybd_event(0x1A, 0, 0, [UIntPtr]::Zero); [ActiveCell3.Native]::keybd_event(0x1A, 0, 2, [UIntPtr]::Zero)   # VK_IME_OFF
    }
}

$cases = [ordered]@{}

$cases['0'] = {
    # The settings Excel's Enter and wheel depend on.
    [uint32]$lines = 0
    [void][ActiveCell3.Native]::SystemParametersInfo(0x0068, 0, [ref]$lines, 0)   # SPI_GETWHEELSCROLLLINES
    $row = [ordered]@{ case = '0'; step = 'settings'; method = 'COM and SystemParametersInfo'
        moveAfterReturn = [bool]$xl.MoveAfterReturn; moveAfterReturnDirection = [int]$xl.MoveAfterReturnDirection
        zoom = [int]$xl.ActiveWindow.Zoom; wheelScrollLines = [int]$lines
        excel = ('{0} (build {1})' -f $xl.Version, $xl.Build) }
    Write-Row $row
    Write-Host ($row | ConvertTo-Json -Compress)
}

# 1. Shift+Down after Enter has cycled back into the earlier range.
$cases['1'] = {
    [void](Fresh)
    ClickCell 'A1'; ClickCell 'B2' -Shift; CtrlDrag 'D4' 'E5'
    Snap-Quiet '1' 'A1:B2, then Ctrl+drag D4:E5' 'mouse' -Shot
    for ($i = 1; $i -le 10; $i++) {
        Keys '~' 350
        $s = Snap '1' "Enter x$i" 'keys'
        if (Test-In $s.activeCell 'A1:B2') { break }
    }
    Snap-Quiet '1' 'back in A1:B2' 'keys' -Shot
    Keys '+{DOWN}'; Snap-Quiet '1' 'then Shift+Down' 'keys' -Shot
    Keys '+{RIGHT}'; Snap-Quiet '1' 'then Shift+Right' 'keys' -Shot
    # One Enter further, onto A2 (not a corner the range was made from), then Shift+Down.
    [void](Fresh)
    ClickCell 'A1'; ClickCell 'B2' -Shift; CtrlDrag 'D4' 'E5'
    for ($i = 1; $i -le 10; $i++) { Keys '~' 350; if (Test-In ([string]$xl.ActiveCell.Address($false, $false)) 'A1:B2') { break } }
    Keys '~' 350; Snap-Quiet '1' 'back in A1:B2, one Enter more' 'keys'
    Keys '+{DOWN}'; Snap-Quiet '1' 'then Shift+Down (from the second cell)' 'keys' -Shot
}

# 2. The cycling order through two disjoint ranges, made in either order.
$cases['2'] = {
    foreach ($order in @(@('A1', 'B2', 'D4', 'E5', 'A1:B2 then D4:E5'), @('D4', 'E5', 'A1', 'B2', 'D4:E5 then A1:B2'))) {
        [void](Fresh)
        ClickCell $order[0]; ClickCell $order[1] -Shift; CtrlDrag $order[2] $order[3]
        Snap-Quiet '2' ("{0}: made" -f $order[4]) 'mouse' -Shot
        for ($i = 1; $i -le 10; $i++) { Keys '~' 350; Snap-Quiet '2' ("{0}: Enter x{1}" -f $order[4], $i) 'keys' }
        for ($i = 1; $i -le 10; $i++) { Keys '{TAB}' 350; Snap-Quiet '2' ("{0}: then Tab x{1}" -f $order[4], $i) 'keys' }
        for ($i = 1; $i -le 3; $i++) { Keys '+~' 350; Snap-Quiet '2' ("{0}: then Shift+Enter x{1}" -f $order[4], $i) 'keys' }
    }
}

# 3. Ctrl+click on the active cell when it is not the range's top-left, and Ctrl+click on a cell of
#    the range that does not hold the active cell.
$cases['3'] = {
    foreach ($how in @(@('B2', '~', 4, 'Enter x4'), @('C3', '+~', 1, 'Shift+Enter'), @('C1', '~', 6, 'Enter x6'), @('A3', '~', 2, 'Enter x2'))) {
        [void](Fresh)
        ClickCell 'A1'; ClickCell 'C3' -Shift
        for ($i = 1; $i -le $how[2]; $i++) { Keys $how[1] 350 }
        Snap-Quiet '3' ("A1:C3, {0} (active {1})" -f $how[3], $how[0]) 'keys'
        ClickCell $how[0] -Ctrl
        Snap-Quiet '3' ("A1:C3 active {0}, Ctrl+click {0}" -f $how[0]) 'mouse' -Shot
        Keys '+{DOWN}'; Snap-Quiet '3' ("A1:C3 active {0}, Ctrl+click {0}, then Shift+Down" -f $how[0]) 'keys' -Shot
    }
    # Two ranges; the active cell is in the later one (D4). A Ctrl+click on a cell of A1:B2.
    foreach ($target in 'A1', 'B2') {
        [void](Fresh)
        ClickCell 'A1'; ClickCell 'B2' -Shift; CtrlDrag 'D4' 'E5'
        ClickCell $target -Ctrl
        Snap-Quiet '3' "A1:B2 and D4:E5 (active D4), Ctrl+click $target" 'mouse' -Shot
        Keys '+{DOWN}'; Snap-Quiet '3' "A1:B2 and D4:E5, Ctrl+click $target, then Shift+Down" 'keys' -Shot
    }
    # The same with the active cell moved by Enter into A1:B2, and a Ctrl+click on E5.
    [void](Fresh)
    ClickCell 'A1'; ClickCell 'B2' -Shift; CtrlDrag 'D4' 'E5'
    for ($i = 1; $i -le 10; $i++) { Keys '~' 350; if (Test-In ([string]$xl.ActiveCell.Address($false, $false)) 'A1:B2') { break } }
    Snap-Quiet '3' 'A1:B2 and D4:E5, Enter back into A1:B2' 'keys'
    ClickCell 'E5' -Ctrl
    Snap-Quiet '3' 'then Ctrl+click E5' 'mouse' -Shot
    Keys '+{DOWN}'; Snap-Quiet '3' 'then Ctrl+click E5, then Shift+Down' 'keys' -Shot
}

# 4. What the implementation read without Excel (ADR-0052, "What the implementation settled").
$cases['4'] = {
    With-EnglishKeyboard '4' {
        [void](Fresh)
        ClickCell 'C3'; Keys '^ '; Snap-Quiet '4' 'C3, Ctrl+Space' 'keys' -Shot
        Keys '+{DOWN}'; Snap-Quiet '4' 'C3, Ctrl+Space, then Shift+Down' 'keys' -Shot
        Keys '+{RIGHT}'; Snap-Quiet '4' 'then Shift+Right' 'keys' -Shot
        [void](Fresh)
        ClickCell 'C3'; Keys '^ '; Keys '+{RIGHT}'; Snap-Quiet '4' 'C3, Ctrl+Space, then Shift+Right alone' 'keys' -Shot
    }
    foreach ($how in @(@(1, 'A2'), @(4, 'B2'))) {
        [void](Fresh)
        ClickCell 'A1'; ClickCell 'C3' -Shift
        for ($i = 1; $i -le $how[0]; $i++) { Keys '~' 350 }
        Snap-Quiet '4' ("A1:C3, Enter x{0} (active {1})" -f $how[0], $how[1]) 'keys'
        for ($i = 1; $i -le 5; $i++) { Keys '^.' 350; Snap-Quiet '4' ("A1:C3 active {0}: Ctrl+. x{1}" -f $how[1], $i) 'keys' }
    }
    # A take-out: the order Selection.Address lists the fragments in, and the order Enter visits them.
    foreach ($out in 'B2', 'C3', 'A1') {
        [void](Fresh)
        ClickCell 'A1'; ClickCell 'C3' -Shift; ClickCell $out -Ctrl
        Snap-Quiet '4' "A1:C3, Ctrl+click $out (take-out)" 'mouse' -Shot
        $n = $xl.Selection.Areas.Count
        $areas = @(for ($k = 1; $k -le $n; $k++) { [string]$xl.Selection.Areas.Item($k).Address($false, $false) })
        Write-Row ([ordered]@{ case = '4'; step = "A1:C3 take-out $out, the areas in order"; method = 'COM'; areas = ($areas -join ' ') })
        Write-Host ('4   areas after taking out {0}: {1}' -f $out, ($areas -join ' '))
        for ($i = 1; $i -le 10; $i++) { Keys '~' 350; Snap-Quiet '4' "take-out $out, then Enter x$i" 'keys' }
    }
    # B4:B2 made from B4, filled down by the handle to B6.
    $ws = Fresh @{ B2 = '1'; B3 = '2'; B4 = '3' }
    ClickCell 'B4'; ClickCell 'B2' -Shift
    Snap-Quiet '4' 'B4:B2 from B4' 'mouse' -Shot
    $corner = Get-CellRect $xl 'B4'; $to = Get-CellRect $xl 'B6'
    Drag-From $xl ($corner.Right - 1) ($corner.Bottom - 1) ($corner.Right - 1) ($to.Bottom - 3) 16
    Snap-Quiet '4' 'B4:B2 from B4, filled by the handle to B6' 'mouse' -Shot
    $cells = @('B2', 'B3', 'B4', 'B5', 'B6' | ForEach-Object { '{0}={1}' -f $_, $ws.Range($_).Value2 })
    Write-Row ([ordered]@{ case = '4'; step = 'after the fill, B2:B6'; method = 'COM'; cells = ($cells -join ' ') })
    Write-Host ('4   after the fill: ' + ($cells -join ' '))
}

# 5. Scrolling, for ADR-0053: one wheel notch, one click on the arrow and one on the track, at 100%.
function Get-ScrollBarRect {
    # Excel's vertical scroll bar, as UI Automation sees it in Excel's window.
    Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
    $root = [Windows.Automation.AutomationElement]::FromHandle($script:Hwnd)
    $cond = New-Object Windows.Automation.PropertyCondition ([Windows.Automation.AutomationElement]::ControlTypeProperty), ([Windows.Automation.ControlType]::ScrollBar)
    $bars = $root.FindAll([Windows.Automation.TreeScope]::Descendants, $cond)
    foreach ($b in $bars) {
        $r = $b.Current.BoundingRectangle
        if ($r.Height -gt $r.Width * 3) {
            $parts = @{}
            foreach ($c in $b.FindAll([Windows.Automation.TreeScope]::Children, [Windows.Automation.Condition]::TrueCondition)) {
                $parts[[string]$c.Current.Name + '|' + [string]$c.Current.AutomationId] = $c.Current.BoundingRectangle
            }
            return [pscustomobject]@{ Name = $b.Current.Name; Rect = $r; Parts = $parts }
        }
    }
    return $null
}
$cases['5'] = {
    $ws = Fresh
    $was = [int]$xl.ActiveWindow.Zoom
    $xl.ActiveWindow.Zoom = 100
    [uint32]$lines = 0
    [void][ActiveCell3.Native]::SystemParametersInfo(0x0068, 0, [ref]$lines, 0)
    Write-Row ([ordered]@{ case = '5'; step = 'settings'; method = 'COM and SystemParametersInfo'; zoomBefore = $was; zoom = [int]$xl.ActiveWindow.Zoom; wheelScrollLines = [int]$lines })
    $e10 = Get-CellRect $xl 'E10'
    # One notch down, one up, over the cells.
    ClickCell 'A1'
    Snap-Quiet '5' 'empty sheet at 100%, A1' 'COM'
    Show-Excel $xl; Move-Mouse $e10.X $e10.Y
    [ActiveCell3.Native]::mouse_event(0x0800, 0, 0, [uint32]4294967176, [UIntPtr]::Zero); Start-Sleep -Milliseconds 600   # WHEEL_DELTA -120
    Snap-Quiet '5' 'one wheel notch down' 'mouse wheel' -Shot
    [ActiveCell3.Native]::mouse_event(0x0800, 0, 0, [uint32]120, [UIntPtr]::Zero); Start-Sleep -Milliseconds 600
    Snap-Quiet '5' 'one wheel notch up' 'mouse wheel'
    for ($i = 1; $i -le 3; $i++) { [ActiveCell3.Native]::mouse_event(0x0800, 0, 0, [uint32]4294967176, [UIntPtr]::Zero); Start-Sleep -Milliseconds 400 }
    Snap-Quiet '5' 'three notches down' 'mouse wheel'
    # The vertical scroll bar's arrow and track.
    $xl.ActiveWindow.ScrollRow = 1; Start-Sleep -Milliseconds 300
    $bar = Get-ScrollBarRect
    if ($null -eq $bar) { throw 'no vertical scroll bar found through UI Automation' }
    Write-Row ([ordered]@{ case = '5'; step = 'the vertical scroll bar'; method = 'UI Automation'; name = $bar.Name
        rect = ('{0},{1} {2}x{3}' -f $bar.Rect.X, $bar.Rect.Y, $bar.Rect.Width, $bar.Rect.Height)
        parts = (($bar.Parts.GetEnumerator() | ForEach-Object { '{0}: {1},{2} {3}x{4}' -f $_.Key, $_.Value.X, $_.Value.Y, $_.Value.Width, $_.Value.Height }) -join '; ') })
    $x = [int]($bar.Rect.X + $bar.Rect.Width / 2)
    $w = [int]$bar.Rect.Width
    # The down arrow is the square at the bar's bottom. The track below the thumb is the part UI
    # Automation calls "Page down": in an empty sheet the thumb fills most of the bar, so a point
    # at a fixed fraction of the bar lands on the thumb (the first attempt did).
    $arrowY = [int]($bar.Rect.Y + $bar.Rect.Height - $w / 2)
    $pageDown = $bar.Parts['Page down|']
    if ($null -eq $pageDown -or [double]::IsInfinity($pageDown.Y) -or $pageDown.Height -lt 4) { throw 'no track below the thumb to click' }
    $trackY = [int]($pageDown.Y + $pageDown.Height / 2)
    Write-Row ([ordered]@{ case = '5'; step = 'points clicked'; method = 'screen pixels'; x = $x; arrowY = $arrowY; trackY = $trackY })
    Snap-Quiet '5' 'before the arrow' 'COM'
    Click-At $xl $x $arrowY; Start-Sleep -Milliseconds 400
    Snap-Quiet '5' 'one click on the down arrow' 'mouse' -Shot
    Click-At $xl $x $arrowY; Start-Sleep -Milliseconds 400
    Snap-Quiet '5' 'a second click on the down arrow' 'mouse'
    $xl.ActiveWindow.ScrollRow = 1; Start-Sleep -Milliseconds 300
    Snap-Quiet '5' 'before the track' 'COM'
    Click-At $xl $x $trackY; Start-Sleep -Milliseconds 400
    Snap-Quiet '5' 'one click on the track below the thumb' 'mouse' -Shot
    Click-At $xl $x $trackY; Start-Sleep -Milliseconds 400
    Snap-Quiet '5' 'a second click on the track' 'mouse'
    $xl.ActiveWindow.ScrollRow = 1
    $xl.ActiveWindow.Zoom = $was
}

foreach ($id in $Case) {
    if (-not $cases.Contains($id)) { throw "no case $id" }
    Set-CaseDue 240
    try { & $cases[$id] }
    catch {
        $row = [ordered]@{ case = $id; step = 'failed'; error = $_.Exception.Message; line = $_.InvocationInfo.ScriptLineNumber }
        Write-Row $row
        Write-Host "case $id failed: $($_.Exception.Message) (line $($_.InvocationInfo.ScriptLineNumber))"
        if (-not $script:Guard.Ended) { for ($i = 0; $i -lt 3; $i++) { try { [void]$xl.Workbooks.Count; [void]$xl.ActiveSheet.Name; break } catch { try { Send-KeysRaw '{ESC}' 400 } catch { } } } }
    }
    Clear-CaseDue
    if ($script:Guard.Ended) {
        Write-Row ([ordered]@{ case = $id; step = 'Excel ended'; error = $script:Guard.Ended })
        Write-Host "case ${id}: $($script:Guard.Ended)"
        $xl = Reconnect-Excel
    }
}
