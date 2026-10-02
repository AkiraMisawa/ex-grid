using Bunit;
using ExSheet.Engine;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExSheet.MudBlazor.Tests;

/// <summary>
/// The Sheet Toolbar under the MudBlazor Chrome (ADR-0100; ticket 54; SH-50): the same items ExSheet
/// describes, drawn in MudBlazor's controls — a toggle as a MudToggleIconButton, a list as a MudMenu
/// — with the same meaning: a press sets what the built-in item sets, and a pointer press keeps the
/// keyboard on the Sheet.
/// </summary>
public class MudToolbarTests : MudSheetTestContext
{
    private static AngleSharp.Dom.IElement Wrapper(Bunit.IRenderedComponent<Bunit.Rendering.ContainerFragment> page, string name) =>
        page.FindAll(".mud-ex-sheet-toolbar-item").Single(w => w.QuerySelector($"[aria-label=\"{name}\"]") is not null);

    [Fact] // ADR-0100, SH-50: every item is drawn in MudBlazor's controls, in the default row's order
    public void The_items_are_drawn_in_mudblazors_controls()
    {
        var page = RenderPage(showToolbar: true);

        Assert.Empty(page.FindAll(".ex-sheet-toolbar-item"));
        var names = page.FindAll(".mud-ex-sheet-toolbar-item").Select(w => w.QuerySelector("[aria-label]")?.GetAttribute("aria-label")).Where(n => n is not null).ToList();
        Assert.Equal(["Bold", "Italic", "Underline", "Strikethrough", "Font Colour", "Fill Colour", "Borders",
            "Align Left", "Centre", "Align Right", "Number Format", "Percent Style", "Comma Style", "Format Cells"], names);
        Assert.NotNull(Wrapper(page, "Bold").QuerySelector(".mud-toggle-icon-button, .mud-icon-button"));
        Assert.NotNull(Wrapper(page, "Number Format").QuerySelector(".mud-menu"));
    }

    [Fact] // ADR-0100, SH-50: a pointer press on a MudBlazor item does not take DOM focus
    public void A_pointer_press_does_not_take_focus()
    {
        var page = RenderPage(showToolbar: true);

        Assert.All(page.FindAll(".mud-ex-sheet-toolbar-item"), w => Assert.True(w.HasAttribute("blazor:onmousedown:preventdefault")));
    }

    [Fact] // ADR-0100, ADR-0010, SH-50: Bold means under MudBlazor what it means built in: on over the Selection, then pressed
    public async Task Bold_sets_bold_and_shows_pressed()
    {
        var page = RenderPage(DocumentOf(("B2", "12")), showToolbar: true);
        await GoToAsync(page, "B2");

        await Wrapper(page, "Bold").QuerySelector("button")!.ClickAsync(new MouseEventArgs());

        Assert.True(FormatAt(page, "B2").Font.Bold);
        page.WaitForAssertion(() => Assert.Equal("true", Wrapper(page, "Bold").QuerySelector("[aria-pressed]")!.GetAttribute("aria-pressed")));
    }

    [Fact] // ADR-0100: the Number Format's MudMenu lists the categories, Accounting disabled, and a choice sets its format
    public async Task The_number_format_menu_sets_a_category()
    {
        var page = RenderPage(DocumentOf(("B2", "12")), showToolbar: true);
        await GoToAsync(page, "B2");

        await Wrapper(page, "Number Format").QuerySelector("button")!.ClickAsync(new MouseEventArgs());
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll(".mud-menu-item")));
        var items = page.FindAll(".mud-menu-item");
        Assert.Contains(items, i => i.TextContent.StartsWith("Accounting") && i.ClassList.Contains("mud-disabled"));

        await items.Single(i => i.TextContent.StartsWith("Percentage")).ClickAsync(new MouseEventArgs());

        Assert.Equal("0.00%", FormatAt(page, "B2").NumberFormat.Code);
    }

    [Fact] // ADR-0100: the Fill's list is Excel's palette, and a swatch sets that Fill
    public async Task A_fill_swatch_sets_the_fill()
    {
        var page = RenderPage(DocumentOf(("B2", "12")), showToolbar: true);
        await GoToAsync(page, "B2");

        await Wrapper(page, "Fill Colour").QuerySelector(".mud-ex-sheet-toolbar-arrow button")!.ClickAsync(new MouseEventArgs());
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("button.mud-ex-sheet-swatch")));
        await page.FindAll("button.mud-ex-sheet-swatch").Single(b => b.GetAttribute("aria-label") == "Green").ClickAsync(new MouseEventArgs());

        Assert.Equal(CellFill.Solid(CellColour.FromRgb(0x00B050)), FormatAt(page, "B2").Fill);
    }
}
