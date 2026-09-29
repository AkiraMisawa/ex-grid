<#
.SYNOPSIS
    Real OS input for end-probe.mjs, kept running so that a gesture goes out within milliseconds of
    the page being ready for it (os-input.ps1 starts a PowerShell per gesture, about half a second).

        powershell -File input-server.ps1        (commands on stdin, one per line; one answer each)

    front <title prefix>                 the window whose title starts with it, to the front
    move <x> <y>                         the cursor, to a screen pixel (moved 1 px and back, so the page sees it)
    click <x> <y>                        a left click there
    drag <x1> <y1> <x2> <y2> <steps> <ms> left button down at the first point, <steps> moves to the
                                         second <ms> apart, up
    keys <SendKeys text>                 keys, to whatever is in front
    shot <title prefix> <file>           that window as the screen shows it, to a PNG
    quit

    Added for the fifth run (verify-on-windows-5.md, Part B):

    shiftclick <x> <y>                   a left click there with Shift held
    imeoff                               VK_IME_OFF, so the Japanese IME cannot take the keys that follow
    col <x> <y1> <y2>                    the screen's pixels on that vertical line, run-length encoded
    row <y> <x1> <x2>                    the same on a horizontal line ("rrggbb*count,...")
    grab <l> <t> <r> <b> <file>          that rectangle of the screen, to a PNG
    setclip <text>                       the clipboard, set to that text (a sentinel before a copy)
    clip                                 what the clipboard holds: its formats and its text
    notepadcopy <file>                   the text <file> holds, copied from Notepad: the file opened in
                                         Notepad, Ctrl+A, Ctrl+C, then Ctrl+W to close that tab alone.
                                         No window title is read into the answer

    The process is DPI-aware, so coordinates are the screen's physical pixels. Each answer is one
    line, "ok ..." or "err ...", with the time (ms since this helper started) the command finished.
