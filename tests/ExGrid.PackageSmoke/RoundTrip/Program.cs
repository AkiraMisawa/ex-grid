using PackageSmoke;

// check.sh fails on a non-zero exit: the Snapshot read back through the packed packages is not the
// one written (ADR-0065, DA-16).
var differences = await ArrowRoundTrip.RunAsync(CancellationToken.None);
foreach (var difference in differences)
    Console.Error.WriteLine($"Arrow round trip: {difference}");
if (differences.Count > 0)
    return 1;
Console.WriteLine("Arrow round trip: the Snapshot read back through the packed packages is the one written");
// A workbook written and read back through the packed ExGrid.Data.Xlsx (ADR-0330).
var workbook = await XlsxRoundTrip.RunAsync(CancellationToken.None);
foreach (var difference in workbook)
    Console.Error.WriteLine($"Xlsx round trip: {difference}");
if (workbook.Count > 0)
    return 1;
Console.WriteLine("Xlsx round trip: the workbook read back through the packed package is the one written");
await PivotReportRoundTrip.RunAsync();
return 0;
