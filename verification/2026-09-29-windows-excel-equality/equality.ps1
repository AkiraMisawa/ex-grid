<#
.SYNOPSIS
    docs/specs/exsheet/verify-equality-on-windows.md, asked with real keys:

        powershell -File equality.ps1 -Case typed          ARITH-136..149, each Formula typed
        powershell -File equality.ps1 -Case byhand         the three the procedure asks by hand
        powershell -File equality.ps1 -Case paste          =A1 from Notepad over B2:C3, made from C3

    Why this script: under the machine's own en-GB, oracle.ps1 at the verified commit reports every
    case without a "culture" as blocked ("needs en-US"), and with -Keys it types a Formula only
    under en-US. So the Formulas are typed here, as the fourth run's Part B typed its own.

    typed / byhand: in one fresh sheet, a row per Formula. Column B gets the Formula typed with real
    keys, one Unicode character at a time through SendInput, as oracle.ps1 -Keys types, and
    committed with Enter; once Excel is Ready, COM reads B's Value2,
    Text and Formula2. Then, through COM only, C and D get the two sides of the "=" (Formula2), and
    their Value2 is read with round-trip precision and their Text at a width of 26 characters. A is
    the case's label. Nothing else is in the sheet.

    paste: the fourth run's Part B item 3 (../2026-09-29-windows-excel-4/active-cell.ps1), with the
    range made from C3: click C3, Shift+click B2, Ctrl+V. Only the Notepad tab this script opened is
    closed. No window title other than this file's and Excel's is written down.

    Rows go to equality.jsonl beside this script; crops of the Name Box, the Formula Bar and the
    cells to shots\. Driven through ..\2026-09-27-windows-excel\excel-driver.ps1, with the second
    run's case guard.
#>
#Requires -Version 5.1
param([string[]]$Case)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($Case) { $Case = @($Case | ForEach-Object { $_ -split ',' }) }

. (Join-Path $PSScriptRoot '..\2026-09-27-windows-excel\excel-driver.ps1')
$script:Shots = Join-Path $PSScriptRoot 'shots'
$Log = Join-Path $PSScriptRoot 'equality.jsonl'
$Utf8 = New-Object Text.UTF8Encoding $false
$xl = Connect-Excel
. (Join-Path $PSScriptRoot '..\2026-09-27-windows-excel-2\case-guard.ps1')
Start-CaseGuard

function Write-Row($Row) {
    [IO.File]::AppendAllText($Log, (($Row | ConvertTo-Json -Compress -Depth 5) + "`n"), $Utf8)
}
function Keys([string]$K, [int]$Settle = 450) { Send-Keys $xl $K $Settle }
function ClickCell([string]$A1, [switch]$Shift) { $r = Get-CellRect $xl $A1; Click-At $xl $r.X $r.Y -Shift:$Shift; Start-Sleep -Milliseconds 250 }
function Fresh { $ws = Reset-Book $xl; Show-Excel $xl; Keys '{ESC}' 200; return $ws }
function Wait-Ready { $until = (Get-Date).AddSeconds(10); while (-not $xl.Ready) { if ((Get-Date) -gt $until) { throw 'Excel not Ready after 10 s' }; Start-Sleep -Milliseconds 100 } }
function Read-Cell($Range) {
    $v = $Range.Value2
    return [ordered]@{
        value2 = if ($v -is [double]) { $v.ToString('R', $script:Invariant) } else { $v }
        value2Type = if ($null -eq $v) { 'empty' } else { $v.GetType().Name }
        text = [string]$Range.Text; formula2 = [string]$Range.Formula2; hasFormula = [bool]$Range.HasFormula
    }
}
# Keyboard input as oracle.ps1 -Keys sends it: SendInput, one Unicode character at a time.
Add-Type -Namespace EqualityKeys -Name Native -MemberDefinition @'
[StructLayout(LayoutKind.Sequential)] public struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
[StructLayout(LayoutKind.Sequential)] public struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
[StructLayout(LayoutKind.Explicit)] public struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
[StructLayout(LayoutKind.Sequential)] public struct INPUT { public uint type; public InputUnion U; }
[DllImport("user32.dll", SetLastError = true)] public static extern uint SendInput(uint n, INPUT[] inputs, int size);
public static void Key(ushort vk, bool up) {
    var i = new INPUT[1]; i[0].type = 1; i[0].U.ki.wVk = vk; i[0].U.ki.dwFlags = up ? 2u : 0u;
    SendInput(1, i, Marshal.SizeOf(typeof(INPUT)));
}
public static void Char(char c) {
    var i = new INPUT[2];
    i[0].type = 1; i[0].U.ki.wScan = c; i[0].U.ki.dwFlags = 4;
    i[1].type = 1; i[1].U.ki.wScan = c; i[1].U.ki.dwFlags = 4 | 2;
    SendInput(2, i, Marshal.SizeOf(typeof(INPUT)));
}
'@
function Type-Entry([string]$Text) {
    Show-Excel $xl
    foreach ($c in $Text.ToCharArray()) { [EqualityKeys.Native]::Char($c); Start-Sleep -Milliseconds 15 }
    Start-Sleep -Milliseconds 250
    Show-Excel $xl
    [EqualityKeys.Native]::Key(0x0D, $false); [EqualityKeys.Native]::Key(0x0D, $true)
    Start-Sleep -Milliseconds 500
}

