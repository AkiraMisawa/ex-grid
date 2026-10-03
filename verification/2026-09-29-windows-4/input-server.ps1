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
