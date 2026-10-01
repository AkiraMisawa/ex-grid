<#
.SYNOPSIS
    The display settings high contrast touches, as text, so the state before it was switched on can
    be compared with the state after it was switched off (verify-on-windows-8.md, Part B, "with
    Windows' high contrast on"). Reads only.

        powershell -File theme-state.ps1 > before.txt
#>
$ErrorActionPreference = 'Continue'
Add-Type -Namespace ThemeState -Name Native -MemberDefinition @'
[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool SystemParametersInfo(uint action, uint param, ref HIGHCONTRAST value, uint winIni);
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] public struct HIGHCONTRAST { public uint cbSize; public uint dwFlags; public IntPtr lpszDefaultScheme; }
'@
$hc = New-Object ThemeState.Native+HIGHCONTRAST; $hc.cbSize = [uint32][Runtime.InteropServices.Marshal]::SizeOf($hc)
[void][ThemeState.Native]::SystemParametersInfo(0x0042, $hc.cbSize, [ref]$hc, 0)
$scheme = if ($hc.lpszDefaultScheme -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::PtrToStringUni($hc.lpszDefaultScheme) } else { '' }
'SPI_GETHIGHCONTRAST flags=0x{0:x} scheme={1}' -f $hc.dwFlags, $scheme
foreach ($key in @(
        'HKCU:\Control Panel\Accessibility\HighContrast',
        'HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes',
        'HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize',
        'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Accent',
        'HKCU:\Software\Microsoft\Windows\DWM',
        'HKCU:\Control Panel\Colors')) {
    "[$key]"
    $item = Get-ItemProperty $key -ErrorAction SilentlyContinue
    if ($null -eq $item) { '  (missing)'; continue }
    foreach ($p in $item.PSObject.Properties | Where-Object { $_.Name -notlike 'PS*' } | Sort-Object Name) {
        $v = $p.Value
        if ($v -is [byte[]]) { $v = ($v | ForEach-Object { $_.ToString('x2') }) -join '' }
        '  {0} = {1}' -f $p.Name, $v
    }
}
