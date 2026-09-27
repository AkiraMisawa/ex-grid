using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>Inserting and deleting rows and columns (ticket 13): SH-5's engine half.</summary>
public class StructureTests
{
    private static string? Formula(Sheet sheet, string address) => sheet.GetEntry(CellAddress.Parse(address))?.Formula;

    [Fact] // ADR-0047 (SH-5): deleting a referenced cell makes the stored Formula #REF!
    public void Deleting_a_referenced_cell_makes_the_formula_ref_error()
    {
        var sheet = NewSheet();
        sheet.Enter("A5", "1");
        sheet.Enter("B1", "=A5+1");

        var change = sheet.DeleteRows(4);

        Assert.Equal("=#REF!+1", Formula(sheet, "B1"));
        Assert.Equal(ErrorValue.Ref, sheet.Error("B1"));
        Assert.Contains(CellAddress.Parse("B1"), change.ValueChanges);
    }

    [Fact] // ADR-0047: whole columns do not change on a row edit, whole rows move with one
    public void Whole_column_and_whole_row_references_on_row_edits()
    {
        var sheet = NewSheet();
        sheet.Enter("A3", "5");
        sheet.Enter("A4", "7");
        sheet.Enter("B1", "=SUM(A:A)+SUM(3:3)");

        sheet.InsertRows(1);
        Assert.Equal("=SUM(A:A)+SUM(4:4)", Formula(sheet, "B1"));
        Assert.Equal(17, sheet.Number("B1"));

        // The text stays, but a deleted cell leaves what A:A covers: the Value follows.
        var change = sheet.DeleteRows(4);
        Assert.Equal("=SUM(A:A)+SUM(4:4)", Formula(sheet, "B1"));
        Assert.Equal(10, sheet.Number("B1"));
        Assert.Contains(CellAddress.Parse("B1"), change.ValueChanges);
    }

    [Fact] // ADR-0046: inserted rows take the number format and alignment of the row above, never its Entries
    public void Inserted_rows_take_the_formatting_of_the_row_above()
    {
        var sheet = NewSheet();
        sheet.Enter("A2", "1.5");
        sheet.SetFormat(CellAddress.Parse("A2"), NumberFormat.Parse("0.00"));
        sheet.SetAlignment(CellAddress.Parse("B2"), HorizontalAlignment.Center);
        sheet.SetFormat(CellAddress.Parse("C3"), NumberFormat.Parse("0%"));

        var change = sheet.InsertRows(2, 2);

        foreach (var name in new[] { "A3", "A4" })
        {
            var at = CellAddress.Parse(name);
            Assert.Equal("0.00", sheet.GetFormat(at).Code);
            Assert.Equal(HorizontalAlignment.General, sheet.GetAlignment(at));
            Assert.Null(sheet.GetEntry(at));
            Assert.Null(sheet.GetValue(at));
        }
        Assert.Equal(HorizontalAlignment.Center, sheet.GetAlignment(CellAddress.Parse("B3")));
        Assert.Equal(HorizontalAlignment.Center, sheet.GetAlignment(CellAddress.Parse("B4")));
        // The row that was below keeps its own formatting, moved down; it lends nothing upwards.
        Assert.True(sheet.GetFormat(CellAddress.Parse("C3")).IsGeneral);
        Assert.Equal("0%", sheet.GetFormat(CellAddress.Parse("C5")).Code);
        Assert.Equal(1.5, sheet.Number("A2"));
        Assert.Contains(2, change.Rows);
        Assert.Contains(3, change.Rows);
        sheet.Enter("A3", "2");
        Assert.Equal("2.00", sheet.GetDisplay(CellAddress.Parse("A3")).Text);
    }

    [Fact] // ADR-0046: inserted columns take the number format and alignment of the column to the left
    public void Inserted_columns_take_the_formatting_of_the_column_to_the_left()
    {
        var sheet = NewSheet();
        sheet.Enter("B1", "x");
        sheet.SetAlignment(CellAddress.Parse("B1"), HorizontalAlignment.Right);
        sheet.SetFormat(CellAddress.Parse("B7"), NumberFormat.Parse("#,##0"));

        sheet.InsertColumns(2, 3);

        foreach (var name in new[] { "C1", "D1", "E1" })
        {
            Assert.Equal(HorizontalAlignment.Right, sheet.GetAlignment(CellAddress.Parse(name)));
            Assert.Null(sheet.GetEntry(CellAddress.Parse(name)));
        }
        foreach (var name in new[] { "C7", "D7", "E7" }) Assert.Equal("#,##0", sheet.GetFormat(CellAddress.Parse(name)).Code);
        Assert.Equal("x", sheet.Value("B1")!.Value.Text);
    }

