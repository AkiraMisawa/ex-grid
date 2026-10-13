<#
    Where a picture holds a filled square of Excel's selection green (#217346), at least 5 by 5 device pixels: the
    fill handle. Each square's box, once. The headings' green lines (2 px) and the active cell's border (1 px) are
    thinner, so they are not found.

        powershell -File handle.ps1 <png> [x0 y0 x1 y1]
#>
param([string]$Png, [int]$X0 = 0, [int]$Y0 = 0, [int]$X1 = -1, [int]$Y1 = -1)
Add-Type -AssemblyName System.Drawing
$b = [Drawing.Bitmap]::FromFile($Png)
if ($X1 -lt 0) { $X1 = $b.Width - 1 }; if ($Y1 -lt 0) { $Y1 = $b.Height - 1 }
$w = $X1 - $X0 + 1; $h = $Y1 - $Y0 + 1
$rect = New-Object Drawing.Rectangle $X0, $Y0, $w, $h
$d = $b.LockBits($rect, [Drawing.Imaging.ImageLockMode]::ReadOnly, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
$bytes = New-Object byte[] ($d.Stride * $h); [Runtime.InteropServices.Marshal]::Copy($d.Scan0, $bytes, 0, $bytes.Length); $b.UnlockBits($d)
$g = New-Object 'bool[,]' $w, $h
for ($y = 0; $y -lt $h; $y++) { for ($x = 0; $x -lt $w; $x++) { $i = $y * $d.Stride + $x * 4
    $g[$x, $y] = ([Math]::Abs($bytes[$i + 2] - 0x21) -lt 12) -and ([Math]::Abs($bytes[$i + 1] - 0x73) -lt 12) -and ([Math]::Abs($bytes[$i] - 0x46) -lt 12) } }
$found = @()
for ($y = 0; $y -lt $h - 4; $y++) { for ($x = 0; $x -lt $w - 4; $x++) {
    if (-not $g[$x, $y]) { continue }
    $ok = $true; for ($j = 0; $j -lt 5 -and $ok; $j++) { for ($k = 0; $k -lt 5; $k++) { if (-not $g[($x + $k), ($y + $j)]) { $ok = $false; break } } }
    if (-not $ok) { continue }
    $ax = $X0 + $x; $ay = $Y0 + $y
    if ($found | Where-Object { [Math]::Abs($_[0] - $ax) -lt 12 -and [Math]::Abs($_[1] - $ay) -lt 12 }) { continue }
    $ex = $x; while ($ex + 1 -lt $w -and $g[($ex + 1), $y]) { $ex++ }; $ey = $y; while ($ey + 1 -lt $h -and $g[$x, ($ey + 1)]) { $ey++ }
    $found += , @($ax, $ay, ($X0 + $ex), ($Y0 + $ey))
} }
$b.Dispose()
if ($found.Count -eq 0) { 'none' } else { ($found | ForEach-Object { 'square {0},{1} to {2},{3}' -f $_[0], $_[1], $_[2], $_[3] }) -join "`n" }
