using System.Text.Json;
using Bench.Live;
using ExGrid;
using ExGrid.Columns;
using ExGrid.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Bench.BatchCount;

/// <summary>
/// Calibration: the same ticks rendered by the real ExGrid (variant A, its only keying today),
/// so that what the Bench.Live mirror's rows carry can be read against what ExGrid's rows carry.
/// ExGrid's JavaScript is stood in for by a runtime that answers every call with a default and
/// every module or handle with itself; the grid is given a Viewport tall and wide enough to paint
/// all 40 rows and every column.
/// </summary>
public static class RealGrid
{
    public static async Task RunAsync(string output, int ticks, int warmup, double widthPx = DefaultCalibrationWidthPx)
    {
        var results = new List<object>();
        Console.WriteLine($"{"real ExGrid (A)",-16} {"config",-24} {"batches",7} {"diffs",7} {"edits",8} {"frames",8} {"strB",8} {"estB",9} {"disposed",8}");
        foreach (var changed in new[] { 3, -1 })
        {
            foreach (var cols in new[] { 20, 50 })
            {
                foreach (var k in new[] { 1, 5, 40 })
                {
                    var seed = 1000 * cols + 10 * k + (changed < 0 ? 1 : 0);
                    var ticker = new LiveTicker(40, seed);
                    // By default wide enough that no number overflows: at the mirror's 90 px most N2
                    // values of seven digits are painted as #### (ADR-0016), which adds a span and
                    // an aria-label per cell that the mirror does not have.
                    var columns = Columns(cols, widthPx);
                    var renderer = new CountingRenderer(new Services());
                    var grid = renderer.Create<ExGrid<LiveRecord>>();
                    var id = renderer.Attach(grid);

                    async Task PushAsync()
                    {
                        var parameters = ParameterView.FromDictionary(new Dictionary<string, object?>
                        {
                            [nameof(ExGrid<LiveRecord>.Window)] = ticker.Window,
                            [nameof(ExGrid<LiveRecord>.Columns)] = columns,
                            [nameof(ExGrid<LiveRecord>.ViewportHeight)] = (ViewportSize)2000,
                            [nameof(ExGrid<LiveRecord>.ViewportWidth)] = (ViewportSize)((widthPx + 10) * (cols + 2)),
                        });
                        await renderer.Dispatcher.InvokeAsync(() => renderer.RenderRootAsync(id, parameters));
                        // Whatever the grid renders again after the push, on its own continuations.
                        for (var settle = 0; settle < 5; settle++)
                            await renderer.Dispatcher.InvokeAsync(() => Task.Yield());
                    }

                    await PushAsync();
                    for (var w = 0; w < warmup; w++)
                    {
                        ticker.Tick(k, changed, cols);
                        await PushAsync();
                    }
                    renderer.Take();
                    var perTick = new List<BatchCounts>(ticks);
                    for (var t = 0; t < ticks; t++)
                    {
                        ticker.Tick(k, changed, cols);
                        await PushAsync();
                        perTick.Add(renderer.Take());
                    }
                    await renderer.DisposeAsync();

                    var result = new
                    {
                        Variant = "real ExGrid (A)",
                        Columns = cols,
                        K = k,
                        ChangedCells = changed,
                        Ticks = ticks,
                        BatchesPerTick = perTick.Average(p => p.Batches),
                        Diffs = perTick.Average(p => p.Diffs),
                        Edits = perTick.Average(p => p.Edits),
                        EditsByType = perTick.SelectMany(p => p.EditsByType.Keys).Distinct().OrderBy(x => x)
                            .ToDictionary(x => x, x => perTick.Average(p => p.EditsByType.GetValueOrDefault(x))),
                        Frames = perTick.Average(p => p.Frames),
                        FramesByType = perTick.SelectMany(p => p.FramesByType.Keys).Distinct().OrderBy(x => x)
                            .ToDictionary(x => x, x => perTick.Average(p => p.FramesByType.GetValueOrDefault(x))),
                        StringBytes = perTick.Average(p => p.StringBytes),
                        EstimatedBytes = perTick.Average(p => p.EstimatedBytes),
                        DisposedComponents = perTick.Average(p => p.DisposedComponents),
                    };
                    results.Add(result);
                    Console.WriteLine($"{"real ExGrid (A)",-16} {$"{cols} cols, k {k}, {(changed < 0 ? "all" : changed.ToString())} cells",-24} "
                        + $"{result.BatchesPerTick,7:F1} {result.Diffs,7:F1} {result.Edits,8:F1} {result.Frames,8:F1} {result.StringBytes,8:F0} {result.EstimatedBytes,9:F0} {result.DisposedComponents,8:F1}");
                }
            }
        }
        File.WriteAllText(output, JsonSerializer.Serialize(new { ticks, warmup, results }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"wrote {output}");
    }

    private const double DefaultCalibrationWidthPx = 160;

    internal static List<GridColumn<LiveRecord>> Columns(int count, double widthPx = 90)
    {
        var columns = new List<GridColumn<LiveRecord>>(count + 1)
        {
            new("Book", ColumnType.Text, r => r.Book, width: new ColumnWidthSpec(ColumnWidth.Fixed(widthPx))),
        };
        for (var c = 0; c < count; c++)
        {
            var index = c;
            columns.Add(new GridColumn<LiveRecord>($"M{c}", ColumnType.Number, r => r.Values[index],
                width: new ColumnWidthSpec(ColumnWidth.Fixed(widthPx)), format: v => ((decimal)v).ToString("N2", System.Globalization.CultureInfo.InvariantCulture)));
        }
        return columns;
    }

    internal sealed class Services : IServiceProvider
    {
        private readonly FakeJS _js = new();

        public object? GetService(Type serviceType)
            => serviceType == typeof(IJSRuntime) ? _js
             : serviceType == typeof(IServiceProvider) ? this
             : null;
    }

    /// <summary>Every call answered with a default; a module or a handle is this object again.</summary>
    private sealed class FakeJS : IJSRuntime, IJSObjectReference
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(Make<TValue>());

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => ValueTask.FromResult(Make<TValue>());

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private TValue Make<TValue>() => typeof(TValue) == typeof(IJSObjectReference) ? (TValue)(object)this : default!;
    }
}
