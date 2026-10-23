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
    /// The name of the choice the keyboard was last handed to through the grid (ToolbarItemContext.TakeKeyboard),
    /// read by the element's reference: a MudMenuItem's text, a swatch's label, or null for anything else.
    /// </summary>
    private string? LastFocused(Bunit.IRenderedComponent<Bunit.Rendering.ContainerFragment> page)
    {
        if (LastHandedToId is not { } id) return null;
        if (page.FindComponents<global::MudBlazor.MudMenuItem>().FirstOrDefault(i => i.Instance.ElementReference.Id == id) is { } item)
            return string.Concat(item.Nodes.Select(n => n.TextContent)).Trim();
        return page.FindAll("button.mud-ex-sheet-swatch").FirstOrDefault(e => e.GetAttribute("blazor:elementReference") == id)?.GetAttribute("aria-label");
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

    [Fact] // ADR-0100, ADR-0071 (2026-10-14, decided with the user): under MudBlazor too, the Font Colour widens a column whose number shows ####, by its face and from its list, as Excel's Home tab Font Color button did (the readings of 2026-10-14, run 18a, widening case 5), each in its press's one undo step; the Fill Colour widens nothing
    public async Task The_font_colour_widens_by_its_face_and_from_its_list_18a()
    {
        var page = RenderPage(DocumentOf(
            sheet => sheet.SetCellFormat([CellRange.Parse("B2")], new CellFormatChange { NumberFormat = NumberFormat.Parse("0.00") }),
            ("B2", "123456789.12")), showToolbar: true);
        var grid = page.FindComponent<ExGrid<SheetRow>>();
        double WidthOf(int column) => grid.Instance.Columns[column].Width.Width.FixedPx;
        var standard = WidthOf(1);
        await GoToAsync(page, "B2");

        await Wrapper(page, "Fill Colour").QuerySelector("button.mud-ex-sheet-toolbar-face")!.ClickAsync(new MouseEventArgs());
        page.WaitForAssertion(() => Assert.Equal(CellFill.Solid(CellColour.FromRgb(0xFFFF00)), FormatAt(page, "B2").Fill));
        Assert.Equal(standard, WidthOf(1));
        Assert.Empty(Sheet(page).ToDocument().ColumnWidths);

        // Its face: red, as Excel's starts.
        await Wrapper(page, "Font Colour").QuerySelector("button.mud-ex-sheet-toolbar-face")!.ClickAsync(new MouseEventArgs());
        page.WaitForAssertion(() => Assert.Equal(CellColour.FromRgb(0xFF0000), FormatAt(page, "B2").Font.Colour));
        Assert.True(WidthOf(1) > standard);
        var widened = Assert.Single(Sheet(page).ToDocument().ColumnWidths);
        Assert.Equal((1, SheetColumnWidthKind.WidenedByEntry), (widened.First, widened.Kind));
        Assert.True(await page.InvokeAsync(Sheet(page).UndoAsync));
        Assert.Equal(standard, WidthOf(1));
        Assert.Empty(Sheet(page).ToDocument().ColumnWidths);

        // From its list: Blue, as run 18a chose.
        await Wrapper(page, "Font Colour").QuerySelector("button.mud-ex-sheet-toolbar-arrow")!.ClickAsync(new MouseEventArgs());
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("button.mud-ex-sheet-swatch")));
        await page.FindAll("button.mud-ex-sheet-swatch").Single(b => b.GetAttribute("aria-label") == "Blue").ClickAsync(new MouseEventArgs());
        page.WaitForAssertion(() => Assert.Equal(CellColour.FromRgb(0x0070C0), FormatAt(page, "B2").Font.Colour));
        Assert.True(WidthOf(1) > standard);
        widened = Assert.Single(Sheet(page).ToDocument().ColumnWidths);
        Assert.Equal((1, SheetColumnWidthKind.WidenedByEntry), (widened.First, widened.Kind));
    }

    [Fact] // ADR-0100, ADR-0039, SH-52 (the seventeenth Windows run, D-mud-lists), ADR-0240 section 6, ticket 06: Alt, H, F, C opens the Font Colour list, which joins the Sheet's Input Order and takes the keyboard through the grid, on the choice the Focus cell has
    public async Task A_list_opened_from_the_keys_takes_the_keyboard()
    {
        var page = RenderPage(DocumentOf(("B2", "12")), showToolbar: true);
        await GoToAsync(page, "B2");
        var steps = SurfaceSteps.Count;

        await KeyTipsAsync(page, "h", "f", "c");

        page.WaitForAssertion(() => Assert.Single(Lists(page)));
        page.WaitForAssertion(() => Assert.Equal("Automatic", LastFocused(page)));
        // Told the keyboard is on its way to a frame as the KeyTip is answered; joined before it takes it.
        var after = SurfaceSteps.Skip(steps).ToList();
        Assert.True(after.LastIndexOf("handOff:frame") < after.IndexOf("joinSurface"));
        Assert.True(after.IndexOf("joinSurface") < after.LastIndexOf("handKeyboardTo"));
        Assert.Equal("keydown", Lists(page)[0].GetAttribute("data-ex-claims"));
    }

    [Fact] // ADR-0100, ADR-0039, SH-52 and R2 (b) of the Windows readings of 2026-10-12 (observed: after Alt, H, F, C one Escape shows the Home tab's KeyTips), ADR-0240 section 6, ticket 06: Escape in a list a KeyTip opened goes back to its row's KeyTips, the band taking the keyboard through the grid from the list in Escape's turn, and only then does the list close and leave the Sheet's Input Order
    public async Task Escape_in_a_list_a_keytip_opened_goes_back_to_its_rows_keytips()
    {
        var page = RenderPage(DocumentOf(("B2", "12")), showToolbar: true);
        await GoToAsync(page, "B2");
        await KeyTipsAsync(page, "h", "h");
        page.WaitForAssertion(() => Assert.Equal("No Fill", LastFocused(page)));
        await page.Find(".ex-sheet-toolbar").FocusOutAsync(new FocusEventArgs());
        var taken = TakeBackCount;
        var steps = SurfaceSteps.Count;

        await TypeInListAsync(page, "Escape");

        page.WaitForAssertion(() => Assert.Empty(Lists(page)));
        page.WaitForAssertion(() => Assert.Contains("FC", KeyTips(page)));
        Assert.Equal(["handKeyboardTo", "leaveSurface"], SurfaceSteps.Skip(steps));
        Assert.Equal(taken, TakeBackCount);
        Assert.Equal(CellFill.None, FormatAt(page, "B2").Fill);
        Assert.Empty(page.FindAll(".mud-ex-sheet-toolbar-leaving"));

        // And the next list is the only one.
        await page.Find(".ex-sheet-toolbar").FocusAsync(new FocusEventArgs());
        await page.Find(".ex-sheet-toolbar").KeyDownAsync(new KeyboardEventArgs { Key = "b" });
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
        var steps = SurfaceSteps.Count;
        await TypeInListAsync(page, "Tab");
        Assert.Equal(steps, SurfaceSteps.Count);
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

    [Fact] // ADR-0039 (2026-10-13) / ADR-0050 item 16: More Number Formats… in a MudBlazor list opens Format Cells, and the hand-off to its frame is told after the toolbar gives the keyboard back through the grid, so the keys handed back go on to it
    public async Task More_number_formats_hands_the_keyboard_on_to_format_cells_after_the_toolbar_gives_it_back()
    {
        var page = RenderPage(DocumentOf(("B2", "12")), showToolbar: true);
        await GoToAsync(page, "B2");
        await Wrapper(page, "Number Format").QuerySelector("button[aria-label=\"Number Format\"]")!.ClickAsync(new MouseEventArgs());
        page.WaitForAssertion(() => Assert.Single(Lists(page)));
        var ways = KeyboardWays.Count;

        await page.FindAll(".mud-ex-sheet-toolbar-choices .mud-menu-item").Single(i => i.TextContent.Trim() == "More Number Formats…").ClickAsync(new MouseEventArgs());

        page.WaitForAssertion(() => Assert.True(IsOpen(page)));
        page.WaitForAssertion(() => Assert.Equal(["takeKeyboardBack", "handOff:frame"], KeyboardWays.Skip(ways)));
    }

    [Fact] // ADR-0039 / ADR-0050 item 16: the next frame cannot take focus before the toolbar has handed back its keyboard and held keys
    public async Task More_number_formats_draws_its_frame_only_after_the_toolbar_hands_back_the_keyboard()
    {
        var page = RenderPage(DocumentOf(("B2", "12")), showToolbar: true);
        await GoToAsync(page, "B2");
        await KeyTipsAsync(page, "h", "n");
        page.WaitForAssertion(() => Assert.Single(Lists(page)));
        await TypeInListAsync(page, "End");
        var returned = HoldKeyboardHandBack();
        var taken = TakeBackCount;

        var enter = TypeInListAsync(page, "Enter");

        page.WaitForAssertion(() => Assert.True(TakeBackCount > taken));
        Assert.False(IsOpen(page));

        returned();
        await enter;
        page.WaitForAssertion(() => Assert.True(IsOpen(page)));
    }

    [Fact] // ADR-0100, SH-50, ADR-0240 section 6: a choice pressed with the pointer claims its click, and the list's keys typed on it, closes the list and gives the keyboard back to the Sheet; a disabled choice, which hears no click, claims none
    public async Task A_pressed_choice_closes_the_list_and_gives_the_keyboard_back()
    {
        var page = RenderPage(DocumentOf(("B2", "12")), showToolbar: true);
        await GoToAsync(page, "B2");
        await Wrapper(page, "Borders").QuerySelector("button.mud-ex-sheet-toolbar-arrow")!.ClickAsync(new MouseEventArgs());
        page.WaitForAssertion(() => Assert.Single(Lists(page)));
        var taken = TakeBackCount;
        var top = page.FindAll(".mud-ex-sheet-toolbar-choices .mud-menu-item").Single(i => i.TextContent.Trim() == "Top Border");
        Assert.Equal("click keydown", top.GetAttribute("data-ex-claims"));

        await top.ClickAsync(new MouseEventArgs());

        page.WaitForAssertion(() => Assert.Empty(Lists(page)));
        page.WaitForAssertion(() => Assert.Equal(new BorderLine(BorderLineStyle.Thin), FormatAt(page, "B2").Borders.Top));
        page.WaitForAssertion(() => Assert.True(TakeBackCount > taken));

        await Wrapper(page, "Number Format").QuerySelector("button[aria-label=\"Number Format\"]")!.ClickAsync(new MouseEventArgs());
        page.WaitForAssertion(() => Assert.Single(Lists(page)));
        var accounting = page.FindAll(".mud-ex-sheet-toolbar-choices .mud-menu-item").Single(i => i.TextContent.StartsWith("Accounting"));
        Assert.False(accounting.HasAttribute("data-ex-claims"));
    }

    [Fact] // ADR-0100, ADR-0050 item 16's note of 2026-10-12 (decided with the user: the keys typed while a list closes go where the keyboard lands), ADR-0240 section 6, ticket 06: Enter in the list is answered in its turn, which makes the choice and gives the keyboard back from the list before the list closes and leaves the Sheet's Input Order, so the grid holds the keys typed after it until the keyboard has landed
    public async Task A_choice_made_from_the_keys_gives_the_keyboard_back_before_the_list_goes()
    {
        var page = RenderPage(DocumentOf(("B2", "12")), showToolbar: true);
        await GoToAsync(page, "B2");
        await KeyTipsAsync(page, "h", "h");
        page.WaitForAssertion(() => Assert.Equal("No Fill", LastFocused(page)));
        await page.Find(".ex-sheet-toolbar").FocusOutAsync(new FocusEventArgs());
        await TypeInListAsync(page, "ArrowDown");
        page.WaitForAssertion(() => Assert.Equal(FormatCellsOffer.ThemeColours[0][0].Name, LastFocused(page)));
        var returned = HoldKeyboardReturns();
        var steps = SurfaceSteps.Count;

        var enter = TypeInListAsync(page, "Enter");

        // The choice is made and the keyboard asked back while the list stands.
        page.WaitForAssertion(() => Assert.Equal(["takeKeyboardBack"], SurfaceSteps.Skip(steps)));
        Assert.Equal(CellFill.Solid(FormatCellsOffer.ThemeColours[0][0].Colour), FormatAt(page, "B2").Fill);
        Assert.Single(Lists(page));

        returned();
        await enter;
        page.WaitForAssertion(() => Assert.Empty(Lists(page)));
        Assert.Equal(["takeKeyboardBack", "leaveSurface"], SurfaceSteps.Skip(steps));
        Assert.Empty(KeyTips(page));
        Assert.Empty(page.FindAll(".mud-ex-sheet-toolbar-leaving"));
    }

    [Fact] // ADR-0100's CloseRequest (decided with the user, 2026-10-12; stop 1 of round 2, kept) and R2 (b) of the Windows readings of 2026-10-12: Escape on the band while the list a KeyTip opened already holds the keyboard closes the list, and the KeyTips go back to its row's letters with the keyboard on the band — never on the Sheet
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

    [Fact] // ADR-0240 section 6, ticket 06: every button of a MudBlazor item claims its click (data-ex-claims), so its press takes its place in the Sheet's Input Order, as the built-in item's does
    public void The_items_buttons_claim_their_clicks()
    {
        var page = RenderPage(DocumentOf(("B2", "12")), showToolbar: true);

        var buttons = page.FindAll(".mud-ex-sheet-toolbar-item button");

        Assert.NotEmpty(buttons);
        Assert.All(buttons, b => Assert.Equal("click", b.GetAttribute("data-ex-claims")));
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

    [Fact] // ADR-0100's OpenRequest and ADR-0240 section 6, ticket 06: a list opened for the toolbar's keys joins the Sheet's Input Order first, then takes the keyboard through the context, on the choice the Focus cell has, else the first that can be made
    public void A_list_opened_for_the_keys_joins_then_takes_the_keyboard()
    {
        SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Server", isInteractive: true));
        var steps = new List<string>();
        var provider = Render<MudProviders.MudPopoverProvider>();
        ToolbarItemContext Context(int open) => ItemContext(open, 0, () => { }) with
        {
            JoinSurface = (_, _, _) =>
            {
                steps.Add("join");
                return Task.FromResult<IAsyncDisposable>(new Membership(() => steps.Add("leave")));
            },
            TakeKeyboard = _ =>
            {
                steps.Add("take");
                return Task.CompletedTask;
            },
        };
        var item = Render<MudToolbarItem>(ps => ps.Add(p => p.Context, Context(0)));

        item.Render(ps => ps.Add(p => p.Context, Context(1)));

        provider.WaitForAssertion(() => Assert.Equal(["join", "take"], steps));
    }

    [Fact] // ADR-0100's OpenRequest and ChoicesClosed: a list asked to open that cannot — its item disabled meanwhile — says so with no choice, so the keys held for it go to the toolbar
    public void A_list_that_cannot_open_says_it_closed_with_no_choice()
    {
        SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Server", isInteractive: true));
        var closed = new List<(ToolbarChoice? Choice, bool Escaped)>();
        var provider = Render<MudProviders.MudPopoverProvider>();
        ToolbarItemContext Context(int open) => ItemContext(open, 0, () => { }) with
        {
            Enabled = false,
            ChoicesClosed = (choice, escaped) =>
            {
                closed.Add((choice, escaped));
                return Task.CompletedTask;
            },
        };
        var item = Render<MudToolbarItem>(ps => ps.Add(p => p.Context, Context(0)));

        item.Render(ps => ps.Add(p => p.Context, Context(1)));

        item.WaitForAssertion(() => Assert.Equal([(null, false)], closed));
        Assert.Empty(provider.FindAll(".mud-ex-sheet-toolbar-choices"));
    }

    private static ToolbarItemContext ItemContext(int open, int close, Action closed) => new(
        "test-fill", ToolbarItemKind.Split, "Fill Colour", null, ToolbarIcon.Fill, null, false, true, null,
        () => Task.CompletedTask, [new ToolbarChoice("No Fill", () => Task.CompletedTask), new ToolbarChoice("Red", () => Task.CompletedTask, Colour: CellColour.FromRgb(0xFF0000))],
        null, false, open, close, (_, _) =>
        {
            closed();
            return Task.CompletedTask;
        }, null, null, false);

    // A list's membership of the Sheet's Input Order, as the context hands it back.
    private sealed class Membership(Action left) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            left();
            return ValueTask.CompletedTask;
        }
    }
}
