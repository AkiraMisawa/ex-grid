using Bunit;
using ExSheet.Engine;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExSheet.MudBlazor.Tests;

/// <summary>
/// The Sheet Toolbar under the MudBlazor Chrome (ADR-0100; ticket 54; SH-50): the same items ExSheet
/// describes, drawn in MudBlazor's controls — a toggle as a MudToggleIconButton, a list as a MudMenu
/// — with the same meaning: a press sets what the built-in item sets, and a pointer press keeps the
/// keyboard on the Sheet. A list opened from the toolbar's keys takes the keyboard into its choices,
/// whose keys are the built-in list's, and Escape closes it with the keyboard handed back to the
/// Sheet (the seventeenth Windows run, Part D: the lists stayed open, four at once).
/// </summary>
public class MudToolbarTests : MudSheetTestContext
{
    private const string Focus = "Blazor._internal.domWrapper.focus";

    private static AngleSharp.Dom.IElement Wrapper(Bunit.IRenderedComponent<Bunit.Rendering.ContainerFragment> page, string name) =>
        page.FindAll(".mud-ex-sheet-toolbar-item").Single(w => w.QuerySelector($"[aria-label=\"{name}\"]") is not null);

    /// <summary>The toolbar's list that stands open, if one does.</summary>
    private static IReadOnlyList<AngleSharp.Dom.IElement> Lists(Bunit.IRenderedComponent<Bunit.Rendering.ContainerFragment> page) =>
        page.FindAll(".mud-ex-sheet-toolbar-choices");

    /// <summary>F10, then each letter typed on the toolbar, as KeyTips are typed (ADR-0100).</summary>
    private static async Task KeyTipsAsync(Bunit.IRenderedComponent<Bunit.Rendering.ContainerFragment> page, params string[] letters)
    {
        await PressAsync(page, "F10");
        foreach (var letter in letters) await page.Find(".ex-sheet-toolbar").KeyDownAsync(new KeyboardEventArgs { Key = letter });
    }

    /// <summary>A key typed in the open list.</summary>
    private static Task TypeInListAsync(Bunit.IRenderedComponent<Bunit.Rendering.ContainerFragment> page, string key) =>
        page.Find(".mud-ex-sheet-toolbar-choices").KeyDownAsync(new KeyboardEventArgs { Key = key });

    /// <summary>
    /// The name of what the last DOM focus asked for was asked on, read by the element's reference: a
    /// MudMenuItem's text, a swatch's label, or null for anything else.
    /// </summary>
    private string? LastFocused(Bunit.IRenderedComponent<Bunit.Rendering.ContainerFragment> page)
    {
        var last = JSInterop.Invocations.LastOrDefault(i => i.Identifier == Focus);
        if (last.Arguments is not [Microsoft.AspNetCore.Components.ElementReference element, ..]) return null;
        if (page.FindComponents<global::MudBlazor.MudMenuItem>().FirstOrDefault(i => i.Instance.ElementReference.Id == element.Id) is { } item)
            return string.Concat(item.Nodes.Select(n => n.TextContent)).Trim();
        return page.FindAll("button.mud-ex-sheet-swatch").FirstOrDefault(e => e.GetAttribute("blazor:elementReference") == element.Id)?.GetAttribute("aria-label");
    }

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

    [Fact] // ADR-0100, ADR-0039, SH-52 (the seventeenth Windows run, D-mud-lists): Alt, H, F, C opens the Font Colour list with the keyboard in it, on the choice the Focus cell has
    public async Task A_list_opened_from_the_keys_takes_the_keyboard()
    {
        var page = RenderPage(DocumentOf(("B2", "12")), showToolbar: true);
        await GoToAsync(page, "B2");

        await KeyTipsAsync(page, "h", "f", "c");

        page.WaitForAssertion(() => Assert.Single(Lists(page)));
        page.WaitForAssertion(() => Assert.Equal("Automatic", LastFocused(page)));
    }