#>
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -Namespace InputServer -Name Native -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
[DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
[DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
[DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, int data, UIntPtr extra);
[DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
[DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder s, int n);
[StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
'@
[void][InputServer.Native]::SetProcessDPIAware()
$clock = [Diagnostics.Stopwatch]::StartNew()

# A window's title changes a moment after the page sets it, so the lookup tries for 3 seconds.
function Find-Window([string]$Prefix) {
    $until = (Get-Date).AddSeconds(3)
    do {
        $p = Get-Process | Where-Object { $_.MainWindowHandle -ne 0 -and $_.MainWindowTitle.StartsWith($Prefix) } | Select-Object -First 1
        if ($null -ne $p) { return $p.MainWindowHandle }
        Start-Sleep -Milliseconds 150
    } while ((Get-Date) -lt $until)
    throw "no window titled '$Prefix...'"
}
function Get-FrontTitle { $sb = New-Object Text.StringBuilder 512; [void][InputServer.Native]::GetWindowText([InputServer.Native]::GetForegroundWindow(), $sb, 512); return $sb.ToString() }
# A line of the screen, one pixel wide, as runs of one colour.
function Get-Line([int]$X, [int]$Y, [int]$W, [int]$H) {
    $bmp = New-Object Drawing.Bitmap $W, $H
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($X, $Y, 0, 0, $bmp.Size)
    $runs = New-Object Collections.Generic.List[string]
    $last = $null; $n = 0
    $count = [Math]::Max($W, $H)
    for ($i = 0; $i -lt $count; $i++) {
        $c = if ($W -ge $H) { $bmp.GetPixel($i, 0) } else { $bmp.GetPixel(0, $i) }
        $hex = '{0:x2}{1:x2}{2:x2}' -f $c.R, $c.G, $c.B
        if ($hex -eq $last) { $n++ } else { if ($null -ne $last) { $runs.Add("$last*$n") }; $last = $hex; $n = 1 }
    }
    $runs.Add("$last*$n")
    $g.Dispose(); $bmp.Dispose()
    return ($runs -join ',')
}
# The clipboard is one per desktop, and a browser writing it holds it for a moment: an access that
# finds it held is tried again, 20 times, 100 ms apart.
function With-Clipboard([scriptblock]$Do) {
    for ($i = 1; ; $i++) {
        try { return & $Do } catch { if ($i -ge 20) { throw }; Start-Sleep -Milliseconds 100 }
    }
}
function Answer([string]$Text) { [Console]::Out.WriteLine(('{0} t={1}' -f $Text, $clock.ElapsedMilliseconds)); [Console]::Out.Flush() }

[Console]::Out.WriteLine('ready'); [Console]::Out.Flush()
while ($true) {
    $line = [Console]::In.ReadLine()
    if ($null -eq $line -or $line -eq 'quit') { break }
    $parts = $line.Split(' ', 2)
    $cmd = $parts[0]; $rest = if ($parts.Count -gt 1) { $parts[1] } else { '' }
    try {
        switch ($cmd) {
            'front' {
                $h = Find-Window $rest
                if ([InputServer.Native]::GetForegroundWindow() -ne $h) {
                    [InputServer.Native]::keybd_event(0x12, 0, 0, [UIntPtr]::Zero); [InputServer.Native]::keybd_event(0x12, 0, 2, [UIntPtr]::Zero)
                    [void][InputServer.Native]::SetForegroundWindow($h)
                    Start-Sleep -Milliseconds 300
                }
                if ([InputServer.Native]::GetForegroundWindow() -ne $h) { throw "the window '$rest...' is not in front" }
                Answer 'ok front'
            }
            'move' {
                $a = $rest.Split(' ') | ForEach-Object { [int]$_ }
                [void][InputServer.Native]::SetCursorPos($a[0] + 1, $a[1]); Start-Sleep -Milliseconds 30
                [void][InputServer.Native]::SetCursorPos($a[0], $a[1]); Start-Sleep -Milliseconds 30
                Answer 'ok move'
            }
            'click' {
                $a = $rest.Split(' ') | ForEach-Object { [int]$_ }
                [void][InputServer.Native]::SetCursorPos($a[0], $a[1]); Start-Sleep -Milliseconds 20
                [InputServer.Native]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 30
                [InputServer.Native]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
                Answer 'ok click'
            }
            'drag' {
                $a = $rest.Split(' ') | ForEach-Object { [int]$_ }
                [void][InputServer.Native]::SetCursorPos($a[0], $a[1]); Start-Sleep -Milliseconds 20
                [InputServer.Native]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
                $down = $clock.ElapsedMilliseconds
                for ($i = 1; $i -le $a[4]; $i++) {
                    Start-Sleep -Milliseconds $a[5]
                    [void][InputServer.Native]::SetCursorPos([int]($a[0] + ($a[2] - $a[0]) * $i / $a[4]), [int]($a[1] + ($a[3] - $a[1]) * $i / $a[4]))
                }
                Start-Sleep -Milliseconds $a[5]
                [InputServer.Native]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
                Answer "ok drag down=$down"
            }
            'keys' { [System.Windows.Forms.SendKeys]::SendWait($rest); Answer 'ok keys' }
            'shiftclick' {
                $a = $rest.Split(' ') | ForEach-Object { [int]$_ }
                [void][InputServer.Native]::SetCursorPos($a[0], $a[1]); Start-Sleep -Milliseconds 20
                [InputServer.Native]::keybd_event(0x10, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 30
                [InputServer.Native]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 30
                [InputServer.Native]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 30
                [InputServer.Native]::keybd_event(0x10, 0, 2, [UIntPtr]::Zero)
                Answer 'ok shiftclick'
            }
            'imeoff' { [InputServer.Native]::keybd_event(0x1A, 0, 0, [UIntPtr]::Zero); [InputServer.Native]::keybd_event(0x1A, 0, 2, [UIntPtr]::Zero); Start-Sleep -Milliseconds 100; Answer 'ok imeoff' }
            'col' { $a = $rest.Split(' ') | ForEach-Object { [int]$_ }; Answer ('ok col ' + (Get-Line $a[0] $a[1] 1 ($a[2] - $a[1]))) }
            'row' { $a = $rest.Split(' ') | ForEach-Object { [int]$_ }; Answer ('ok row ' + (Get-Line $a[1] $a[0] ($a[2] - $a[1]) 1)) }
            'grab' {
                $p2 = $rest.Split(' ', 5)
                $l = [int]$p2[0]; $t = [int]$p2[1]; $r2 = [int]$p2[2]; $b2 = [int]$p2[3]
                $bmp = New-Object Drawing.Bitmap ($r2 - $l), ($b2 - $t)
                $g = [Drawing.Graphics]::FromImage($bmp)
                $g.CopyFromScreen($l, $t, 0, 0, $bmp.Size)
                $bmp.Save($p2[4], [Drawing.Imaging.ImageFormat]::Png); $g.Dispose(); $bmp.Dispose()
                Answer 'ok grab'
            }
            'setclip' { With-Clipboard { [Windows.Forms.Clipboard]::SetText($rest) }; Answer 'ok setclip' }
            'clip' {
                $r = With-Clipboard {
                    $o = [Windows.Forms.Clipboard]::GetDataObject()
                    $formats = if ($null -eq $o) { @() } else { @($o.GetFormats($false)) }
                    $text = [Windows.Forms.Clipboard]::GetText()
                    @{ formats = $formats; text = $text; length = $text.Length }
                }
                Answer ('ok clip ' + (ConvertTo-Json -Compress $r))
            }
            'notepadcopy' {
                $src = $rest
                $tag = [IO.Path]::GetFileNameWithoutExtension($src)
                $before = @(Get-Process notepad -ErrorAction SilentlyContinue).Count
                Start-Process notepad.exe -ArgumentList ('"{0}"' -f $src)
                $deadline = (Get-Date).AddSeconds(15)
                while ((Get-FrontTitle) -notlike "*$tag*") {
                    if ((Get-Date) -gt $deadline) { throw 'Notepad did not come to the front with the file' }
                    $np = @(Get-Process notepad -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle -like "*$tag*" })
                    if ($np.Count -gt 0) {
                        [InputServer.Native]::keybd_event(0x12, 0, 0, [UIntPtr]::Zero); [InputServer.Native]::keybd_event(0x12, 0, 2, [UIntPtr]::Zero)
                        [void][InputServer.Native]::SetForegroundWindow($np[0].MainWindowHandle)
                    }
                    Start-Sleep -Milliseconds 400
                }
                Start-Sleep -Milliseconds 800
                [System.Windows.Forms.SendKeys]::SendWait('^a'); Start-Sleep -Milliseconds 300
                [System.Windows.Forms.SendKeys]::SendWait('^c'); Start-Sleep -Milliseconds 500
                $formats = With-Clipboard { @([Windows.Forms.Clipboard]::GetDataObject().GetFormats($false)) }
                $text = With-Clipboard { [Windows.Forms.Clipboard]::GetText() }
                $closed = $false
                if ((Get-FrontTitle) -like "*$tag*") { [System.Windows.Forms.SendKeys]::SendWait('^w'); Start-Sleep -Milliseconds 800; $closed = $true }
                $left = @(Get-Process notepad -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 })
                $still = @($left | Where-Object { $_.MainWindowTitle -like "*$tag*" }).Count -gt 0
                Answer ('ok notepadcopy ' + (ConvertTo-Json -Compress @{ notepadProcessesBefore = $before; formats = $formats; text = $text; length = $text.Length; closedWithCtrlW = $closed; notepadWindowsAfter = $left.Count; thisFileStillOpen = $still }))
            }
            'shot' {
                $p2 = $rest.Split(' ', 2)
                $h = Find-Window $p2[0]
                $r = New-Object InputServer.Native+RECT
                [void][InputServer.Native]::GetWindowRect($h, [ref]$r)
                $bmp = New-Object Drawing.Bitmap ($r.Right - $r.Left), ($r.Bottom - $r.Top)
                $g = [Drawing.Graphics]::FromImage($bmp)
                $g.CopyFromScreen($r.Left, $r.Top, 0, 0, $bmp.Size)
                $bmp.Save($p2[1], [Drawing.Imaging.ImageFormat]::Png); $g.Dispose(); $bmp.Dispose()
                Answer ("ok shot {0},{1},{2},{3}" -f $r.Left, $r.Top, $r.Right, $r.Bottom)
            }
            default { throw "unknown command '$cmd'" }
        }
    }
    catch { Answer ('err ' + $_.Exception.Message.Replace("`n", ' ')) }
}
