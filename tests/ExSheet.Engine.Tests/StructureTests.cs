using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>Inserting and deleting rows and columns (ticket 13): SH-5's engine half.</summary>
public class StructureTests
{
    private static string? Formula(Sheet sheet, string address) => sheet.GetEntry(CellAddress.Parse(address))?.Formula;

    [Fact] // ADR-0046/0047 (SH-5): inserting above a referenced cell rewrites the Reference to it
    public void Inserting_above_a_referenced_cell_rewrites_the_reference()
    {
        var sheet = NewSheet();
        sheet.Enter("A5", "21");
        sheet.Enter("B1", "=A5*2");

        sheet.InsertRows(2);

        Assert.Equal("=A6*2", Formula(sheet, "B1"));
        Assert.Equal(21, sheet.Number("A6"));
        Assert.Null(sheet.Value("A5"));
        Assert.Equal(42, sheet.Number("B1"));
    }

    [Fact] // ADR-0047 (SH-5): absolute and mixed References are rewritten alike
    public void Absolute_and_mixed_references_are_rewritten()
    {
        var sheet = NewSheet();
        sheet.Enter("A5", "1");
        sheet.Enter("C5", "2");
        sheet.Enter("B1", "=$A$5+A$5+$C5");

        sheet.InsertRows(0, 2);

        Assert.Equal("=$A$7+A$7+$C7", Formula(sheet, "B3"));
        Assert.Equal(4, sheet.Number("B3"));
    }

    [Theory] // ADR-0047 (SH-5): a range grows when a row goes inside it, moves when one goes at or above its top, and stays when one goes below it
    [InlineData(2, "=SUM(A2:A6)")]
    [InlineData(1, "=SUM(A3:A6)")]
    [InlineData(0, "=SUM(A3:A6)")]
    [InlineData(4, "=SUM(A2:A6)")]
    [InlineData(5, "=SUM(A2:A5)")]
    public void A_range_grows_or_moves_on_insertion(int row, string expected)
    {
        var sheet = NewSheet();
        sheet.Enter("C1", "=SUM(A2:A5)");

        sheet.InsertRows(row);

        Assert.Equal(expected, Formula(sheet, row == 0 ? "C2" : "C1"));
    }

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

    [Theory] // ADR-0047 (SH-5): a range shrinks when some of its rows are deleted, and is #REF! when all are
    [InlineData(2, 1, "C9", "=SUM(A2:A4)", 11)]
    [InlineData(0, 2, "C8", "=SUM(A1:A3)", 12)]
    [InlineData(3, 5, "C5", "=SUM(A2:A3)", 5)]
    [InlineData(1, 4, "C6", "=SUM(#REF!)", -1)]
    [InlineData(6, 3, "C7", "=SUM(A2:A5)", 14)]
    public void A_range_shrinks_on_deletion(int row, int count, string formulaCell, string expected, double sum)
    {
        var sheet = NewSheet();
        for (var i = 2; i <= 5; i++) sheet.Enter($"A{i}", i.ToString(System.Globalization.CultureInfo.InvariantCulture));
        sheet.Enter("C10", "=SUM(A2:A5)");

        sheet.DeleteRows(row, count);

        Assert.Equal(expected, Formula(sheet, formulaCell));
        if (sum < 0) Assert.Equal(ErrorValue.Ref, sheet.Error(formulaCell));
        else Assert.Equal(sum, sheet.Number(formulaCell));
    }

    [Fact] // ADR-0047 (SH-5): columns are rewritten as rows are, whole columns included
    public void Inserting_a_column_rewrites_column_references()
    {
        var sheet = NewSheet();
        sheet.Enter("B1", "3");
        sheet.Enter("B2", "4");
        sheet.Enter("D1", "=B1+SUM(B:B)+SUM(A1:C1)");

        sheet.InsertColumns(1);

        Assert.Equal("=C1+SUM(C:C)+SUM(A1:D1)", Formula(sheet, "E1"));
        Assert.Equal(13, sheet.Number("E1"));
    }

