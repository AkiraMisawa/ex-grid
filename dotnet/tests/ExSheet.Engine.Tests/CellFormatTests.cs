using ExSheet.Engine;
using Xunit;

namespace ExSheet.Engine.Tests;

/// <summary>
/// ADR-0071 (SH-38, SH-44): what a Cell Format holds and how a change sets it is in the shared cases
/// (<c>tests/cases/sheet/cell-format.json</c>). What stays here is the construction of its parts, which no Sheet Edit makes.
/// </summary>
public class CellFormatTests
{
    private static readonly CellColour Red = CellColour.FromRgb(0xFF0000);

    [Fact] // ADR-0071: a colour is Automatic or RGB; a Fill is RGB; no line has no colour; nothing else is constructed
    public void The_parts_hold_only_what_they_may()
    {
        Assert.True(default(CellColour).IsAutomatic);
        Assert.Equal(0x00FF7F, CellColour.FromRgb(0x00FF7F).Rgb);
        Assert.Equal(CellColour.FromRgb(0x123456), CellColour.FromRgb(0x12, 0x34, 0x56));
        Assert.Equal("#000000", CellColour.FromRgb(0).ToString());
        Assert.NotEqual(CellColour.Automatic, CellColour.FromRgb(0));
        Assert.Throws<InvalidOperationException>(() => CellColour.Automatic.Rgb);
        Assert.Throws<ArgumentOutOfRangeException>(() => CellColour.FromRgb(0x1000000));
        Assert.Throws<ArgumentOutOfRangeException>(() => CellColour.FromRgb(-1));

        Assert.Throws<ArgumentException>(() => CellFill.Solid(CellColour.Automatic));
        Assert.True(default(CellFill).IsNone);

        Assert.Equal(BorderLine.None, new BorderLine(BorderLineStyle.None, Red));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BorderLine((BorderLineStyle)99));
        Assert.Equal(13, Enum.GetValues<BorderLineStyle>().Count(s => s != BorderLineStyle.None));
    }
}
