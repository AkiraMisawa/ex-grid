using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Bench.BatchCount;
using Bench.Live;
using Microsoft.AspNetCore.Components;

// dotnet run -c Release -- <out.json> [ticks] [warmup] [timing warm-up]
//
// Per configuration (columns 20/50, k 1/5/40, 3 changed cells or all, variant A/B), the same ticks
// as the /live page's: a timing pass with the counting off, so that the clock covers Blazor's render
// alone, then a counting pass per variant, whose numbers are deterministic.

if (args.Length > 0 && args[0] == "calibrate")
{
    // dotnet run -c Release -- calibrate <out.json> [ticks] [warmup] [column width px]: the real
    // ExGrid, variant A.
    await RealGrid.RunAsync(args.Length > 1 ? args[1] : "realgrid.json",
        args.Length > 2 ? int.Parse(args[2]) : 200, args.Length > 3 ? int.Parse(args[3]) : 20,
        args.Length > 4 ? double.Parse(args[4], System.Globalization.CultureInfo.InvariantCulture) : 160);
    return;
}

if (args.Length > 0 && args[0] == "window")
{
    // dotnet run -c Release -- window <out.json> [sizes] [runs]: candidate 5 in the real grid.
    await WindowPass.RunAsync(args.Length > 1 ? args[1] : "window.json",
        args.Length > 2 ? args[2].Split(',').Select(int.Parse).ToArray() : [45_017, 451_115],
        args.Length > 3 ? int.Parse(args[3]) : 30);
    return;
}

var output = args.Length > 0 ? args[0] : "batchcount.json";
var ticks = args.Length > 1 ? int.Parse(args[1]) : 200;
var warmup = args.Length > 2 ? int.Parse(args[2]) : 50;
var timingWarmup = args.Length > 3 ? int.Parse(args[3]) : 1000;

var results = new List<object>();
Console.WriteLine($"{"variant",-16} {"config",-24} {"diffs",7} {"edits",8} {"frames",8} {"strB",8} {"estB",9} {"disposed",8}  {"render ms med",13} {"p95",7}");
// The whole matrix runs twice and only the second pass is kept: in the first, the JIT is still
// tiering up, which made the first configurations slower than later, larger ones.
for (var pass = 0; pass < 2; pass++)
foreach (var changed in new[] { 3, -1 })
{
    foreach (var cols in new[] { 20, 50 })
    {
        foreach (var k in new[] { 1, 5, 40 })
        {
            var seed = 1000 * cols + 10 * k + (changed < 0 ? 1 : 0);

            // Timing pass: the same ticks, A and B interleaved tick by tick so that whatever the
            // machine and the JIT do lands on both alike, nothing done with the batch.
            var timing = new Dictionary<bool, List<double>> { [true] = new(ticks), [false] = new(ticks) };
            {
                var mounted = new Dictionary<bool, (CountingRenderer Renderer, LiveTicker Ticker, LiveGrid Grid, LiveCounters Counters)>
                {
                    [true] = await MountAsync(seed, cols, true),
                    [false] = await MountAsync(seed, cols, false),
                };
                foreach (var m in mounted.Values)
                    m.Renderer.Counting = false;
                for (var t = 0; t < timingWarmup + ticks; t++)
                {
                    foreach (var byInstance in new[] { true, false })
                    {
                        var (renderer, ticker, grid, _) = mounted[byInstance];
                        ticker.Tick(k, changed, cols);
                        var t0 = Stopwatch.GetTimestamp();
                        await renderer.Dispatcher.InvokeAsync(grid.RenderAsync);
                        var ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
                        if (t >= timingWarmup)
                            timing[byInstance].Add(ms);
                    }
                }
                foreach (var m in mounted.Values)
                    await m.Renderer.DisposeAsync();
            }

            foreach (var byInstance in new[] { true, false })
            {
                var variant = byInstance ? "A: instance key" : "B: stable key";

                // Counting pass.
                var (renderer, ticker, grid, counters) = await MountAsync(seed, cols, byInstance);
                for (var w = 0; w < warmup; w++)
                    await TickAsync(renderer, ticker, grid, k, changed, cols);
                renderer.Take();
                counters.Take();
                var perTick = new List<BatchCounts>(ticks);
                var mounts = 0;
                var renders = 0;
                for (var t = 0; t < ticks; t++)
                {
                    await TickAsync(renderer, ticker, grid, k, changed, cols);
                    perTick.Add(renderer.Take());
                    var (m, r, _) = counters.Take();
                    mounts += m;
                    renders += r;
                }
                await renderer.DisposeAsync();

                var times = timing[byInstance];

                var editTypes = perTick.SelectMany(p => p.EditsByType.Keys).Distinct().OrderBy(x => x)
                    .ToDictionary(x => x, x => perTick.Average(p => p.EditsByType.GetValueOrDefault(x)));
                var frameTypes = perTick.SelectMany(p => p.FramesByType.Keys).Distinct().OrderBy(x => x)
                    .ToDictionary(x => x, x => perTick.Average(p => p.FramesByType.GetValueOrDefault(x)));
                var sorted = times.OrderBy(x => x).ToArray();
                var result = new
                {
                    Variant = variant,
                    Columns = cols,
                    K = k,
                    ChangedCells = changed,
                    Ticks = ticks,
                    BatchesPerTick = perTick.Average(p => p.Batches),
                    Diffs = perTick.Average(p => p.Diffs),
                    Edits = perTick.Average(p => p.Edits),
                    EditsByType = editTypes,
                    Frames = perTick.Average(p => p.Frames),
                    FramesByType = frameTypes,
                    Strings = perTick.Average(p => p.Strings),
                    StringBytes = perTick.Average(p => p.StringBytes),
                    EstimatedBytes = perTick.Average(p => p.EstimatedBytes),
                    EstimatedBytesMin = perTick.Min(p => p.EstimatedBytes),
                    EstimatedBytesMax = perTick.Max(p => p.EstimatedBytes),
                    DisposedComponents = perTick.Average(p => p.DisposedComponents),
                    MountsPerTick = (double)mounts / ticks,
                    RendersPerTick = (double)renders / ticks,
                    RenderMsMin = sorted[0],
                    RenderMsMedian = sorted[sorted.Length / 2],
                    RenderMsP95 = sorted[(int)Math.Ceiling(0.95 * sorted.Length) - 1],
                    RenderMsMax = sorted[^1],
                };
                if (pass == 0)
                    continue;
                results.Add(result);
                Console.WriteLine($"{variant,-16} {$"{cols} cols, k {k}, {(changed < 0 ? "all" : changed.ToString())} cells",-24} "
                    + $"{result.Diffs,7:F1} {result.Edits,8:F1} {result.Frames,8:F1} {result.StringBytes,8:F0} {result.EstimatedBytes,9:F0} {result.DisposedComponents,8:F1}  "
                    + $"{result.RenderMsMedian,13:F3} {result.RenderMsP95,7:F3}");
            }
        }
    }
}

