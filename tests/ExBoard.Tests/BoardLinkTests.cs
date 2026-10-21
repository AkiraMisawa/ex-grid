using Bunit;
using ExGrid;
using ExGrid.Components;
using ExGrid.Data;
using ExGrid.Selection;
using BoardComponent = ExBoard.Components.ExBoard;

namespace ExBoard.Tests;

public sealed class BoardLinkTests : BunitContext
{
    private const string Inputs = """
        "inputs": {
          "trades": {"columns": [{"name": "desk", "type": "text"}, {"name": "month", "type": "text"},
                                 {"name": "pnl", "type": "decimal"}, {"name": "day", "type": "date"}]},
          "limits": {"columns": [{"name": "desk", "type": "text"}, {"name": "limit", "type": "decimal"}]},
          "quotes": {"columns": [{"name": "desk", "type": "integer"}]}}
        """;

    private const string Blotter = """{"id": "blotter", "grid": {"data": "trades", "columns": [{"column": "desk"}, {"column": "month"}, {"column": "pnl", "format": "#,##0.00"}]}}""";
    private const string Other = """{"id": "other", "grid": {"data": "trades", "columns": [{"column": "desk"}, {"column": "month"}]}}""";
    private const string Limits = """{"id": "limits", "grid": {"data": "limits", "columns": [{"column": "desk"}, {"column": "limit", "format": "#,##0"}]}}""";
    private const string Quotes = """{"id": "quotes", "grid": {"data": "quotes", "columns": [{"column": "desk"}]}}""";
    private const string Pivot = """{"id": "byDesk", "pivot": {"data": "trades", "layout": {"version": 1, "rows": [{"field": "desk"}], "values": [{"field": "pnl", "aggregation": "sum"}]}}}""";
    private const string Chart = """{"id": "curve", "chart": {"version": 1, "axis": {"values": "dates"}, "series": [{"id": "pnl", "name": "P&L", "kind": "level", "data": "trades", "x": "day", "y": "pnl"}]}}""";

    private static string Board(string links, string views = $"{Blotter}, {Other}, {Limits}, {Quotes}, {Pivot}, {Chart}") =>
        $$"""
        {"version": 1, {{Inputs}}, "views": [{{views}}],
         "layout": [{"views": [{{string.Join(", ", System.Text.RegularExpressions.Regex.Matches(views, "\"id\": (\"[A-Za-z]+\"), \"(grid|pivot|chart)\"").Select(m => m.Groups[1].Value))}}]}],
         "links": [{{links}}]}
        """;

    private static BoardInput Declared(BoardReading board, string name) => board.Input(name)!;

    private static Dictionary<string, Snapshot> Data(BoardReading board) => new()
    {
        ["trades"] = Declared(board, "trades").FromJson("""
            [{"desk": "Rates", "month": "Jan", "pnl": 100, "day": "2026-01-05"},
             {"desk": "Credit", "month": "Feb", "pnl": 200, "day": "2026-01-06"},
             {"desk": "Rates", "month": "Feb", "pnl": 300, "day": "2026-01-07"},
             {"desk": "Credit", "month": "Jan", "pnl": 400, "day": "2026-01-08"},
             {"desk": "FX", "month": "Jan", "pnl": 500, "day": "2026-01-09"}]
            """),
        ["limits"] = Declared(board, "limits").FromJson("""
            [{"desk": "rates", "limit": 1000}, {"desk": "Credit", "limit": 2000}, {"desk": "FX", "limit": 3000}]
            """),
        ["quotes"] = Declared(board, "quotes").FromJson("""[{"desk": 1}]"""),
    };

    // A selection of whole rows, as a click on each row's first cell and a Ctrl+click on the next
    // make it, in the order the grid shows its rows.
    private static GridSelection Rows(params int[] rows)
    {
        var extent = new GridExtent(100, 3);
        var selection = GridSelection.Empty.Click(new CellPosition(rows[0], 0), extent);
        foreach (var row in rows.Skip(1))
            selection = selection.ToggleRange(new CellPosition(row, 0), extent);
        return selection;
    }

