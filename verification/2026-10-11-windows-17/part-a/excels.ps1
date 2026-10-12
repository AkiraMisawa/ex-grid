Get-CimInstance Win32_Process | Where-Object { $_.Name -eq 'EXCEL.EXE' } | ForEach-Object { "{0} {1} {2}" -f $_.ProcessId, $_.CreationDate, $_.CommandLine }
