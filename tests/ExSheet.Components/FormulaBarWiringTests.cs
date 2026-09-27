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
/// The editor opens on the Entry and the Formula Bar shows it (ticket 08, ADR-0051).
/// </summary>
public class FormulaBarWiringTests : SheetTestContext
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

    [Fact] // ADR-0051: F2 on a Formula's cell opens on the Formula, not the Value
    public async Task F2_opens_on_the_formula()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A1", "21"), ("B1", "=A1*2"))));
        await GoToAsync(cut, "B1");

        await PressAsync(cut, "F2");

        Assert.Equal("=A1*2", cut.Find(".ex-viewport .ex-editor").GetAttribute("value"));
    }

    [Fact] // ADR-0051: the Formula Bar follows the Focus and shows the Entry; the Name Box shows the address
    public async Task The_formula_bar_shows_the_entry_and_the_name_box_the_address()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A1", "21"), ("B1", "=A1*2"))));

        await GoToAsync(cut, "B1");
        Assert.Equal("=A1*2", FormulaBar(cut));
        Assert.Equal("42", CellText(cut, "B1"));
        Assert.Equal("B1", NameBox(cut));

        await PressAsync(cut, "ArrowLeft");
        Assert.Equal("21", FormulaBar(cut));
        Assert.Equal("A1", NameBox(cut));
    }

    [Fact] // ADR-0048/0051: a constant opens as it would be typed under the Sheet's culture
    public async Task A_constant_opens_as_typed_under_the_culture()
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("de-DE"));
        sheet.Enter(CellAddress.Parse("A1"), "1234,5");
        var cut = RenderSheet(ps => ps.Add(s => s.Document, sheet.ToDocument()));

        await GoToAsync(cut, "A1");

        Assert.Equal(sheet.GetEntryText(CellAddress.Parse("A1")), FormulaBar(cut));
        Assert.Contains(",5", FormulaBar(cut));
    }
}
