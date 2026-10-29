using Bunit;
using ExChart;
using ExChart.Engine;
using ExGrid;
using ExGrid.Columns;
using ExGrid.Data;
using ExPivot.Engine;

namespace ExBoard.Tests;

public sealed class BoardDiffTests : BunitContext
{
    private const string Grid = """
        {"id": "blotter", "grid": {"data": "trades", "columns": [{"column": "id", "pinned": true}, {"column": "desk"}, {"column": "pnl", "format": "#,##0", "width": 90}]}}
        """;

    private const string Pivot = """
        {"id": "byDesk", "pivot": {"data": "trades", "layout": {"version": 1, "rows": [{"field": "desk"}], "values": [{"field": "pnl", "aggregation": "sum"}]}}}
        """;

    private const string Chart = """
        {"id": "curve", "chart": {"version": 1, "axis": {"values": "dates"}, "series": [{"id": "pnl", "name": "P&L", "kind": "level", "data": "trades", "x": "day", "y": "pnl"}]}}
        """;

    private static string Board(string grid = Grid, string pivot = Pivot, string chart = Chart) => $$$"""
        {"version": 1,
         "inputs": {"trades": {"columns": [{"name": "id", "type": "text"}, {"name": "desk", "type": "text"},
                                           {"name": "pnl", "type": "decimal"}, {"name": "day", "type": "date"}]}},
         "views": [{{{grid}}}, {{{pivot}}}, {{{chart}}}],
         "layout": [{"views": ["blotter", "byDesk"]}, {"views": ["curve"]}]}
        """;

    private static Dictionary<string, Snapshot> Data(BoardReading board) => new()
    {
        ["trades"] = board.Input("trades")!.FromJson("""
            [{"id": "T-1", "desk": "Rates", "pnl": 100, "day": "2026-10-19"},
             {"id": "T-2", "desk": "FX", "pnl": -50, "day": "2026-10-20"},
             {"id": "T-3", "desk": "Rates", "pnl": 75, "day": "2026-10-21"}]
            """),
    };

    private static readonly PivotLayout ByDay = PivotLayoutJson.Read("""
        {"version": 1, "rows": [{"field": "day"}], "values": [{"field": "pnl", "aggregation": "sum"}]}
        """);

