using System.Diagnostics;
using ExGrid;

namespace Requery;

/// <summary>
/// M4: what a live batch of k changes costs at n rows, under a Filter and a two-key Sort.
/// <list type="bullet">
/// <item>(a) today: k calls of <c>GridSource.From(...).ReplaceRow</c>, each of which re-runs the
/// whole query;</item>
/// <item>(b) one full <see cref="GridQueryEngine.Apply{TRow}"/> for the batch — what a batch method
/// would cost if it requeried — with the locate and sequence passes such a method needs;</item>
/// <item>(c) the incremental prototype;</item>
/// <item>(grid) the grid's pass over every new Window instance, a reference-equality set
/// (<c>ExGrid.razor</c>'s <c>RequireDistinctRows</c>, transcribed).</item>
/// </list>
/// Warm-up first, then min and median of N runs, each from the same starting state.
/// </summary>
public static class Timing
{
    // A filter that keeps about 45% of the rows, on a column a live change rarely touches and one
    // it sometimes does, and a sort whose first key has many ties.
    public static readonly GridFilter Filter = new(new Dictionary<string, FilterSpec>(StringComparer.Ordinal)
    {
        ["Desk"] = new([new FilterClause(FilterOperator.In, Values: ["Rates", "Credit", "FX"])]),
        ["Notional"] = new([new FilterClause(FilterOperator.GreaterThanOrEqual, 500_000m)]),
    });

    public static readonly IReadOnlyList<SortSpec> Sorts =
        [new SortSpec("Book", SortDirection.Ascending), new SortSpec("Pnl", SortDirection.Descending)];

    private static readonly string[] Desks = ["Rates", "Credit", "FX", "Equities", "Commodities", "Treasury"];

    public static Trade[] Generate(int n, int seed)
    {
        var random = new Random(seed);
        var rows = new Trade[n];
        for (var i = 0; i < n; i++)
        {
            rows[i] = new Trade(
                i,
                "BK" + random.Next(200).ToString("000", System.Globalization.CultureInfo.InvariantCulture),
                Desks[random.Next(Desks.Length)],
                random.Next(20) == 0 ? null : Math.Round((decimal)(random.NextDouble() - 0.45) * 100_000m, 2),
                random.Next(1, 500) * 10_000m,
                random.Next(50) == 0 ? null : new DateTime(2026, 1, 2).AddDays(random.Next(270)),
                random.Next(10) > 0);
        }
        return rows;
    }

    /// <summary>A live tick: k distinct rows, each a new instance with a new P&amp;L (the second sort
    /// key, so the row moves within its book), and one in ten with a new notional too (which can
    /// move it across the Filter).</summary>
    public static RowBatch Tick(Trade[] rows, int k, Random random)
    {
        var picked = new HashSet<int>();
        while (picked.Count < k)
            picked.Add(random.Next(rows.Length));
        var replaced = new List<(Trade, Trade)>(k);
        foreach (var at in picked)
        {
            var old = rows[at];
            var pnl = Math.Round((decimal)(random.NextDouble() - 0.45) * 100_000m, 2);
            var notional = random.Next(10) == 0 ? random.Next(1, 500) * 10_000m : old.Notional;
            replaced.Add((old, new Trade(old.Id, old.Book, old.Desk, pnl, notional, old.TradeDate, old.Confirmed)));
        }
        return RowBatch.Replacing(replaced);
    }

    public sealed record Stat(string Path, int N, int K, int Runs, double MinMs, double MedianMs, double MaxMs, string Note);

