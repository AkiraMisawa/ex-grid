# While the IME's candidate list is open (run from ime-ui-trial.mjs): the Windows Input Experience's
# window (TextInputHost's CoreWindow) pictured with PrintWindow, with and without PW_RENDERFULLCONTENT.
# Every pixel came back empty both ways (report.md, "The IME's candidate window").
Add-Type -AssemblyName System.Drawing
Add-Type -Namespace T -Name N -MemberDefinition '[DllImport("user32.dll")] public static extern bool SetProcessDPIAware(); [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint f); [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string c, string t);'
[void][T.N]::SetProcessDPIAware()
$h = [T.N]::FindWindow('Windows.UI.Core.CoreWindow', 'Windows Input Experience')
"core window $h"
foreach ($flag in 2, 0) {
  $bmp = New-Object Drawing.Bitmap 3840, 2160; $g = [Drawing.Graphics]::FromImage($bmp); $dc = $g.GetHdc()
  $ok = [T.N]::PrintWindow($h, $dc, $flag); $g.ReleaseHdc($dc); $g.Dispose()
  $lit = 0; for ($y = 0; $y -lt 2160; $y += 9) { for ($x = 0; $x -lt 3840; $x += 9) { $c = $bmp.GetPixel($x, $y); if ($c.A -gt 0 -and ($c.R + $c.G + $c.B) -gt 0) { $lit++ } } }
  "PrintWindow flag $flag ok=$ok lit=$lit"
  if ($lit -gt 0) { $bmp.Save("$env:LOCALAPPDATA\exgrid-layer3\run-2026-10-01-15\core-$flag.png") }
  $bmp.Dispose()
}
