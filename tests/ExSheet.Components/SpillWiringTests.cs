using Bunit;
using ExSheet.Components.Tests.Support;
using Xunit;

namespace ExSheet.Components.Tests;

/// <summary>
/// Spilled arrays through the component (ADR-0125): a spilled cell paints its Value, the Formula
/// Bar shows its Anchor's Formula dimmed and opens no edit on it, typing into it enters an Entry
/// there and makes the Anchor <c>#SPILL!</c>, and clearing it lets the Formula spill again.
/// </summary>
public class SpillWiringTests : SheetTestContext
{
    private static AngleSharp.Dom.IElement Bar(IRenderedComponent<Components.ExSheet> cut) => cut.Find("input.ex-formula-bar-text");

    private async Task<IRenderedComponent<Components.ExSheet>> SpilledAsync()
    {
        var cut = RenderSheet();
        await EnterAsync(cut, "A1", "1");
        await EnterAsync(cut, "A2", "2");
        await EnterAsync(cut, "A3", "3");
        await EnterAsync(cut, "C1", "=A1:A3*10");
        return cut;
    }

    [Fact] // ADR-0125: the Formula's cell and the cells it spills into paint the array
    public async Task A_spill_paints_its_values()
    {
        var cut = await SpilledAsync();

        Assert.Equal(["10", "20", "30"], [CellText(cut, "C1"), CellText(cut, "C2"), CellText(cut, "C3")]);
    }

    [Fact] // ADR-0125: a spilled cell's Formula Bar shows the Anchor's Formula, dimmed and read-only; the Anchor's is its own
    public async Task The_bar_shows_the_anchors_formula_dimmed()
    {
        var cut = await SpilledAsync();

        await GoToAsync(cut, "C2");
        Assert.Equal("=A1:A3*10", Bar(cut).GetAttribute("value"));
        Assert.Contains("ex-formula-bar-borrowed", Bar(cut).ClassList);
        Assert.True(Bar(cut).HasAttribute("readonly"));

        await GoToAsync(cut, "C1");
        Assert.Equal("=A1:A3*10", Bar(cut).GetAttribute("value"));
        Assert.DoesNotContain("ex-formula-bar-borrowed", Bar(cut).ClassList);
        Assert.False(Bar(cut).HasAttribute("readonly"));
    }

    [Fact] // ADR-0125: a press on the bar over a spilled cell opens no edit, since the Formula is not that cell's
    public async Task The_bar_opens_no_edit_over_a_spilled_cell()
    {
        var cut = await SpilledAsync();
        await GoToAsync(cut, "C3");

        await Bar(cut).FocusAsync(new Microsoft.AspNetCore.Components.Web.FocusEventArgs());

        Assert.Empty(cut.FindAll(".ex-editing"));
        Assert.Equal("30", CellText(cut, "C3"));
    }

    [Fact] // ADR-0125: typing into a spilled cell enters an Entry there and the Anchor shows #SPILL!; clearing it spills again
    public async Task Typing_into_a_spilled_cell_makes_the_anchor_spill()
    {
        var cut = await SpilledAsync();

        await EnterAsync(cut, "C2", "7");

        Assert.Equal("#SPILL!", CellText(cut, "C1"));
        Assert.Equal("7", CellText(cut, "C2"));
        Assert.Equal("", CellText(cut, "C3"));

        await GoToAsync(cut, "C2");
        await PressAsync(cut, "Delete");

        Assert.Equal("10", CellText(cut, "C1"));
        Assert.Equal("20", CellText(cut, "C2"));
        Assert.Equal("30", CellText(cut, "C3"));
    }
}
