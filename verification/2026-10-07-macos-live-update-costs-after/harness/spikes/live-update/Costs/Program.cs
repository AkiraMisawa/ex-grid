using System.Globalization;
using Costs;

// Ticket 01 (docs/specs/live-data/issues/01-measure-what-an-update-costs-end-to-end.md), repeated
// after the changes by ticket 13 (…/13-measure-after-the-changes.md).
// Run in Release with DOTNET_TieredCompilation=0, from the repository root:
//
//   DOTNET_TieredCompilation=0 nix develop -c dotnet run -c Release --project spikes/live-update/Costs -- grid  <out.json> <rows> <k,k,…> [warmup] [runs]
//   DOTNET_TieredCompilation=0 nix develop -c dotnet run -c Release --project spikes/live-update/Costs -- pivot <out.json> <A> <B> <k,k,…> [warmup] [runs]
//   DOTNET_TieredCompilation=0 nix develop -c dotnet run -c Release --project spikes/live-update/Costs -- vouch <out.json> <rows>
//   … -- pivot-memory <out.json> <A> <B> <k> <redraws>
//   … -- pivot-gc <out.json> <A> <B> [warmup] [runs] [full|compact|none] [verbose|info|off]
//   … -- pivot-steady <out.json> <A> <B> [redraws]
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
    case "push-count":
    {
        var rows = int.Parse(args[2], CultureInfo.InvariantCulture);
        var updates = Arg(args, 3, 3);
        result = new { mode = "push-count", machine, measured = await GridCosts.CountPushedAsync(rows, updates) };
        break;
    }
    case "pivot-gc":
    {
        var a = int.Parse(args[2], CultureInfo.InvariantCulture);
        var b = int.Parse(args[3], CultureInfo.InvariantCulture);
        var warmup = Arg(args, 4, 3);
        var runs = Arg(args, 5, 15);
        var precollect = args.Length > 6 ? args[6] : "full";
        var listen = args.Length > 7 ? args[7] : "verbose";
        result = new { mode = "pivot-gc", machine, measured = await PivotGc.RunAsync(a, b, warmup, runs, precollect, listen) };
        break;
    }
    case "pivot-steady":
    {
        var a = int.Parse(args[2], CultureInfo.InvariantCulture);
        var b = int.Parse(args[3], CultureInfo.InvariantCulture);
        var redraws = Arg(args, 4, 60);
        result = new { mode = "pivot-steady", machine, measured = await PivotGc.SteadyAsync(a, b, redraws) };
        break;
    }
    default:
        Console.Error.WriteLine($"no mode {args[0]}");
        return 2;
}
Machine.Write(output, result);
return 0;
