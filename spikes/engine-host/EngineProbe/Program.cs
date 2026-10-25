// Probe.cs on the .NET JIT: `dotnet run -c Release -- [repeats]`. `-- document` prints the head of the
// Sheet Document's JSON instead, the form the Worker is handed (client/src/sheet-page.ts writes it), and
// `-- report` a report Window as PivotReportJson writes it (client/src/pivot-page.ts reads it).
using ExPivot.Engine;

if (args is ["document"])
{
    Console.WriteLine(EngineHost.Probe.SheetDocument().ToJson()[..600]);
    return;
}
if (args is ["report"])
{
    await using var reports = PivotReportSource.From(PivotSource.From(EngineHost.Probe.MakeTrades(), EngineHost.Probe.Fields));
    var update = await reports.WindowAsync(new PivotReportRequest("r", EngineHost.Probe.Layout("Currency"),
        new PivotReportSettings { CultureName = "en-US" }, new PivotReportWindow(0, 3)));
    Console.WriteLine(PivotReportJson.Write(update));
    return;
}
Console.WriteLine(await EngineHost.Probe.Run("jit", args.Length > 0 ? int.Parse(args[0], System.Globalization.CultureInfo.InvariantCulture) : 5));
