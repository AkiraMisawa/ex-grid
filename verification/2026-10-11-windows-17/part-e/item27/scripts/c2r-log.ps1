$logs = @(Get-ChildItem $env:TEMP, 'C:\Windows\Temp' -Filter '*.log' -ErrorAction SilentlyContinue | Where-Object { $_.LastWriteTime -gt (Get-Date).AddMinutes(-15) } | Sort-Object LastWriteTime)
$logs | ForEach-Object { "log $($_.FullName) $($_.Length) $($_.LastWriteTime.ToString('HH:mm:ss'))" }
foreach ($l in $logs) {
  Select-String -Path $l.FullName -Pattern '30038|ErrorCode|HRESULT.*0x8|failed|Failure|DiskSpace|ja-jp' -ErrorAction SilentlyContinue | Select-Object -Last 25 | ForEach-Object { $_.Line.Substring(0, [Math]::Min(260, $_.Line.Length)) }
}
Get-PSDrive C | ForEach-Object { "C: free {0:N1} GB" -f ($_.Free / 1GB) }
