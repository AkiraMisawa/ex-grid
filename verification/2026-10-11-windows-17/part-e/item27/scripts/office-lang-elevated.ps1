$c2r = 'C:\Program Files\Common Files\microsoft shared\ClickToRun\OfficeClickToRun.exe'
try {
  $p = Start-Process $c2r -Verb RunAs -PassThru -ArgumentList 'scenario=install','scenariosubtype=ARP','sourcetype=None','productstoadd=LanguagePack.16_ja-jp_x-none','culture=ja-jp','version.16=16.0'
  "started elevated: process $($p.Id)"
} catch { "not started: $($_.Exception.Message)" }
