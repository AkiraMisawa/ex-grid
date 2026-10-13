# h6: the Excel the clipboard probe started, quit: only when every workbook open in it is the driver's own
# scratch book (marked by the Name ExSheetVerifyScratch); otherwise nothing is touched and it says so.
try { $xl = [Runtime.InteropServices.Marshal]::GetActiveObject('Excel.Application') } catch { 'no Excel running'; exit 0 }
$books = @($xl.Workbooks); $other = @($books | Where-Object { -not (@($_.Names) | Where-Object { $_.Name -eq 'ExSheetVerifyScratch' }) })
if ($other.Count) { "not quit: other workbooks are open ($($other.Count))"; exit 0 }
foreach ($b in $books) { $b.Close($false) }
$xl.Quit(); [void][Runtime.InteropServices.Marshal]::ReleaseComObject($xl); 'quit'