    [Fact] // ADR-0046/0048: undoing an insertion removes the formatting it copied, exactly
    public void Undoing_an_insertion_removes_the_copied_formatting()
    {
        var sheet = NewSheet();
        sheet.Enter("A2", "1");
        sheet.SetFormat(CellAddress.Parse("A2"), NumberFormat.Parse("0.00"));
        var before = sheet.ToDocument().ToJson();

        var step = sheet.Do(SheetEdit.InsertRows(2, 3));
        Assert.Equal("0.00", sheet.GetFormat(CellAddress.Parse("A5")).Code);

        step.Undo();
        Assert.Equal(before, sheet.ToDocument().ToJson());
    }

    [Fact] // ADR-0046 (SH-5): Entries move with their formats and alignment
    public void Entries_formats_and_alignment_move()
    {
        var sheet = NewSheet();
        sheet.Enter("A5", "1.5");
        sheet.SetFormat(CellAddress.Parse("A5"), NumberFormat.Parse("0.00"));
        sheet.SetAlignment(CellAddress.Parse("B5"), HorizontalAlignment.Center);

        sheet.InsertRows(1, 3);

        Assert.Equal("1.50", sheet.GetDisplay(CellAddress.Parse("A8")).Text);
        Assert.Equal(HorizontalAlignment.Center, sheet.GetAlignment(CellAddress.Parse("B8")));
        Assert.True(sheet.GetFormat(CellAddress.Parse("A5")).IsGeneral);
        Assert.Equal(HorizontalAlignment.General, sheet.GetAlignment(CellAddress.Parse("B5")));
    }

    [Fact] // ADR-0046 (SH-5): the change names every row whose content moved or changed, and no other
    public void The_change_names_every_row_that_differs()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "1");
        sheet.Enter("A3", "3");
        sheet.Enter("A5", "5");
        sheet.Enter("C1", "=A1");

        var change = sheet.InsertRows(2);

        // Row 3 (index 2) is now blank, row 4 holds 3, row 5 is blank, row 6 holds 5.
        Assert.Equal([2, 3, 4, 5], change.Rows);
        Assert.Equal(["A3", "A4", "A5", "A6"], change.ValueChanges.Addresses());
        Assert.Empty(change.Recalculated);
    }

    [Fact] // ADR-0047: a Formula that moved but reads nothing that moved keeps its Value without recomputing
    public void Only_rewritten_formulas_recompute()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "2");
        sheet.Enter("A10", "=A1*3");
        sheet.Enter("A20", "7");
        sheet.Enter("B1", "=A20");

        var change = sheet.InsertRows(4);

        Assert.Equal(["B1"], change.Recalculated.Addresses());
        Assert.Equal(6, sheet.Number("A11"));
    }

    [Fact] // ADR-0047: the dependency graph follows the moved cells
    public void Dependencies_follow_the_moved_cells()
    {
        var sheet = NewSheet();
        sheet.Enter("A5", "1");
        sheet.Enter("B5", "=A5*10");
        sheet.Enter("C1", "=SUM(B1:B9)");

        sheet.InsertRows(0);
        var change = sheet.Enter("A6", "2");

        Assert.Equal(["C2", "B6"], change.Recalculated.Addresses());
        Assert.Equal(20, sheet.Number("C2"));
    }

    [Fact] // ADR-0046: an edit outside the Sheet's extent is refused as an argument
    public void An_edit_outside_the_extent_is_an_argument_error()
    {
        var sheet = NewSheet();
        Assert.Throws<ArgumentOutOfRangeException>(() => sheet.InsertRows(Sheet.RowCount));
        Assert.Throws<ArgumentOutOfRangeException>(() => sheet.DeleteRows(Sheet.RowCount - 1, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => sheet.DeleteColumns(0, 0));
    }

    [Fact] // ADR-0048: the rewritten Formulas are what the Sheet Document records
    public void The_document_records_the_rewritten_formulas()
    {
        var sheet = NewSheet();
        sheet.Enter("A5", "1");
        sheet.Enter("B1", "=A5");
        sheet.InsertRows(0);

        var reopened = Sheet.Open(SheetDocument.FromJson(sheet.ToDocument().ToJson()));

        Assert.Equal("=A6", Formula(reopened, "B2"));
        Assert.Equal(1, reopened.Number("B2"));
    }
}
