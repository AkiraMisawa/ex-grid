using Bunit;
using ExGrid.Components;
using ExSheet.Engine;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using MudProviders = global::MudBlazor;

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

    /// <summary>F10, then each letter typed on the toolbar, as KeyTips are typed (ADR-0100): the band
    /// takes DOM focus, as the browser tells it.</summary>
    private static async Task KeyTipsAsync(Bunit.IRenderedComponent<Bunit.Rendering.ContainerFragment> page, params string[] letters)
    {
        await PressAsync(page, "F10");
        await page.Find(".ex-sheet-toolbar").FocusAsync(new FocusEventArgs());
        foreach (var letter in letters) await page.Find(".ex-sheet-toolbar").KeyDownAsync(new KeyboardEventArgs { Key = letter });
    }

    /// <summary>The KeyTips the toolbar shows.</summary>
    private static IReadOnlyList<string> KeyTips(Bunit.IRenderedComponent<Bunit.Rendering.ContainerFragment> page) =>
        [.. page.FindAll(".ex-sheet-toolbar .ex-sheet-keytip").Select(k => k.TextContent)];

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

    [Fact] // ADR-0100, SH-50 (decided with the user, 2026-10-13): the toolbar is one Tab stop under MudBlazor too: no control inside the band, a list's button among them, takes Tab
    public void No_control_inside_the_band_takes_tab()
    {
        var page = RenderPage(showToolbar: true);

        var band = page.Find(".ex-sheet-toolbar");
        Assert.Equal("0", band.GetAttribute("tabindex"));
        // Every button inside it is out of the Tab order, and nothing inside it is put into it: MudMenu's
        // own button, and the wrapper its ActivatorContent is drawn in, both took Tab.
        var tabbable = band.QuerySelectorAll("button, a[href], input, select, textarea, [tabindex]")
            .Where(e => e.GetAttribute("tabindex") is not { } at || int.Parse(at, System.Globalization.CultureInfo.InvariantCulture) >= 0)
            .Select(e => e.GetAttribute("aria-label") ?? e.ClassName)
            .ToList();
        Assert.Empty(tabbable);
        // The lists' buttons are still there to press.
        Assert.NotNull(Wrapper(page, "Number Format").QuerySelector("button.mud-ex-sheet-toolbar-menu[tabindex='-1']"));
        Assert.Equal(3, page.FindAll("button.mud-ex-sheet-toolbar-arrow[tabindex='-1']").Count);
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

        // The choice is made once the list has closed, after the click's own render.
        page.WaitForAssertion(() => Assert.Equal("0.00%", FormatAt(page, "B2").NumberFormat.Code));
    }

    [Fact] // ADR-0100: the Fill's list is Excel's palette, and a swatch sets that Fill
    public async Task A_fill_swatch_sets_the_fill()
    {
        var page = RenderPage(DocumentOf(("B2", "12")), showToolbar: true);
        await GoToAsync(page, "B2");

        await Wrapper(page, "Fill Colour").QuerySelector("button.mud-ex-sheet-toolbar-arrow")!.ClickAsync(new MouseEventArgs());
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("button.mud-ex-sheet-swatch")));
        await page.FindAll("button.mud-ex-sheet-swatch").Single(b => b.GetAttribute("aria-label") == "Green").ClickAsync(new MouseEventArgs());

        page.WaitForAssertion(() => Assert.Equal(CellFill.Solid(CellColour.FromRgb(0x00B050)), FormatAt(page, "B2").Fill));
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

    [Fact] // ADR-0100, ADR-0039, SH-52 (the seventeenth Windows run, D-mud-lists) and R2 (b) of the Windows readings of 2026-10-12 (observed: after Alt, H, F, C one Escape shows the Home tab's KeyTips): Escape in a list a KeyTip opened closes it, through the item's own element inside the band, never aria-hidden, and goes back to its row's KeyTips with the keyboard on the band
    public async Task Escape_in_a_list_a_keytip_opened_goes_back_to_its_rows_keytips()
    {
        var page = RenderPage(DocumentOf(("B2", "12")), showToolbar: true);
        await GoToAsync(page, "B2");
        await KeyTipsAsync(page, "h", "h");
        page.WaitForAssertion(() => Assert.Equal("No Fill", LastFocused(page)));
        await page.Find(".ex-sheet-toolbar").FocusOutAsync(new FocusEventArgs());
        var taken = TakeBackCount;
        // The browser has not yet put the keyboard on the item's element: it stands a round trip.
        var focus = JSInterop.SetupVoid(Focus, _ => true);

        await TypeInListAsync(page, "Escape");
        // Typed in the list while it closes: the KeyTips', once the band has the keyboard again.
        await TypeInListAsync(page, "1");

        var leaving = page.WaitForElement(".mud-ex-sheet-toolbar-leaving");
        Assert.False(leaving.HasAttribute("aria-hidden"));
        Assert.NotNull(leaving.Closest(".ex-sheet-toolbar"));
        focus.SetVoidResult();
        page.WaitForAssertion(() => Assert.Empty(Lists(page)));
        page.WaitForAssertion(() => Assert.Contains("FC", KeyTips(page)));
        Assert.Equal(taken, TakeBackCount);
        Assert.Equal(CellFill.None, FormatAt(page, "B2").Fill);
        Assert.False(FormatAt(page, "B2").Font.Bold);

        // The browser puts the keyboard on the band: the key held for it is its own now.
        await page.Find(".ex-sheet-toolbar").FocusAsync(new FocusEventArgs());
        page.WaitForAssertion(() => Assert.True(FormatAt(page, "B2").Font.Bold));
        Assert.Empty(KeyTips(page));
        page.WaitForAssertion(() => Assert.Equal(taken + 1, TakeBackCount));
        // The grid has the keyboard, and asks for what was typed on the way: nothing more.
        Assert.Empty(AnswerTakeBacks(page.FindComponent<ExGrid<SheetRow>>()));

        // And the next list is the only one.
        await KeyTipsAsync(page, "h", "b");
        page.WaitForAssertion(() => Assert.Single(Lists(page)));
    }

    [Fact] // ADR-0100, ADR-0039: Escape in a list the pointer opened closes it, and the keyboard goes back to the Sheet, as before
    public async Task Escape_in_a_list_the_pointer_opened_gives_the_keyboard_back()
    {
        var page = RenderPage(DocumentOf(("B2", "12")), showToolbar: true);
        await GoToAsync(page, "B2");
        await Wrapper(page, "Borders").QuerySelector("button.mud-ex-sheet-toolbar-arrow")!.ClickAsync(new MouseEventArgs());
        page.WaitForAssertion(() => Assert.Single(Lists(page)));
        // A choice holds DOM focus, as MudBlazor's own keys would have put it there.
        await page.FindAll(".mud-ex-sheet-toolbar-choices .mud-menu-item").First().FocusAsync(new FocusEventArgs());
        var taken = TakeBackCount;

        await TypeInListAsync(page, "Escape");

        page.WaitForAssertion(() => Assert.Empty(Lists(page)));
        page.WaitForAssertion(() => Assert.Equal(taken + 1, TakeBackCount));
        Assert.Empty(KeyTips(page));
        Assert.Empty(AnswerTakeBacks(page.FindComponent<ExGrid<SheetRow>>()));
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
        var taken = TakeBackCount;

        await TypeInListAsync(page, "Enter");

        page.WaitForAssertion(() => Assert.Empty(Lists(page)));
        page.WaitForAssertion(() => Assert.Equal(new BorderLine(BorderLineStyle.Thick), FormatAt(page, "B2").Borders.Top));
        page.WaitForAssertion(() => Assert.True(TakeBackCount > taken));
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
        page.WaitForAssertion(() => Assert.Equal(CellFill.Solid(below.Colour), FormatAt(page, "B2").Fill));
    }

    [Fact] // ADR-0100, SH-50: a choice pressed with the pointer closes the list and gives the keyboard back to the Sheet
    public async Task A_pressed_choice_closes_the_list_and_gives_the_keyboard_back()
    {
        var page = RenderPage(DocumentOf(("B2", "12")), showToolbar: true);
        await GoToAsync(page, "B2");
        await Wrapper(page, "Borders").QuerySelector("button.mud-ex-sheet-toolbar-arrow")!.ClickAsync(new MouseEventArgs());
        page.WaitForAssertion(() => Assert.Single(Lists(page)));
        var taken = TakeBackCount;

        await page.FindAll(".mud-ex-sheet-toolbar-choices .mud-menu-item").Single(i => i.TextContent.Trim() == "Top Border").ClickAsync(new MouseEventArgs());

        page.WaitForAssertion(() => Assert.Empty(Lists(page)));
        page.WaitForAssertion(() => Assert.Equal(new BorderLine(BorderLineStyle.Thin), FormatAt(page, "B2").Borders.Top));
        page.WaitForAssertion(() => Assert.True(TakeBackCount > taken));
    }

    [Fact] // ADR-0100, ADR-0050 item 16's note of 2026-10-12, and the seam member decided with the user on 2026-10-12: the keys typed in the list while it closes, then on the item's element while the keyboard goes back through it, are held in order and handed to the Sheet with the keyboard: Enter presses no item and chooses nothing again
    public async Task Keys_typed_while_a_list_closes_are_the_sheets_in_order()
    {
        var page = RenderPage(DocumentOf(("B2", "12")), showToolbar: true);
        await GoToAsync(page, "B2");
        await KeyTipsAsync(page, "h", "h");
        page.WaitForAssertion(() => Assert.Equal("No Fill", LastFocused(page)));
        // The band gave the keyboard to the list: it holds it no longer.
        await page.Find(".ex-sheet-toolbar").FocusOutAsync(new FocusEventArgs());
        await TypeInListAsync(page, "ArrowDown");
        page.WaitForAssertion(() => Assert.Equal(FormatCellsOffer.ThemeColours[0][0].Name, LastFocused(page)));
        // The browser has not yet put the keyboard on the item's element: it stands a round trip.
        var focus = JSInterop.SetupVoid(Focus, _ => true);
        await TypeInListAsync(page, "Enter");

        // In the list itself, which stands until the keyboard has left it, and then on the element.
        await TypeInListAsync(page, "Enter");
        await TypeInListAsync(page, "ArrowUp");
        var leaving = page.WaitForElement(".mud-ex-sheet-toolbar-leaving");
        await leaving.KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });
        await leaving.KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });

        focus.SetVoidResult();
        page.WaitForAssertion(() => Assert.Empty(Lists(page)));
        page.WaitForAssertion(() => Assert.Empty(page.FindAll(".mud-ex-sheet-toolbar-leaving")));
        Assert.Equal(CellFill.Solid(FormatCellsOffer.ThemeColours[0][0].Colour), FormatAt(page, "B2").Fill);
        Assert.False(FormatAt(page, "B2").Font.Bold);
        Assert.Empty(KeyTips(page));
        Assert.Equal(["Enter", "ArrowUp", "Enter", "ArrowDown"], AnswerTakeBacks(page.FindComponent<ExGrid<SheetRow>>()).Select(k => k.Key));
    }

    [Fact] // ADR-0100's CloseRequest (decided with the user, 2026-10-12; stop 1 of round 2, kept) and R2 (b) of the Windows readings of 2026-10-12: Escape on the band while the list a KeyTip opened already holds the keyboard closes the list, which leaves through its element, and the KeyTips go back to its row's letters with the keyboard on the band — never on the Sheet
    public async Task A_close_request_closes_a_list_that_holds_the_keyboard_back_to_the_keytips()
    {
        var page = RenderPage(DocumentOf(("B2", "12")), showToolbar: true);
        await GoToAsync(page, "B2");
        await KeyTipsAsync(page, "h", "f", "c");
        page.WaitForAssertion(() => Assert.Equal("Automatic", LastFocused(page)));
        var taken = TakeBackCount;

        // The list's focus was on its way when Escape reached the band.
        await page.Find(".ex-sheet-toolbar").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        page.WaitForAssertion(() => Assert.Empty(Lists(page)));
        page.WaitForAssertion(() => Assert.Empty(page.FindAll(".mud-ex-sheet-toolbar-leaving")));
        page.WaitForAssertion(() => Assert.Contains("FC", KeyTips(page)));
        Assert.Equal(taken, TakeBackCount);
        Assert.Equal(CellColour.Automatic, FormatAt(page, "B2").Font.Colour);
        // Escape again: the rows' letters; and again: none, with the keyboard back on the Sheet.
        await page.Find(".ex-sheet-toolbar").FocusAsync(new FocusEventArgs());
        await page.Find(".ex-sheet-toolbar").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });
        Assert.Equal(["H"], KeyTips(page));
        await page.Find(".ex-sheet-toolbar").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });
        Assert.Empty(KeyTips(page));
        Assert.Equal(taken + 1, TakeBackCount);
    }

    [Fact] // ADR-0100 (the verifier's round-2 finding 1, a keyboard trap): a key typed on a list's button inside the band, should DOM focus reach it (a script, assistive technology), is the button's: the band holds nothing and presses nothing
    public async Task A_key_on_a_menus_button_is_its_own()
    {
        var page = RenderPage(DocumentOf(("B2", "12")), showToolbar: true);
        await GoToAsync(page, "B2");
        await page.Find(".ex-sheet-toolbar").FocusAsync(new FocusEventArgs());
        // DOM focus moves on to the Font Colour's arrow, which Tab no longer reaches but a script can.
        await page.Find(".ex-sheet-toolbar").FocusOutAsync(new FocusEventArgs());
        var arrow = Wrapper(page, "Font Colour").QuerySelector("button.mud-ex-sheet-toolbar-arrow")!;
        var taken = TakeBackCount;

        await arrow.KeyDownAsync(new KeyboardEventArgs { Key = "x" });
        await arrow.KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });
        await arrow.KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        Assert.False(FormatAt(page, "B2").Font.Bold);
        Assert.Equal(taken, TakeBackCount);
        Assert.False(page.Find(".ex-sheet-toolbar").HasAttribute("blazor:onkeydown:preventdefault"));
        // Back on the band, its own keys work, and nothing typed on the button reaches the Sheet.
        await page.Find(".ex-sheet-toolbar").FocusAsync(new FocusEventArgs());
        await page.Find(".ex-sheet-toolbar").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });
        Assert.Equal(taken + 1, TakeBackCount);
        Assert.Empty(AnswerTakeBacks(page.FindComponent<ExGrid<SheetRow>>()));
    }

    [Fact] // ADR-0100's CloseRequest (decided with the user, 2026-10-12): an opening and a close asked for before the Chrome answers either open nothing, and the Chrome gives no keyboard back for that close
    public void An_opening_closed_before_it_is_answered_opens_nothing()
    {
        SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Server", isInteractive: true));
        var closed = 0;
        var provider = Render<MudProviders.MudPopoverProvider>();
        var item = Render<MudToolbarItem>(ps => ps.Add(p => p.Context, ItemContext(open: 0, close: 0, () => closed++)));

        item.Render(ps => ps.Add(p => p.Context, ItemContext(open: 1, close: 1, () => closed++)));
        Assert.Empty(provider.FindAll(".mud-ex-sheet-toolbar-choices"));
        Assert.Equal(0, closed);

        // A later opening is answered.
        item.Render(ps => ps.Add(p => p.Context, ItemContext(open: 2, close: 1, () => closed++)));
        provider.WaitForAssertion(() => Assert.Single(provider.FindAll(".mud-ex-sheet-toolbar-choices")));
        Assert.Equal(0, closed);
    }

    private static ToolbarItemContext ItemContext(int open, int close, Action closed) => new(
        "test-fill", ToolbarItemKind.Split, "Fill Colour", null, ToolbarIcon.Fill, null, false, true, null,
        () => Task.CompletedTask, [new ToolbarChoice("No Fill", () => Task.CompletedTask), new ToolbarChoice("Red", () => Task.CompletedTask, Colour: CellColour.FromRgb(0xFF0000))],
        null, false, open, close, () =>
        {
            closed();
            return Task.CompletedTask;
        }, _ => _ => { }, null, null, false);
}
