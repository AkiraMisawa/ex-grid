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
}
