<#
    A picture's ink as text, one line per pixel row: '#' where the pixel is darker than the threshold, '.' elsewhere.
        powershell -File ink.ps1 <png> <x0> <y0> <x1> <y1> [threshold 0-255]
#>
param([string]$Png, [int]$X0, [int]$Y0, [int]$X1, [int]$Y1, [int]$T = 160)
Add-Type -AssemblyName System.Drawing
$b = [Drawing.Bitmap]::FromFile($Png)
for ($y = $Y0; $y -le $Y1; $y++) { $s = ''; for ($x = $X0; $x -le $X1; $x++) { $c = $b.GetPixel($x, $y); $s += $(if ((($c.R + $c.G + $c.B) / 3) -lt $T) { '#' } else { '.' }) }; '{0,4} {1}' -f $y, $s }
$b.Dispose()
