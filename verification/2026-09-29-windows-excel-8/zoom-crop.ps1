# A crop of a saved screenshot, enlarged with nearest-neighbour, for reading pixels by eye.
param([string]$Src, [int]$X, [int]$Y, [int]$W, [int]$H, [int]$Scale = 8, [string]$Out)
Add-Type -AssemblyName System.Drawing
$img = [Drawing.Image]::FromFile($Src)
$bmp = New-Object Drawing.Bitmap ($W * $Scale), ($H * $Scale)
$g = [Drawing.Graphics]::FromImage($bmp)
$g.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$g.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::Half
$g.DrawImage($img, (New-Object Drawing.Rectangle 0, 0, ($W * $Scale), ($H * $Scale)), (New-Object Drawing.Rectangle $X, $Y, $W, $H), [Drawing.GraphicsUnit]::Pixel)
$g.Dispose(); $bmp.Save($Out); $bmp.Dispose(); $img.Dispose()
