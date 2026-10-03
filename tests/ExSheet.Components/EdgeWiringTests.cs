using System.Globalization;
using Bunit;
using ExGrid.Selection;
using ExSheet.Components.Tests.Support;
using ExSheet.Engine;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExSheet.Components.Tests;

/// <summary>
/// Ctrl+arrow on a Sheet (ticket 07): ExGrid asks, and ExSheet answers from the Sheet's blanks
/// (ADR-0050, item 2). The table of answers is layer 1's, in <see cref="SheetEdgesTests"/>.
/// </summary>
public class EdgeWiringTests : SheetTestContext
{
    private static SheetDocument DocumentOf(params (string Address, string Typed)[] cells)
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        foreach (var (address, typed) in cells) sheet.Enter(CellAddress.Parse(address), typed);
        return sheet.ToDocument();
    }

    private static string NameBox(IRenderedComponent<ExSheet> cut) => cut.Find(".ex-name-box").GetAttribute("value") ?? "";

    private static string FormulaBar(IRenderedComponent<ExSheet> cut) => cut.Find(".ex-formula-bar-text").GetAttribute("value") ?? "";

    private static double HeadingWidth(IRenderedComponent<ExSheet> cut) =>
        double.Parse(cut.Find(".ex-row-heading").GetAttribute("style")!.Replace("width:", "").Replace("px", "").Trim(), CultureInfo.InvariantCulture);

    private static Task PressViewportAsync(IRenderedComponent<ExSheet> cut, double x, double y, bool shift = false) =>
        cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, ShiftKey = shift, OffsetX = x, OffsetY = y });

    [Fact] // ADR-0050 item 2, DC-7: Ctrl+arrow stops where the Sheet's data ends, and Ctrl+Shift+arrow extends to it
    public async Task Ctrl_arrow_asks_the_sheet()
    {
        var selections = new List<GridSelection>();
        var cut = RenderSheet(ps => ps
            .Add(s => s.Document, DocumentOf(("A1", "1"), ("A2", "2"), ("A3", "3"), ("A9", "9")))
            .Add(s => s.SelectionChanged, selections.Add));
        await GoToAsync(cut, "A1");

        await PressAsync(cut, "ArrowDown", ctrl: true);
        Assert.Equal("A3", NameBox(cut));
        await PressAsync(cut, "ArrowDown", ctrl: true);
        Assert.Equal("A9", NameBox(cut));

        await GoToAsync(cut, "A1");
        await PressAsync(cut, "ArrowDown", ctrl: true, shift: true);
        Assert.Equal(new SelectionRange(0, 0, 3, 1), Assert.Single(selections[^1].Ranges));
    }

    [Fact] // ADR-0050 item 2: an edit moves where Ctrl+arrow stops
    public async Task Ctrl_arrow_follows_an_edit()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A1", "1"), ("A2", "2"))));
        await EnterAsync(cut, "A3", "3");

        await GoToAsync(cut, "A1");
        await PressAsync(cut, "ArrowDown", ctrl: true);

        Assert.Equal("A3", NameBox(cut));
    }
}
