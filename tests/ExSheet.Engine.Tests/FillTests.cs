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

    [Fact] // ADR-0050 (SH-15): 1, 2 dragged gives 1, 2, 3, 4
    public void Two_numbers_continue_as_a_series()
    {
        var sheet = Column("1", "2");

        Fill(sheet, "A1:A2", "A3:A4", FillDirection.Down);

        Assert.Equal([1, 2, 3, 4], new[] { "A1", "A2", "A3", "A4" }.Select(sheet.Number));
    }

    [Theory] // ADR-0050 (SH-15): Microsoft's examples of an AutoFill linear series
    [InlineData(new[] { "1", "2" }, new[] { 3.0, 4, 5 })]
    [InlineData(new[] { "1", "3" }, new[] { 5.0, 7, 9 })]
    [InlineData(new[] { "100", "95" }, new[] { 90.0, 85 })]
    [InlineData(new[] { "1", "3", "4" }, new[] { 5.666666666666667, 7.166666666666667, 8.666666666666666 })]
    public void Numbers_continue_as_the_linear_trend(string[] typed, double[] expected)
    {
        var sheet = Column(typed);
        var first = typed.Length + 1;

        Fill(sheet, $"A1:A{typed.Length}", $"A{first}:A{first + expected.Length - 1}", FillDirection.Down);

        for (var i = 0; i < expected.Length; i++) Assert.Equal(expected[i], sheet.Number($"A{first + i}"), 12);
        // The source cells are left as typed, not replaced by the fitted line.
        Assert.Equal(double.Parse(typed[^1], System.Globalization.CultureInfo.InvariantCulture), sheet.Number($"A{typed.Length}"));
    }

    [Fact] // ADR-0050: a series filled upward goes back along the same line
    public void A_series_filled_up_goes_back()
    {
        var sheet = NewSheet();
        sheet.Enter("A5", "1");
        sheet.Enter("A6", "2");

        Fill(sheet, "A5:A6", "A3:A4", FillDirection.Up);

        Assert.Equal(0, sheet.Number("A4"));
        Assert.Equal(-1, sheet.Number("A3"));
    }

    [Fact] // ADR-0050: a series fills to the right as it fills down
    public void A_series_filled_right()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "10");
        sheet.Enter("B1", "20");

        Fill(sheet, "A1:B1", "C1:E1", FillDirection.Right);

        Assert.Equal([30, 40, 50], new[] { "C1", "D1", "E1" }.Select(sheet.Number));
    }

    [Fact] // ADR-0050: a single number is copied, as Excel's plain drag copies it
    public void A_single_number_is_copied()
    {
        var sheet = Column("5");

        Fill(sheet, "A1", "A2:A3", FillDirection.Down);

        Assert.Equal([5, 5], new[] { "A2", "A3" }.Select(sheet.Number));
    }

    [Fact] // ADR-0050 (SH-15): a date gives the following days, in the date's format
    public void A_date_gives_the_following_days()
    {
        var sheet = Column("9/27/2026");

        Fill(sheet, "A1", "A2:A4", FillDirection.Down);

        Assert.Equal(["9/28/2026", "9/29/2026", "9/30/2026"], new[] { "A2", "A3", "A4" }.Select(a => sheet.GetDisplay(At(a)).Text));
    }

    [Fact] // ADR-0050: a date filled up gives the days before it
    public void A_date_filled_up_gives_the_days_before()
    {
        var sheet = NewSheet();
        sheet.Enter("A3", "3/1/2026");

        Fill(sheet, "A3", "A1:A2", FillDirection.Up);

        Assert.Equal("2/28/2026", sheet.GetDisplay(At("A2")).Text);
        Assert.Equal("2/27/2026", sheet.GetDisplay(At("A1")).Text);
    }

    [Theory] // ADR-0050: dates beyond a single one, and times of day, step in ways ExSheet does not yet take: refused
    [InlineData(new[] { "1/1/2026", "2/1/2026" }, "yyyy-mm-dd")]
    [InlineData(new[] { "9/27/2026 10:00" }, null)]
    [InlineData(new[] { "10:00" }, null)]
    public void Other_date_patterns_are_refused(string[] typed, string? format)
    {
        var sheet = Column(typed);
        if (format is not null) sheet.SetFormat(Enumerable.Range(1, typed.Length).Select(i => At($"A{i}")), NumberFormat.Parse(format));

        var refusal = Refusal(sheet, $"A1:A{typed.Length}", $"A{typed.Length + 1}:A{typed.Length + 3}", FillDirection.Down);

        Assert.Equal(SheetRefusalReason.FillPatternNotSupported, refusal!.Reason);
    }

    [Fact] // ADR-0050 (SH-15): a Formula dragged shifts its References
    public void A_formula_shifts_its_references()
    {
        var sheet = Column("1", "2", "3", "4");
        sheet.Enter("B1", "=A1*$A$1+A$1");

        Fill(sheet, "B1", "B2:B4", FillDirection.Down);

        Assert.Equal("=A4*$A$1+A$1", sheet.GetEntry(At("B4"))!.Formula);
        Assert.Equal(5, sheet.Number("B4"));
    }

    [Fact] // ADR-0050: several Formulas repeat in order, each shifted from where it came
    public void Several_formulas_repeat_in_order()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "=B1");
        sheet.Enter("A2", "=C2");

        Fill(sheet, "A1:A2", "A3:A5", FillDirection.Down);

        Assert.Equal(["=B3", "=C4", "=B5"], new[] { "A3", "A4", "A5" }.Select(a => sheet.GetEntry(At(a))!.Formula!));
    }

    [Fact] // ADR-0050: several Formulas filled up repeat backwards
    public void Several_formulas_filled_up_repeat_backwards()
    {
        var sheet = NewSheet();
        sheet.Enter("A5", "=B5");
        sheet.Enter("A6", "=C6");

        Fill(sheet, "A5:A6", "A2:A4", FillDirection.Up);

        Assert.Equal(["=C2", "=B3", "=C4"], new[] { "A2", "A3", "A4" }.Select(a => sheet.GetEntry(At(a))!.Formula!));
    }

    [Theory] // ADR-0050 (SH-15): text Excel would continue is refused, not filled with copies
    [InlineData("Item 1")]
    [InlineData("Q1")]
    [InlineData("Monday")]
    [InlineData("jan")]
    [InlineData("Sep")]
    public void Text_excel_would_continue_is_refused(string typed)
    {
        var sheet = Column(typed);

        var edit = SheetEdit.Fill(CellRange.Parse("A1"), CellRange.Parse("A2:A3"), FillDirection.Down);

        Assert.Equal(SheetRefusalReason.FillPatternNotSupported, sheet.Check(edit)!.Reason);
        Assert.Throws<SheetRefusedException>(() => sheet.Do(edit));
        Assert.Null(sheet.GetEntry(At("A2")));
    }

    [Fact] // ADR-0050: day names are refused in the Sheet's culture too
    public void Day_names_of_the_sheets_culture_are_refused()
    {
        var sheet = new Sheet(System.Globalization.CultureInfo.GetCultureInfo("de-DE"));
        sheet.Enter(At("A1"), "Montag");

        Assert.Equal(SheetRefusalReason.FillPatternNotSupported, Refusal(sheet, "A1", "A2", FillDirection.Down)!.Reason);
    }

    [Fact] // ADR-0050: text with no pattern, booleans, Error Values and blanks are copied, repeating the source
    public void Plain_values_are_copied()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "apple");
        sheet.Enter("A2", "TRUE");
        sheet.Enter("A3", "#N/A");
        sheet.Enter("A7", "stale");

        Fill(sheet, "A1:A4", "A5:A8", FillDirection.Down);

        Assert.Equal("apple", sheet.Value("A5")!.Value.Text);
        Assert.True(sheet.Value("A6")!.Value.Boolean);
        Assert.Equal(ErrorValue.NA, sheet.Error("A7"));
        Assert.Null(sheet.Value("A8"));
    }

    [Theory] // ADR-0050: numbers mixed with anything else are refused
    [InlineData("1", "a")]
    [InlineData("1", "=A1")]
    [InlineData("1", "")]
    public void Numbers_mixed_with_other_cells_are_refused(string first, string second)
    {
        var sheet = Column(first, second);

        Assert.Equal(SheetRefusalReason.FillPatternNotSupported, Refusal(sheet, "A1:A2", "A3:A4", FillDirection.Down)!.Reason);
    }

    [Fact] // ADR-0050: each column of a vertical fill follows its own rule
    public void Each_line_follows_its_own_rule()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "1");
        sheet.Enter("A2", "2");
        sheet.Enter("B1", "=A1");
        sheet.Enter("C1", "x");

        Fill(sheet, "A1:C2", "A3:C4", FillDirection.Down);

        Assert.Equal(4, sheet.Number("A4"));
        Assert.Equal("=A3", sheet.GetEntry(At("B3"))!.Formula);
        Assert.Null(sheet.GetEntry(At("B4")));
        Assert.Equal("x", sheet.Value("C3")!.Value.Text);
        Assert.Null(sheet.Value("C4"));
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

    [Fact] // ADR-0050: the target may be given as the whole extended range
    public void The_target_may_include_the_source()
    {
        var sheet = Column("1", "2");

        Fill(sheet, "A1:A2", "A1:A4", FillDirection.Down);

        Assert.Equal(4, sheet.Number("A4"));
        Assert.Equal(1, sheet.Number("A1"));
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
}
