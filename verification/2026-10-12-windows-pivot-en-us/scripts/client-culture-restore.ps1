# Run elevated: Click-to-Run's ClientCulture back to what it was before the English (US) pack (en-gb).
$k = 'HKLM:\SOFTWARE\Microsoft\Office\ClickToRun\Configuration'
$before = (Get-ItemProperty $k).ClientCulture
Set-ItemProperty -Path $k -Name ClientCulture -Value 'en-gb'
$after = (Get-ItemProperty $k).ClientCulture
[IO.File]::WriteAllText((Join-Path $env:LOCALAPPDATA 'exgrid-layer3\pivot-en-us-2026-10-12\client-culture-restore.log'), "$(Get-Date -Format 'HH:mm:ss') ClientCulture $before -> $after`r`n")
