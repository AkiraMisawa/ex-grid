<#
    The two colons drawn after "さい" in a font at a size, at 144 DPI (this display's 150%), and where their ink lies:
    the right edge of い's ink, and the columns of the colon's dots. For telling, from a picture, whether a line ends
    with ':' (U+003A) or '：' (U+FF1A).
        powershell -File colon-render.ps1 <font> <points, joined by +>
#>
param([string]$Font, [string]$SizeList)
$Sizes = @($SizeList -split "\+" | ForEach-Object { [double]::Parse($_, [Globalization.CultureInfo]::InvariantCulture) })
Add-Type -AssemblyName System.Drawing
foreach ($pt in $Sizes) {
  foreach ($colon in @([char]0x3A, [char]0xFF1A)) {
    $b = New-Object Drawing.Bitmap 200, 60; $b.SetResolution(144, 144)
    $g = [Drawing.Graphics]::FromImage($b); $g.Clear([Drawing.Color]::White); $g.TextRenderingHint = [Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $f = New-Object Drawing.Font($Font, [single]$pt)
    $g.DrawString(([string][char]0x3055 + [char]0x3044 + $colon), $f, [Drawing.Brushes]::Black, 2, 2); $g.Dispose()
    $cols = @(); for ($x = 0; $x -lt 200; $x++) { $ink = $false; for ($y = 0; $y -lt 60; $y++) { $c = $b.GetPixel($x, $y); if ((($c.R + $c.G + $c.B) / 3) -lt 160) { $ink = $true; break } }; if ($ink) { $cols += $x } }
    # Runs of inked columns.
    $runs = @(); $s = $null; $p = $null; foreach ($x in $cols) { if ($null -eq $s) { $s = $x } elseif ($x -ne $p + 1) { $runs += "$s-$p"; $s = $x }; $p = $x }; if ($null -ne $s) { $runs += "$s-$p" }
    '{0} {1}pt U+{2:X4}: ink columns {3}' -f $Font, $pt, [int]$colon, ($runs -join ' ')
    $b.Dispose(); $f.Dispose()
  }
}
