using System.Globalization;
using ExGrid.Selection;
using ExSheet;
using ExSheet.Engine;
using Xunit;

namespace ExSheet.Components.Tests;

/// <summary>
/// Layer 1: where Ctrl+arrow stops on a Sheet (ADR-0050, item 2), as Excel answers it from the
/// Sheet's blanks. Each row of the table is one Excel observation: the filled cells, where the
/// Focus stands, the direction, and the cell Excel's Ctrl+arrow lands on.
/// </summary>
public class SheetEdgesTests
{
    private static Sheet SheetWith(params string[] filled)
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        foreach (var address in filled) sheet.Enter(CellAddress.Parse(address), "1");
        return sheet;
    }

    public static TheoryData<string[], string, GridDirection, string> ExcelTable => new()
    {
        // An empty column: to the Sheet's edge, and nowhere from the edge itself.
        { [], "A1", GridDirection.Down, "A1048576" },
        { [], "A1", GridDirection.Up, "A1" },
        { [], "A500", GridDirection.Up, "A1" },
        { [], "A1", GridDirection.Left, "A1" },
        { [], "A1", GridDirection.Right, "XFD1" },
        { [], "XFD1048576", GridDirection.Down, "XFD1048576" },
        { [], "XFD1048576", GridDirection.Right, "XFD1048576" },
        // Inside a block: to the block's last cell.
        { ["A1", "A2", "A3", "A4", "A5"], "A1", GridDirection.Down, "A5" },
        { ["A1", "A2", "A3", "A4", "A5"], "A3", GridDirection.Down, "A5" },
        { ["A1", "A2", "A3", "A4", "A5"], "A5", GridDirection.Up, "A1" },
        // At a block's last cell: to the next block's first cell, or to the edge.
        { ["A1", "A2", "A3", "A4", "A5"], "A5", GridDirection.Down, "A1048576" },
        { ["A1", "A2", "A3", "A4", "A5", "A10", "A11", "A12"], "A5", GridDirection.Down, "A10" },
        { ["A1", "A2", "A3", "A4", "A5", "A10", "A11", "A12"], "A10", GridDirection.Down, "A12" },
        { ["A1", "A2", "A3", "A4", "A5", "A10", "A11", "A12"], "A10", GridDirection.Up, "A5" },
        // From a blank cell: to the next filled cell along the line.
        { ["A1", "A2", "A3", "A4", "A5", "A10", "A11", "A12"], "A7", GridDirection.Down, "A10" },
        { ["A1", "A2", "A3", "A4", "A5", "A10", "A11", "A12"], "A7", GridDirection.Up, "A5" },
        { ["A10"], "A1", GridDirection.Down, "A10" },
        // A filled cell with a gap after it: across the gap, not to the edge.
        { ["A1", "A3"], "A1", GridDirection.Down, "A3" },
        { ["A1", "A3"], "A3", GridDirection.Up, "A1" },
        // A lone filled cell at the top: up goes nowhere.
        { ["A1"], "A1", GridDirection.Up, "A1" },
        // Along a row, the same rules.
        { ["A1", "B1", "C1", "E1"], "A1", GridDirection.Right, "C1" },
        { ["A1", "B1", "C1", "E1"], "C1", GridDirection.Right, "E1" },
        { ["A1", "B1", "C1", "E1"], "E1", GridDirection.Right, "XFD1" },
        { ["A1", "B1", "C1", "E1"], "E1", GridDirection.Left, "C1" },
        { ["A1", "B1", "C1", "E1"], "D1", GridDirection.Left, "C1" },
        { ["A1", "B1", "C1", "E1"], "XFD1", GridDirection.Left, "E1" },
        // Another column's data does not stop this one.
        { ["B3"], "A1", GridDirection.Down, "A1048576" },
        // The last row: a block running to the edge ends there.
        { ["A1048575", "A1048576"], "A1048575", GridDirection.Down, "A1048576" },
        { ["A1048576"], "A1", GridDirection.Down, "A1048576" },
    };

    [Theory] // ADR-0050 item 2: ExSheet's answer matches Excel's over blocks, gaps and an empty column
    [MemberData(nameof(ExcelTable))]
    public void Ctrl_arrow_stops_where_excel_stops(string[] filled, string from, GridDirection direction, string expected)
    {
        var edges = SheetEdges.Of(SheetWith(filled));

        Assert.Equal(expected, edges.Find(CellAddress.Parse(from), direction).ToString());
    }

    [Fact] // ADR-0050 item 2: formatting alone does not fill a cell; a Formula showing empty text does
    public void A_cell_is_filled_by_an_entry_not_by_formatting()
    {
        var sheet = SheetWith("A1", "A2");
        sheet.SetNumberFormat(CellAddress.Parse("A3"), NumberFormat.Parse("0.00"));
        sheet.Enter(CellAddress.Parse("A5"), "=\"\"");
        var edges = SheetEdges.Of(sheet);

        Assert.Equal("A2", edges.Find(CellAddress.Parse("A1"), GridDirection.Down).ToString());
        Assert.Equal("A5", edges.Find(CellAddress.Parse("A2"), GridDirection.Down).ToString());
    }

    [Fact] // ADR-0050 item 2: the answer follows every change the Sheet reports — an entry, a clearing, an insertion
    public void The_answer_follows_the_sheets_changes()
    {
        var sheet = SheetWith("A1", "A2");
        var edges = SheetEdges.Of(sheet);

        edges.Update(sheet, sheet.Enter(CellAddress.Parse("A3"), "x"));
        Assert.Equal("A3", edges.Find(CellAddress.Parse("A1"), GridDirection.Down).ToString());

        edges.Update(sheet, sheet.Enter(CellAddress.Parse("A2"), ""));
        Assert.Equal("A3", edges.Find(CellAddress.Parse("A1"), GridDirection.Down).ToString());
        Assert.Equal("A1", edges.Find(CellAddress.Parse("A3"), GridDirection.Up).ToString());

        edges.Update(sheet, sheet.Do(SheetEdit.InsertRows(0)).Change);
        Assert.Equal("A2", edges.Find(CellAddress.Parse("A1"), GridDirection.Down).ToString());
        Assert.Equal("A4", edges.Find(CellAddress.Parse("A2"), GridDirection.Down).ToString());
    }
}
