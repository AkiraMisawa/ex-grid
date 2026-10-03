using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// ADR-0047 (SH-21): formats at cell, row and column level. What Excel shows is in the case
/// corpus (<c>ExcelCases/format-levels.json</c>); these pin what is recorded, what a change
/// reports, and that undoing puts every level back exactly.
/// </summary>
public class FormatLevelTests
{
    private static readonly NumberFormat TwoPlaces = NumberFormat.Parse("0.00");
    private static readonly NumberFormat Percent = NumberFormat.Parse("0%");

    private static CellAddress At(string address) => CellAddress.Parse(address);

    [Fact] // ADR-0047 (SH-21): formatting a whole column records one entry, not a million
    public void Formatting_a_whole_column_records_one_entry()
    {
        var sheet = NewSheet();

        sheet.SetNumberFormat(CellRange.Parse("B:B"), TwoPlaces);

        var document = sheet.ToDocument();
        Assert.Empty(document.Cells);
        var column = Assert.Single(document.Columns);
        Assert.Equal((1, 1, TwoPlaces), (column.First, column.Last, column.NumberFormat));
        Assert.Equal(TwoPlaces, sheet.GetColumnNumberFormat(1));
        Assert.Equal(TwoPlaces, sheet.GetNumberFormat(At("B1048576")));
        Assert.Equal(NumberFormat.General, sheet.GetNumberFormat(At("C1")));
    }

    [Fact] // ADR-0047 (SH-21): the whole Sheet is whole columns, one run in the document
    public void Formatting_the_whole_sheet_is_one_run()
    {
        var sheet = NewSheet();

        sheet.SetNumberFormat(CellRange.Parse("A:XFD"), TwoPlaces);

        var run = Assert.Single(sheet.ToDocument().Columns);
        Assert.Equal((0, Sheet.ColumnCount - 1), (run.First, run.Last));
        Assert.Empty(sheet.ToDocument().Rows);
    }

    [Fact] // ADR-0048 (SH-12): a Sheet formatted whole is written as A:XFD and opens again with the same format
    public void A_whole_sheet_format_round_trips_through_the_document()
    {
        var sheet = NewSheet();
        sheet.SetNumberFormat(CellRange.Parse("A:XFD"), TwoPlaces);

        var reopened = Sheet.Open(SheetDocument.FromJson(sheet.ToDocument().ToJson()));

        Assert.Equal(TwoPlaces, reopened.GetNumberFormat(At("A1")));
        Assert.Equal(TwoPlaces, reopened.GetNumberFormat(At("XFD1048576")));
        Assert.Equal(sheet.ToDocument().ToJson(), reopened.ToDocument().ToJson());
    }

    [Fact] // ADR-0047: cell over row over column
    public void Cell_over_row_over_column()
    {
        var sheet = NewSheet();
        sheet.SetNumberFormat(CellRange.Parse("B:B"), TwoPlaces);
        sheet.SetNumberFormat(CellRange.Parse("3:3"), Percent);
        sheet.SetNumberFormat(At("B4"), NumberFormat.Parse("#,##0"));

        Assert.Equal(Percent, sheet.GetNumberFormat(At("B3")));
        Assert.Equal(TwoPlaces, sheet.GetNumberFormat(At("B2")));
        Assert.Equal("#,##0", sheet.GetNumberFormat(At("B4")).Code);
        Assert.Equal(Percent, sheet.GetNumberFormat(At("Z3")));
        Assert.Equal(Percent, sheet.GetRowNumberFormat(2));
        Assert.Null(sheet.GetRowNumberFormat(3));
    }

    [Fact] // ADR-0047: a cell records only what differs from what its row or column gives it
    public void A_cell_records_only_what_it_would_not_take_anyway()
    {
        var sheet = NewSheet();
        sheet.SetNumberFormat(CellRange.Parse("B:B"), TwoPlaces);

        sheet.SetNumberFormat(At("B2"), TwoPlaces);
        sheet.SetNumberFormat(At("C2"), NumberFormat.General);

        Assert.Empty(sheet.ToDocument().Cells);
    }

