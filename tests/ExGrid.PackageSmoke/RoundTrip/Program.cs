using PackageSmoke;

// check.sh fails on a non-zero exit: the Snapshot read back through the packed packages is not the
// one written (ADR-0064, DA-16).
var differences = await ArrowRoundTrip.RunAsync(CancellationToken.None);
foreach (var difference in differences)
    Console.Error.WriteLine($"Arrow round trip: {difference}");
if (differences.Count > 0)
    return 1;
Console.WriteLine("Arrow round trip: the Snapshot read back through the packed packages is the one written");
return 0;
