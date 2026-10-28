using ExGrid;
using ExPivot.Engine;

namespace ExBoard.Tests;

/// <summary>
/// ADR-0370 section 1: Blazor's Script ExBoard hands its Script Core the Board as JSON, so a Board read in .NET is written
/// back to JSON that reads as the same Board: every part, each written by the writer of what it holds. Every accepted
/// shared case is also held to this (<see cref="Cases.BoardSpecCaseTests"/>).
/// </summary>
public class BoardSpecWritingTests
{
    /// <summary>That <paramref name="board"/> written and read again is the same Board, and writes the same JSON again.</summary>
    internal static void AssertWrittenWhole(BoardReading board)
    {
        var written = BoardSpec.Write(board);
        var again = BoardSpec.Read(written);
        Assert.Equal(written, BoardSpec.Write(again));
        Assert.Equal(board.Title, again.Title);
        Assert.Equal(board.Inputs.Select(i => (i.Name, Columns: string.Join(",", i.Columns.Select(c => $"{c.Name}:{c.Kind}")))),
            again.Inputs.Select(i => (i.Name, Columns: string.Join(",", i.Columns.Select(c => $"{c.Name}:{c.Kind}")))));
        Assert.Equal(board.Views.Select(Said), again.Views.Select(Said));
        Assert.Equal(board.Layout.Select(r => (string.Join(",", r.Cells), r.Height)), again.Layout.Select(r => (string.Join(",", r.Cells), r.Height)));
        Assert.Equal(board.Links.Select(l => $"{l.From}>{string.Join(",", l.To)}:{string.Join(",", l.On)}"), again.Links.Select(l => $"{l.From}>{string.Join(",", l.To)}:{string.Join(",", l.On)}"));
        Assert.Equal(board.Outputs, again.Outputs, (a, b) => a.Name == b.Name && a.From == b.From && a.On.SequenceEqual(b.On) && a.Confirm == b.Confirm);
    }

    private static string Said(BoardView view) => view switch
    {
        BoardGridView grid => $"{view.Id}|{view.Title}|grid {grid.Grid.Data} {string.Join(",", grid.Grid.Columns)} {GridViewStateJson.Write(grid.Grid.ViewState)}",
        BoardPivotView pivot => $"{view.Id}|{view.Title}|pivot {pivot.Input} {PivotLayoutJson.Write(pivot.Layout)}",
        BoardChartView chart => $"{view.Id}|{view.Title}|chart {chart.Spec}",
        _ => throw new InvalidOperationException(view.GetType().Name),
    };

    [Fact] // ADR-0370 section 1, ADR-0342: a grid's headers, formats, widths, pinning, sorts and filter cross as they were read
    public void ADR0370_a_grid_is_written_with_its_View_State() => AssertWrittenWhole(BoardSpec.Read("""
        {"version": 1, "title": "Desk P&L",
         "inputs": {"trades": {"columns": [{"name": "id", "type": "integer"}, {"name": "desk", "type": "text"}, {"name": "pnl", "type": "decimal"}, {"name": "day", "type": "date"}]}},
         "views": [{"id": "blotter", "title": "Trades", "grid": {"data": "trades",
                      "columns": [{"column": "id", "pinned": true}, {"column": "desk", "header": "Desk", "width": 140},
                                  {"column": "pnl", "header": "P&L", "format": "#,##0.00", "width": 90}, {"column": "day", "format": "yyyy-mm-dd"}],
                      "sorts": [{"column": "pnl", "direction": "Descending"}],
                      "filter": {"columns": {"desk": {"combinator": "And", "clauses": [{"operator": "Equals", "value": {"type": "String", "value": "Rates & <FX>"}}]}}}}},
                   {"id": "byDesk", "pivot": {"data": "trades", "layout": {"version": 1, "rows": [{"field": "desk"}], "values": [{"field": "pnl", "aggregation": "sum"}]}}},
                   {"id": "curve", "title": "Curve", "chart": {"version": 1, "axis": {"values": "dates"}, "series": [{"id": "pnl", "name": "P&L", "kind": "level", "data": "trades", "x": "day", "y": "pnl"}]}}],
         "layout": [{"views": ["blotter", "byDesk"], "widths": [2, 1], "height": 360}, {"views": ["curve"]}],
         "links": [{"from": "blotter", "to": ["curve"], "on": ["desk"]}],
         "outputs": {"chosen": {"from": "blotter", "on": ["id"], "confirm": "Book these"}}}
        """));
}