    [Fact] // ADR-0047: a column's format repaints the rows that hold something in it, and changes no Value
    public void A_column_format_reports_the_rows_it_repaints()
    {
        var sheet = NewSheet();
        sheet.Enter("B2", "1");
        sheet.Enter("B9", "2");
        sheet.Enter("C5", "3");

        var change = sheet.SetNumberFormat(CellRange.Parse("B:B"), TwoPlaces);

        Assert.Equal([1, 8], change.Rows);
        Assert.Empty(change.ValueChanges);
    }

    [Theory] // ADR-0048 (SH-21): undoing a format at any level puts the Sheet back exactly, and redoing sets it again
    [InlineData("B:B")]
    [InlineData("3:4")]
    [InlineData("A:XFD")]
    [InlineData("B2:C3")]
    public void Undo_restores_every_level_exactly(string range)
    {
        var sheet = NewSheet();
        sheet.Enter("B2", "0.5");
        sheet.Enter("B3", "0.25");
        sheet.SetNumberFormat(At("B2"), Percent);
        sheet.SetNumberFormat(CellRange.Parse("3:3"), NumberFormat.Parse("#,##0"));
        sheet.SetAlignment(CellRange.Parse("C:C"), HorizontalAlignment.Center);
        var before = sheet.ToDocument().ToJson();

        var step = sheet.Do(SheetEdit.SetNumberFormat(CellRange.Parse(range), TwoPlaces));
        var after = sheet.ToDocument().ToJson();
        Assert.NotEqual(before, after);

        var undone = step.Undo();
        Assert.Equal(before, sheet.ToDocument().ToJson());
        Assert.Contains(2, undone.Rows);

        step.Redo();
        Assert.Equal(after, sheet.ToDocument().ToJson());
    }

    [Fact] // ADR-0048: an alignment on a range is one step, undone exactly
    public void An_alignment_on_a_range_is_one_step()
    {
        var sheet = NewSheet();
        sheet.Enter("D2", "x");
        var before = sheet.ToDocument().ToJson();

        var step = sheet.Do(SheetEdit.SetAlignment(CellRange.Parse("2:2"), HorizontalAlignment.Right));
        Assert.Equal(HorizontalAlignment.Right, sheet.GetAlignment(At("D2")));
        Assert.Equal(HorizontalAlignment.Right, sheet.GetRowAlignment(1));

        step.Undo();
        Assert.Equal(before, sheet.ToDocument().ToJson());
    }

    [Theory] // ADR-0046/0048 (SH-21): an insertion or deletion moves row and column formats, and undoing it puts them back exactly
    [InlineData("insertRows")]
    [InlineData("deleteRows")]
    [InlineData("insertColumns")]
    [InlineData("deleteColumns")]
    public void Structure_moves_levels_and_undo_restores_them(string edit)
    {
        var sheet = NewSheet();
        sheet.SetNumberFormat(CellRange.Parse("2:3"), Percent);
        sheet.SetNumberFormat(CellRange.Parse("5:5"), TwoPlaces);
        sheet.SetNumberFormat(CellRange.Parse("B:C"), Percent);
        sheet.SetAlignment(CellRange.Parse("E:E"), HorizontalAlignment.Center);
        sheet.Enter("E5", "1");
        var before = sheet.ToDocument().ToJson();

        var step = sheet.Do(edit switch
        {
            "insertRows" => SheetEdit.InsertRows(3, 2),
            "deleteRows" => SheetEdit.DeleteRows(2, 2),
            "insertColumns" => SheetEdit.InsertColumns(2, 2),
            _ => SheetEdit.DeleteColumns(1, 2),
        });

        switch (edit)
        {
            case "insertRows":
                // Rows 4 and 5 are new and take row 3's format; row 5's moved to row 7.
                Assert.Equal(Percent, sheet.GetRowNumberFormat(3));
                Assert.Equal(Percent, sheet.GetRowNumberFormat(4));
                Assert.Equal(TwoPlaces, sheet.GetRowNumberFormat(6));
                Assert.Null(sheet.GetRowNumberFormat(5));
                break;
            case "deleteRows":
                // Row 2 survives, row 3 goes with row 4, row 5 moves to row 3.
                Assert.Equal(Percent, sheet.GetRowNumberFormat(1));
                Assert.Equal(TwoPlaces, sheet.GetRowNumberFormat(2));
                Assert.Null(sheet.GetRowNumberFormat(4));
                break;
            case "insertColumns":
                Assert.Equal(Percent, sheet.GetColumnNumberFormat(2));
                Assert.Equal(Percent, sheet.GetColumnNumberFormat(3));
                Assert.Equal(HorizontalAlignment.Center, sheet.GetColumnAlignment(6));
                Assert.Equal(HorizontalAlignment.Center, sheet.GetAlignment(At("G5")));
                break;
            default:
                Assert.Null(sheet.GetColumnNumberFormat(1));
                Assert.Equal(HorizontalAlignment.Center, sheet.GetColumnAlignment(2));
                break;
        }

        step.Undo();
        Assert.Equal(before, sheet.ToDocument().ToJson());
    }

