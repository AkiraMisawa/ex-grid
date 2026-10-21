namespace ExGrid.Docs.Examples.Board;

using ExBoard;
using ExGrid.Data;
using ExPivot.Engine;

/// <summary>A Board whose input is held on a server, and the host's code that reaches it.</summary>
public static class ServerBoard
{
    public const string Json = """
        {"version": 1, "title": "All trades",
         "inputs": {"trades": {"columns": [{"name": "id", "type": "text"}, {"name": "desk", "type": "text"},
                                           {"name": "pnl", "type": "decimal"}]}},
         "views": [
           {"id": "blotter", "title": "Trades", "grid": {"data": "trades", "columns": [{"column": "id", "header": "Trade"},
             {"column": "desk", "header": "Desk"}, {"column": "pnl", "header": "P&L", "format": "#,##0;(#,##0)"}]}},
           {"id": "byDesk", "title": "By desk", "pivot": {"data": "trades", "layout": {"version": 1,
             "rows": [{"field": "desk"}], "values": [{"field": "pnl", "aggregation": "sum", "numberFormat": "#,##0"}]}}}],
         "layout": [{"views": ["blotter", "byDesk"], "widths": [2, 1], "height": 280}],
         "links": [{"from": "blotter", "to": ["byDesk"], "on": ["desk"]}]}
        """;

    public static readonly BoardReading Board = BoardSpec.Read(Json);

    // Stands in for the server: 20,000 trades it never sends whole.
    private static readonly Snapshot Held = Make(20_000);

    /// <summary>
    /// The input's source. Each call is the host's own: here a few lines over the rows in process; in an
    /// application, the query and each condition's values sent over its own protocol, and the page read
    /// back as Arrow (SnapshotArrow.ReadAsync).
    /// </summary>
    public static IReadOnlyDictionary<string, BoardSource> Sources() => new Dictionary<string, BoardSource>
    {
        ["trades"] = new(_ => ValueTask.FromResult(DataShape.Of(Held)), (query, _) =>
        {
            // The server executes the narrowing the Board hands it, then the grid's own filter and sort with
            // GridQueryEngine's semantics, the reference a server's are written to match (ADR-0001).
            var through = Copy(Through(query.Narrowing));
            var shown = ShapedGrid.Of(through, ((BoardGridView)Board.View(query.View)!).Grid);
            var rows = GridQueryEngine.Apply(shown.Rows, [.. shown.Columns.Select(c => c.Info)], query.Grid.Filter, query.Grid.Sorts);
            var start = Math.Min(query.Grid.Range.Start, rows.Count);
            var count = Math.Min(query.Grid.Range.Count, rows.Count - start);
            return ValueTask.FromResult(new ShapedPage(Copy(through, rows.Skip(start).Take(count).Select(r => r.Position)), start, rows.Count));
        })
        {
            Report = narrowing => PivotReportSource.From(PivotSource.From(Copy(Through(narrowing)))),
        },
    };

    // A row is let through when, for every condition, the tuple of its values in that condition's columns is one it holds.
    private static List<int> Through(IReadOnlyList<LinkCondition> narrowing)
    {
        var kept = new List<int>();
        for (var i = 0; i < Held.RowCount; i++)
        {
            var row = Held.Rows[i];
            if (narrowing.All(c => c.Matches([.. c.On.Select(n => Held.ValueAt(row, Held[n]))])))
                kept.Add(i);
        }
        return kept;
    }

    private static Snapshot Copy(IEnumerable<int> positions) => Copy(Held, positions);

    private static Snapshot Copy(Snapshot from, IEnumerable<int> positions)
    {
        var builder = new SnapshotColumnsBuilder { Version = from.Version };
        var id = builder.Text("id");
        var desk = builder.Text("desk");
        var pnl = builder.Decimal("pnl");
        foreach (var p in positions)
        {
            var row = from.Rows[p];
            id.Append((string)from.ValueAt(row, from["id"])!);
            desk.Append((string)from.ValueAt(row, from["desk"])!);
            pnl.Append((decimal)from.ValueAt(row, from["pnl"])!);
        }
        return builder.Build();
    }

    private static Snapshot Make(int count)
    {
        string[] desks = ["Rates", "FX", "Credit", "Equities"];
        var builder = new SnapshotColumnsBuilder { Version = 1 };
        var id = builder.Text("id");
        var desk = builder.Text("desk");
        var pnl = builder.Decimal("pnl");
        for (var i = 0; i < count; i++)
        {
            id.Append($"T-{i + 1:D5}");
            desk.Append(desks[i % desks.Length]);
            pnl.Append((i * 7919 % 20001) - 10000);
        }
        return builder.Build();
    }
}