    private static IEnumerable<string> Desks(BoundGridView grid) =>
        grid.Grid.Source.Window.Select(r => (string)grid.Grid.Snapshot.ValueAt(r.At, grid.Grid.Snapshot["desk"])!);

    [Fact] // ADR-0340 Q5: a link names a grid, the views it narrows and the columns it compares
    public void A_link_is_read_from_its_grid_to_its_views_on_its_columns()
    {
        var board = BoardSpec.Read(Board("""{"from": "blotter", "to": ["byDesk", "curve", "limits"], "on": ["desk"]}"""));

        var link = Assert.Single(board.Links);
        Assert.Equal("blotter", link.From);
        Assert.Equal(["byDesk", "curve", "limits"], link.To);
        Assert.Equal(["desk"], link.On);
        Assert.Empty(BoardSpec.Read(Board("").Replace(""", "links": []""", "", StringComparison.Ordinal)).Links);
    }

    public static TheoryData<string, string, string> Refusals => new()
    {
        { """{"from": "byDesk", "to": ["blotter"], "on": ["desk"]}""", "links[0].from", "The link starts at view 'byDesk', a pivot; a link starts at a grid, whose selected rows narrow." },
        { """{"from": "curve", "to": ["blotter"], "on": ["desk"]}""", "links[0].from", "The link starts at view 'curve', a chart; a link starts at a grid, whose selected rows narrow." },
        { """{"from": "book", "to": ["blotter"], "on": ["desk"]}""", "links[0].from", "The link starts at view 'book', which the Board does not have." },
        { """{"from": "blotter", "to": ["byBook"], "on": ["desk"]}""", "links[0].to[0]", "The link narrows view 'byBook', which the Board does not have." },
        { """{"from": "blotter", "to": ["blotter"], "on": ["desk"]}""", "links[0].to[0]", "The link narrows its own view 'blotter'; a view's selection narrows other views." },
        { """{"from": "blotter", "to": ["limits"], "on": ["month"]}""", "links[0].on[0]", "The link compares column 'month', which input 'limits' of view 'limits' does not declare." },
        { """{"from": "blotter", "to": ["other"], "on": ["book"]}""", "links[0].on[0]", "The link compares column 'book', which input 'trades' of view 'blotter' does not declare." },
        { """{"from": "blotter", "to": ["quotes"], "on": ["desk"]}""", "links[0].on[0]", "The link compares column 'desk', a text in input 'trades' and a integer in input 'quotes'; a value is compared only with one of its kind." },
        { """{"from": "blotter", "to": ["other"], "on": ["desk", "desk"]}""", "links[0].on[1]", "'desk' is named twice." },
        { """{"from": "blotter", "to": [], "on": ["desk"]}""", "links[0].to", "A link names the views it narrows, in 'to'." },
        { """{"from": "blotter", "to": ["other"], "on": ["desk"], "when": "click"}""", "links[0].when", "A link has no member 'when'." },
        { """{"from": "blotter", "to": ["other"], "on": ["desk"]}, {"from": "other", "to": ["limits", "blotter"], "on": ["desk"]}""", "links[1].to", "The link narrows view 'blotter', which already narrows view 'other'; links run one way." },
    };

    [Theory, MemberData(nameof(Refusals))] // ADR-0340: a link that cannot narrow is refused by path
    public void A_link_that_cannot_narrow_is_refused_by_path(string links, string path, string message)
    {
        var refused = Assert.Throws<BoardRefusedException>(() => BoardSpec.Read(Board(links)));
        Assert.Equal(path, refused.Path);
        Assert.Equal(message, refused.Message);
    }

