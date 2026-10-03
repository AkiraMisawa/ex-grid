<#
.SYNOPSIS
    A screenshot of what the screen shows of one window, for wide-probe.mjs: the window whose
    title starts with -Title, captured from the screen (not through the browser), saved to -Out.

        powershell -File window-shot.ps1 -Title 'wide-probe-zoom-...' -Out C:\...\shot.png
#>
param([string]$Title, [string]$Out)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -Namespace WinShot -Name Native -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
[DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
[StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
'@
[void][WinShot.Native]::SetProcessDPIAware()
$p = Get-Process | Where-Object { $_.MainWindowHandle -ne 0 -and $_.MainWindowTitle.StartsWith($Title) } | Select-Object -First 1
if ($null -eq $p) { throw "no window titled '$Title...'" }
$r = New-Object WinShot.Native+RECT
[void][WinShot.Native]::GetWindowRect($p.MainWindowHandle, [ref]$r)
$bmp = New-Object Drawing.Bitmap ($r.Right - $r.Left), ($r.Bottom - $r.Top)
$g = [Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($r.Left, $r.Top, 0, 0, $bmp.Size)
$bmp.Save($Out, [Drawing.Imaging.ImageFormat]::Png); $g.Dispose(); $bmp.Dispose()
'ok'
