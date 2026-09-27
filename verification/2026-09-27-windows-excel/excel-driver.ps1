<#
.SYNOPSIS
    Drives a visible Excel with real keys and the real mouse, for excel-behaviours.md (Part D of
    verify-on-windows.md). Dot-source it, then call the functions:

        . .\excel-driver.ps1
        $xl = Connect-Excel
        Reset-Book $xl @{ A1 = '1'; A2 = '2' }
        Send-Keys '^{DOWN}'
        Get-State $xl

    Keys go in through System.Windows.Forms.SendKeys, the mouse through Win32 SetCursorPos and
    mouse_event, and screenshots through Graphics.CopyFromScreen. The process is made DPI-aware
    first, so screen pixels mean the same thing to Excel, to the mouse and to the screenshot on a
    display scaled to 125% or 150%.

    While Excel is in Edit or Point mode it refuses COM calls, so what is written mid-edit is read
    from a screenshot, not through COM.
#>
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
if (-not ('ExcelDriver.Native' -as [type])) {
    Add-Type -Namespace ExcelDriver -Name Native -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
[DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
[DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
[DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
[DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
[DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
[DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
[DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
[StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
'@
}
[void][ExcelDriver.Native]::SetProcessDPIAware()
$script:Invariant = [Globalization.CultureInfo]::InvariantCulture
$script:EnUs = [Globalization.CultureInfo]::GetCultureInfo('en-US')
$script:Shots = Join-Path $PSScriptRoot 'shots'

# Keys without COM, for when Excel is mid-edit and refuses every call: the window comes from the
# process, not from Application.Hwnd.
function Send-KeysRaw([string]$Keys, [int]$Settle = 400) {
    $script:Hwnd = [IntPtr][long](Get-Content $script:HwndFile)
    Send-Keys $null $Keys $Settle
}
$script:HwndFile = Join-Path $env:LOCALAPPDATA 'exgrid-layer3\excel-driver-hwnd.txt'

function Connect-Excel {
    try { $xl = [Runtime.InteropServices.Marshal]::GetActiveObject('Excel.Application') }
    catch { $xl = New-Object -ComObject Excel.Application }
    $xl.Visible = $true
    $xl.DisplayAlerts = $false
    $xl.WindowState = -4137   # xlMaximized
    [void](Get-Book $xl)
    # Cached: while Excel is in Edit or Point mode every COM call is refused, Hwnd included.
    $script:Hwnd = [IntPtr]$xl.Hwnd
    Set-Content $script:HwndFile ([long]$script:Hwnd)
    return $xl
}

# The one workbook this driver works in, made by it and marked by a workbook-level Name. Any
# other workbook open in Excel (the user's, or one Excel recovered at startup) is never touched.
$script:BookMark = 'ExSheetVerifyScratch'
function Get-Book($xl) {
    foreach ($w in $xl.Workbooks) {
        foreach ($n in $w.Names) { if ($n.Name -eq $script:BookMark) { return $w } }
    }
    $w = $xl.Workbooks.Add()
    [void]$w.Names.Add($script:BookMark, '=TRUE')
    $w.EnableAutoRecover = $false
    return $w
}

function Set-ComProperty($Object, [string]$Name, $Value) {
    [void]$Object.GetType().InvokeMember($Name, [Reflection.BindingFlags]::SetProperty, $null, $Object, @(, $Value), $null, $script:EnUs, $null)
}
function Get-ComProperty($Object, [string]$Name) {
    return $Object.GetType().InvokeMember($Name, [Reflection.BindingFlags]::GetProperty, $null, $Object, @(), $null, $script:EnUs, $null)
}

# A fresh sheet in the one open workbook: every cell cleared, formats and widths back to their
# defaults, A1 selected and scrolled into view; then the given cells entered (text starting with
# "=" through Formula2, anything else through FormulaLocal, as typed).
function Reset-Book($xl, [hashtable]$Cells = @{}) {
    $wb = Get-Book $xl
    [void]$wb.Activate()
    while ($wb.Worksheets.Count -gt 1) { $wb.Worksheets.Item($wb.Worksheets.Count).Delete() }
    $ws = $wb.Worksheets.Item(1)
    $ws.Name = 'Sheet1'
    [void]$ws.Cells.Clear()
    $ws.Cells.ColumnWidth = $ws.StandardWidth
    [void]$ws.Cells.EntireRow.AutoFit()
    [void]$ws.Activate()
    foreach ($a in $Cells.Keys) {
        $v = [string]$Cells[$a]
        if ($v.StartsWith('=')) { $ws.Range($a).Formula2 = $v } else { $ws.Range($a).FormulaLocal = $v }
    }
    [void]$xl.Goto($ws.Range('A1'), $true)
    [void]$ws.Range('A1').Select()
    return $ws
}

function Show-Excel($xl) {
    $hwnd = $script:Hwnd
    # In front already when the foreground window is Excel's own: its main window, or a menu,
    # list or dialog it has open. Taking the foreground again would close that popup.
    [uint32]$excelPid = 0; [uint32]$frontPid = 0
    [void][ExcelDriver.Native]::GetWindowThreadProcessId($hwnd, [ref]$excelPid)
    [void][ExcelDriver.Native]::GetWindowThreadProcessId([ExcelDriver.Native]::GetForegroundWindow(), [ref]$frontPid)
    if ($excelPid -ne 0 -and $excelPid -eq $frontPid) { return }
    # A process that is not in the foreground may not take it; an Alt tap first lets it. Only when
    # Excel is not already in front: an Alt tap inside Excel shows the ribbon's KeyTips, which
    # swallow the next key.
    [ExcelDriver.Native]::keybd_event(0x12, 0, 0, [UIntPtr]::Zero)
    [ExcelDriver.Native]::keybd_event(0x12, 0, 2, [UIntPtr]::Zero)
    [void][ExcelDriver.Native]::ShowWindow($hwnd, 3)   # SW_MAXIMIZE
    [void][ExcelDriver.Native]::SetForegroundWindow($hwnd)
    Start-Sleep -Milliseconds 400
    if ([ExcelDriver.Native]::GetForegroundWindow() -ne $hwnd) { throw 'Excel is not in the foreground; keys would go elsewhere.' }
}

function Send-Keys($xl, [string]$Keys, [int]$Settle = 250) {
    Show-Excel $xl
    [System.Windows.Forms.SendKeys]::SendWait($Keys)
    Start-Sleep -Milliseconds $Settle
}

# The screen pixel rectangle of a cell, from the active pane (zoom and scroll included).
function Get-CellRect($xl, [string]$Address) {
    $r = $xl.ActiveSheet.Range($Address)
    $pane = $xl.ActiveWindow.ActivePane
    $left = $pane.PointsToScreenPixelsX($r.Left); $top = $pane.PointsToScreenPixelsY($r.Top)
    $right = $pane.PointsToScreenPixelsX($r.Left + $r.Width); $bottom = $pane.PointsToScreenPixelsY($r.Top + $r.Height)
    return [pscustomobject]@{ Left = $left; Top = $top; Right = $right; Bottom = $bottom; X = [int](($left + $right) / 2); Y = [int](($top + $bottom) / 2) }
}

function Move-Mouse([int]$X, [int]$Y) { [void][ExcelDriver.Native]::SetCursorPos($X, $Y); Start-Sleep -Milliseconds 60 }
function Press-Mouse { [ExcelDriver.Native]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 60 }
function Release-Mouse { [ExcelDriver.Native]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 60 }
function Hold-Key([byte]$Vk) { [ExcelDriver.Native]::keybd_event($Vk, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 40 }
function Let-Key([byte]$Vk) { [ExcelDriver.Native]::keybd_event($Vk, 0, 2, [UIntPtr]::Zero); Start-Sleep -Milliseconds 40 }

function Click-At($xl, [int]$X, [int]$Y, [switch]$Shift, [switch]$Ctrl) {
    Show-Excel $xl
    if ($Shift) { Hold-Key 0x10 }
    if ($Ctrl) { Hold-Key 0x11 }
    Move-Mouse $X $Y; Press-Mouse; Release-Mouse
    if ($Ctrl) { Let-Key 0x11 }
    if ($Shift) { Let-Key 0x10 }
    Start-Sleep -Milliseconds 250
}

# A drag in small steps, as a hand makes it, so Excel sees the pointer move between the ends.
function Drag-From($xl, [int]$X1, [int]$Y1, [int]$X2, [int]$Y2, [int]$Steps = 20) {
    Show-Excel $xl
    Move-Mouse $X1 $Y1; Press-Mouse
    for ($i = 1; $i -le $Steps; $i++) {
        Move-Mouse ([int]($X1 + ($X2 - $X1) * $i / $Steps)) ([int]($Y1 + ($Y2 - $Y1) * $i / $Steps))
    }
    Start-Sleep -Milliseconds 200
    Release-Mouse
    Start-Sleep -Milliseconds 300
}

# A screenshot of Excel's window, or of a part of it given in screen pixels, saved as shots\<name>.png.
function Save-Shot($xl, [string]$Name, $Rect = $null) {
    if (-not (Test-Path $script:Shots)) { [void](New-Item -ItemType Directory $script:Shots) }
    if ($null -eq $Rect) {
        $w = New-Object ExcelDriver.Native+RECT
        [void][ExcelDriver.Native]::GetWindowRect($script:Hwnd, [ref]$w)
        $Rect = [pscustomobject]@{ Left = $w.Left; Top = $w.Top; Right = $w.Right; Bottom = $w.Bottom }
    }
    $width = $Rect.Right - $Rect.Left; $height = $Rect.Bottom - $Rect.Top
    $bitmap = New-Object Drawing.Bitmap $width, $height
    $g = [Drawing.Graphics]::FromImage($bitmap)
    $g.CopyFromScreen($Rect.Left, $Rect.Top, 0, 0, $bitmap.Size)
    $path = Join-Path $script:Shots ($Name + '.png')
    $bitmap.Save($path, [Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bitmap.Dispose()
    return $path
}

# What COM can read once Excel is back in Ready mode.
function Get-State($xl, [string[]]$Cells = @()) {
    $ws = $xl.ActiveSheet
    $state = [ordered]@{
        selection = [string]$xl.Selection.Address($false, $false)
        activeCell = [string]$xl.ActiveCell.Address($false, $false)
        scrollRow = [int]$xl.ActiveWindow.ScrollRow
        scrollColumn = [int]$xl.ActiveWindow.ScrollColumn
        sheetName = [string]$ws.Name
    }
    foreach ($a in $Cells) {
        $c = $ws.Range($a)
        $v = $c.Value2
        $state[$a] = [ordered]@{
            value2 = if ($v -is [double]) { $v.ToString('R', $script:Invariant) } else { $v }
            text = [string]$c.Text; formula2 = [string]$c.Formula2
            numberFormat = [string](Get-ComProperty $c 'NumberFormat')
        }
    }
    return [pscustomobject]$state
}

# Two crops of the window: the Name Box, the Formula Bar and the cells A1:J14 ("<name>.png"),
# and the status bar's left half ("<name>-status.png"). The rectangles are read through COM, so
# Get-ViewRects is called while Excel is still Ready, and Save-Crops afterwards needs no COM.
function Get-ViewRects($xl, [string]$Through = 'J14') {
    $w = New-Object ExcelDriver.Native+RECT
    [void][ExcelDriver.Native]::GetWindowRect($script:Hwnd, [ref]$w)
    $a1 = Get-CellRect $xl ($xl.ActiveWindow.VisibleRange.Cells.Item(1, 1).Address($false, $false))
    $end = Get-CellRect $xl $Through
    # Screen pixels per point, from row 1's own height: 2 at 150% and 100% zoom.
    $f = ($a1.Bottom - $a1.Top) / [double]$xl.ActiveSheet.Range('A1').Height
    $left = [Math]::Max($w.Left, 0)
    return [pscustomobject]@{
        View = [pscustomobject]@{ Left = $left; Top = [int]($a1.Top - 42 * $f); Right = $end.Right; Bottom = $end.Bottom }
        Status = [pscustomobject]@{ Left = $left; Top = $w.Bottom - [int](26 * $f); Right = $left + [int](420 * $f); Bottom = $w.Bottom - [int](4 * $f) }
    }
}
function Save-Crops($Rects, [string]$Name) {
    [void](Save-Shot $null $Name $Rects.View)
    [void](Save-Shot $null ($Name + '-status') $Rects.Status)
}
function Save-View($xl, [string]$Name, [string]$Through = 'J14') {
    Save-Crops (Get-ViewRects $xl $Through) $Name
}