    [Fact] // ADR-0340 Q5: the condition is the selected rows' tuples, never a cross product of lists
    public void A_condition_lets_through_only_the_tuples_selected()
    {
        var condition = new LinkCondition(["desk", "month"], [["Rates", "Jan"], ["Credit", "Feb"], ["rates", "jan"]]);

        Assert.Equal(2, condition.Values.Count);
        Assert.True(condition.Matches(["Rates", "Jan"]));
        Assert.True(condition.Matches(["CREDIT", "feb"]));
        Assert.False(condition.Matches(["Rates", "Feb"]));
        Assert.False(condition.Matches(["Credit", "Jan"]));
        Assert.False(condition.Matches([null, "Jan"]));
        Assert.True(new LinkCondition(["desk"], [[null]]).Matches([null]));
        Assert.False(new LinkCondition(["pnl"], [[1m]]).Matches([1.0]));
    }

    [Fact] // ADR-0340 Q5: a grid's selected rows narrow a grid, a pivot and a chart; a drop restores them (ADR-0011)
    public void A_selection_narrows_every_view_it_reaches_and_a_drop_restores_them()
    {
        var reading = BoardSpec.Read(Board("""{"from": "blotter", "to": ["other", "byDesk", "curve", "limits"], "on": ["desk"]}"""));
        var board = BoundBoard.Bind(reading, Data(reading));
        var link = reading.Links[0];

        var condition = board.ConditionOf(link, Rows(0, 2));
        Assert.Equal([["Rates"]], condition!.Values);
        board.Narrow(link, condition);

        Assert.Same(condition, board.ConditionOf(link));
        Assert.Equal(["Rates", "Rates"], Desks((BoundGridView)board["other"]));
        // Across inputs that share the column, and as the grid's value list compares text.
        Assert.Equal(["rates"], Desks((BoundGridView)board["limits"]));
        Assert.Equal(2, ((BoundPivotView)board["byDesk"]).Source.Snapshot.RowCount);
        Assert.Equal<object>([100m, 300m], ((BoundChartView)board["curve"]).Reading.Definition.Series[0].Points.Select(p => p.Y!.Value.Value));

        board.Narrow(link, board.ConditionOf(link, GridSelection.Empty));

        Assert.Null(board.ConditionOf(link));
        Assert.Equal(5, ((BoundGridView)board["other"]).Grid.Source.Window.Count);
        Assert.Same(board.Data["trades"], ((BoundPivotView)board["byDesk"]).Source.Snapshot);
        Assert.Equal(5, ((BoundChartView)board["curve"]).Reading.Definition.Series[0].Points.Count);
    }

    [Fact] // ADR-0340 Q5: on two columns, rows (Rates, Jan) and (Credit, Feb) let through no (Rates, Feb)
    public void A_selection_on_two_columns_narrows_to_its_own_rows()
    {
        var reading = BoardSpec.Read(Board("""{"from": "blotter", "to": ["other"], "on": ["desk", "month"]}"""));
        var board = BoundBoard.Bind(reading, Data(reading));
        var link = reading.Links[0];

        board.Narrow(link, board.ConditionOf(link, Rows(0, 1)));

        var other = (BoundGridView)board["other"];
        Assert.Equal([0, 1], other.Grid.Source.Window.Select(r => r.Position));
    }

    [Fact] // ADR-0340 Q5 / ADR-0011: the selection is read against the rows as the grid shows them
    public void A_selection_is_read_in_the_order_the_grid_shows()
    {
        var reading = BoardSpec.Read(Board("""{"from": "blotter", "to": ["other"], "on": ["desk"]}"""));
        var board = BoundBoard.Bind(reading, Data(reading));
        var link = reading.Links[0];
        var blotter = (BoundGridView)board["blotter"];
        blotter.Grid.Source.OnSortChanged([new SortSpec("pnl", SortDirection.Descending)]);

        // Shown first: FX (500); a selection taken against more rows than are shown narrows nothing.
        Assert.Equal([["FX"]], board.ConditionOf(link, Rows(0))!.Values);
        Assert.Null(board.ConditionOf(link, Rows(7)));
    }

