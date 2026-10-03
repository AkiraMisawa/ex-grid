<#
.SYNOPSIS
    Part B of docs/specs/exsheet/verify-on-windows-4.md: Excel's answers the implementation still
    reads, asked with real keys and the real mouse, one case at a time:

        powershell -File active-cell.ps1 -Case 0,1,2,3

    1. A take-out of the whole range made last (a Ctrl+click on F6 twice; a Ctrl+drag over D4:E5
       twice), then Shift+Down.
    2. Ctrl+Enter from a Focus that is not the top-left: =B2+$A$1 over B2:C3 from C3, and from B3.
    3. The text =A1 put on the clipboard from Notepad, pasted over B2:C3.

    The third run's method (../2026-09-28-windows-excel-3/active-cell.ps1): each step records the
    Selection, the ActiveCell, the scroll position, the visible range and the cells it names, and a
    crop of the Name Box, the Formula Bar and the cells. Steps are appended to active-cell.jsonl
    beside this script; screenshots go to shots\ (B<item>-...). Driven through
    ..\2026-09-27-windows-excel\excel-driver.ps1, with the second run's case guard.
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
    [IO.File]::AppendAllText($Log, (($Row | ConvertTo-Json -Compress -Depth 5) + "`n"), $Utf8)
}

function Snap([string]$CaseId, [string]$Step, [string]$Method, [string[]]$Cells = @(), [switch]$Shot) {
    $state = [ordered]@{ case = $CaseId; step = $Step; method = $Method }
    $read = Get-State $xl $Cells
    foreach ($p in $read.PSObject.Properties) { $state[$p.Name] = $p.Value }
    $state['visible'] = [string]$xl.ActiveWindow.VisibleRange.Address($false, $false)
    $n = $xl.Selection.Areas.Count
    $state['areas'] = (@(for ($k = 1; $k -le $n; $k++) { [string]$xl.Selection.Areas.Item($k).Address($false, $false) }) -join ' ')
    $state['shot'] = $null
    if ($Shot) {
        $name = ('B{0}-{1}' -f $CaseId, ($Step -replace '[^A-Za-z0-9]+', '-')).Trim('-')
        Save-View $xl $name ($xl.ActiveWindow.VisibleRange.Cells.Item(16, 12).Address($false, $false))
        $state.shot = "shots/$name.png"
    }
    Write-Row $state
    $shown = ($Cells | ForEach-Object { '{0}={1}' -f $_, $state[$_].formula2 }) -join ' '
    Write-Host ('{0,-3} {1,-58} sel {2,-22} active {3,-5} areas [{4}] {5}' -f $CaseId, $Step, $state.selection, $state.activeCell, $state.areas, $shown)
}

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