    public static List<Stat> Run(int[] sizes, int[] ks, TextWriter log, bool withToday = true)
    {
        var stats = new List<Stat>();
        foreach (var n in sizes)
        {
            var rows = Generate(n, seed: 20261005);
            var columns = TradeColumns.All;
            var window = GridQueryEngine.Apply(rows, columns, Filter, Sorts);
            log.WriteLine($"n = {n:N0}: the Filter keeps {window.Count:N0} rows");

            // The grid's pass over a new Window instance.
            {
                var samples = Measure(runs: n >= 1_000_000 ? 15 : 40, warmup: 3, prepare: () => window, run: w => RequireDistinctRows(w));
                stats.Add(Summarise("grid: RequireDistinctRows over the Window", n, 0, samples, $"Window of {window.Count:N0} rows"));
                log.WriteLine(Line(stats[^1]));
            }

            // (b)'s core alone: one full Apply over n rows.
            {
                var samples = Measure(runs: n >= 1_000_000 ? 7 : 20, warmup: 2, prepare: () => rows, run: r => GridQueryEngine.Apply(r, columns, Filter, Sorts));
                stats.Add(Summarise("(b) GridQueryEngine.Apply alone", n, 0, samples, "filter + two-key stable sort over every row"));
                log.WriteLine(Line(stats[^1]));
            }

            foreach (var k in ks)
            {
                var random = new Random(k * 7919 + n);

                // (a) today: k ReplaceRow calls on a source already under the Filter and Sort.
                if (withToday)
                {
                    // The calls actually timed, and how many runs: at 10^6 a call is a full requery,
                    // so the larger batches are timed per call and multiplied (and said so).
                    var callsTimed = n >= 1_000_000 ? Math.Min(k, 100) : k;
                    var runs = callsTimed == 1 ? (n >= 1_000_000 ? 10 : 30) : callsTimed <= 100 && n < 1_000_000 ? 5 : 1;
                    var perCall = new List<double>();
                    var totals = new List<double>();
                    var source = GridSource.From(rows);
                    source.OnColumnsChanged(columns);
                    source.OnFilterChanged(Filter);
                    source.OnSortChanged(Sorts);
                    var current = rows.ToArray(); // the base as the source holds it, to pick from
                    // Warm-up: two calls.
                    for (var w = 0; w < 2; w++)
                        ReplaceAll(source, current, Tick(current, 1, random), perCall: null);
                    for (var run = 0; run < runs; run++)
                    {
                        var batch = Tick(current, callsTimed, random);
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        var t0 = Stopwatch.GetTimestamp();
                        ReplaceAll(source, current, batch, perCall);
                        totals.Add(Ms(t0));
                    }
                    var note = callsTimed == k
                        ? $"{runs} run(s) of {k} calls, timed whole"
                        : $"{callsTimed} calls timed one by one; the batch is {k} x the median call (derived, not run)";
                    var median = Median(perCall);
                    if (callsTimed == k)
                        stats.Add(Summarise("(a) today: k x ReplaceRow", n, k, totals, note));
                    else
                        stats.Add(new Stat("(a) today: k x ReplaceRow", n, k, perCall.Count, perCall.Min() * k, median * k, perCall.Max() * k, note));
                    log.WriteLine(Line(stats[^1]));
                    stats.Add(Summarise("(a) one ReplaceRow call", n, k, perCall, $"each call: locate by reference, Apply, compare the sequence"));
                    log.WriteLine(Line(stats[^1]));
                }

                // (b) a batch method that requeries once: locate the k rows in one pass, replace,
                // one Apply, and the sequence comparison with the replacements mapped across.
                {
                    var samples = Measure(runs: n >= 1_000_000 ? 7 : 20, warmup: 2,
                        prepare: () => (Rows: rows.ToArray(), Window: window, Batch: Tick(rows, k, random)),
                        run: s => BatchRequery(s.Rows, s.Window, s.Batch, columns));
                    stats.Add(Summarise("(b) one requery for the batch", n, k, samples, "locate + replace + Apply + sequence check"));
                    log.WriteLine(Line(stats[^1]));
                }

                // (c) the incremental prototype.
                {
                    var prototype = new IncrementalQuery(rows, columns, Filter, Sorts);
                    var samples = Measure(runs: n >= 1_000_000 ? 15 : 40, warmup: 3,
                        prepare: () => (Query: prototype.Clone(), Batch: Tick(rows, k, random)),
                        run: s => s.Query.Apply(s.Batch));
                    stats.Add(Summarise("(c) incremental", n, k, samples, "binary-search out, Apply on the k, binary-search in, block merge"));
                    log.WriteLine(Line(stats[^1]));
                }

                // (d) what shipped (ADR-0141): GridSource.From with a Row Key, a Change Batch of the k
                // changed rows applied and published at once (GatherInterval 0), the Change Highlight
                // not asked for. Batches follow one another on one source, each from the rows it holds.
                {
                    var source = GridSource.From(rows, t => t.Id, TimeProvider.System);
                    source.OnColumnsChanged(columns);
                    source.OnFilterChanged(Filter);
                    source.OnSortChanged(Sorts);
                    source.GatherInterval = TimeSpan.Zero;
                    var held = rows.ToArray();
                    var samples = Measure(runs: n >= 1_000_000 ? 15 : 40, warmup: 3,
                        prepare: () =>
                        {
                            var batch = Tick(held, k, random);
                            foreach (var (old, replacement) in batch.Replaced)
                                held[old.Id] = replacement;
                            return new GridChangeBatch<Trade>(changed: batch.Replaced.Select(p => p.New).ToArray());
                        },
                        run: batch => source.Apply(batch));
                    stats.Add(Summarise("(d) shipped: From with a Row Key, Apply", n, k, samples, "incremental requery in src, published at once"));
                    log.WriteLine(Line(stats[^1]));
                }
            }
        }
        return stats;
    }

