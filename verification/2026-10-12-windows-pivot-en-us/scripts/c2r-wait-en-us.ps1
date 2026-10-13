$t0 = Get-Date
while (((Get-Date) - $t0).TotalMinutes -lt 40) {
  $ids = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Office\ClickToRun\Configuration').ProductReleaseIds
  $p = Get-Process -Name OfficeSetup-lap-en-us-x64 -ErrorAction SilentlyContinue
  if (-not $p) { "installer process ended at " + (Get-Date -Format 'HH:mm:ss') + "; ProductReleaseIds=$ids"; break }
  Start-Sleep -Seconds 10
}
Get-CimInstance Win32_Process | Where-Object { $_.Name -match 'ClickToRun|C2R' } | ForEach-Object { "{0} {1}" -f $_.ProcessId, $_.Name }
$lr = Get-ItemProperty 'HKCU:\Software\Microsoft\Office\16.0\Common\LanguageResources'
"UILanguageTag=$($lr.UILanguageTag) UIFallbackLanguages=$($lr.UIFallbackLanguages)"
$cfg = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Office\ClickToRun\Configuration'
$cfg.PSObject.Properties | Where-Object { $_.Name -match 'en-us|Culture|ProductReleaseIds' } | ForEach-Object { "$($_.Name)=$($_.Value)" }