    [Fact] // ADR-0340 Q5: a narrowed grid keeps the sort the user set on it
    public void A_narrowed_grid_keeps_its_sort()
    {
        var reading = BoardSpec.Read(Board("""{"from": "blotter", "to": ["other"], "on": ["desk"]}"""));
        var board = BoundBoard.Bind(reading, Data(reading));
        var link = reading.Links[0];
        var other = (BoundGridView)board["other"];
        other.Grid.Source.OnSortChanged([new SortSpec("month", SortDirection.Ascending)]);

        board.Narrow(link, board.ConditionOf(link, Rows(0)));

        // Rates' rows, Feb before Jan.
        Assert.Equal([2, 0], other.Grid.Source.Window.Select(r => r.Position));
        Assert.Equal("month", Assert.Single(other.Grid.Source.Sorts).Column);
    }

    [Fact] // ADR-0340 Q5: two links into one view narrow it to what both let through
    public void Two_links_into_one_view_narrow_it_to_what_both_let_through()
    {
        var reading = BoardSpec.Read(Board("""
            {"from": "blotter", "to": ["byDesk"], "on": ["desk"]}, {"from": "other", "to": ["byDesk"], "on": ["month"]}
            """));
        var board = BoundBoard.Bind(reading, Data(reading));

        board.Narrow(reading.Links[0], new LinkCondition(["desk"], [["Rates"]]));
        board.Narrow(reading.Links[1], new LinkCondition(["month"], [["Feb"]]));

        var pivot = ((BoundPivotView)board["byDesk"]).Source.Snapshot;
        Assert.Equal(1, pivot.RowCount);
        Assert.Equal(300m, pivot.ValueAt(pivot.Rows[0], pivot["pnl"]));
        Assert.Throws<ArgumentException>(() => board.Narrow(reading.Links[0], new LinkCondition(["month"], [["Feb"]])));
    }

    [Fact] // ADR-0340 Q5: a chart narrowed to no rows draws none, never the rows it drew before
    public void A_chart_narrowed_to_no_rows_draws_none()
    {
        var reading = BoardSpec.Read(Board("""{"from": "blotter", "to": ["curve"], "on": ["desk"]}"""));
        var board = BoundBoard.Bind(reading, Data(reading));
        var chart = (BoundChartView)board["curve"];

        board.Narrow(reading.Links[0], new LinkCondition(["desk"], [["Equities"]]));
        Assert.Null(chart.Refusal);
        Assert.Empty(chart.Reading.Definition.Series[0].Points);

        board.Narrow(reading.Links[0], null);
        Assert.Equal(5, chart.Reading.Definition.Series[0].Points.Count);
    }

    [Fact] // ADR-0340 Q5: the grid's raised selection narrows, and its drop, raised as an empty one, restores
    public async Task The_board_narrows_on_the_selection_its_grid_raises()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("WebAssembly", isInteractive: true));
        var reading = BoardSpec.Read(Board("""{"from": "blotter", "to": ["byDesk"], "on": ["desk"]}""", $"{Blotter}, {Pivot}"));
        var cut = Render<BoardComponent>(ps => ps.Add(b => b.Board, reading).Add(b => b.Data, Data(reading)));
        var grids = cut.FindComponents<ExGrid<ShapedRow>>();
        var blotter = grids.Single(g => g.Instance.SelectionChanged.HasDelegate);
        var pivots = cut.FindComponents<ExPivot.Components.ExPivot>();

        await cut.InvokeAsync(() => blotter.Instance.SelectionChanged.InvokeAsync(Rows(4)));
        Assert.Equal(1, ((ExPivot.Engine.SnapshotPivotSource)Assert.Single(pivots).Instance.Source!).Snapshot.RowCount);

        await cut.InvokeAsync(() => blotter.Instance.SelectionChanged.InvokeAsync(GridSelection.Empty));
        Assert.Equal(5, ((ExPivot.Engine.SnapshotPivotSource)Assert.Single(pivots).Instance.Source!).Snapshot.RowCount);
    }
}