# A Formula of the form =<left>=<right>, typed into B<row>; its two sides through COM beside it.
function Ask-Typed([string]$Label, [string]$Formula, [int]$Row) {
    $ws = $xl.ActiveSheet
    $ws.Range("A$Row").Value2 = $Label
    [void]$ws.Range("B$Row").Select()
    Type-Entry $Formula
    Wait-Ready
    $check = Read-Cell $ws.Range("B$Row")
    $body = $Formula.Substring(1)
    $eq = $body.IndexOf('=')
    $left = '=' + $body.Substring(0, $eq); $right = '=' + $body.Substring($eq + 1)
    $ws.Range("C$Row").Formula2 = $left
    $ws.Range("D$Row").Formula2 = $right
    $rec = [ordered]@{
        case = $Label; typed = $Formula; method = 'keys (SendInput, one Unicode character at a time), Enter; read through COM'
        cell = "B$Row"; answer = $check
        left = [ordered]@{ formula = $left; cell = "C$Row"; read = (Read-Cell $ws.Range("C$Row")) }
        right = [ordered]@{ formula = $right; cell = "D$Row"; read = (Read-Cell $ws.Range("D$Row")) }
        selectionAfter = [string]$xl.Selection.Address($false, $false)
    }
    Write-Row $rec
    Write-Host ('{0,-10} {1,-24} -> {2,-6} ({3}; stored {4})   left {5} [{6}]   right {7} [{8}]' -f $Label, $Formula, $check.value2, $check.value2Type, $check.formula2, $rec.left.read.value2, $rec.left.read.text, $rec.right.read.value2, $rec.right.read.text)
}

function Prepare-Sheet {
    $ws = Fresh
    $ws.Range('C:D').ColumnWidth = 26
    $ws.Range('A:A').ColumnWidth = 12
    $ws.Range('B:B').ColumnWidth = 10
    return $ws
}

$cases = [ordered]@{}

$cases['typed'] = {
    $row = [ordered]@{ case = 'settings'; method = 'COM'; excel = ('{0} (build {1})' -f $xl.Version, $xl.Build)
        regionalFormat = (Get-Culture).Name; decimalSeparator = [string]$xl.International(3); listSeparator = [string]$xl.International(5)
        useSystemSeparators = [bool]$xl.UseSystemSeparators; precisionAsDisplayed = [bool]$xl.ActiveWorkbook.PrecisionAsDisplayed
        calculation = [int]$xl.Calculation }
    Write-Row $row
    Write-Host ($row | ConvertTo-Json -Compress)
    [void](Prepare-Sheet)
    $corpus = [ordered]@{
        'ARITH-136' = '=1+4.2E-15=1'; 'ARITH-137' = '=1+4.4E-15=1'; 'ARITH-138' = '=1+4.6E-15=1'; 'ARITH-139' = '=1+4.8E-15=1'
        'ARITH-140' = '=1+1.3E-15-1'; 'ARITH-141' = '=1+1.6E-15-1'; 'ARITH-142' = '=1+1.8E-15-1'; 'ARITH-143' = '=3^0.5'
        'ARITH-144' = '="it''s">"its"'; 'ARITH-145' = ('="{0}"<"ss"' -f [char]0xDF)
        'ARITH-146' = '=9+3E-14=9'; 'ARITH-147' = '=1+4E-15=1+6E-15'; 'ARITH-148' = '=1000+3.6E-12=1000'; 'ARITH-149' = '=9+5E-15=9'
    }
    $r = 2
    foreach ($id in $corpus.Keys) {
        $f = $corpus[$id]
        if ($f.Substring(1).Contains('=')) { Ask-Typed $id $f $r }
        else {
            # Not an equality: typed and read, with no sides to split.
            $ws = $xl.ActiveSheet
            $ws.Range("A$r").Value2 = $id
            [void]$ws.Range("B$r").Select()
            Type-Entry $f
            Wait-Ready
            $check = Read-Cell $ws.Range("B$r")
            Write-Row ([ordered]@{ case = $id; typed = $f; method = 'keys (SendInput, one Unicode character at a time), Enter; read through COM'; cell = "B$r"; answer = $check; selectionAfter = [string]$xl.Selection.Address($false, $false) })
            Write-Host ('{0,-10} {1,-24} -> {2} ({3}; stored {4})' -f $id, $f, $check.value2, $check.value2Type, $check.formula2)
        }
        $r++
    }
    [void]$xl.ActiveSheet.Range('A1').Select()
    Save-View $xl 'typed-ARITH-136-149' 'D16'
    Write-Row ([ordered]@{ case = 'typed'; step = 'screenshot'; shot = 'shots/typed-ARITH-136-149.png' })
}