    private static void ReplaceAll(InMemoryGridSource<Trade> source, Trade[] current, RowBatch batch, List<double>? perCall)
    {
        foreach (var (old, replacement) in batch.Replaced)
        {
            var t0 = Stopwatch.GetTimestamp();
            source.ReplaceRow(old, replacement);
            perCall?.Add(Ms(t0));
            current[old.Id] = replacement;
        }
    }

    private static (IReadOnlyList<Trade> Window, bool Moved) BatchRequery(Trade[] rows, IReadOnlyList<Trade> window, RowBatch batch, IReadOnlyList<ColumnInfo<Trade>> columns)
    {
        var replacementOf = new Dictionary<Trade, Trade>(batch.Replaced.Count, ReferenceEqualityComparer.Instance);
        foreach (var (old, replacement) in batch.Replaced)
            replacementOf.Add(old, replacement);
        for (var i = 0; i < rows.Length; i++)
        {
            if (replacementOf.Count > 0 && replacementOf.TryGetValue(rows[i], out var replacement))
                rows[i] = replacement;
        }
        var next = GridQueryEngine.Apply(rows, columns, Filter, Sorts);
        var moved = next.Count != window.Count;
        if (!moved)
        {
            var oldOf = new Dictionary<Trade, Trade>(batch.Replaced.Count, ReferenceEqualityComparer.Instance);
            foreach (var (old, replacement) in batch.Replaced)
                oldOf.Add(replacement, old);
            for (var i = 0; i < next.Count && !moved; i++)
            {
                if (!ReferenceEquals(next[i], window[i])
                    && !(oldOf.TryGetValue(next[i], out var old) && ReferenceEquals(old, window[i])))
                {
                    moved = true;
                }
            }
        }
        return (next, moved);
    }

    /// <summary>ExGrid.razor's RequireDistinctRows, transcribed (it is private to the component).</summary>
    public static void RequireDistinctRows(IReadOnlyList<Trade> window)
    {
        var seen = new HashSet<object>(window.Count, ReferenceEqualityComparer.Instance);
        for (var i = 0; i < window.Count; i++)
        {
            var row = window[i];
            if (row is null)
                throw new InvalidOperationException($"Window[{i}] is null; a Window holds rows, not gaps.");
            if (!seen.Add(row))
                throw new InvalidOperationException($"Window[{i}] is the same row instance as an earlier position.");
        }
    }

    private static List<double> Measure<TState>(int runs, int warmup, Func<TState> prepare, Action<TState> run)
    {
        for (var w = 0; w < warmup; w++)
            run(prepare());
        var samples = new List<double>(runs);
        for (var i = 0; i < runs; i++)
        {
            var state = prepare();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            var t0 = Stopwatch.GetTimestamp();
            run(state);
            samples.Add(Ms(t0));
        }
        return samples;
    }

    private static List<double> Measure<TState, TResult>(int runs, int warmup, Func<TState> prepare, Func<TState, TResult> run)
        => Measure<TState>(runs, warmup, prepare, s => { _ = run(s); });

    private static double Ms(long t0) => (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;

    private static double Median(List<double> samples)
    {
        var sorted = samples.OrderBy(x => x).ToArray();
        return sorted.Length == 0 ? double.NaN
            : sorted.Length % 2 == 1 ? sorted[sorted.Length / 2]
            : (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]) / 2;
    }

    private static Stat Summarise(string path, int n, int k, List<double> samples, string note)
        => new(path, n, k, samples.Count, samples.Min(), Median(samples), samples.Max(), note);

    private static string Line(Stat s) => $"  {s.Path,-44} n={s.N,9:N0} k={s.K,5} runs={s.Runs,4}  min {s.MinMs,10:F3}  median {s.MedianMs,10:F3}  max {s.MaxMs,10:F3} ms  ({s.Note})";
}
