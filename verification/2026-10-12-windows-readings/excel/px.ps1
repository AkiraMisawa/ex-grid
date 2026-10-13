<#
    The colours along one line of a picture, as runs: "x,y #RRGGBB xN". For measuring a border's width and colour
    in device pixels from a picture Excel's window drew of itself.

        powershell -File px.ps1 <png> <x0> <y0> <x1> <y1>     (a horizontal or a vertical line, ends included)
#>
param([string]$Png, [int]$X0, [int]$Y0, [int]$X1, [int]$Y1)
Add-Type -AssemblyName System.Drawing
$b = [Drawing.Bitmap]::FromFile($Png)
$pts = if ($Y0 -eq $Y1) { $X0..$X1 | ForEach-Object { , @($_, $Y0) } } else { $Y0..$Y1 | ForEach-Object { , @($X0, $_) } }
$prev = $null; $start = $null; $n = 0; $out = @()
foreach ($p in $pts) {
    $c = $b.GetPixel($p[0], $p[1]); $h = '#{0:X2}{1:X2}{2:X2}' -f $c.R, $c.G, $c.B
    if ($h -ne $prev) { if ($prev) { $out += ('{0},{1} {2} x{3}' -f $start[0], $start[1], $prev, $n) }; $prev = $h; $start = $p; $n = 0 }
    $n++
}
$out += ('{0},{1} {2} x{3}' -f $start[0], $start[1], $prev, $n)
$b.Dispose()
$out -join "`n"