$cases['byhand'] = {
    [void](Prepare-Sheet)
    $r = 2
    foreach ($f in @('=0.9+4E-16=0.9', '=99+4E-13=99', '=1+4.9E-15=1')) { Ask-Typed ('by hand ' + ($r - 1)) $f $r; $r++ }
    [void]$xl.ActiveSheet.Range('A1').Select()
    Save-View $xl 'byhand' 'D6'
    Write-Row ([ordered]@{ case = 'byhand'; step = 'screenshot'; shot = 'shots/byhand.png' })
}

Add-Type -Namespace Equality -Name Native -MemberDefinition @'
[DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder s, int n);
[DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
'@
function Get-FrontTitle { $sb = New-Object Text.StringBuilder 512; [void][Equality.Native]::GetWindowText([Equality.Native]::GetForegroundWindow(), $sb, 512); return $sb.ToString() }
$FileTag = 'paste-source-equality'

$cases['paste'] = {
    $Block = @('B2', 'C2', 'B3', 'C3')
    $src = Join-Path $env:LOCALAPPDATA "exgrid-layer3\$FileTag.txt"
    [IO.File]::WriteAllText($src, '=A1', $Utf8)
    $before = @(Get-Process notepad -ErrorAction SilentlyContinue).Count
    Start-Process notepad.exe -ArgumentList ('"{0}"' -f $src)
    $deadline = (Get-Date).AddSeconds(15)
    while ((Get-FrontTitle) -notlike "*$FileTag*") {
        if ((Get-Date) -gt $deadline) { throw 'Notepad did not come to the front with the file' }
        $np = @(Get-Process notepad -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle -like "*$FileTag*" })
        if ($np.Count -gt 0) {
            [Equality.Native]::keybd_event(0x12, 0, 0, [UIntPtr]::Zero); [Equality.Native]::keybd_event(0x12, 0, 2, [UIntPtr]::Zero)
            [void][Equality.Native]::SetForegroundWindow($np[0].MainWindowHandle)
        }
        Start-Sleep -Milliseconds 400
    }
    Start-Sleep -Milliseconds 800
    [System.Windows.Forms.SendKeys]::SendWait('^a'); Start-Sleep -Milliseconds 300
    [System.Windows.Forms.SendKeys]::SendWait('^c'); Start-Sleep -Milliseconds 500
    $formats = @([Windows.Forms.Clipboard]::GetDataObject().GetFormats($false))
    $text = [Windows.Forms.Clipboard]::GetText()
    Write-Row ([ordered]@{ case = 'paste'; step = 'copied from Notepad'; method = 'keys (Ctrl+A, Ctrl+C in Notepad)'; notepadProcessesBefore = $before
        clipboardFormats = ($formats -join ', '); clipboardText = $text; clipboardTextLength = $text.Length })
    Write-Host ('paste: clipboard [{0}] "{1}"' -f ($formats -join ', '), $text)
    $closed = $false
    if ((Get-FrontTitle) -like "*$FileTag*") { [System.Windows.Forms.SendKeys]::SendWait('^w'); Start-Sleep -Milliseconds 800; $closed = $true }
    $left = @(Get-Process notepad -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 })
    Write-Row ([ordered]@{ case = 'paste'; step = 'Notepad tab closed'; method = 'keys (Ctrl+W)'; closedWithCtrlW = $closed
        notepadWindowsAfter = $left.Count; thisFileStillOpen = (@($left | Where-Object { $_.MainWindowTitle -like "*$FileTag*" }).Count -gt 0) })
    [void](Fresh)
    ClickCell 'C3'; ClickCell 'B2' -Shift
    $s = Get-State $xl $Block
    Write-Row ([ordered]@{ case = 'paste'; step = 'click C3, Shift+click B2'; method = 'mouse'; state = $s })
    Save-View $xl 'paste-B2-C3-from-C3-selected'
    Write-Host ('paste: before  sel {0} active {1}' -f $s.selection, $s.activeCell)
    Keys '^v' 900
    Wait-Ready
    $s = Get-State $xl $Block
    $cells = [ordered]@{}
    foreach ($a in $Block) { $cells[$a] = Read-Cell $xl.ActiveSheet.Range($a) }
    Write-Row ([ordered]@{ case = 'paste'; step = 'Ctrl+V of the text =A1 from Notepad'; method = 'keys'; selection = $s.selection; activeCell = $s.activeCell; cells = $cells; shot = 'shots/paste-Ctrl-V-from-C3.png' })
    Save-View $xl 'paste-Ctrl-V-from-C3'
    Write-Host ('paste: after   sel {0} active {1}  ' -f $s.selection, $s.activeCell) (($Block | ForEach-Object { '{0}={1}' -f $_, $cells[$_].formula2 }) -join ' ')
}

foreach ($id in $Case) {
    if (-not $cases.Contains($id)) { throw "no case $id" }
    Write-Row ([ordered]@{ case = $id; step = 'started'; at = (Get-Date -Format 'yyyy-MM-dd HH:mm:ss') })
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
    Write-Row ([ordered]@{ case = $id; step = 'ended'; at = (Get-Date -Format 'yyyy-MM-dd HH:mm:ss') })
}
