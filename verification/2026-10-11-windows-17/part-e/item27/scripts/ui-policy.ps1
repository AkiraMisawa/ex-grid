param([string]$Cmd)
# Run elevated (the caller starts it with -Verb RunAs). Its log is written beside it, since an elevated window closes.
$d = Join-Path $env:LOCALAPPDATA 'exgrid-layer3\run-2026-10-11-17'
$log = Join-Path $d "ui-policy-$Cmd.log"
$k = 'HKCU:\Software\Policies\Microsoft\Office\16.0\Common\LanguageResources'
$out = @("$Cmd at " + (Get-Date -Format 'HH:mm:ss') + " as " + [Security.Principal.WindowsIdentity]::GetCurrent().Name)
try {
  switch ($Cmd) {
    'set' {
      $out += "policy key existed before: " + (Test-Path $k)
      if (-not (Test-Path $k)) { New-Item -Path $k -Force | Out-Null }
      New-ItemProperty -Path $k -Name UILanguage -PropertyType DWord -Value 1041 -Force | Out-Null
      $out += "UILanguage=" + (Get-ItemProperty $k).UILanguage
    }
    'restore' {
      if (Test-Path $k) { Remove-ItemProperty -Path $k -Name UILanguage -ErrorAction SilentlyContinue
        if (-not (Get-Item $k).Property) { Remove-Item $k; $out += "policy key removed (it held nothing else)" } else { $out += "UILanguage removed; the key keeps: " + ((Get-Item $k).Property -join ', ') } }
      $c = 'HKLM:\SOFTWARE\Microsoft\Office\ClickToRun\Configuration'
      Set-ItemProperty -Path $c -Name ClientCulture -Value 'en-gb'
      $out += "ClientCulture=" + (Get-ItemProperty $c).ClientCulture + " ScenarioCulture=" + (Get-ItemProperty $c).ScenarioCulture
      $out += "policy key exists after: " + (Test-Path $k)
    }
  }
} catch { $out += "error: $($_.Exception.Message)" }
$out | Set-Content $log -Encoding UTF8
