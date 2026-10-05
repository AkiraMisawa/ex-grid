using System.Runtime.InteropServices;
using System.Text.Json;
using Requery;

// dotnet run -c Release -- check [cases] [seed]
// dotnet run -c Release -- time <out.json> [sizes, comma-separated] [ks, comma-separated] [no-a]
//   (no-a leaves out today's k ReplaceRow calls, for batches too large to run that way)
//
// M5 gates M4: `time` runs the property check first and refuses to time a prototype that
// disagrees with GridQueryEngine.Apply.

var command = args.Length > 0 ? args[0] : "check";
switch (command)
{
    case "check":
    {
        var cases = args.Length > 1 ? int.Parse(args[1]) : 5000;
        var seed = args.Length > 2 ? int.Parse(args[2]) : 20261005;
        return Check(cases, seed) ? 0 : 1;
    }
    case "time":
    {
        var output = args.Length > 1 ? args[1] : "requery.json";
        var sizes = args.Length > 2 ? args[2].Split(',').Select(int.Parse).ToArray() : [100_000, 1_000_000];
        var ks = args.Length > 3 ? args[3].Split(',').Select(int.Parse).ToArray() : [1, 100, 1000];
        if (IncrementalQuery.Fault is not null)
        {
            Console.Error.WriteLine("REQUERY_FAULT is set: refusing to time a prototype with a planted defect.");
            return 1;
        }
        if (!Check(2000, 7))
            return 1;
        var environment = new Dictionary<string, string>
        {
            ["runtime"] = RuntimeInformation.FrameworkDescription,
            ["os"] = RuntimeInformation.OSDescription,
            ["arch"] = RuntimeInformation.ProcessArchitecture.ToString(),
            ["processors"] = Environment.ProcessorCount.ToString(),
            ["serverGc"] = System.Runtime.GCSettings.IsServerGC.ToString(),
#if DEBUG
            ["configuration"] = "Debug",
#else
            ["configuration"] = "Release",
#endif
            ["started"] = DateTimeOffset.Now.ToString("O"),
        };
        var stats = Timing.Run(sizes, ks, Console.Out, withToday: !(args.Length > 4 && args[4] == "no-a"));
        environment["finished"] = DateTimeOffset.Now.ToString("O");
        File.WriteAllText(output, JsonSerializer.Serialize(new { environment, stats }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"wrote {output}");
        return 0;
    }
    default:
        Console.Error.WriteLine($"Unknown command '{command}'.");
        return 2;
}

static bool Check(int cases, int seed)
{
    var outcome = PropertyCheck.Run(cases, seed);
    Console.WriteLine($"M5: {outcome.Cases} cases, {outcome.Batches} batches, {outcome.RowsCompared:N0} rows compared, "
        + $"{outcome.ReplaceRowCases} cases also against ReplaceRow one at a time (seed {seed}): "
        + (outcome.Failures.Count == 0 ? "the incremental result equals GridQueryEngine.Apply in every batch" : $"{outcome.Failures.Count} FAILURE(S)"));
    foreach (var (what, count) in outcome.Coverage.OrderBy(c => c.Key, StringComparer.Ordinal))
        Console.WriteLine($"  {what}: {count}");
    foreach (var failure in outcome.Failures)
        Console.WriteLine("  " + failure);
    return outcome.Failures.Count == 0;
}
