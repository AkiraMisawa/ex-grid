using System.Diagnostics;
using System.Text.Json;
using Bench.Live;
using ExGrid;
using ExGrid.Components;
using Microsoft.AspNetCore.Components;

namespace Bench.BatchCount;

/// <summary>
/// Candidate 5 in the real grid: what one new Window instance costs ExGrid when the Window is a
/// whole large result (as under GridSource.From) and only one off-screen row changed — the grid's
/// parameter pass (ApplyState, with RequireDistinctRows over every row) and its render, in which
/// every painted row skips. Against it, the same Window instance pushed again. .NET only, no DOM.
/// </summary>
public static class WindowPass
{
    public static async Task RunAsync(string output, int[] sizes, int runs)
    {
        var results = new List<object>();
        foreach (var n in sizes)
        {
            var random = new Random(n);
            var rows = new LiveRecord[n];
            for (var i = 0; i < n; i++)
            {
                var values = new decimal[LiveTicker.MaxColumns];
                for (var c = 0; c < 20; c++)
                    values[c] = Math.Round((decimal)((random.NextDouble() - 0.5) * 2_000_000), 2);
                rows[i] = new LiveRecord(i, "BK" + (i % 120).ToString("000", System.Globalization.CultureInfo.InvariantCulture), values);
            }
            var columns = RealGrid.Columns(20);
            var renderer = new CountingRenderer(new RealGrid.Services()) { Counting = false };
            var grid = renderer.Create<ExGrid<LiveRecord>>();
            var id = renderer.Attach(grid);

            async Task<double> PushAsync(IReadOnlyList<LiveRecord> window)
            {
                var parameters = ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(ExGrid<LiveRecord>.Window)] = window,
                    [nameof(ExGrid<LiveRecord>.Columns)] = columns,
                    [nameof(ExGrid<LiveRecord>.ViewportHeight)] = (ViewportSize)1000,
                    [nameof(ExGrid<LiveRecord>.ViewportWidth)] = (ViewportSize)2200,
                });
                var t0 = Stopwatch.GetTimestamp();
                await renderer.Dispatcher.InvokeAsync(() => renderer.RenderRootAsync(id, parameters));
                var ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
                for (var settle = 0; settle < 5; settle++)
                    await renderer.Dispatcher.InvokeAsync(() => Task.Yield());
                return ms;
            }

            await PushAsync(rows);
            var same = new List<double>();
            var replaced = new List<double>();
            var current = rows;
            for (var r = 0; r < runs + 5; r++)
            {
                GC.Collect();
                var sameMs = await PushAsync(current);
                // A new Window instance: one row near the end replaced by a new instance, off screen.
                var next = (LiveRecord[])current.Clone();
                var at = n - 1 - r % 100;
                next[at] = new LiveRecord(current[at].Id, current[at].Book, (decimal[])current[at].Values.Clone());
                GC.Collect();
                var replacedMs = await PushAsync(next);
                current = next;
                if (r >= 5)
                {
                    same.Add(sameMs);
                    replaced.Add(replacedMs);
                }
            }
            await renderer.DisposeAsync();

            static (double Min, double Median, double Max) Stat(List<double> s)
            {
                var o = s.OrderBy(x => x).ToArray();
                return (o[0], o[o.Length / 2], o[^1]);
            }
            var a = Stat(same);
            var b = Stat(replaced);
            results.Add(new { rows = n, runs, sameInstance = new { a.Min, a.Median, a.Max }, newInstanceOneRowOffScreen = new { b.Min, b.Median, b.Max } });
            Console.WriteLine($"Window of {n,9:N0} rows: same instance pushed again {a.Median,8:F3} ms (min {a.Min:F3}); new instance, one off-screen row replaced {b.Median,8:F3} ms (min {b.Min:F3}, max {b.Max:F3})");
        }
        File.WriteAllText(output, JsonSerializer.Serialize(new { results }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"wrote {output}");
    }
}
