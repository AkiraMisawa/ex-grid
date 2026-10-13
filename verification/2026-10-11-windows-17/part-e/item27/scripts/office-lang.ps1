param([string]$Cmd)
$c2r = 'C:\Program Files\Common Files\microsoft shared\ClickToRun\OfficeClickToRun.exe'
switch ($Cmd) {
  'state' {
    $cfg = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Office\ClickToRun\Configuration'
    "ProductReleaseIds=$($cfg.ProductReleaseIds)"
    $cfg.PSObject.Properties | Where-Object { $_.Name -match 'Language|culture|\.Cultures' } | ForEach-Object { "$($_.Name)=$($_.Value)" }
    $lr = Get-ItemProperty 'HKCU:\Software\Microsoft\Office\16.0\Common\LanguageResources' -ErrorAction SilentlyContinue
    if ($lr) { $lr.PSObject.Properties | Where-Object { $_.Name -notmatch '^PS' } | ForEach-Object { "LanguageResources $($_.Name)=$($_.Value)" } } else { 'LanguageResources: none' }
    Get-Process OfficeClickToRun, OfficeC2RClient -ErrorAction SilentlyContinue | ForEach-Object { "running $($_.ProcessName) $($_.Id)" }
  }
  'install' {
    Start-Process $c2r -ArgumentList 'scenario=install','scenariosubtype=ARP','sourcetype=None','productstoadd=LanguagePack.16_ja-jp_x-none','culture=ja-jp','version.16=16.0','displaylevel=False'
    'started'
  }
}
