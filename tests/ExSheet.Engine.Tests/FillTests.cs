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
        sheet.Enter("A1", "1");
        sheet.Enter("B1", "Item 1");

        var refusal = Refusal(sheet, "A1:B1", "A2:B3", FillDirection.Down);

        Assert.Equal(SheetRefusalReason.FillPatternNotSupported, refusal!.Reason);
        Assert.Contains("B1", refusal.Message, StringComparison.Ordinal);
    }

    [Fact] // ADR-0050: formats and alignment repeat from the source
    public void Formats_repeat_from_the_source()
    {
        var sheet = Column("1", "2");
        sheet.SetFormat(At("A1"), NumberFormat.Parse("0.00"));
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
        sheet.SetFormat(At("A1"), NumberFormat.Parse("0.00"));
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