    public BoardDiffTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("WebAssembly", isInteractive: true));
    }

    private static BoundBoard Bound(BoardReading reading, BoardDiff? diff = null) => BoundBoard.Bind(reading, Data(reading), diff: diff);

    // Every kind of change a user makes, once.
    private static BoundBoard Changed(BoardReading reading)
    {
        var board = Bound(reading);
        var grid = (BoundGridView)board["blotter"];
        grid.Resize(new ColumnWidthChange("desk", 140));
        grid.Reorder(["id", "pnl", "desk"]);
        Assert.True(grid.Hide("desk"));
        grid.Pin(2);
        grid.Grid!.Source.OnSortChanged([new SortSpec("pnl", SortDirection.Descending)]);
        ((BoundPivotView)board["byDesk"]).ChangeLayout(ByDay);
        var chart = (BoundChartView)board["curve"];
        chart.ChangeVisibility(new SeriesVisibility("pnl", true));
        chart.ChangeRange(ChartRange.Dates(new DateOnly(2026, 10, 20), null));
        chart.ChangeScale(AxisScale.Categories);
        return board;
    }

    [Fact] // ADR-0340 Q7: a Board the user has not changed has an empty Diff
    public void A_board_as_written_has_an_empty_diff()
    {
        var diff = Bound(BoardSpec.Read(Board())).Diff;

        Assert.True(diff.IsEmpty);
        Assert.Equal("""{"version":1,"views":{}}""", diff.ToJson());
    }

    [Fact] // ADR-0340 Q7: what the user changes is one Diff; the Board itself is not rewritten
    public void What_the_user_changes_is_raised_as_one_diff_and_the_board_is_not_rewritten()
    {
        var reading = BoardSpec.Read(Board());
        var diff = Changed(reading).Diff;

        var grid = diff.Grids["blotter"];
        Assert.Equal(["id", "pnl"], grid.ViewState.Columns.Select(c => c.Column));
        Assert.Equal(2, grid.ViewState.PinnedColumnCount);
        Assert.Equal(["desk"], grid.Hidden);
        Assert.Equal([new SortSpec("pnl", SortDirection.Descending)], grid.ViewState.Sorts);
        Assert.Equal(PivotLayoutJson.Write(ByDay), PivotLayoutJson.Write(diff.Pivots["byDesk"].Layout));
        Assert.Equal(SnapshotKind.Date, diff.Pivots["byDesk"].Kinds["day"]);
        var chart = diff.Charts["curve"];
        Assert.Equal(new[] { "pnl" }, chart.HiddenSeries);
        Assert.Equal(AxisScale.Categories, chart.Scale);
        // The reading the Board was drawn from is as it was.
        Assert.Equal(["id", "desk", "pnl"], ((BoardGridView)reading.View("blotter")!).Grid.ViewState.Columns.Select(c => c.Column));
        Assert.Empty(Bound(reading).Diff.Grids);
    }

    [Fact] // ADR-0340 Q7: a change undone is no part of the Diff
    public void A_change_undone_leaves_no_part()
    {
        var reading = BoardSpec.Read(Board());
        var board = Bound(reading);
        var pivot = (BoundPivotView)board["byDesk"];

        pivot.ChangeLayout(ByDay);
        pivot.ChangeLayout(((BoardPivotView)reading.View("byDesk")!).Layout);

        Assert.True(board.Diff.IsEmpty);
    }

    [Fact] // ADR-0340 Q7: the Diff round-trips its JSON and lays over the same Board next time
    public void A_diff_read_back_lays_over_the_board_as_it_was_left()
    {
        var reading = BoardSpec.Read(Board());
        var json = Changed(reading).Diff.ToJson();

        var read = BoardDiff.Read(json);
        Assert.Equal(json, read.ToJson());

        var again = Bound(reading, read);
        Assert.Empty(again.Dropped);
        var grid = (BoundGridView)again["blotter"];
        Assert.Equal(["id", "pnl"], grid.Columns.Select(c => c.Name));
        Assert.Equal(2, grid.PinnedColumnCount);
        Assert.Equal([new SortSpec("pnl", SortDirection.Descending)], grid.Grid!.Source.Sorts);
        Assert.Equal(0, grid.Grid!.Source.Window[0].Position);
        Assert.Equal(PivotLayoutJson.Write(ByDay), PivotLayoutJson.Write(((BoundPivotView)again["byDesk"]).Layout));
        var chart = (BoundChartView)again["curve"];
        Assert.Equal(new[] { "pnl" }, chart.HiddenSeries);
        Assert.Equal(new ChartRange(new DateOnly(2026, 10, 20).DayNumber, null), chart.Range);
        Assert.True(again.Diff.SameAs(read));
    }

    [Fact] // ADR-0340 Q7, ADR-0016: a width the user dragged is Fixed, and read back at that width
    public void A_dragged_width_is_laid_over_as_fixed()
    {
        var reading = BoardSpec.Read(Board());
        var board = Bound(reading);
        ((BoundGridView)board["blotter"]).Resize(new ColumnWidthChange("desk", 140));

        var again = Bound(reading, BoardDiff.Read(board.Diff.ToJson()));

        Assert.Equal(ColumnWidth.Fixed(140), ((BoundGridView)again["blotter"]).Columns.Single(c => c.Name == "desk").Width.Width);
    }

    [Fact] // ADR-0340 Q7: what no longer fits is dropped by name, and the rest applies
    public void What_no_longer_fits_is_dropped_by_name_and_the_rest_applies()
    {
        var json = Changed(BoardSpec.Read(Board())).Diff.ToJson();
        // Next time the Board shows no pnl in its grid, has no pivot, and its chart is a grid.
        var changed = BoardSpec.Read(Board(
            grid: """{"id": "blotter", "grid": {"data": "trades", "columns": [{"column": "id"}, {"column": "desk"}, {"column": "day"}]}}""",
            pivot: """{"id": "byDay", "pivot": {"data": "trades", "layout": {"version": 1, "rows": [{"field": "day"}]}}}""",
            chart: """{"id": "curve", "grid": {"data": "trades", "columns": [{"column": "day"}]}}""")
            .Replace("\"byDesk\"]", "\"byDay\"]", StringComparison.Ordinal));

        var board = Bound(changed, BoardDiff.Read(json));

        Assert.Equal(
        [
            "views.blotter.grid: column 'pnl' is no longer shown by view 'blotter'.",
            "views.blotter.grid: the sort on column 'pnl', which view 'blotter' no longer shows.",
            "views.byDesk.pivot: the Board no longer has view 'byDesk'.",
            "views.curve.chart: view 'curve' is now a grid, not a chart.",
        ], board.Dropped);
        var grid = (BoundGridView)board["blotter"];
        // id stays pinned; desk stays hidden; day, which the Board added, is shown, last.
        Assert.Equal(["id", "day"], grid.Columns.Select(c => c.Name));
        Assert.Equal(1, grid.PinnedColumnCount);
        Assert.Empty(grid.Grid!.Source.Sorts);
    }

    // What a Diff refuses, writes, and drops when laid against a Board is the shared cases' (tests/cases/board/diff.json).
    [Fact] // ADR-0340 Q7, spine rule 1: a filter keeps the kind its column was declared as when it was set
    public void A_filter_keeps_the_kind_of_its_column()
    {
        var board = Bound(BoardSpec.Read(Board()));
        ((BoundGridView)board["blotter"]).Grid!.Source.OnFilterChanged(new GridFilter(new Dictionary<string, FilterSpec>
        {
            ["desk"] = new FilterSpec([new FilterClause(FilterOperator.Equals, "Rates")]),
        }));

        Assert.Contains("\"kinds\":{\"desk\":\"text\"}", board.Diff.ToJson(), StringComparison.Ordinal);
    }

    [Fact] // ADR-0340 Q7, spine rule 6: a gesture naming a column no longer shown is refused, not applied to others
    public void A_gesture_behind_a_hide_is_refused()
    {
        var grid = (BoundGridView)Bound(BoardSpec.Read(Board()))["blotter"];
        Assert.True(grid.Hide("desk"));

        Assert.False(grid.Resize(new ColumnWidthChange("desk", 140)));
        Assert.False(grid.Reorder(["desk", "id", "pnl"]));
        Assert.False(grid.Hide("desk"));
        Assert.False(grid.Pin(3));
        Assert.Equal(["id", "pnl"], grid.Columns.Select(c => c.Name));
    }

    [Fact] // ADR-0340 Q7: the last column shown is kept, so the grid can still be shown one
    public void The_last_column_is_not_hidden()
    {
        var grid = (BoundGridView)Bound(BoardSpec.Read(Board()))["blotter"];

        Assert.True(grid.Hide("id"));
        Assert.True(grid.Hide("desk"));
        Assert.False(grid.Hide("pnl"));
        Assert.Equal(["pnl"], grid.Columns.Select(c => c.Name));
    }

}
