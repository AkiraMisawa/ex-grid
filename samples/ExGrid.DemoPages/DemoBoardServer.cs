using ExBoard;
using ExChart;
using ExChart.Engine;
using ExGrid;
using ExGrid.Data;
using ExPivot.Engine;

namespace ExGrid.DemoPages;

/// <summary>
/// A server holding 50,000 trades, as a host's own code reaches it (ADR-0343): it answers the input's
/// shape, each grid's query under the narrowing the Board hands it, a pivot's report over the rows that
/// narrowing lets through, a chart's points thinned where the trades are (ADR-0344), and the values under
/// a pivot's selected cells (ADR-0345). In process here; a host would send the query and the condition's values
/// over its own protocol and read the page back as Arrow.
/// </summary>
public static class DemoBoardServer
{
    private static readonly string[] Desks = ["Rates", "FX", "Credit", "Equities", "Commodities"];

    /// <summary>The trades the server holds, at version 1.</summary>
    public static Snapshot Held { get; } = Make(50_000);

    /// <summary>The source a Board binds its input "trades" to: the narrowing executed first, then the
    /// grid's filter and sort with <see cref="GridQueryEngine"/>'s semantics (ADR-0001).</summary>
    public static BoardSource Source(BoardReading board) => new(_ => ValueTask.FromResult(DataShape.Of(Held)), (query, _) =>
    {
        var through = Copy(Held, Through(query.Narrowing));
        var shown = ShapedGrid.Of(through, ((BoardGridView)board.View(query.View)!).Grid);
        var rows = GridQueryEngine.Apply(shown.Rows, [.. shown.Columns.Select(c => c.Info)], query.Grid.Filter, query.Grid.Sorts);
        var start = Math.Min(query.Grid.Range.Start, rows.Count);
        var count = Math.Min(query.Grid.Range.Count, rows.Count - start);
        return ValueTask.FromResult(new ShapedPage(Copy(through, rows.Skip(start).Take(count).Select(r => r.Position)), start, rows.Count));
    })
    {
        Pivot = narrowing => PivotSource.From(Copy(Held, Through(narrowing))),
        // Thinned here, as ChartSource.Thin thins: only the points kept cross to the chart.
        Thin = (query, _) => ValueTask.FromResult(ChartSource.Thin(new SnapshotChartData(Copy(Held, Through(query.Narrowing))), query.Chart, Held.Version)),
        Under = (query, _) => ValueTask.FromResult(BoardSource.ValuesUnder(Held, query)),
    };

    // The positions every condition lets through, each read as the Board reads it: the tuple of the
    // row's values in the condition's columns.
    private static List<int> Through(IReadOnlyList<LinkCondition> narrowing)
    {
        var kept = new List<int>();
        var rows = Held.Rows;
        for (var i = 0; i < rows.Count; i++)
        {
            if (narrowing.All(c => c.Matches([.. c.On.Select(n => Held.ValueAt(rows[i], Held[n]))])))
                kept.Add(i);
        }
        return kept;
    }

    private static Snapshot Copy(Snapshot from, IEnumerable<int> positions)
    {
        var builder = new SnapshotColumnsBuilder { Version = from.Version };
        var id = builder.Text("id");
        var desk = builder.Text("desk");
        var day = builder.Date("day");
        var seq = builder.Integer("seq");
        var pnl = builder.Decimal("pnl");
        var rows = from.Rows;
        foreach (var p in positions)
        {
            id.Append((string)from.ValueAt(rows[p], from["id"])!);
            desk.Append((string)from.ValueAt(rows[p], from["desk"])!);
            day.Append((DateTime)from.ValueAt(rows[p], from["day"])!);
            seq.Append((long)from.ValueAt(rows[p], from["seq"])!);
            pnl.Append((decimal)from.ValueAt(rows[p], from["pnl"])!);
        }
        return builder.Build();
    }

    private static Snapshot Make(int count)
    {
        var builder = new SnapshotColumnsBuilder { Version = 1 };
        var id = builder.Text("id");
        var desk = builder.Text("desk");
        var day = builder.Date("day");
        var seq = builder.Integer("seq");
        var pnl = builder.Decimal("pnl");
        for (var i = 0; i < count; i++)
        {
            id.Append($"T-{i + 1:D5}");
            desk.Append(Desks[i % Desks.Length]);
            day.Append(new DateOnly(2026, 1, 1).AddDays(i / 200));
            seq.Append(i + 1);
            pnl.Append((i * 7919 % 20001) - 10000);
        }
        return builder.Build();
    }
}
