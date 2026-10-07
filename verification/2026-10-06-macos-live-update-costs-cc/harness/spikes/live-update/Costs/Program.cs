using System.Globalization;
using Costs;

// Ticket 01 (docs/specs/live-data/issues/01-measure-what-an-update-costs-end-to-end.md).
// Run in Release with DOTNET_TieredCompilation=0, from the repository root:
//
//   DOTNET_TieredCompilation=0 nix develop -c dotnet run -c Release --project spikes/live-update/Costs -- grid  <out.json> <rows> <k,k,…> [warmup] [runs]
//   DOTNET_TieredCompilation=0 nix develop -c dotnet run -c Release --project spikes/live-update/Costs -- pivot <out.json> <A> <B> <k,k,…> [warmup] [runs]
//   DOTNET_TieredCompilation=0 nix develop -c dotnet run -c Release --project spikes/live-update/Costs -- vouch <out.json> <rows>
//
// The output path is written as given: never under Path.GetTempPath(), which nix develop removes.

if (args.Length < 2)
{
    Console.Error.WriteLine("grid <out.json> <rows> <k,k,…> [warmup] [runs] | pivot <out.json> <A> <B> <k,k,…> [warmup] [runs] | vouch <out.json> <rows>");
    return 2;
}

static int[] List(string text) => [.. text.Split(',').Select(s => int.Parse(s, CultureInfo.InvariantCulture))];
static int Arg(string[] args, int at, int otherwise) => args.Length > at ? int.Parse(args[at], CultureInfo.InvariantCulture) : otherwise;

var output = args[1];
var machine = Machine.Snapshot();
Console.WriteLine($"load {((dynamic)machine).loadavg}; TieredCompilation={Environment.GetEnvironmentVariable("DOTNET_TieredCompilation") ?? "(default)"}");
object result;
switch (args[0])
{
    case "grid":
    {
        var rows = int.Parse(args[2], CultureInfo.InvariantCulture);
        var ks = List(args[3]);
        var warmup = Arg(args, 4, 3);
        var runs = Arg(args, 5, 15);
        result = new { mode = "grid", warmup, runs, machine, measured = await GridCosts.RunAsync(rows, ks, warmup, runs) };
        break;
    }
    case "pivot":
    {
        var a = int.Parse(args[2], CultureInfo.InvariantCulture);
        var b = int.Parse(args[3], CultureInfo.InvariantCulture);
        var ks = List(args[4]);
        var warmup = Arg(args, 5, 3);
        var runs = Arg(args, 6, 15);
        result = new { mode = "pivot", warmup, runs, machine, measured = await PivotCosts.RunAsync(a, b, ks, warmup, runs) };
        break;
    }
    case "pivot-memory":
    {
        var a = int.Parse(args[2], CultureInfo.InvariantCulture);
        var b = int.Parse(args[3], CultureInfo.InvariantCulture);
        var k = int.Parse(args[4], CultureInfo.InvariantCulture);
        var redraws = int.Parse(args[5], CultureInfo.InvariantCulture);
        result = new { mode = "pivot-memory", machine, measured = await PivotCosts.MemoryAsync(a, b, k, redraws) };
        break;
    }
    case "vouch":
    {
        var rows = int.Parse(args[2], CultureInfo.InvariantCulture);
        result = new { mode = "vouch", machine, measured = await GridCosts.CheckVouchAsync(rows) };
        break;
    }
    default:
        Console.Error.WriteLine($"no mode {args[0]}");
        return 2;
}
Machine.Write(output, result);
return 0;