    [Fact] // ADR-0046: a row format pushed off the bottom edge by an insertion is dropped, and undoing puts it back
    public void A_row_format_pushed_off_the_edge_comes_back_on_undo()
    {
        var sheet = NewSheet();
        sheet.SetNumberFormat(CellRange.WholeRows(Sheet.RowCount - 1, Sheet.RowCount - 1), Percent);
        var before = sheet.ToDocument().ToJson();

        var step = sheet.Do(SheetEdit.InsertRows(0));

        Assert.Empty(sheet.ToDocument().Rows);
        step.Undo();
        Assert.Equal(before, sheet.ToDocument().ToJson());
    }

    [Fact] // ADR-0047: the existing per-cell commands keep their meaning: one cell, its own format
    public void Per_cell_commands_set_the_cell()
    {
        var sheet = NewSheet();
        sheet.SetNumberFormat(CellRange.Parse("3:3"), Percent);

        sheet.SetNumberFormat([At("B3")], TwoPlaces);
        sheet.SetAlignment(At("B3"), HorizontalAlignment.Left);

        Assert.Equal(TwoPlaces, sheet.GetNumberFormat(At("B3")));
        Assert.Equal(Percent, sheet.GetNumberFormat(At("C3")));
        var cell = Assert.Single(sheet.ToDocument().Cells);
        Assert.Equal((TwoPlaces, (HorizontalAlignment?)HorizontalAlignment.Left), (cell.NumberFormat, cell.Alignment));
    }

    [Fact] // ADR-0047/0048: the format a Formula takes at entry is part of the entry's step, and undoing it puts General back
    public void A_formula_format_taken_at_entry_is_undone_with_it()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "9/26/2026");
        var before = sheet.ToDocument().ToJson();

        var step = sheet.Do(SheetEdit.Enter(At("B1"), "=A1+1"));

        Assert.True(sheet.GetNumberFormat(At("B1")).IsDate);
        Assert.Contains(0, step.Change.Rows);
        step.Undo();
        Assert.Equal(before, sheet.ToDocument().ToJson());
        Assert.True(sheet.GetNumberFormat(At("B1")).IsGeneral);
    }

    [Theory] // ADR-0047: whole columns and rows are written as Excel writes them
    [InlineData("B:D", true, false)]
    [InlineData("3:4", false, true)]
    [InlineData("A:XFD", true, true)]
    [InlineData("A1:B2", false, false)]
    public void Whole_columns_and_rows_parse(string text, bool columns, bool rows)
    {
        var range = CellRange.Parse(text);

        Assert.Equal(columns, range.IsWholeColumns);
        Assert.Equal(rows, range.IsWholeRows);
    }
}
