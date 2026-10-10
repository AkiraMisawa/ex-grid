using System.Collections;
using System.Diagnostics;
using System.Reflection;
using ExGrid;
using ExGrid.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Time.Testing;

namespace Costs;

/// <summary>
/// ExGrid over GridSource.From keyed, as /grid-live-local runs it (its trades, columns, Viewport and
/// amendment), at n rows and k changes a batch. One live update, step by step:
/// <list type="number">
/// <item>the source folding the batch (Apply, while it gathers, so nothing is published);</item>
/// <item>its publication (PublishGathered): the incremental requery (LiveRequery.Apply, also timed
/// alone on the same inputs) and the Change Highlight's record (CellChangeTimes.Record, also alone);</item>
/// <item>the grid taking the new Window in (ApplyState, reached by reflection), and within it the
/// check a pushed Window gets (RequireDistinctKeys, RequireDistinctRows) and the Selection Summary's
/// walk (NoteRowsForSummary), each alone on the same two Windows;</item>
/// <item>the render (the renderer's ProcessPendingRender), with the rows it rendered and the bytes its
/// batches carry (M2's estimate).</item>
/// </list>
/// And the whole update as the grid takes it (Apply on the renderer's context, interval 0), as a sum to
/// check the steps against. A pushed Window's ApplyState, with and without a Row Key, stands for an
/// ExGrid page that pushes the same Window itself (ticket 02).
/// </summary>
public static class GridCosts
{
    private static readonly MethodInfo LiveRequeryApply = typeof(InMemoryGridSource<>).Assembly
        .GetType("ExGrid.LiveRequery", throwOnError: true)!.GetMethod("Apply", BindingFlags.Public | BindingFlags.Static)!
        .MakeGenericMethod(typeof(Trade));

    private static readonly Type RowChange = typeof(InMemoryGridSource<>).Assembly
        .GetType("ExGrid.RowChange`1", throwOnError: true)!.MakeGenericType(typeof(Trade));

