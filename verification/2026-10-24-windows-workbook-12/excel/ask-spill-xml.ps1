<#
.SYNOPSIS
    exsheet-workbook ticket 11's one question of an .xlsx: how Excel stores =SUM(Sheet2:Sheet4!B2#) in the
    file. A workbook of Out, Sheet2, Sheet3, Sheet4 saved as .xlsx to the temporary folder, Out's <f> printed,
    the file deleted.
#>
$ErrorActionPreference = 'Stop'
$xl = New-Object -ComObject Excel.Application; $xl.Visible = $false; $xl.DisplayAlerts = $false
$b = $xl.Workbooks.Add()
$b.Worksheets.Item(1).Name = 'Out'
foreach ($n in 'Sheet2', 'Sheet3', 'Sheet4') { $s = $b.Worksheets.Add([Type]::Missing, $b.Worksheets.Item($b.Worksheets.Count)); $s.Name = $n }
$b.Worksheets.Item('Out').Range('A1').Formula2 = '=SUM(Sheet2:Sheet4!B2#)'
$b.Worksheets.Item('Out').Range('A2').Formula2 = '=SUM(Sheet2:Sheet4!B2)'
$f = Join-Path $env:TEMP 'spill-3d.xlsx'; $b.SaveAs($f, 51); $b.Close($false); $xl.Quit()
Add-Type -AssemblyName System.IO.Compression.FileSystem
$z = [IO.Compression.ZipFile]::OpenRead($f)
$e = $z.GetEntry('xl/worksheets/sheet1.xml'); $r = New-Object IO.StreamReader($e.Open()); $t = $r.ReadToEnd(); $r.Close(); $z.Dispose(); Remove-Item $f
[regex]::Matches($t, '<c r="A[12]"[^>]*>.*?</c>') | ForEach-Object { $_.Value }