    [Fact] // ADR-0047 (SH-5): deleting a column rewrites and #REF!s as rows do
    public void Deleting_a_column_rewrites_and_breaks_references()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "1");
        sheet.Enter("B1", "2");
        sheet.Enter("C1", "3");
        sheet.Enter("E1", "=A1+C1");
        sheet.Enter("E2", "=B1");
        sheet.Enter("E3", "=SUM(A1:C1)");

        sheet.DeleteColumns(1);

        Assert.Equal("=A1+B1", Formula(sheet, "D1"));
        Assert.Equal("=#REF!", Formula(sheet, "D2"));
        Assert.Equal("=SUM(A1:B1)", Formula(sheet, "D3"));
        Assert.Equal(4, sheet.Number("D1"));
        Assert.Equal(ErrorValue.Ref, sheet.Error("D2"));
        Assert.Equal(4, sheet.Number("D3"));
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

    [Fact] // ADR-0047: A1:A1048576 is A:A to Excel, and an insertion does not push it off the Sheet
    public void A_range_spanning_the_whole_column_is_left_alone()
    {
        var sheet = NewSheet();
        sheet.Enter("B1", "=SUM(A1:A1048576)");

        sheet.InsertRows(3);

        Assert.Equal("=SUM(A1:A1048576)", Formula(sheet, "B1"));
    }

    [Fact] // ADR-0046: a Reference qualified with a Sheet name is not this Sheet's, and is not rewritten
    public void A_sheet_qualified_reference_is_not_rewritten()
    {
        var sheet = NewSheet();
        sheet.Enter("B1", "=Sheet2!A5");

        sheet.InsertRows(0);

        Assert.Equal("=Sheet2!A5", Formula(sheet, "B2"));
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

    [Fact] // ADR-0047 (SH-9): deleting a cell of a cycle breaks it, and the rest recovers
    public void Deleting_a_cell_of_a_cycle_breaks_it()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "=B1+1");
        sheet.Enter("B1", "=A1+1");
        sheet.Enter("C2", "=A1");
        Assert.Equal(ErrorValue.Circ, sheet.Error("C2"));

        sheet.DeleteColumns(1);

        Assert.Equal("=#REF!+1", Formula(sheet, "A1"));
        Assert.Equal(ErrorValue.Ref, sheet.Error("A1"));
        Assert.Equal(ErrorValue.Ref, sheet.Error("B2"));
    }

    [Fact] // ADR-0047: a Formula inside the deleted rows goes with them, and what read it is #REF!
    public void A_deleted_formula_goes_and_its_readers_break()
    {
        var sheet = NewSheet();
        sheet.Enter("A2", "=1+1");
        sheet.Enter("A4", "=A2*2");

        sheet.DeleteRows(1);

        Assert.Null(sheet.GetEntry(CellAddress.Parse("A2")));
        Assert.Equal("=#REF!*2", Formula(sheet, "A3"));
        Assert.Equal(ErrorValue.Ref, sheet.Error("A3"));
    }

    [Fact] // ADR-0046: an insertion that would push an Entry off the Sheet is refused, and nothing changes
    public void Pushing_an_entry_off_the_sheet_is_refused()
    {
        var sheet = NewSheet();
        sheet.Enter("A1048576", "1");
        sheet.Enter("B1", "2");

        var refused = Assert.Throws<SheetRefusedException>(() => sheet.InsertRows(0));

        Assert.Equal(SheetRefusalReason.EntriesWouldLeaveSheet, refused.Refusal.Reason);
        Assert.Equal(2, sheet.Number("B1"));
        Assert.Equal(1, sheet.Number("A1048576"));
    }

    [Fact] // ADR-0046: a cell holding only formatting at the edge is dropped, not a reason to refuse
    public void Formatting_pushed_off_the_sheet_is_dropped()
    {
        var sheet = NewSheet();
        sheet.SetFormat(CellAddress.Parse("XFD1"), NumberFormat.Parse("0.00"));

        sheet.InsertColumns(0);

        Assert.True(sheet.GetFormat(CellAddress.Parse("XFD1")).IsGeneral);
    }

    [Fact] // ADR-0047: what Excel writes for a Reference pushed off the Sheet is not pinned, so the insertion is refused
    public void Pushing_a_reference_off_the_sheet_is_refused()
    {
        var sheet = NewSheet();
        sheet.Enter("B1", "=SUM(A2:A1048576)");

        var refused = Assert.Throws<SheetRefusedException>(() => sheet.InsertRows(0));

        Assert.Equal(SheetRefusalReason.ReferenceWouldLeaveSheet, refused.Refusal.Reason);
        Assert.Equal("=SUM(A2:A1048576)", Formula(sheet, "B1"));
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