    public static async Task<object> RunAsync(int n, int[] ks, int warmup, int runs)
    {
        Console.WriteLine($"== ExGrid, GridSource.From keyed, {n:N0} rows ==");
        var t0 = Stopwatch.GetTimestamp();
        var trades = Trades.Generate(n);
        var infos = Trades.Columns.Select(c => c.Info).ToArray();
        Console.WriteLine($"   {n:N0} trades made in {Clock.Ms(t0):F0} ms");

        // The source the grid is bound to, as the page binds it: interval 0, its Change Highlight read.
        var system = TimeProvider.System;
        var source = GridSource.From(trades, t => t.Id);
        var cellChangedAt = source.CellChangedAt;
        source.GatherInterval = TimeSpan.Zero;
        var newest = (Trade[])trades.Clone();

        var renderer = new CountingRenderer(new Services(system)) { Watched = typeof(ExGridRow<>) };
        var grid = renderer.Create<ExGrid<Trade>>();
        var gridId = renderer.Attach(grid);
        await renderer.Dispatcher.InvokeAsync(() => renderer.RenderRootAsync(gridId, ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(ExGrid<Trade>.Source)] = source,
            [nameof(ExGrid<Trade>.Columns)] = Trades.Columns,
            [nameof(ExGrid<Trade>.PinnedColumnCount)] = 1,
            [nameof(ExGrid<Trade>.CellChangedAt)] = cellChangedAt,
            [nameof(ExGrid<Trade>.ChangeHighlightDuration)] = TimeSpan.FromSeconds(1),
            [nameof(ExGrid<Trade>.ViewportHeight)] = (ViewportSize)480,
            [nameof(ExGrid<Trade>.ViewportWidth)] = (ViewportSize)1270,
        })));
        await SettleAsync(renderer);
        var painted = PaintedRows(renderer);
        Console.WriteLine($"   grid bound; {painted} rows painted");

        // A second source over the same trades, bound to nothing, on a clock that does not move: its
        // first batch is published at once, every later one is gathered until PublishGathered.
        var fake = new FakeTimeProvider(DateTimeOffset.UnixEpoch.AddYears(56));
        var alone = GridSource.From(trades, t => t.Id, fake);
        alone.OnColumnsChanged(infos);
        _ = alone.CellChangedAt;
        var aloneNewest = (Trade[])trades.Clone();

        // A pushed Window (ticket 02): a grid handed the Window itself, with a Row Key of the
        // Consumer's own, and one with none (checked by instance).
        var pushKeyed = await PushGridAsync(renderer, system, source.Window, t => t.Id);
        var pushPlain = await PushGridAsync(renderer, system, source.Window, rowKey: null);
        // Ticket 07: the same pushed Window, vouched for beside its Row Key.
        var pushVouched = await PushGridAsync(renderer, system, source.Window, t => t.Id, vouch: true);

        var applyState = Reflect.Method(typeof(ExGrid<Trade>), "ApplyState");
        var noteRows = Reflect.Method(typeof(ExGrid<Trade>), "NoteRowsForSummary");
        var requireKeys = (Action<IReadOnlyList<Trade>, int, Func<Trade, object>>)Reflect.Method(typeof(ExGrid<Trade>), "RequireDistinctKeys")
            .CreateDelegate(typeof(Action<IReadOnlyList<Trade>, int, Func<Trade, object>>));
        var requireRows = (Action<IReadOnlyList<Trade>>)Reflect.Method(typeof(ExGrid<Trade>), "RequireDistinctRows")
            .CreateDelegate(typeof(Action<IReadOnlyList<Trade>>));
        var stateChanged = typeof(InMemoryGridSource<Trade>).GetEvent(nameof(InMemoryGridSource<Trade>.StateChanged))!;
        var handler = Reflect.Method(typeof(ExGrid<Trade>), "OnSourceStateChanged").CreateDelegate(typeof(Action), grid);
        var changeTimes = Reflect.Get(alone, "_changeTimes")!;
        var record = Reflect.Method(changeTimes.GetType(), "Record");

        var results = new List<object>();
        foreach (var k in ks)
        {
            Console.WriteLine($"-- k = {k:N0} changes a batch --");
            var random = new Random(20261006 + k);
            var machineBefore = Machine.Snapshot();

            // 1, 2. The source alone: fold, publication, and the requery and the record alone.
            if (!(bool)Reflect.Get(alone, "_publishing")! && alone.Window.Count > 0)
                alone.Apply(new GridChangeBatch<Trade>(changed: Trades.Amend(aloneNewest, 1, random)));
            var fold = new List<double>();
            var publish = new List<double>();
            Stat? requery = null;
            Stat? highlight = null;
            for (var r = 0; r < warmup + runs; r++)
            {
                var before = (Trade[])aloneNewest.Clone();
                var amended = Trades.Amend(aloneNewest, k, random);
                var batch = new GridChangeBatch<Trade>(changed: amended);
                Clock.Collect();
                var a = Stopwatch.GetTimestamp();
                alone.Apply(batch);
                var foldMs = Clock.Ms(a);
                if (r == warmup + runs - 1)
                {
                    // The last batch's publication, alone, on its own inputs: the Window before it,
                    // and the changes as PublishPending makes them (each changed row keeps its place
                    // in the base, its ordinal, which is its index: nothing was added or removed).
                    var window = Reflect.Get<Trade[]>(alone, "_window");
                    var ordinals = Reflect.Get<long[]>(alone, "_windowOrdinals");
                    var changes = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(RowChange))!;
                    var pairs = new List<(object Key, Trade Old, Trade New)>(amended.Length);
                    foreach (var row in amended)
                    {
                        var at = IndexOf(row.Id);
                        changes.Add(Activator.CreateInstance(RowChange, row.Id, true, before[at], (long)at, true, row, (long)at)!);
                        pairs.Add((row.Id, before[at], row));
                    }
                    var wholeBase = new Func<(Trade[], long[])>(() => throw new InvalidOperationException("whole requery"));
                    requery = Clock.Repeat(warmup, runs, () => LiveRequeryApply.Invoke(null,
                        [window, ordinals, changes, infos, null, Array.Empty<SortSpec>(), n, wholeBase, false]));
                    highlight = Clock.Repeat(warmup, runs, () => record.Invoke(changeTimes,
                        [infos, pairs, Array.Empty<object>(), Array.Empty<object>()]));
                }
                Clock.Collect();
                var p = Stopwatch.GetTimestamp();
                alone.PublishGathered();
                var publishMs = Clock.Ms(p);
                if (r >= warmup)
                {
                    fold.Add(foldMs);
                    publish.Add(publishMs);
                }
            }

            // 3, 4. The bound grid, each real update taken apart: the source's Apply with the grid's
            // own handler detached, then the grid's ApplyState and render, as its handler runs them.
            stateChanged.RemoveEventHandler(source, handler);
            var sourceStep = new List<double>();
            var takeIn = new List<double>();
            var render = new List<double>();
            var renderOnly = new List<double>();
            var rowsRendered = new List<double>();
            IReadOnlyList<Trade> windowBefore = source.Window;
            IReadOnlyList<Trade> windowAfter = source.Window;
            for (var r = 0; r < warmup + runs; r++)
            {
                var batch = new GridChangeBatch<Trade>(changed: Trades.Amend(newest, k, random));
                windowBefore = source.Window;
                double s = 0, t = 0, u = 0, uOnly = 0;
                BatchCounts? counts = null;
                await renderer.Dispatcher.InvokeAsync(() =>
                {
                    Clock.Collect();
                    var a = Stopwatch.GetTimestamp();
                    source.Apply(batch);
                    s = Clock.Ms(a);
                    Clock.Collect();
                    a = Stopwatch.GetTimestamp();
                    applyState.Invoke(grid, null);
                    t = Clock.Ms(a);
                    Clock.Collect();
                    renderer.Take();
                    a = Stopwatch.GetTimestamp();
                    Reflect.StateHasChanged(grid);
                    u = Clock.Ms(a);
                    (counts, uOnly) = renderer.Take();
                });
                await SettleAsync(renderer);
                windowAfter = source.Window;
                if (r >= warmup)
                {
                    sourceStep.Add(s);
                    takeIn.Add(t);
                    render.Add(u);
                    renderOnly.Add(uOnly);
                    rowsRendered.Add(counts!.WatchedRendered);
                }
            }
            stateChanged.AddEventHandler(source, handler);

            // The grid's own per-Window passes, alone, on the last update's two Windows.
            var summary = Clock.Repeat(warmup, runs, () =>
            {
                Reflect.Set(grid, "_summaryRows", windowBefore);
                Reflect.Set(grid, "_summaryRowsStart", 0);
                Reflect.Set(grid, "_summaryRowsTotal", (int?)windowBefore.Count);
                Reflect.Set(grid, "_window", windowAfter);
                noteRows.Invoke(grid, null);
            });
            // Where that pair first differs, which is where the walk stops; and the walk's worst
            // case, a Window whose last row alone is a new instance.
            var firstDifference = -1;
            for (var i = 0; i < windowAfter.Count && firstDifference < 0; i++)
            {
                if (!ReferenceEquals(windowAfter[i], windowBefore[i]))
                    firstDifference = i;
            }
            var lastReplaced = windowAfter.ToArray();
            lastReplaced[^1] = lastReplaced[^1] with { };
            var summaryWorst = Clock.Repeat(warmup, runs, () =>
            {
                Reflect.Set(grid, "_summaryRows", windowAfter);
                Reflect.Set(grid, "_summaryRowsStart", 0);
                Reflect.Set(grid, "_summaryRowsTotal", (int?)windowAfter.Count);
                Reflect.Set(grid, "_window", lastReplaced);
                noteRows.Invoke(grid, null);
            });
            Reflect.Set(grid, "_window", windowAfter);
            Reflect.Set(grid, "_summaryRows", windowAfter);
            var keyFn = source.RowKey!;
            var checkKeys = Clock.Repeat(warmup, runs, () => requireKeys(windowAfter, 0, keyFn));
            var checkRows = Clock.Repeat(warmup, runs, () => requireRows(windowAfter));
            var flip = false;
            var pushedKeyed = await OnContextAsync(renderer, () => Clock.Repeat(warmup, runs, () =>
            {
                pushKeyed.Window = (flip = !flip) ? windowAfter : windowBefore;
                applyState.Invoke(pushKeyed, null);
            }));
            var pushedPlain = await OnContextAsync(renderer, () => Clock.Repeat(warmup, runs, () =>
            {
                pushPlain.Window = (flip = !flip) ? windowAfter : windowBefore;
                applyState.Invoke(pushPlain, null);
            }));
            var pushedVouched = await OnContextAsync(renderer, () => Clock.Repeat(warmup, runs, () =>
            {
                pushVouched.Window = (flip = !flip) ? windowAfter : windowBefore;
                applyState.Invoke(pushVouched, null);
            }));
            // The vouched grid's whole take-in and render, where the painted rows' keys are checked.
            var pushedVouchedRender = await OnContextAsync(renderer, () => Clock.Repeat(warmup, runs, () =>
            {
                pushVouched.Window = (flip = !flip) ? windowAfter : windowBefore;
                applyState.Invoke(pushVouched, null);
                Reflect.StateHasChanged(pushVouched);
            }));
            await SettleAsync(renderer);

            // The whole update as the grid takes it, the handler attached: Apply on the renderer's
            // context, interval 0, publication, ApplyState and render inline.
            var whole = new List<double>();
            for (var r = 0; r < warmup + runs; r++)
            {
                var batch = new GridChangeBatch<Trade>(changed: Trades.Amend(newest, k, random));
                double e = 0;
                await renderer.Dispatcher.InvokeAsync(() =>
                {
                    Clock.Collect();
                    var a = Stopwatch.GetTimestamp();
                    source.Apply(batch);
                    e = Clock.Ms(a);
                });
                await SettleAsync(renderer);
                if (r >= warmup)
                    whole.Add(e);
            }

            // The bytes an update's batches carry, with counting on (not timed).
            renderer.Counting = true;
            var bytes = new List<BatchCounts>();
            for (var r = 0; r < runs; r++)
            {
                var batch = new GridChangeBatch<Trade>(changed: Trades.Amend(newest, k, random));
                renderer.Take();
                await renderer.Dispatcher.InvokeAsync(() => source.Apply(batch));
                await SettleAsync(renderer);
                bytes.Add(renderer.Take().Counts);
            }
            renderer.Counting = false;

            var result = new
            {
                rows = n,
                k,
                paintedRows = painted,
                windowRows = windowAfter.Count,
                sourceFold = Stat.Of(fold),
                sourcePublish = Stat.Of(publish),
                liveRequeryAlone = requery,
                changeHighlightRecordAlone = highlight,
                boundSourceApply = Stat.Of(sourceStep),
                gridApplyState = Stat.Of(takeIn),
                gridRender = Stat.Of(render),
                gridRenderInRenderer = Stat.Of(renderOnly),
                rowsRenderedPerUpdate = Stat.Of(rowsRendered),
                noteRowsForSummaryAlone = summary,
                noteRowsForSummaryFirstDifference = firstDifference,
                noteRowsForSummaryWorstCase = summaryWorst,
                requireDistinctKeysAlone = checkKeys,
                requireDistinctRowsAlone = checkRows,
                pushedWindowApplyStateWithRowKey = pushedKeyed,
                pushedWindowApplyStateByInstance = pushedPlain,
                pushedWindowApplyStateVouched = pushedVouched,
                pushedWindowApplyStateAndRenderVouched = pushedVouchedRender,
                wholeUpdate = Stat.Of(whole),
                perUpdateBatches = new
                {
                    batches = bytes.Average(b => b.Batches),
                    diffs = bytes.Average(b => b.Diffs),
                    edits = bytes.Average(b => b.Edits),
                    frames = bytes.Average(b => b.Frames),
                    stringBytes = bytes.Average(b => b.StringBytes),
                    estimatedBytes = Stat.Of(bytes.Select(b => (double)b.EstimatedBytes).ToArray()),
                    rowsRendered = bytes.Average(b => b.WatchedRendered),
                    rowsMounted = bytes.Average(b => b.WatchedMounted),
                    disposedComponents = bytes.Average(b => b.DisposedComponents),
                },
                machineBefore,
                machineAfter = Machine.Snapshot(),
            };
            results.Add(result);
            Print("fold (Apply, gathered)", result.sourceFold);
            Print("publish (PublishGathered)", result.sourcePublish);
            Print("  LiveRequery.Apply alone", requery!);
            Print("  CellChangeTimes.Record alone", highlight!);
            Print("bound source Apply (no grid)", result.boundSourceApply);
            Print("grid ApplyState", result.gridApplyState);
            Print($"  NoteRowsForSummary alone (stops at {firstDifference})", summary);
            Print("  NoteRowsForSummary, last row only", summaryWorst);
            Print("  RequireDistinctKeys alone", checkKeys);
            Print("  RequireDistinctRows alone", checkRows);
            Print("grid render", result.gridRender);
            Print("whole update", result.wholeUpdate);
            Print("pushed ApplyState, Row Key", pushedKeyed);
            Print("pushed ApplyState, instance", pushedPlain);
            Print("pushed ApplyState, vouched", pushedVouched);
            Print("pushed ApplyState+render, vouched", pushedVouchedRender);
            Console.WriteLine($"   rows rendered {result.rowsRenderedPerUpdate.Median} (of {painted} painted); bytes/update {result.perUpdateBatches.estimatedBytes.Median:F0}");
        }
        await renderer.DisposeAsync();
        return new { rows = n, results };

        // The page's trades keep their index: an amended trade replaces the one at its index.
        static int IndexOf(string id) => int.Parse(id.AsSpan(1), System.Globalization.CultureInfo.InvariantCulture) - 100000;
    }

    private static void Print(string name, Stat stat) => Console.WriteLine($"   {name,-34} {stat}");

    private static async Task<ExGrid<Trade>> PushGridAsync(CountingRenderer renderer, TimeProvider clock, IReadOnlyList<Trade> window, Func<Trade, object>? rowKey, bool vouch = false)
    {
        var grid = renderer.Create<ExGrid<Trade>>();
        var id = renderer.Attach(grid);
        var parameters = new Dictionary<string, object?>
        {
            [nameof(ExGrid<Trade>.Window)] = window,
            [nameof(ExGrid<Trade>.Columns)] = Trades.Columns,
            [nameof(ExGrid<Trade>.PinnedColumnCount)] = 1,
            [nameof(ExGrid<Trade>.ViewportHeight)] = (ViewportSize)480,
            [nameof(ExGrid<Trade>.ViewportWidth)] = (ViewportSize)1270,
        };
        if (rowKey is not null)
            parameters[nameof(ExGrid<Trade>.RowKey)] = rowKey;
        if (vouch)
            parameters[nameof(ExGrid<Trade>.VouchesDistinctRows)] = true;
        await renderer.Dispatcher.InvokeAsync(() => renderer.RenderRootAsync(id, ParameterView.FromDictionary(parameters)));
        await SettleAsync(renderer);
        return grid;
    }

    private static Task<T> OnContextAsync<T>(CountingRenderer renderer, Func<T> work) => renderer.Dispatcher.InvokeAsync(work);

    /// <summary>Whatever the grid renders again after an update, on its own continuations.</summary>
    internal static async Task SettleAsync(CountingRenderer renderer)
    {
        for (var settle = 0; settle < 5; settle++)
            await renderer.Dispatcher.InvokeAsync(() => Task.Yield());
    }

    internal static int PaintedRows(CountingRenderer renderer)
        => renderer.LiveWatched.OfType<ExGridRow<Trade>>().Count(r => !r.Placeholder);

    /// <summary>Ticket 13: the key function's calls over a whole pushed update — the new Window set
    /// as a parameter, the render it causes, and whatever renders after it — unvouched and vouched,
    /// so that a Window checked more than once per update would show.</summary>
    public static async Task<object> CountPushedAsync(int n, int updates)
    {
        var calls = 0;
        Func<Trade, object> counted = t => { calls++; return t.Id; };
        var trades = Trades.Generate(n);
        var source = GridSource.From(trades, t => t.Id);
        source.GatherInterval = TimeSpan.Zero;
        var newest = (Trade[])trades.Clone();
        var renderer = new CountingRenderer(new Services(TimeProvider.System)) { Watched = typeof(ExGridRow<>) };
        var random = new Random(7);
        var outcome = new List<object>();
        foreach (var vouch in new[] { false, true })
        {
            var grid = renderer.Create<ExGrid<Trade>>();
            var id = renderer.Attach(grid);
            Dictionary<string, object?> Parameters(IReadOnlyList<Trade> window) => new()
            {
                [nameof(ExGrid<Trade>.Window)] = window,
                [nameof(ExGrid<Trade>.TotalCount)] = window.Count,
                [nameof(ExGrid<Trade>.RowKey)] = counted,
                [nameof(ExGrid<Trade>.VouchesDistinctRows)] = vouch,
                [nameof(ExGrid<Trade>.Columns)] = Trades.Columns,
                [nameof(ExGrid<Trade>.PinnedColumnCount)] = 1,
                [nameof(ExGrid<Trade>.ViewportHeight)] = (ViewportSize)480,
                [nameof(ExGrid<Trade>.ViewportWidth)] = (ViewportSize)1270,
            };
            await renderer.Dispatcher.InvokeAsync(() => renderer.RenderRootAsync(id, ParameterView.FromDictionary(Parameters(source.Window))));
            await SettleAsync(renderer);
            var perUpdate = new List<int>();
            for (var u = 0; u < updates; u++)
            {
                source.Apply(new GridChangeBatch<Trade>(changed: Trades.Amend(newest, 1000, random)));
                calls = 0;
                await renderer.Dispatcher.InvokeAsync(() => renderer.RenderRootAsync(id, ParameterView.FromDictionary(Parameters(source.Window))));
                for (var settle = 0; settle < 20; settle++)
                {
                    await Task.Delay(5);
                    await SettleAsync(renderer);
                }
                perUpdate.Add(calls);
            }
            outcome.Add(new { vouched = vouch, windowRows = source.Window.Count, keyCallsPerUpdate = perUpdate });
            Console.WriteLine($"   pushed, vouched {vouch}: key calls per update over a Window of {source.Window.Count:N0}: {string.Join(", ", perUpdate.Select(c => c.ToString("N0")))}");
            await renderer.Dispatcher.InvokeAsync(() => renderer.RemoveRoot(id));
        }
        await renderer.DisposeAsync();
        return outcome;
    }

    /// <summary>LV-10: the key function's calls while the grid takes a new Window in, under a
    /// source that vouches, under a Row Key of the grid's own over that source, and pushed.</summary>
    public static async Task<object> CheckVouchAsync(int n)
    {
        var calls = 0;
        Func<Trade, object> counted = t => { calls++; return t.Id; };
        Func<Trade, object> countedOwn = t => { calls++; return t.Id; };
        var trades = Trades.Generate(n);
        var source = GridSource.From(trades, counted);
        source.GatherInterval = TimeSpan.Zero;
        var newest = (Trade[])trades.Clone();
        var renderer = new CountingRenderer(new Services(TimeProvider.System)) { Watched = typeof(ExGridRow<>) };
        var applyState = Reflect.Method(typeof(ExGrid<Trade>), "ApplyState");
        var random = new Random(7);
        var outcome = new List<object>();
        foreach (var (name, gridKey) in new (string, Func<Trade, object>?)[]
                 { ("source's own key, vouched", null), ("a Row Key of the grid's own over the source", countedOwn) })
        {
            var grid = renderer.Create<ExGrid<Trade>>();
            var id = renderer.Attach(grid);
            var parameters = new Dictionary<string, object?>
            {
                [nameof(ExGrid<Trade>.Source)] = source,
                [nameof(ExGrid<Trade>.Columns)] = Trades.Columns,
                [nameof(ExGrid<Trade>.ViewportHeight)] = (ViewportSize)480,
                [nameof(ExGrid<Trade>.ViewportWidth)] = (ViewportSize)1270,
            };
            if (gridKey is not null)
                parameters[nameof(ExGrid<Trade>.RowKey)] = gridKey;
            await renderer.Dispatcher.InvokeAsync(() => renderer.RenderRootAsync(id, ParameterView.FromDictionary(parameters)));
            await SettleAsync(renderer);
            var stateChanged = typeof(InMemoryGridSource<Trade>).GetEvent(nameof(InMemoryGridSource<Trade>.StateChanged))!;
            var handler = Reflect.Method(typeof(ExGrid<Trade>), "OnSourceStateChanged").CreateDelegate(typeof(Action), grid);
            stateChanged.RemoveEventHandler(source, handler);
            source.Apply(new GridChangeBatch<Trade>(changed: Trades.Amend(newest, 100, random)));
            var during = await renderer.Dispatcher.InvokeAsync(() =>
            {
                calls = 0;
                applyState.Invoke(grid, null);
                return calls;
            });
            outcome.Add(new { grid = name, windowRows = source.Window.Count, keyCallsInApplyState = during, vouched = Reflect.Get(grid, "_windowVouched") });
            Console.WriteLine($"   {name}: {during:N0} key calls over a new Window of {source.Window.Count:N0} rows; vouched {Reflect.Get(grid, "_windowVouched")}");
            stateChanged.AddEventHandler(source, handler);
            await renderer.Dispatcher.InvokeAsync(() => renderer.RemoveRoot(id));
        }
        await renderer.DisposeAsync();
        return outcome;
    }
}