var environment = new Dictionary<string, string>
{
    ["runtime"] = RuntimeInformation.FrameworkDescription,
    ["os"] = RuntimeInformation.OSDescription,
    ["arch"] = RuntimeInformation.ProcessArchitecture.ToString(),
    ["components"] = typeof(ComponentBase).Assembly.GetName().Version?.ToString() ?? "",
#if DEBUG
    ["configuration"] = "Debug",
#else
    ["configuration"] = "Release",
#endif
};
File.WriteAllText(output, JsonSerializer.Serialize(new { environment, ticks, warmup, timingWarmup, results }, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"wrote {output}");

static async Task<(CountingRenderer, LiveTicker, LiveGrid, LiveCounters)> MountAsync(int seed, int cols, bool byInstance)
{
    var renderer = new CountingRenderer();
    var ticker = new LiveTicker(40, seed);
    var counters = new LiveCounters();
    var grid = new LiveGrid();
    var id = renderer.Attach(grid);
    var parameters = ParameterView.FromDictionary(new Dictionary<string, object?>
    {
        [nameof(LiveGrid.Ticker)] = ticker,
        [nameof(LiveGrid.Columns)] = new LiveColumns(cols),
        [nameof(LiveGrid.KeyByInstance)] = byInstance,
        [nameof(LiveGrid.IdPrefix)] = "g",
        [nameof(LiveGrid.Counters)] = counters,
    });
    await renderer.Dispatcher.InvokeAsync(() => renderer.RenderRootAsync(id, parameters));
    return (renderer, ticker, grid, counters);
}

static Task TickAsync(CountingRenderer renderer, LiveTicker ticker, LiveGrid grid, int k, int changed, int cols)
{
    ticker.Tick(k, changed, cols);
    return renderer.Dispatcher.InvokeAsync(grid.RenderAsync);
}