Add-Type -Namespace ActiveCell4 -Name Native -MemberDefinition @'
[DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder s, int n);
[DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
[DllImport("user32.dll")] public static extern bool SystemParametersInfo(uint action, uint param, ref uint value, uint winIni);
'@
function Get-FrontTitle { $sb = New-Object Text.StringBuilder 512; [void][ActiveCell4.Native]::GetWindowText([ActiveCell4.Native]::GetForegroundWindow(), $sb, 512); return $sb.ToString() }

$Block = @('B2', 'C2', 'B3', 'C3')
$cases = [ordered]@{}

$cases['0'] = {
    [uint32]$lines = 0
    [void][ActiveCell4.Native]::SystemParametersInfo(0x0068, 0, [ref]$lines, 0)   # SPI_GETWHEELSCROLLLINES
    $row = [ordered]@{ case = '0'; step = 'settings'; method = 'COM and SystemParametersInfo'
        moveAfterReturn = [bool]$xl.MoveAfterReturn; moveAfterReturnDirection = [int]$xl.MoveAfterReturnDirection
        zoom = [int]$xl.ActiveWindow.Zoom; wheelScrollLines = [int]$lines
        excel = ('{0} (build {1})' -f $xl.Version, $xl.Build) }
    Write-Row $row
    Write-Host ($row | ConvertTo-Json -Compress)
}

# 1. A take-out of the whole range made last.
$cases['1'] = {
    [void](Fresh)
    ClickCell 'A1'; ClickCell 'B2' -Shift
    ClickCell 'F6' -Ctrl
    Snap '1' 'A1:B2, Ctrl+click F6' 'mouse' -Shot
    ClickCell 'F6' -Ctrl
    Snap '1' 'A1:B2, Ctrl+click F6, Ctrl+click F6 again' 'mouse' -Shot
    Keys '+{DOWN}'
    Snap '1' 'A1:B2, F6 twice, then Shift+Down' 'keys' -Shot
    [void](Fresh)
    ClickCell 'A1'; ClickCell 'B2' -Shift
    CtrlDrag 'D4' 'E5'
    Snap '1' 'A1:B2, Ctrl+drag D4:E5' 'mouse' -Shot
    CtrlDrag 'D4' 'E5'
    Snap '1' 'A1:B2, Ctrl+drag D4:E5, Ctrl+drag D4:E5 again' 'mouse' -Shot
    Keys '+{DOWN}'
    Snap '1' 'A1:B2, D4:E5 twice, then Shift+Down' 'keys' -Shot
}

# 2. Ctrl+Enter from a Focus that is not the top-left of B2:C3.
$cases['2'] = {
    [void](Fresh)
    ClickCell 'C3'; ClickCell 'B2' -Shift
    Snap '2' 'C3, Shift+click B2' 'mouse' $Block -Shot
    Keys '=B2{+}$A$1' 300   # '+' alone is Shift in SendKeys
    Keys '^~'
    Snap '2' 'from C3: typed =B2+$A$1, Ctrl+Enter' 'keys' $Block -Shot
    [void](Fresh)
    ClickCell 'B2'; ClickCell 'C3' -Shift
    Keys '~' 350
    Snap '2' 'B2, Shift+click C3, Enter' 'keys' $Block -Shot
    Keys '=B2{+}$A$1' 300
    Keys '^~'
    Snap '2' 'from B3: typed =B2+$A$1, Ctrl+Enter' 'keys' $Block -Shot
}

# 3. The text =A1 copied from Notepad, pasted over B2:C3. Notepad opens a file this script writes,
#    so nothing is typed into Notepad and the file is closed unchanged (Ctrl+W closes only its tab).
$cases['3'] = {
    $src = Join-Path $env:LOCALAPPDATA 'exgrid-layer3\paste-source-run4.txt'
    [IO.File]::WriteAllText($src, '=A1', $Utf8)
    $before = @(Get-Process notepad -ErrorAction SilentlyContinue).Count
    Start-Process notepad.exe -ArgumentList ('"{0}"' -f $src)
    $deadline = (Get-Date).AddSeconds(15)
    while ((Get-FrontTitle) -notlike '*paste-source-run4*') {
        if ((Get-Date) -gt $deadline) { throw ('Notepad did not come to the front with the file; the front window is "{0}"' -f (Get-FrontTitle)) }
        # A process started from the background may open behind; an Alt tap lets this one hand it the foreground.
        $np = @(Get-Process notepad -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle -like '*paste-source-run4*' })
        if ($np.Count -gt 0) {
            [ActiveCell4.Native]::keybd_event(0x12, 0, 0, [UIntPtr]::Zero); [ActiveCell4.Native]::keybd_event(0x12, 0, 2, [UIntPtr]::Zero)
            [void][ActiveCell4.Native]::SetForegroundWindow($np[0].MainWindowHandle)
        }
        Start-Sleep -Milliseconds 400
    }
    Start-Sleep -Milliseconds 800
    $title = Get-FrontTitle
    [System.Windows.Forms.SendKeys]::SendWait('^a'); Start-Sleep -Milliseconds 300
    [System.Windows.Forms.SendKeys]::SendWait('^c'); Start-Sleep -Milliseconds 500
    $data = [Windows.Forms.Clipboard]::GetDataObject()
    $formats = @($data.GetFormats($false))
    $text = [Windows.Forms.Clipboard]::GetText()
    Write-Row ([ordered]@{ case = '3'; step = 'copied from Notepad'; method = 'keys (Ctrl+A, Ctrl+C in Notepad)'; notepadTitle = $title; notepadProcessesBefore = $before
        clipboardFormats = ($formats -join ', '); clipboardText = $text; clipboardTextLength = $text.Length })
    Write-Host ('3   Notepad "{0}": clipboard [{1}] "{2}"' -f $title, ($formats -join ', '), $text)
    if ((Get-FrontTitle) -like '*paste-source-run4*') { [System.Windows.Forms.SendKeys]::SendWait('^w'); Start-Sleep -Milliseconds 800 }
    Write-Row ([ordered]@{ case = '3'; step = 'Notepad tab closed'; method = 'keys (Ctrl+W)'; notepadProcessesAfter = @(Get-Process notepad -ErrorAction SilentlyContinue).Count; frontAfter = (Get-FrontTitle) })
    [void](Fresh)
    ClickCell 'B2'; ClickCell 'C3' -Shift
    Snap '3' 'B2:C3 selected' 'mouse' $Block -Shot
    Keys '^v' 900
    Snap '3' 'Ctrl+V of the text =A1 from Notepad' 'keys' $Block -Shot
    $after = [ordered]@{ case = '3'; step = 'cells after the paste'; method = 'COM' }
    foreach ($a in $Block) { $c = $xl.ActiveSheet.Range($a); $after[$a] = [ordered]@{ formula2 = [string]$c.Formula2; text = [string]$c.Text; hasFormula = [bool]$c.HasFormula } }
    Write-Row $after
}

foreach ($id in $Case) {
    if (-not $cases.Contains($id)) { throw "no case $id" }
    Set-CaseDue 180
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
