param([string]$Cmd)
$d = Join-Path $env:LOCALAPPDATA 'exgrid-layer3\run-2026-10-11-17\lap-download'
$exe = Join-Path $d 'OfficeSetup-lap-ja-jp-x64.exe'
switch ($Cmd) {
  'download' {
    $u = 'https://c2rsetup.officeapps.live.com/c2r/download.aspx?ProductreleaseID=languagepack&language=ja-jp&platform=x64&source=O16LAP&version=O16GA'
    & curl.exe -sSL -o $exe $u
    "downloaded " + (Get-Item $exe).Length + " bytes"
    $s = Get-AuthenticodeSignature $exe
    "signature: $($s.Status) | $($s.SignerCertificate.Subject)"
    "file: " + (Get-Item $exe).VersionInfo.FileDescription + " | " + (Get-Item $exe).VersionInfo.ProductVersion
  }
  'run' {
    $s = Get-AuthenticodeSignature $exe
    if ($s.Status -ne 'Valid' -or $s.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') { "not run: signature $($s.Status) $($s.SignerCertificate.Subject)"; break }
    try { $p = Start-Process $exe -Verb RunAs -PassThru; "started elevated: process $($p.Id) at " + (Get-Date -Format 'HH:mm:ss') } catch { "not started: $($_.Exception.Message)" }
  }
}
