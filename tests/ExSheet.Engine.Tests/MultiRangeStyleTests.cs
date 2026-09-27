using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// ADR-0046: a style can be applied to several ranges in one step, whole columns and rows
/// included, and it is one undo step like any other operation (ADR-0048).
/// </summary>
public class MultiRangeStyleTests
{
    private static readonly NumberFormat TwoPlaces = NumberFormat.Parse("0.00");
    private static readonly NumberFormat Percent = NumberFormat.Parse("0%");

    private static CellAddress At(string address) => CellAddress.Parse(address);

    private static CellRange[] Ranges(params string[] ranges) => [.. ranges.Select(CellRange.Parse)];

    [Fact] // ADR-0046/0047: every range takes the style, whole columns and rows recorded as one entry each
    public void Every_range_takes_the_style()
    {
        var sheet = NewSheet();
        sheet.Enter("B2", "0.5");
        sheet.Enter("E7", "0.5");
        sheet.Enter("Z4", "0.5");

        var step = sheet.Do(SheetEdit.SetStyle(Ranges("B2:C3", "E:E", "4:4"), TwoPlaces, HorizontalAlignment.Right));

        foreach (var cell in new[] { "B2", "C3", "E7", "E1048576", "Z4", "A4" })
        {
            Assert.Equal(TwoPlaces, sheet.GetFormat(At(cell)));
            Assert.Equal(HorizontalAlignment.Right, sheet.GetAlignment(At(cell)));
        }
        Assert.Equal(NumberFormat.General, sheet.GetFormat(At("D2")));
        Assert.Equal(TwoPlaces, sheet.GetColumnFormat(4));
        Assert.Equal(TwoPlaces, sheet.GetRowFormat(3));
        var document = sheet.ToDocument();
        Assert.Single(document.Columns);
        Assert.Single(document.Rows);
        Assert.Equal([1, 2, 3, 6], step.Change.Rows);
        Assert.Empty(step.Change.ValueChanges);
    }

    [Theory] // ADR-0046/0048: several ranges are one step, and undoing it puts every level back exactly
    [InlineData("B:B", "3:4")]
    [InlineData("3:4", "B:B")]
    [InlineData("B2:C3", "C:C", "2:2")]
    [InlineData("B2:C3", "B2:C3")]
    [InlineData("A:XFD", "D5")]
    public void Undo_restores_every_level_exactly(params string[] ranges)
    {
        var sheet = NewSheet();
        sheet.Enter("B2", "0.5");
        sheet.Enter("C3", "0.25");
        sheet.Enter("D5", "7");
        sheet.SetFormat(At("B2"), Percent);
        sheet.SetFormat(CellRange.Parse("3:3"), NumberFormat.Parse("#,##0"));
        sheet.SetAlignment(CellRange.Parse("C:C"), HorizontalAlignment.Center);
        var before = sheet.ToDocument().ToJson();

        var step = sheet.Do(SheetEdit.SetStyle(Ranges(ranges), TwoPlaces, HorizontalAlignment.Left));
        var after = sheet.ToDocument().ToJson();
        Assert.NotEqual(before, after);

        var undone = step.Undo();
        Assert.Equal(before, sheet.ToDocument().ToJson());
        Assert.NotEmpty(undone.Rows);

        step.Redo();
        Assert.Equal(after, sheet.ToDocument().ToJson());
        step.Undo();
        Assert.Equal(before, sheet.ToDocument().ToJson());
    }

    [Fact] // ADR-0046: a null property is left as it is; General is set as General
    public void Null_leaves_a_property_and_General_sets_it()
    {
        var sheet = NewSheet();
        sheet.SetFormat(At("B2"), Percent);
        sheet.SetAlignment(At("D4"), HorizontalAlignment.Center);

        sheet.Do(SheetEdit.SetStyle(Ranges("B2", "D4"), alignment: HorizontalAlignment.Right));
        Assert.Equal(Percent, sheet.GetFormat(At("B2")));
        Assert.Equal(HorizontalAlignment.Right, sheet.GetAlignment(At("D4")));

        sheet.Do(SheetEdit.SetStyle(Ranges("B2", "D4"), format: NumberFormat.General));
        Assert.Equal(NumberFormat.General, sheet.GetFormat(At("B2")));
        Assert.Equal(HorizontalAlignment.Right, sheet.GetAlignment(At("B2")));
    }

    [Fact] // ADR-0046: the Sheet's own form does the same as the step
    public void The_sheet_sets_a_style_on_several_ranges_directly()
    {
        var sheet = NewSheet();
        sheet.Enter("B2", "0.5");

        var change = sheet.SetStyle(Ranges("B:B", "D:D"), Percent, null);

        Assert.Equal("50%", sheet.GetDisplay(At("B2")).Text);
        Assert.Equal(Percent, sheet.GetColumnFormat(3));
        Assert.Null(sheet.GetColumnFormat(2));
        Assert.Equal([1], change.Rows);
    }

    [Fact] // ADR-0046: a style on no range, or one setting nothing, is a mistake the caller made
    public void A_style_on_nothing_or_of_nothing_is_refused()
    {
        Assert.Throws<ArgumentException>(() => SheetEdit.SetStyle([], TwoPlaces));
        Assert.Throws<ArgumentException>(() => SheetEdit.SetStyle(Ranges("B2")));
        Assert.Throws<ArgumentOutOfRangeException>(() => SheetEdit.SetStyle(Ranges("B2"), alignment: (HorizontalAlignment)42));
        Assert.Throws<ArgumentNullException>(() => SheetEdit.SetStyle(null!, TwoPlaces));
    }
}
