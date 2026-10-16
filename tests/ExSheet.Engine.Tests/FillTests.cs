using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>What a Fill Intent writes (ticket 15): SH-15.</summary>
public class FillTests
{
    private static CellAddress At(string address) => CellAddress.Parse(address);

    private static SheetStep Fill(Sheet sheet, string source, string target, FillDirection direction) =>
        sheet.Do(SheetEdit.Fill(CellRange.Parse(source), CellRange.Parse(target), direction));

    private static SheetRefusal? Refusal(Sheet sheet, string source, string target, FillDirection direction) =>
        sheet.Check(SheetEdit.Fill(CellRange.Parse(source), CellRange.Parse(target), direction));

    private static Sheet Column(params string[] typed)
    {
        var sheet = NewSheet();
        for (var i = 0; i < typed.Length; i++) sheet.Enter($"A{i + 1}", typed[i]);
        return sheet;
    }

    [Fact] // ADR-0050: one line that cannot be filled refuses the whole fill
    public void One_refused_line_refuses_the_fill()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "x");
        sheet.Enter("B1", "Item 1");

        var refusal = Refusal(sheet, "A1:B1", "A2:B3", FillDirection.Down);

        Assert.Equal(SheetRefusalReason.FillPatternNotSupported, refusal!.Reason);
        Assert.Contains("B1", refusal.Message, StringComparison.Ordinal);
    }

    [Fact] // ADR-0050: formats and alignment repeat from the source
    public void Formats_repeat_from_the_source()
    {
        var sheet = Column("1", "2");
        sheet.SetNumberFormat(At("A1"), NumberFormat.Parse("0.00"));
        sheet.SetAlignment(At("A2"), HorizontalAlignment.Center);

        Fill(sheet, "A1:A2", "A3:A4", FillDirection.Down);

        Assert.Equal("3.00", sheet.GetDisplay(At("A3")).Text);
        Assert.Equal(HorizontalAlignment.Center, sheet.GetAlignment(At("A4")));
    }

    [Theory] // ADR-0050: a target that does not extend the source along one axis is refused
    [InlineData("A1:A2", "A4:A5", FillDirection.Down)]
    [InlineData("A1:A2", "B1:B2", FillDirection.Down)]
    [InlineData("A1:B2", "A3:A4", FillDirection.Down)]
    [InlineData("B2", "B3", FillDirection.Up)]
    public void A_target_that_does_not_extend_the_source_is_refused(string source, string target, FillDirection direction)
    {
        var sheet = Column("1", "2");

        Assert.Equal(SheetRefusalReason.FillShapeNotSupported, Refusal(sheet, source, target, direction)!.Reason);
    }

    [Fact] // ADR-0048 (SH-13): a fill is one step, and its undo restores what it wrote over
    public void A_fill_is_one_step()
    {
        var sheet = Column("1", "2", "old");
        var before = sheet.ToDocument().ToJson();

        var step = Fill(sheet, "A1:A2", "A3:A6", FillDirection.Down);
        step.Undo();

        Assert.Equal(before, sheet.ToDocument().ToJson());
    }

    // ---- Excel's fill keys, Ctrl+D and Ctrl+R: a copy, never a series (ADR-0050 item 5, 2026-09-28) ----

    private static SheetStep FillCopy(Sheet sheet, string source, string target, FillDirection direction) =>
        sheet.Do(SheetEdit.FillCopy(CellRange.Parse(source), CellRange.Parse(target), direction));

    // ---- A fill from whole columns or whole rows (ADR-0050 item 5: the handle stands beside the Heading, 2026-10-12) ----

    private static SheetStep SetFormat(Sheet sheet, CellFormatChange change, string range) =>
        sheet.Do(SheetEdit.SetCellFormat([CellRange.Parse(range)], change));

    [Fact] // ADR-0050 item 5: column C filled right two columns, as the readings of 2026-10-12 (R5) observed Excel fill it
    public void A_whole_column_filled_right_fills_every_row_and_steps_a_lone_number()
    {
        var sheet = NewSheet();
        sheet.Enter("C1", "1");
        sheet.Enter("C3", "a");
        sheet.Enter("C20", "=ROW()");
        sheet.Enter("C1048576", "z");
        sheet.Enter("D9", "old");
        var before = sheet.ToDocument().ToJson();

        var step = Fill(sheet, "C1:C1048576", "D1:E1048576", FillDirection.Right);

        // Observed (R5, (3)): the lone number goes on as a series, text is copied, =ROW() is
        // filled as a Formula, and the last row is filled too; D2 stays empty.
        Assert.Equal("2", sheet.GetDisplay(At("D1")).Text);
        Assert.Equal("3", sheet.GetDisplay(At("E1")).Text);
        Assert.Null(sheet.GetEntry(At("D2")));
        Assert.Equal("a", sheet.GetDisplay(At("D3")).Text);
        Assert.Equal("a", sheet.GetDisplay(At("E3")).Text);
        Assert.Equal("=ROW()", sheet.GetEntry(At("D20"))!.Formula);
        Assert.Equal("20", sheet.GetDisplay(At("E20")).Text);
        Assert.Equal("z", sheet.GetDisplay(At("D1048576")).Text);
        Assert.Equal("z", sheet.GetDisplay(At("E1048576")).Text);
        // C9 is blank, and a blank is copied as every fill copies one: D9's text is gone (by
        // generalisation from every other fill; R5's target held nothing).
        Assert.Null(sheet.GetEntry(At("D9")));
        Assert.Null(sheet.GetEntry(At("F1")));

        step.Undo();
        Assert.Equal(before, sheet.ToDocument().ToJson());
    }

    [Fact] // ADR-0050 item 5: one cell's handle copies a lone number, as R5's control observed Excel copy C5's
    public void One_cells_lone_number_is_copied_not_stepped()
    {
        var sheet = NewSheet();
        sheet.Enter("C5", "1");

        Fill(sheet, "C5", "C6:C7", FillDirection.Down);

        Assert.Equal("1", sheet.GetDisplay(At("C6")).Text);
        Assert.Equal("1", sheet.GetDisplay(At("C7")).Text);
    }

    [Fact] // ADR-0050 item 5 (2026-10-14, after run 18a, fill-handle-axis 4): C1:C2 of two numbers dragged right copies each, 1 1 and 5 5
    public void Adr0050_a_lone_number_per_line_among_numbers_is_copied()
    {
        var sheet = NewSheet();
        sheet.Enter("C1", "1");
        sheet.Enter("C2", "5");

        Fill(sheet, "C1:C2", "D1:E2", FillDirection.Right);

        Assert.Equal(["1", "1", "5", "5"], new[] { "D1", "E1", "D2", "E2" }.Select(a => sheet.GetDisplay(At(a)).Text));
    }

    [Theory] // ADR-0050 item 5 (2026-10-14, after run 18a, fill-handle-axis 4): the one number of a source of several cells that holds nothing else goes on by one, C1 1 over an empty C2 right giving 2 3 with row 2 empty; down, up and left by generalisation
    [InlineData("C1:C2", "D1:E2", FillDirection.Right, "D1", "2", "E1", "3")]
    [InlineData("C1:D1", "C2:D3", FillDirection.Down, "C2", "2", "C3", "3")]
    [InlineData("C1:C2", "A1:B2", FillDirection.Left, "B1", "0", "A1", "-1")]
    public void Adr0050_the_lone_number_of_a_source_of_several_cells_goes_on_by_one(string source, string target, FillDirection direction, string first, string firstText, string second, string secondText)
    {
        var sheet = NewSheet();
        sheet.Enter("C1", "1");

        Fill(sheet, source, target, direction);

        Assert.Equal(firstText, sheet.GetDisplay(At(first)).Text);
        Assert.Equal(secondText, sheet.GetDisplay(At(second)).Text);
        foreach (var cell in CellRange.Parse(target).Cells().Where(a => a != At(first) && a != At(second)))
            Assert.Null(sheet.GetEntry(cell));
    }

    [Fact] // ADR-0050 item 5 (2026-10-14, after run 18a, fill-handle-axis 5): columns B:C with B1 1 and C1 empty, dragged right to E, give D1 2 and an empty E1: the (number, blank) pattern is extended
    public void Adr0050_whole_columns_extend_a_lone_number_beside_a_blank()
    {
        var sheet = NewSheet();
        sheet.Enter("B1", "1");

        Fill(sheet, "B:C", "D:E", FillDirection.Right);

        Assert.Equal("2", sheet.GetDisplay(At("D1")).Text);
        Assert.Null(sheet.GetEntry(At("E1")));
    }

    [Theory] // ADR-0050 item 5 (2026-10-14, after run 18a): a single number in a line of any other source of several cells, which Excel was not read to copy or continue, is refused, not copied
    [InlineData("1", "", "3")]
    [InlineData("1", "a", "")]
    [InlineData("1", "TRUE", "")]
    public void Adr0050_a_lone_number_in_an_unread_source_is_refused(string c1, string c2, string c3)
    {
        var sheet = NewSheet();
        sheet.Enter("C1", c1);
        if (c2 != "") sheet.Enter("C2", c2);
        if (c3 != "") sheet.Enter("C3", c3);

        Assert.Equal(SheetRefusalReason.FillPatternNotSupported, Refusal(sheet, "C1:C3", "D1:E3", FillDirection.Right)!.Reason);
    }

    [Fact] // ADR-0050 item 5: row 3 filled down two rows, as the readings of 2026-10-12 (R5, (4)) observed Excel fill it
    public void A_whole_row_filled_down_fills_every_column_and_steps_a_lone_number()
    {
        var sheet = NewSheet();
        sheet.Enter("A3", "1");
        sheet.Enter("C3", "a");
        sheet.Enter("H3", "=COLUMN()");
        sheet.Enter("XFD3", "z");

        Fill(sheet, "A3:XFD3", "A4:XFD5", FillDirection.Down);

        Assert.Equal("2", sheet.GetDisplay(At("A4")).Text);
        Assert.Equal("3", sheet.GetDisplay(At("A5")).Text);
        Assert.Equal("a", sheet.GetDisplay(At("C5")).Text);
        Assert.Equal("=COLUMN()", sheet.GetEntry(At("H4"))!.Formula);
        Assert.Equal("8", sheet.GetDisplay(At("H5")).Text);
        Assert.Equal("z", sheet.GetDisplay(At("XFD5")).Text);
        Assert.Null(sheet.GetEntry(At("B4")));
    }

    [Fact] // ADR-0050 item 5: filled left or up, a whole axis's lone number counts back by one (by generalisation from R5's right and down)
    public void A_whole_columns_lone_number_counts_back_filled_left()
    {
        var sheet = NewSheet();
        sheet.Enter("C1", "5");

        Fill(sheet, "C1:C1048576", "A1:B1048576", FillDirection.Left);

        Assert.Equal("4", sheet.GetDisplay(At("B1")).Text);
        Assert.Equal("3", sheet.GetDisplay(At("A1")).Text);
    }

    [Fact] // ADR-0050 item 5 / ADR-0035: Ctrl+R over whole columns copies, never a series, as the keys do over any range (by generalisation: R5 dragged the handle)
    public void Ctrl_r_over_whole_columns_copies_the_lone_number()
    {
        var sheet = NewSheet();
        sheet.Enter("C1", "1");

        FillCopy(sheet, "C1:C1048576", "D1:D1048576", FillDirection.Right);

        Assert.Equal("1", sheet.GetDisplay(At("D1")).Text);
    }

    [Fact] // ADR-0050 item 5 / ADR-0047: a whole column's format is written as each target column's own, never into its million cells
    public void A_whole_columns_format_is_written_as_the_target_columns_own()
    {
        var sheet = NewSheet();
        sheet.Enter("C1", "1");
        sheet.Enter("C1048576", "z");
        SetFormat(sheet, new CellFormatChange { Bold = true }, "C:C");
        SetFormat(sheet, new CellFormatChange { Fill = CellFill.Solid(CellColour.FromRgb(255, 255, 0)) }, "D:D");
        var before = sheet.ToDocument().ToJson();

        var step = Fill(sheet, "C1:C1048576", "D1:E1048576", FillDirection.Right);

        var document = sheet.ToDocument();
        // One run, C to E, bold, and D's yellow written over with C's none: what writing each of
        // their cells would show, recorded on the columns.
        var run = Assert.Single(document.Columns);
        Assert.Equal((2, 4), (run.First, run.Last));
        Assert.Equal(new CellFont(Bold: true), run.Font);
        Assert.Null(run.Fill);
        Assert.True(sheet.GetFont(At("E500")).Bold);
        Assert.Equal(CellFill.None, sheet.GetFill(At("D500")));
        // The cells that hold something are written, and record no format their column gives them.
        Assert.Equal(["C1", "C1048576", "D1", "D1048576", "E1", "E1048576"], document.Cells.Select(c => c.Address.ToString()).Order(StringComparer.Ordinal));
        Assert.All(document.Cells, c => Assert.Null(c.Font));

        step.Undo();
        Assert.Equal(before, sheet.ToDocument().ToJson());
        step.Redo();
        Assert.Equal(document.ToJson(), sheet.ToDocument().ToJson());
    }

    [Fact] // ADR-0050 item 5 / ADR-0047: a whole row's format is written as each target row's own
    public void A_whole_rows_format_is_written_as_the_target_rows_own()
    {
        var sheet = NewSheet();
        sheet.Enter("A3", "1");
        SetFormat(sheet, new CellFormatChange { Italic = true }, "3:3");

        Fill(sheet, "A3:XFD3", "A4:XFD5", FillDirection.Down);

        var run = Assert.Single(sheet.ToDocument().Rows);
        Assert.Equal((2, 4), (run.First, run.Last));
        Assert.True(sheet.GetFont(At("XFD5")).Italic);
        Assert.Equal(3, sheet.ToDocument().Cells.Count);
    }

    [Fact] // ADR-0050 item 5 / ADR-0047: under the target column's new format, a row's format and a cell's own still show as writing each cell would show them
    public void A_whole_column_fill_keeps_row_formats_and_cell_formats_over_the_column()
    {
        var sheet = NewSheet();
        sheet.Enter("C1", "1");
        SetFormat(sheet, new CellFormatChange { Bold = true }, "C:C");
        SetFormat(sheet, new CellFormatChange { Italic = true }, "7:7");
        SetFormat(sheet, new CellFormatChange { NumberFormat = NumberFormat.Parse("0.00") }, "C1");
        SetFormat(sheet, new CellFormatChange { Underline = true }, "D2");

        Fill(sheet, "C1:C1048576", "D1:D1048576", FillDirection.Right);

        // Row 7's font over column D's, as over column C's: italic, not bold.
        Assert.Equal(sheet.GetFont(At("C7")), sheet.GetFont(At("D7")));
        // C1's own Number Format is carried to D1.
        Assert.Equal("2.00", sheet.GetDisplay(At("D1")).Text);
        // D2 held a format of its own, and C2 holds nothing: D2 takes C2's, as every blank is copied.
        Assert.Equal(sheet.GetFont(At("C2")), sheet.GetFont(At("D2")));
    }

    [Fact] // ADR-0050 item 5: the source column's format reaches the rows that hold nothing, as it reaches every row it shows on
    public void A_source_columns_format_is_carried_into_its_blank_rows()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "a");
        sheet.Enter("A3", "c");
        SetFormat(sheet, new CellFormatChange { Bold = true }, "A:A");

        Fill(sheet, "A1:A3", "B1:B3", FillDirection.Right);

        Assert.True(sheet.GetFont(At("B2")).Bold);
        Assert.False(sheet.GetFont(At("B4")).Bold);
    }

    [Fact] // ADR-0050 item 5: a blank row filled over a target column's format writes the source's format over it, as the rows that hold something do
    public void A_target_columns_format_is_written_over_in_blank_rows_too()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "a");
        sheet.Enter("A3", "c");
        SetFormat(sheet, new CellFormatChange { Bold = true }, "B:B");

        Fill(sheet, "A1:A3", "B1:B3", FillDirection.Right);

        Assert.False(sheet.GetFont(At("B1")).Bold);
        Assert.False(sheet.GetFont(At("B2")).Bold);
        Assert.True(sheet.GetFont(At("B4")).Bold);
    }

    [Fact] // ADR-0050 item 5: down, a target row's format is written over in the blank columns too
    public void A_target_rows_format_is_written_over_in_blank_columns_too()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "a");
        sheet.Enter("C1", "c");
        SetFormat(sheet, new CellFormatChange { Bold = true }, "2:2");

        Fill(sheet, "A1:C1", "A2:C2", FillDirection.Down);

        Assert.False(sheet.GetFont(At("B2")).Bold);
        Assert.True(sheet.GetFont(At("D2")).Bold);
    }

    [Fact] // ADR-0050 item 5 / ADR-0035, SH-23: Ctrl+D copies a Formula down with its relative References shifted
    public void Fill_copy_shifts_a_formulas_relative_references()
    {
        var sheet = Column("1", "2", "3");
        sheet.Enter("B1", "=A1*10+$A$1");

        FillCopy(sheet, "B1", "B2:B3", FillDirection.Down);

        Assert.Equal("=A2*10+$A$1", sheet.GetEntry(At("B2"))!.ToString());
        Assert.Equal("31", sheet.GetDisplay(At("B3")).Text);
    }

    [Fact] // ADR-0050 item 5 / ADR-0035, SH-23: Ctrl+R copies across, shifting columns
    public void Fill_copy_right_shifts_columns()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "1");
        sheet.Enter("B1", "2");
        sheet.Enter("A2", "=A1*10");

        FillCopy(sheet, "A2", "B2", FillDirection.Right);

        Assert.Equal("20", sheet.GetDisplay(At("B2")).Text);
    }

    [Theory] // ADR-0050 item 5, SH-23: what the handle would continue or refuse, a fill key copies, as Excel's Ctrl+D does
    [InlineData("9/26/2026", "9/26/2026")]
    [InlineData("Item 1", "Item 1")]
    [InlineData("5", "5")]
    public void Fill_copy_never_continues_a_series(string typed, string expected)
    {
        var sheet = Column(typed);

        Assert.Null(sheet.Check(SheetEdit.FillCopy(CellRange.Parse("A1"), CellRange.Parse("A2:A3"), FillDirection.Down)));
        FillCopy(sheet, "A1", "A2:A3", FillDirection.Down);

        Assert.Equal(expected, sheet.GetDisplay(At("A3")).Text);
    }

    [Fact] // ADR-0048 (SH-13): a fill by key is one step, and its undo restores what it wrote over
    public void A_fill_copy_is_one_step()
    {
        var sheet = Column("=B1", "old", "older");
        var before = sheet.ToDocument().ToJson();

        FillCopy(sheet, "A1", "A2:A3", FillDirection.Down).Undo();

        Assert.Equal(before, sheet.ToDocument().ToJson());
    }

    // ---- Excel's Ctrl+Enter: the typed text entered in one cell and written into the range (ADR-0050 item 5, 2026-09-28) ----

    private static SheetStep EnterInto(Sheet sheet, string targets, string enteredAt, string typed) =>
        sheet.Do(SheetEdit.EnterInto([.. targets.Split(',').Select(CellRange.Parse)], At(enteredAt), typed));

    [Fact] // ADR-0050 item 5 (2026-09-28), SH-27: a Formula is read as entered in its cell, and every other cell takes it shifted by its offset from there
    public void Enter_into_shifts_a_formulas_relative_references_from_where_it_was_entered()
    {
        var sheet = NewSheet();

        EnterInto(sheet, "B2:C3", "C3", "=B2+$A$1");

        Assert.Equal("=B2+$A$1", sheet.GetEntry(At("C3"))!.ToString());
        Assert.Equal("=A1+$A$1", sheet.GetEntry(At("B2"))!.ToString());
        Assert.Equal("=B1+$A$1", sheet.GetEntry(At("C2"))!.ToString());
        Assert.Equal("=A2+$A$1", sheet.GetEntry(At("B3"))!.ToString());
    }

    [Fact] // ADR-0050 item 5 (2026-09-28), SH-27: a Reference shifted off the Sheet is #REF!, as the fill keys make it
    public void Enter_into_makes_a_reference_shifted_off_the_sheet_ref()
    {
        var sheet = NewSheet();

        EnterInto(sheet, "A1:A2", "A2", "=A1");

        Assert.Equal("=A1", sheet.GetEntry(At("A2"))!.ToString());
        Assert.Equal("#REF!", sheet.GetDisplay(At("A1")).Text);
    }

    [Fact] // ADR-0050 item 5 (2026-09-28), SH-27: a signed Formula (-A1) is a Formula too, and shifts
    public void Enter_into_shifts_a_signed_formula()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "1");
        sheet.Enter("A2", "2");

        EnterInto(sheet, "B1:B2", "B1", "-A1");

        Assert.Equal("-2", sheet.GetDisplay(At("B2")).Text);
    }

    [Fact] // ADR-0050 item 5 (2026-09-28), SH-27: text that is not a Formula is written into every cell as typed
    public void Enter_into_writes_a_constant_as_typed()
    {
        var sheet = NewSheet();

        EnterInto(sheet, "A1:B1,D4", "A1", "A1");

        Assert.All(new[] { "A1", "B1", "D4" }, a => Assert.Equal("A1", sheet.GetDisplay(At(a)).Text));
    }

    [Fact] // ADR-0050 item 5 (2026-09-28), SH-27: the entered cell's format is not copied, as Excel's Ctrl+Enter does not copy it
    public void Enter_into_does_not_copy_the_entered_cells_format()
    {
        var sheet = NewSheet();
        sheet.SetNumberFormat(At("A1"), NumberFormat.Parse("0.00"));
        sheet.Enter("C1", "1");
        sheet.Enter("D1", "1");

        EnterInto(sheet, "A1:B1", "A1", "=C1");

        Assert.Equal("1.00", sheet.GetDisplay(At("A1")).Text);
        Assert.Equal("1", sheet.GetDisplay(At("B1")).Text);
    }

    [Fact] // ADR-0048, SH-27: a Ctrl+Enter over a range is one step, and its undo restores what it wrote over
    public void Enter_into_is_one_step()
    {
        var sheet = Column("old", "older");
        var before = sheet.ToDocument().ToJson();

        EnterInto(sheet, "A1:B2", "A1", "=C1").Undo();

        Assert.Equal(before, sheet.ToDocument().ToJson());
    }

    [Fact] // ADR-0034 / TYPED-054: a typed Formula that cannot be read is refused, and nothing is written
    public void Enter_into_refuses_an_unreadable_formula()
    {
        var sheet = NewSheet();

        Assert.Throws<FormulaSyntaxException>(() => EnterInto(sheet, "A1:A2", "A1", "=1+"));
        Assert.Null(sheet.GetEntry(At("A2")));
    }

    [Fact] // SH-27: the cell the text was entered in lies in the range it is written into
    public void Enter_into_refuses_an_entered_cell_outside_the_targets()
    {
        Assert.Throws<ArgumentException>(() => SheetEdit.EnterInto([CellRange.Parse("A1:A2")], At("B1"), "1"));
    }
}