    [Fact] // ADR-0100, ADR-0039, SH-52 (the seventeenth Windows run, D-mud-lists): Escape in the list closes it, and the keyboard goes back to the Sheet through the item's own element, never aria-hidden
    public async Task Escape_in_the_list_closes_it_and_gives_the_keyboard_back()
    {
        var page = RenderPage(DocumentOf(("B2", "12")), showToolbar: true);
        await GoToAsync(page, "B2");
        await KeyTipsAsync(page, "h", "h");
        page.WaitForAssertion(() => Assert.Equal("No Fill", LastFocused(page)));
        var reclaimed = ReclaimCount;
        // The browser has not yet put the keyboard on the item's element: it stands a round trip.
        var focus = JSInterop.SetupVoid(Focus, _ => true);

        await TypeInListAsync(page, "Escape");

        var leaving = page.WaitForElement(".mud-ex-sheet-toolbar-leaving");
        Assert.False(leaving.HasAttribute("aria-hidden"));
        Assert.Equal(reclaimed, ReclaimCount);
        focus.SetVoidResult();
        page.WaitForAssertion(() => Assert.Empty(Lists(page)));
        page.WaitForAssertion(() => Assert.True(ReclaimCount > reclaimed));
        Assert.Empty(page.FindAll(".mud-ex-sheet-toolbar-leaving"));
        Assert.Equal(CellFill.None, FormatAt(page, "B2").Fill);

        // And the next list is the only one.
        await KeyTipsAsync(page, "h", "b");
        page.WaitForAssertion(() => Assert.Single(Lists(page)));
    }

    [Fact] // ADR-0100, ADR-0010, SH-50: the list's keys are the built-in list's — the arrows move among the choices, End to the last, Tab keeps the keyboard, Enter makes one
    public async Task The_lists_keys_are_the_built_in_lists()
    {
        var page = RenderPage(DocumentOf(("B2", "12")), showToolbar: true);
        await GoToAsync(page, "B2");
        await KeyTipsAsync(page, "h", "b");
        page.WaitForAssertion(() => Assert.Equal("Bottom Border", LastFocused(page)));

        await TypeInListAsync(page, "ArrowDown");
        page.WaitForAssertion(() => Assert.Equal("Top Border", LastFocused(page)));
        await TypeInListAsync(page, "End");
        page.WaitForAssertion(() => Assert.Equal("Thick Outside Borders", LastFocused(page)));
        var focusCalls = JSInterop.Invocations.Count(i => i.Identifier == Focus);
        await TypeInListAsync(page, "Tab");
        Assert.Equal(focusCalls, JSInterop.Invocations.Count(i => i.Identifier == Focus));
        Assert.Single(Lists(page));
        var reclaimed = ReclaimCount;

        await TypeInListAsync(page, "Enter");

        page.WaitForAssertion(() => Assert.Empty(Lists(page)));
        Assert.Equal(new BorderLine(BorderLineStyle.Thick), FormatAt(page, "B2").Borders.Top);
        page.WaitForAssertion(() => Assert.True(ReclaimCount > reclaimed));
    }

    [Fact] // ADR-0100, ADR-0010: in a palette the arrows move ten swatches across, as the built-in palette's do, and Enter sets the swatch reached
    public async Task The_arrows_move_across_the_palette()
    {
        var page = RenderPage(DocumentOf(("B2", "12")), showToolbar: true);
        await GoToAsync(page, "B2");
        await KeyTipsAsync(page, "h", "h");
        page.WaitForAssertion(() => Assert.Equal("No Fill", LastFocused(page)));
        var first = FormatCellsOffer.ThemeColours[0][0];
        var below = FormatCellsOffer.ThemeColours[1][0];

        await TypeInListAsync(page, "ArrowDown");
        page.WaitForAssertion(() => Assert.Equal(first.Name, LastFocused(page)));
        await TypeInListAsync(page, "ArrowDown");
        page.WaitForAssertion(() => Assert.Equal(below.Name, LastFocused(page)));
        await TypeInListAsync(page, "Enter");

        page.WaitForAssertion(() => Assert.Empty(Lists(page)));
        Assert.Equal(CellFill.Solid(below.Colour), FormatAt(page, "B2").Fill);
    }

    [Fact] // ADR-0100, SH-50: a choice pressed with the pointer closes the list and gives the keyboard back to the Sheet
    public async Task A_pressed_choice_closes_the_list_and_gives_the_keyboard_back()
    {
        var page = RenderPage(DocumentOf(("B2", "12")), showToolbar: true);
        await GoToAsync(page, "B2");
        await Wrapper(page, "Borders").QuerySelector(".mud-ex-sheet-toolbar-arrow button")!.ClickAsync(new MouseEventArgs());
        page.WaitForAssertion(() => Assert.Single(Lists(page)));
        var reclaimed = ReclaimCount;

        await page.FindAll(".mud-ex-sheet-toolbar-choices .mud-menu-item").Single(i => i.TextContent.Trim() == "Top Border").ClickAsync(new MouseEventArgs());

        page.WaitForAssertion(() => Assert.Empty(Lists(page)));
        Assert.Equal(new BorderLine(BorderLineStyle.Thin), FormatAt(page, "B2").Borders.Top);
        page.WaitForAssertion(() => Assert.True(ReclaimCount > reclaimed));
    }
}
